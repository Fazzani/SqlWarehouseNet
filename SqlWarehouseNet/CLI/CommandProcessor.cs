using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Apache.Arrow;
using Spectre.Console;
using SqlWarehouseNet.Models;
using SqlWarehouseNet.Services;

namespace SqlWarehouseNet.CLI;

public partial class CommandProcessor
{
    private readonly IProfileService _profileService;
    private readonly IDatabricksService _databricksService;
    private readonly IExportService _exportService;

    /// <summary>Max rows shown in the terminal. Users can override with /rows command.</summary>
    public int MaxDisplayRows { get; set; } = 50;

    /// <summary>Regex to validate Databricks identifiers (catalog, schema, table names).</summary>
    [GeneratedRegex(@"^[\w][\w.]*$")]
    private static partial Regex SafeIdentifierRegex();

    public CommandProcessor(
        IProfileService profileService,
        IDatabricksService databricksService,
        IExportService exportService
    )
    {
        _profileService = profileService;
        _databricksService = databricksService;
        _exportService = exportService;
    }

    /// <summary>
    /// Validates that a string is a safe SQL identifier (alphanumeric, underscores, dots).
    /// Prevents SQL injection on /schemas and /tables commands.
    /// </summary>
    private static bool IsValidIdentifier(string value)
    {
        return SafeIdentifierRegex().IsMatch(value);
    }

