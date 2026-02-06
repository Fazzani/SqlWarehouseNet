using System.Diagnostics;
using System.Threading.Channels;
using Apache.Arrow;
using Spectre.Console;
using SqlWarehouseNet.CLI;
using SqlWarehouseNet.Models;
using SqlWarehouseNet.Services;

namespace SqlWarehouseNet;

/// <summary>
/// Main application class that owns the interactive loop, services lifecycle, and graceful shutdown.
/// Extracted from Program.cs to enable testability and proper resource cleanup.
/// </summary>
public sealed class App : IDisposable
{
    private readonly IProfileService _profileService;
    private readonly IDatabricksService _databricksService;
    private readonly CommandProcessor _commandProcessor;
    private readonly InteractiveShell _shell;
    private readonly CancellationTokenSource _cts = new();

    public App(
        IProfileService profileService,
        IDatabricksService databricksService,
        IExportService exportService
    )
    {
        _profileService = profileService;
        _databricksService = databricksService;
        _commandProcessor = new CommandProcessor(profileService, databricksService, exportService);
        _shell = new InteractiveShell();
    }

    public async Task<int> RunAsync()
    {
        // Wire Ctrl+C to CancellationToken
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        var connectionState = InitializeConnection();
        PrintHeader();

        var queryHistory = _profileService.LoadHistory();
        StatementResultResponse? lastResult = null;
        List<RecordBatch>? lastRecordBatches = null;
        var tablesCache = _profileService.LoadTablesCache();
        var schemasCache = _profileService.LoadSchemasCache();

        // ==========================================
        // Interactive Query Loop
        // ==========================================
        while (!_cts.IsCancellationRequested)
        {
            AnsiConsole.Markup("[bold yellow]SQL>[/] ");

            var sqlQuery = _shell.ReadLineWithHistory(queryHistory, tablesCache, schemasCache);

            // null = Escape pressed → graceful exit
            if (sqlQuery == null)
            {
                AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
                break;
            }

            if (string.IsNullOrWhiteSpace(sqlQuery))
                continue;

            var trimmedQuery = sqlQuery.Trim();

            // Command Handling
            if (
                trimmedQuery.Equals("/q", StringComparison.OrdinalIgnoreCase)
                || trimmedQuery.Equals("/quit", StringComparison.OrdinalIgnoreCase)
            )
            {
                AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
                break;
            }

            if (trimmedQuery.Equals("/help", StringComparison.OrdinalIgnoreCase))
            {
                CommandProcessor.DisplayHelp();
                continue;
            }

            if (trimmedQuery.Equals("/clear", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                AnsiConsole.MarkupLine("[bold cyan]===== Databricks SQL Warehouse Client =====[/]");
                continue;
            }

            if (trimmedQuery.StartsWith("/profile", StringComparison.OrdinalIgnoreCase))
            {
                _commandProcessor.HandleProfileCommand(trimmedQuery, connectionState);
                continue;
            }

            if (trimmedQuery.StartsWith("/export", StringComparison.OrdinalIgnoreCase))
            {
                _commandProcessor.HandleExportCommand(trimmedQuery, lastResult, lastRecordBatches);
                continue;
            }

            if (trimmedQuery.StartsWith("/rows", StringComparison.OrdinalIgnoreCase))
            {
                _commandProcessor.HandleRowsCommand(trimmedQuery);
                continue;
            }

            if (trimmedQuery.Equals("/catalogs", StringComparison.OrdinalIgnoreCase))
            {
                await _commandProcessor.HandleCatalogsCommand(
                    connectionState.WarehouseId,
                    _cts.Token
                );
                continue;
            }

            if (trimmedQuery.StartsWith("/schemas", StringComparison.OrdinalIgnoreCase))
            {
                await _commandProcessor.HandleSchemasCommand(
                    trimmedQuery,
                    connectionState.WarehouseId,
                    _cts.Token
                );
                continue;
            }

            if (trimmedQuery.StartsWith("/tables", StringComparison.OrdinalIgnoreCase))
            {
                await _commandProcessor.HandleTablesCommand(
                    trimmedQuery,
                    connectionState.WarehouseId,
                    _cts.Token
                );
                continue;
            }

            if (!connectionState.IsComplete)
            {
                AnsiConsole.MarkupLine(
                    "[red]Error:[/] Connection details are not configured. Use '/profile add' first."
                );
                continue;
            }

            // SQL Execution
            if (
                queryHistory.Count == 0
                || !queryHistory[^1].Equals(sqlQuery, StringComparison.Ordinal)
            )
            {
                queryHistory.Add(sqlQuery);
                _profileService.SaveHistoryEntry(sqlQuery);
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();

                var statementId = await _databricksService.ExecuteStatementAsync(
                    connectionState.WarehouseId!,
                    sqlQuery,
                    _cts.Token
                );
                lastResult = await _databricksService.PollForResultAsync(statementId, _cts.Token);

                var batchChannel = Channel.CreateUnbounded<RecordBatch>();

                var downloadTask = _databricksService.FetchArrowResultsStreamingAsync(
                    lastResult,
                    batchChannel.Writer,
                    _cts.Token
                );
                var displayTask = _commandProcessor.DisplayResultsStreamingAsync(
                    lastResult,
                    batchChannel.Reader,
                    _cts.Token
                );

                lastRecordBatches = await downloadTask;
                await displayTask;

                stopwatch.Stop();
                AnsiConsole.MarkupLine(
                    $"\n[bold green]Execution time: {stopwatch.Elapsed.TotalSeconds:F3} seconds[/]"
                );

                SqlCompletionService.UpdateTablesCache(sqlQuery, tablesCache, schemasCache);
                _profileService.SaveTablesCache(tablesCache);
                _profileService.SaveSchemasCache(schemasCache);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                break;
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red]HTTP Error:[/] {ex.Message.EscapeMarkup()}");
            }
            catch (DatabricksApiException ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Databricks API Error:[/] {ex.Message.EscapeMarkup()}"
                );
                if (!string.IsNullOrEmpty(ex.ErrorCode))
                    AnsiConsole.MarkupLine($"[red]Error Code:[/] {ex.ErrorCode.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Unexpected Error:[/] {ex.Message.EscapeMarkup()}");
            }

            AnsiConsole.WriteLine();
        }

