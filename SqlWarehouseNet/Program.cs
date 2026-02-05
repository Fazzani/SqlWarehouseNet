using System.Diagnostics;
using Spectre.Console;
using Apache.Arrow;
using SqlWarehouseNet.Models;
using SqlWarehouseNet.Services;
using SqlWarehouseNet.CLI;

// ==========================================
// Initialization
// ==========================================

var profileService = new ProfileService();
using var databricksService = new DatabricksService();
var exportService = new ExportService();
var completionService = new SqlCompletionService();

var commandProcessor = new CommandProcessor(profileService, databricksService, exportService);
var shell = new InteractiveShell(completionService);

var currentProfile = profileService.LoadDefaultProfile();
var host = currentProfile?.Host ?? Environment.GetEnvironmentVariable("DATABRICKS_HOST");
var token = currentProfile?.Token ?? Environment.GetEnvironmentVariable("DATABRICKS_TOKEN");
var warehouseId = currentProfile?.WarehouseId ?? Environment.GetEnvironmentVariable("DATABRICKS_WAREHOUSE_ID");

databricksService.UpdateConnection(host, token);

// Check for missing configuration
if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(warehouseId))
{
    AnsiConsole.MarkupLine("[yellow]Warning:[/] Connection details are incomplete.");
    AnsiConsole.MarkupLine("[dim]Use '/profile add' to configure a connection or set environment variables.[/]");
}

// ==========================================
// UI Header
// ==========================================

Console.Clear();
AnsiConsole.Write(
    new FigletText("SqlWarehouse")
        .LeftJustified()
        .Color(Color.Cyan1));

var infoTable = new Spectre.Console.Table().NoBorder().HideHeaders();
infoTable.AddColumn("Info");

if (currentProfile != null)
{
    infoTable.AddRow($"[dim]Connected to profile:[/] [green]{currentProfile.Name}[/]");
}
else
{
    infoTable.AddRow("[dim]Connected via environment variables.[/]");
}

infoTable.AddRow($"[dim]Config directory:[/] [cyan]{profileService.UserProfileDir}[/]");
AnsiConsole.Write(infoTable);

AnsiConsole.MarkupLine("[bold yellow]🚀 Quick Guide:[/] [dim]Enter[/] = Newline | [bold cyan]Ctrl+Enter[/] = Execute | [dim]Tab[/] = Auto-complete commands");
AnsiConsole.MarkupLine("[bold yellow]📋 Commands:[/] [cyan]/help[/], [cyan]/export[/], [cyan]/profile[/], [cyan]/clear[/], [cyan]/q[/]\n");

var queryHistory = profileService.LoadHistory();
StatementResultResponse? lastResult = null;
List<RecordBatch>? lastRecordBatches = null;
var tablesCache = profileService.LoadTablesCache();
var schemasCache = profileService.LoadSchemasCache();

// ==========================================
// Interactive Query Loop
// ==========================================

while (true)
{
    AnsiConsole.Markup("[bold yellow]SQL>[/] ");

    var sqlQuery = shell.ReadLineWithHistory(queryHistory, tablesCache, schemasCache);

    if (string.IsNullOrWhiteSpace(sqlQuery)) continue;

    var trimmedQuery = sqlQuery.Trim();

    // Command Handling
    if (trimmedQuery.Equals("/q", StringComparison.OrdinalIgnoreCase) || trimmedQuery.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
        break;
    }

    if (trimmedQuery.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        commandProcessor.DisplayHelp();
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
        commandProcessor.HandleProfileCommand(trimmedQuery, ref host, ref token, ref warehouseId);
        continue;
    }

    if (trimmedQuery.StartsWith("/export", StringComparison.OrdinalIgnoreCase))
    {
        commandProcessor.HandleExportCommand(trimmedQuery, lastResult, lastRecordBatches);
        continue;
    }

    if (trimmedQuery.Equals("/catalogs", StringComparison.OrdinalIgnoreCase))
    {
        await commandProcessor.HandleCatalogsCommand(warehouseId);
        continue;
    }

    if (trimmedQuery.StartsWith("/schemas", StringComparison.OrdinalIgnoreCase))
    {
        await commandProcessor.HandleSchemasCommand(trimmedQuery, warehouseId);
        continue;
    }

    if (trimmedQuery.StartsWith("/tables", StringComparison.OrdinalIgnoreCase))
    {
        await commandProcessor.HandleTablesCommand(trimmedQuery, warehouseId);
        continue;
    }

    if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(warehouseId))
    {
        AnsiConsole.MarkupLine("[red]Error:[/] Connection details are not configured. Use '/profile add' first.");
        continue;
    }

    // SQL Execution
    if (queryHistory.Count == 0 || !queryHistory[^1].Equals(sqlQuery, StringComparison.Ordinal))
    {
        queryHistory.Add(sqlQuery);
        profileService.SaveHistoryEntry(sqlQuery);
    }

    try
    {
        var stopwatch = Stopwatch.StartNew();

        var statementId = await databricksService.ExecuteStatementAsync(warehouseId!, sqlQuery);
        lastResult = await databricksService.PollForResultAsync(statementId);
        
        var batchChannel = System.Threading.Channels.Channel.CreateUnbounded<RecordBatch>();
        
        var downloadTask = databricksService.FetchArrowResultsStreamingAsync(lastResult, batchChannel.Writer);
        var displayTask = commandProcessor.DisplayResultsStreamingAsync(lastResult, batchChannel.Reader);
        
        lastRecordBatches = await downloadTask;
        await displayTask;

        stopwatch.Stop();
        AnsiConsole.MarkupLine($"\n[bold green]Execution time: {stopwatch.Elapsed.TotalSeconds:F3} seconds[/]");
        
        completionService.UpdateTablesCache(sqlQuery, tablesCache, schemasCache);
        profileService.SaveTablesCache(tablesCache);
        profileService.SaveSchemasCache(schemasCache);
    }
    catch (HttpRequestException ex) { AnsiConsole.MarkupLine($"[red]HTTP Error:[/] {ex.Message.EscapeMarkup()}"); }
    catch (DatabricksApiException ex)
    {
        AnsiConsole.MarkupLine($"[red]Databricks API Error:[/] {ex.Message.EscapeMarkup()}");
        if (!string.IsNullOrEmpty(ex.ErrorCode)) AnsiConsole.MarkupLine($"[red]Error Code:[/] {ex.ErrorCode.EscapeMarkup()}");
    }
    catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Unexpected Error:[/] {ex.Message.EscapeMarkup()}"); }
    
    AnsiConsole.WriteLine();
}

return 0;