    public void HandleProfileCommand(string command, ConnectionState connectionState)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            AnsiConsole.MarkupLine("[bold cyan]Profile Commands:[/]");
            AnsiConsole.MarkupLine("  /profile list                       - List all profiles");
            AnsiConsole.MarkupLine("  /profile use <name>                 - Switch to a profile");
            AnsiConsole.MarkupLine(
                "  /profile add <name> <host> <wid>    - Add profile (token prompted securely)"
            );
            AnsiConsole.MarkupLine("  /profile delete <name>              - Remove a profile");
            AnsiConsole.MarkupLine(
                "  /profile edit                       - Open profiles JSON file in desktop application"
            );
            return;
        }

        var action = parts[1].ToLower();
        var config = _profileService.LoadConfig();

        switch (action)
        {
            case "list":
                var table = new Spectre.Console.Table().AddColumns(
                    "Name",
                    "Host",
                    "Warehouse ID",
                    "Is Default"
                );
                foreach (var p in config.Profiles)
                {
                    table.AddRow(
                        p.Key.EscapeMarkup(),
                        (p.Value.Host ?? "").EscapeMarkup(),
                        (p.Value.WarehouseId ?? "").EscapeMarkup(),
                        p.Key == config.DefaultProfile ? "[green]Yes[/]" : ""
                    );
                }
                AnsiConsole.Write(table);
                break;

            case "use":
                if (parts.Length < 3)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Missing profile name.");
                    return;
                }
                if (config.Profiles.TryGetValue(parts[2], out var profileToUse))
                {
                    config.DefaultProfile = parts[2];
                    _profileService.SaveConfig(config);
                    connectionState.Host = profileToUse.Host;
                    connectionState.Token = profileToUse.Token;
                    connectionState.WarehouseId = profileToUse.WarehouseId;
                    _databricksService.UpdateConnection(
                        connectionState.Host,
                        connectionState.Token
                    );
                    AnsiConsole.MarkupLine(
                        $"[green]✓[/] Switched to profile: [bold]{parts[2].EscapeMarkup()}[/]"
                    );
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Error:[/] Profile '{parts[2].EscapeMarkup()}' not found."
                    );
                }
                break;

            case "add":
                if (parts.Length < 5)
                {
                    AnsiConsole.MarkupLine(
                        "[red]Usage:[/] /profile add <name> <host> <warehouseId>"
                    );
                    return;
                }
                var addHost = parts[3].Trim().Trim('"');
                var addWid = parts[4].Trim().Trim('"');
                // Prompt for token securely — never pass tokens as visible CLI arguments
                var addToken = AnsiConsole.Prompt(
                    new TextPrompt<string>("[yellow]Enter access token:[/]").Secret()
                );
                config.Profiles[parts[2]] = new DatabricksProfile
                {
                    Host = addHost,
                    Token = addToken,
                    WarehouseId = addWid,
                };
                if (string.IsNullOrEmpty(config.DefaultProfile))
                    config.DefaultProfile = parts[2];
                _profileService.SaveConfig(config);
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] Profile '{parts[2].EscapeMarkup()}' added/updated."
                );
                break;

            case "delete":
                if (parts.Length < 3)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Missing profile name.");
                    return;
                }
                if (config.Profiles.Remove(parts[2]))
                {
                    if (config.DefaultProfile == parts[2])
                        config.DefaultProfile = config.Profiles.Keys.FirstOrDefault() ?? "";
                    _profileService.SaveConfig(config);
                    AnsiConsole.MarkupLine(
                        $"[green]✓[/] Profile '{parts[2].EscapeMarkup()}' deleted."
                    );
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Error:[/] Profile '{parts[2].EscapeMarkup()}' not found."
                    );
                }
                break;

            case "edit":
                try
                {
                    Process.Start(
                        new ProcessStartInfo(_profileService.ProfilesFile)
                        {
                            UseShellExecute = true,
                        }
                    );
                    AnsiConsole.MarkupLine(
                        "[green]✓[/] Opening profiles file in default application..."
                    );
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Error:[/] Could not open profiles file: {ex.Message}"
                    );
                }
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown profile action '{action}'.");
                break;
        }
    }

    public void HandleExportCommand(
        string command,
        StatementResultResponse? lastResult,
        List<RecordBatch>? batches
    )
    {
        if (lastResult == null || batches == null || batches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results to export. Run a query first.[/]");
            return;
        }

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] /export <csv|json> [file_path]");
            return;
        }

        var format = parts[1].ToLower();
        var defaultFileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
        var filePath = parts.Length > 2 ? parts[2] : defaultFileName;

        try
        {
            if (format == "csv")
                _exportService.ExportToCsv(filePath, lastResult, batches);
            else if (format == "json")
                _exportService.ExportToJson(filePath, lastResult, batches);
            else
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error:[/] Unknown format '{format}'. Supported: csv, json."
                );
                return;
            }
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Data exported successfully to: [white]{filePath}[/]"
            );
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Export failed:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    /// <summary>Handles the /rows command to change the display row limit.</summary>
    public void HandleRowsCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var rows) || rows < 1)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Current display limit:[/] [bold]{MaxDisplayRows}[/] rows."
            );
            AnsiConsole.MarkupLine("[dim]Usage:[/] /rows <number>");
            return;
        }
        MaxDisplayRows = rows;
        AnsiConsole.MarkupLine($"[green]✓[/] Display limit set to [bold]{MaxDisplayRows}[/] rows.");
    }

    public async Task HandleCatalogsCommand(
        string? warehouseId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(warehouseId))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Warehouse ID not configured.");
            return;
        }
        await ExecuteAndDisplay("SHOW CATALOGS;", warehouseId, cancellationToken);
    }

    public async Task HandleSchemasCommand(
        string command,
        string? warehouseId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(warehouseId))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Warehouse ID not configured.");
            return;
        }
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && !IsValidIdentifier(parts[1]))
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Invalid identifier. Only alphanumeric characters, underscores and dots are allowed."
            );
            return;
        }
        var query = parts.Length > 1 ? $"SHOW SCHEMAS IN {parts[1]};" : "SHOW SCHEMAS;";
        await ExecuteAndDisplay(query, warehouseId, cancellationToken);
    }

    public async Task HandleTablesCommand(
        string command,
        string? warehouseId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(warehouseId))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Warehouse ID not configured.");
            return;
        }
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && !IsValidIdentifier(parts[1]))
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Invalid identifier. Only alphanumeric characters, underscores and dots are allowed."
            );
            return;
        }
        var query = parts.Length > 1 ? $"SHOW TABLES IN {parts[1]};" : "SHOW TABLES;";
        await ExecuteAndDisplay(query, warehouseId, cancellationToken);
    }

    private async Task ExecuteAndDisplay(
        string query,
        string warehouseId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var statementId = await _databricksService.ExecuteStatementAsync(
                warehouseId,
                query,
                cancellationToken
            );
            var result = await _databricksService.PollForResultAsync(
                statementId,
                cancellationToken
            );

            var batchChannel = Channel.CreateUnbounded<RecordBatch>();
            var downloadTask = _databricksService.FetchArrowResultsStreamingAsync(
                result,
                batchChannel.Writer,
                cancellationToken
            );
            var displayTask = DisplayResultsStreamingAsync(
                result,
                batchChannel.Reader,
                cancellationToken
            );

            await downloadTask;
            await displayTask;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    public async Task DisplayResultsStreamingAsync(
        StatementResultResponse result,
        ChannelReader<RecordBatch> batchReader,
        CancellationToken cancellationToken = default
    )
    {
        if (result.Manifest?.Schema?.Columns == null)
        {
            AnsiConsole.MarkupLine("[yellow]No schema information available.[/]");
            return;
        }

        var table = new Spectre.Console.Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Cyan1);
        foreach (var column in result.Manifest.Schema.Columns)
            table.AddColumn(
                new TableColumn($"[bold cyan]{column.Name.EscapeMarkup()}[/]").Centered()
            );

        int rowsDisplayed = 0;
        long totalRowsCount = 0;

        // Accumulate rows, then render the table once to avoid double-display
        await foreach (var batch in batchReader.ReadAllAsync(cancellationToken))
        {
            totalRowsCount += batch.Length;

            int rowsToTake = Math.Min(batch.Length, MaxDisplayRows - rowsDisplayed);
            for (int i = 0; i < rowsToTake; i++)
            {
                var cells = new List<string>();
                for (int col = 0; col < batch.ColumnCount; col++)
                {
                    var value = ArrowUtils.GetValueFromArrowColumn(batch.Column(col), i);
                    cells.Add(value?.ToString()?.EscapeMarkup() ?? "[dim]NULL[/]");
                }
                table.AddRow(cells.ToArray());
                rowsDisplayed++;
            }
            if (rowsDisplayed >= MaxDisplayRows)
                break;
        }

        // Drain remaining batches to get accurate total row count
        await foreach (var batch in batchReader.ReadAllAsync(cancellationToken))
        {
            totalRowsCount += batch.Length;
        }

        if (rowsDisplayed > 0)
            AnsiConsole.Write(table);
        else
            AnsiConsole.MarkupLine("[yellow]No results returned.[/]");

        AnsiConsole.MarkupLine($"\n[dim]Showing top {rowsDisplayed} of {totalRowsCount} rows.[/]");
        if (rowsDisplayed < totalRowsCount)
            AnsiConsole.MarkupLine(
                "[yellow]⚠ Results truncated. Use '/export' to save full data or '/rows <n>' to change the limit.[/]"
            );
    }

    private const string SectionSeparator =
        "[bold cyan]═══════════════════════════════════════════════════════════[/]";

    public static void DisplayHelp()
    {
        var table = new Spectre.Console.Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Key / Command[/]");
        table.AddColumn("[yellow]Description[/]");

        AnsiConsole.MarkupLine(SectionSeparator);
        AnsiConsole.MarkupLine("[bold cyan]EDITOR SHORTCUTS[/]");
        AnsiConsole.MarkupLine(SectionSeparator);

        table.AddRow("[green]Ctrl+Enter[/]", "Execute current SQL query (multi-line supported)");
        table.AddRow("[green]Tab[/]", "Auto-complete SQL keywords or commands (context-aware)");
        table.AddRow("[green]← / →[/]", "Move cursor left / right (by character)");
        table.AddRow("[green]Ctrl+← / Ctrl+→[/]", "Jump to previous / next word");
        table.AddRow(
            "[green]Ctrl+Backspace[/]",
            "Delete entire word (respects separators like . , +)"
        );
        table.AddRow("[green]↑ / ↓[/]", "Navigate query history (↑ for previous, ↓ for next)");
        table.AddRow("[green]Home / End[/]", "Move cursor to start / end of line");
        table.AddRow("[green]Escape[/]", "Exit application");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var commandTable = new Spectre.Console.Table().Border(TableBorder.Rounded);
        commandTable.AddColumn("[yellow]Command[/]");
        commandTable.AddColumn("[yellow]Description[/]");

        AnsiConsole.MarkupLine(SectionSeparator);
        AnsiConsole.MarkupLine(
            "[bold cyan]METADATA COMMANDS[/] [dim]- explore Databricks schema[/]"
        );
        AnsiConsole.MarkupLine(SectionSeparator);

        commandTable.AddRow("[cyan]/catalogs[/]", "List all catalogs in your warehouse");
        commandTable.AddRow(
            "[cyan]/schemas [[catalog]][/]",
            "List schemas (optional: in specific catalog)"
        );
        commandTable.AddRow(
            "[cyan]/tables [[schema]][/]",
            "List tables (optional: in specific schema)"
        );
        commandTable.AddRow("[cyan]/help[/]", "Show this help message");

        AnsiConsole.Write(commandTable);
        AnsiConsole.WriteLine();

        var cliTable = new Spectre.Console.Table().Border(TableBorder.Rounded);
        cliTable.AddColumn("[yellow]Command[/]");
        cliTable.AddColumn("[yellow]Description[/]");

        AnsiConsole.MarkupLine(SectionSeparator);
        AnsiConsole.MarkupLine("[bold cyan]UTILITY COMMANDS[/]");
        AnsiConsole.MarkupLine(SectionSeparator);

        cliTable.AddRow("[cyan]/profile list[/]", "List all saved connection profiles");
        cliTable.AddRow("[cyan]/profile use <name>[/]", "Switch to a specific profile");
        cliTable.AddRow(
            "[cyan]/profile add <name> <host> <wid>[/]",
            "Add a new connection profile (token prompted securely)"
        );
        cliTable.AddRow("[cyan]/profile delete <name>[/]", "Delete a connection profile");
        cliTable.AddRow("[cyan]/profile edit[/]", "Open profiles JSON file in desktop application");
        cliTable.AddRow("[cyan]/export csv [[path]][/]", "Export last results to CSV file");
        cliTable.AddRow("[cyan]/export json [[path]][/]", "Export last results to JSON file");
        cliTable.AddRow("[cyan]/rows <n>[/]", "Set the max number of rows displayed (default: 50)");
        cliTable.AddRow("[cyan]/clear[/]", "Clear the terminal screen");
        cliTable.AddRow("[cyan]/quit or /q[/]", "Exit the application");

        AnsiConsole.Write(cliTable);
        AnsiConsole.WriteLine();
    }
}