        return 0;
    }

    private ConnectionState InitializeConnection()
    {
        var currentProfile = _profileService.LoadDefaultProfile();
        var state = new ConnectionState
        {
            Host = currentProfile?.Host ?? Environment.GetEnvironmentVariable("DATABRICKS_HOST"),
            Token = currentProfile?.Token ?? Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"),
            WarehouseId =
                currentProfile?.WarehouseId
                ?? Environment.GetEnvironmentVariable("DATABRICKS_WAREHOUSE_ID"),
        };

        _databricksService.UpdateConnection(state.Host, state.Token);

        if (!state.IsComplete)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Connection details are incomplete.");
            AnsiConsole.MarkupLine(
                "[dim]Use '/profile add' to configure a connection or set environment variables.[/]"
            );
        }

        return state;
    }

    private void PrintHeader()
    {
        Console.Clear();
        AnsiConsole.Write(new FigletText("SqlWarehouse").LeftJustified().Color(Color.Cyan1));

        var infoTable = new Spectre.Console.Table().NoBorder().HideHeaders();
        infoTable.AddColumn("Info");

        var profile = _profileService.LoadDefaultProfile();
        if (profile != null)
            infoTable.AddRow($"[dim]Connected to profile:[/] [green]{profile.Name}[/]");
        else
            infoTable.AddRow("[dim]Connected via environment variables.[/]");

        infoTable.AddRow($"[dim]Config directory:[/] [cyan]{_profileService.UserProfileDir}[/]");
        AnsiConsole.Write(infoTable);

        AnsiConsole.MarkupLine(
            "[bold yellow]🚀 Quick Guide:[/] [dim]Enter[/] = Newline | [bold cyan]Ctrl+Enter[/] = Execute | [dim]Tab[/] = Auto-complete commands"
        );
        AnsiConsole.MarkupLine(
            "[bold yellow]📋 Commands:[/] [cyan]/help[/], [cyan]/export[/], [cyan]/profile[/], [cyan]/rows[/], [cyan]/clear[/], [cyan]/q[/]\n"
        );
    }

    public void Dispose()
    {
        _cts.Dispose();
        _databricksService.Dispose();
    }
}
