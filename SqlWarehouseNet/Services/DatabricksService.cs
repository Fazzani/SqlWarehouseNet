using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Spectre.Console;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public class DatabricksService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _downloadClient = new HttpClient();
    private string? _host;
    private string? _token;

    public DatabricksService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void UpdateConnection(string? host, string? token)
    {
        if (string.IsNullOrEmpty(host)) return;
        
        _host = host.Trim().Trim('"').TrimEnd('/');
        _token = (token ?? "").Trim().Trim('"');

        _httpClient.BaseAddress = new Uri(_host);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<string> ExecuteStatementAsync(string warehouseId, string query)
    {
        var request = new StatementExecuteRequest
        {
            WarehouseId = warehouseId,
            Statement = query,
            WaitTimeout = "0s",
            Format = "ARROW_STREAM",
            Disposition = "EXTERNAL_LINKS"
        };

        var json = JsonSerializer.Serialize(request, JsonContext.Default.StatementExecuteRequest);
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/2.0/sql/statements");
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Headers.Add("X-Databricks-Result-Compression", "LZ4");

        var response = await _httpClient.SendAsync(httpRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            await ThrowDatabricksApiExceptionAsync(responseBody);
        }

        var executeResponse = JsonSerializer.Deserialize(responseBody, JsonContext.Default.StatementExecuteResponse)
            ?? throw new InvalidOperationException("Failed to deserialize execute response.");

        return executeResponse.StatementId;
    }

    public async Task<StatementResultResponse> PollForResultAsync(string statementId)
    {
        const int maxAttempts = 120;
        const int pollIntervalMs = 1000;

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]Running query...[/]", async ctx => 
            {
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var response = await _httpClient.GetAsync($"/api/2.0/sql/statements/{statementId}");
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        await ThrowDatabricksApiExceptionAsync(responseBody);
                    }

                    var statusResponse = JsonSerializer.Deserialize(responseBody, JsonContext.Default.StatementResultResponse)
                        ?? throw new InvalidOperationException("Failed to deserialize status response.");

                    var state = statusResponse.Status.State;
                    ctx.Status($"[yellow]Query state: {state.EscapeMarkup()} (attempt {attempt + 1}/{maxAttempts})[/]");

                    switch (state)
                    {
                        case "SUCCEEDED":
                            AnsiConsole.MarkupLine("[green]✓[/] Query completed successfully.");
                            return statusResponse;
                        case "FAILED":
                            throw new DatabricksApiException($"Query failed: {statusResponse.Status.Error?.Message ?? "Unknown error"}", statusResponse.Status.Error?.ErrorCode);
                        case "CANCELED":
                            throw new DatabricksApiException("Query was canceled.");
                        case "CLOSED":
                            throw new DatabricksApiException("Query was closed.");
                        case "PENDING":
                        case "RUNNING":
                            await Task.Delay(pollIntervalMs);
                            continue;
                        default:
                            throw new DatabricksApiException($"Unknown state: {state}");
                    }
                }
                throw new TimeoutException($"Query did not complete within the timeout period ({maxAttempts} seconds).");
            });
    }

    public async Task<List<RecordBatch>> FetchArrowResultsStreamingAsync(StatementResultResponse status, 
        System.Threading.Channels.ChannelWriter<RecordBatch> batchWriter)
    {
        var batches = new ConcurrentBag<RecordBatch>();
        if (status.Result?.ExternalLinks == null || status.Result.ExternalLinks.Count == 0)
        {
            batchWriter.TryComplete();
            return new List<RecordBatch>();
        }

        var links = status.Result.ExternalLinks;
        await AnsiConsole.Progress()
            .AutoClear(false)
            .StartAsync(async ctx => 
            {
                var task = ctx.AddTask($"[cyan]Downloading {links.Count} chunks (Parallel Streaming)[/]", true, links.Count);
                await Parallel.ForEachAsync(links, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (link, token) =>
                {
                    try
                    {
                        using var response = await _downloadClient.GetAsync(link.Url, HttpCompletionOption.ResponseHeadersRead, token);
                        response.EnsureSuccessStatusCode();
                        using var stream = await response.Content.ReadAsStreamAsync(token);
                        using var reader = new ArrowStreamReader(stream);
                        RecordBatch batch;
                        while ((batch = await reader.ReadNextRecordBatchAsync()) != null)
                        {
                            batches.Add(batch);
                            await batchWriter.WriteAsync(batch, token);
                        }
                        task.Increment(1);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error downloading chunk:[/] {ex.Message.EscapeMarkup()}");
                    }
                });
            });

        batchWriter.TryComplete();
        return batches.ToList();
    }

    private async Task ThrowDatabricksApiExceptionAsync(string responseBody)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize(responseBody, JsonContext.Default.ErrorResponse);
            throw new DatabricksApiException(errorResponse?.Message ?? "Unknown API error", errorResponse?.ErrorCode);
        }
        catch (JsonException)
        {
            throw new DatabricksApiException($"API request failed with response: {responseBody}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _downloadClient.Dispose();
    }
}
