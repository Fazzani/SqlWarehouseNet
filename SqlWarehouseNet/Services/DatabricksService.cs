using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Spectre.Console;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public class DatabricksService : IDatabricksService
{
    private HttpClient _httpClient;
    private readonly HttpClient _downloadClient = new();
    private bool _disposed;

    public DatabricksService()
    {
        _httpClient = CreateApiClient(null, null);
    }

    private static HttpClient CreateApiClient(string? host, string? token)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
        if (!string.IsNullOrEmpty(host))
        {
            client.BaseAddress = new Uri(host);
        }
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );
        }
        return client;
    }

    public void UpdateConnection(string? host, string? token)
    {
        if (string.IsNullOrEmpty(host))
            return;

        var cleanHost = host.Trim().Trim('"').TrimEnd('/');
        var cleanToken = (token ?? "").Trim().Trim('"');

        // Recreate HttpClient — BaseAddress is immutable after first request
        var oldClient = _httpClient;
        _httpClient = CreateApiClient(cleanHost, cleanToken);
        oldClient.Dispose();
    }

    public async Task<string> ExecuteStatementAsync(
        string warehouseId,
        string query,
        CancellationToken cancellationToken = default
    )
    {
        var request = new StatementExecuteRequest
        {
            WarehouseId = warehouseId,
            Statement = query,
            WaitTimeout = "0s",
            Format = "ARROW_STREAM",
            Disposition = "EXTERNAL_LINKS",
        };

        var json = JsonSerializer.Serialize(request, JsonContext.Default.StatementExecuteRequest);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/2.0/sql/statements");
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Headers.Add("X-Databricks-Result-Compression", "LZ4");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            ThrowDatabricksApiException(responseBody);
        }

        var executeResponse =
            JsonSerializer.Deserialize(responseBody, JsonContext.Default.StatementExecuteResponse)
            ?? throw new InvalidOperationException("Failed to deserialize execute response.");

        return executeResponse.StatementId;
    }

    public async Task<StatementResultResponse> PollForResultAsync(
        string statementId,
        CancellationToken cancellationToken = default
    )
    {
        const int maxAttempts = 120;
        const int initialDelayMs = 500;
        const double backoffMultiplier = 1.5;
        const int maxDelayMs = 10_000;

        return await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(
                "[yellow]Running query...[/]",
                async ctx =>
                {
                    int currentDelay = initialDelayMs;

                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var response = await _httpClient.GetAsync(
                            $"/api/2.0/sql/statements/{statementId}",
                            cancellationToken
                        );
                        var responseBody = await response.Content.ReadAsStringAsync(
                            cancellationToken
                        );

                        if (!response.IsSuccessStatusCode)
                        {
                            ThrowDatabricksApiException(responseBody);
                        }

                        var statusResponse =
                            JsonSerializer.Deserialize(
                                responseBody,
                                JsonContext.Default.StatementResultResponse
                            )
                            ?? throw new InvalidOperationException(
                                "Failed to deserialize status response."
                            );

                        var state = statusResponse.Status.State;
                        ctx.Status(
                            $"[yellow]Query state: {state.EscapeMarkup()} (attempt {attempt + 1}/{maxAttempts})[/]"
                        );

                        switch (state)
                        {
                            case "SUCCEEDED":
                                AnsiConsole.MarkupLine("[green]✓[/] Query completed successfully.");
                                return statusResponse;
                            case "FAILED":
                                throw new DatabricksApiException(
                                    $"Query failed: {statusResponse.Status.Error?.Message ?? "Unknown error"}",
                                    statusResponse.Status.Error?.ErrorCode
                                );
                            case "CANCELED":
                                throw new DatabricksApiException("Query was canceled.");
                            case "CLOSED":
                                throw new DatabricksApiException("Query was closed.");
                            case "PENDING":
                            case "RUNNING":
                                await Task.Delay(currentDelay, cancellationToken);
                                currentDelay = Math.Min(
                                    (int)(currentDelay * backoffMultiplier),
                                    maxDelayMs
                                );
                                continue;
                            default:
                                throw new DatabricksApiException($"Unknown state: {state}");
                        }
                    }
                    throw new TimeoutException(
                        $"Query did not complete within the timeout period ({maxAttempts} attempts)."
                    );
                }
            );
    }

    public async Task<List<RecordBatch>> FetchArrowResultsStreamingAsync(
        StatementResultResponse status,
        ChannelWriter<RecordBatch> batchWriter,
        CancellationToken cancellationToken = default
    )
    {
        if (status.Result?.ExternalLinks == null || status.Result.ExternalLinks.Count == 0)
        {
            batchWriter.TryComplete();
            return new List<RecordBatch>();
        }

        var links = status.Result.ExternalLinks;
        // Use an array indexed by chunk position to preserve order
        var orderedBatches = new ConcurrentDictionary<int, List<RecordBatch>>();

        await AnsiConsole
            .Progress()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(
                    $"[cyan]Downloading {links.Count} chunks (Parallel Streaming)[/]",
                    true,
                    links.Count
                );

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, links.Count),
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 8,
                        CancellationToken = cancellationToken,
                    },
                    async (index, ct) =>
                    {
                        try
                        {
                            var chunkBatches = new List<RecordBatch>();
                            using var response = await _downloadClient.GetAsync(
                                links[index].Url,
                                HttpCompletionOption.ResponseHeadersRead,
                                ct
                            );
                            response.EnsureSuccessStatusCode();
                            using var stream = await response.Content.ReadAsStreamAsync(ct);
                            using var reader = new ArrowStreamReader(stream);
                            RecordBatch batch;
                            while ((batch = await reader.ReadNextRecordBatchAsync(ct)) != null)
                            {
                                chunkBatches.Add(batch);
                                await batchWriter.WriteAsync(batch, ct);
                            }
                            orderedBatches[index] = chunkBatches;
                            task.Increment(1);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            AnsiConsole.MarkupLine(
                                $"[red]Error downloading chunk {index}:[/] {ex.Message.EscapeMarkup()}"
                            );
                        }
                    }
                );
            });

        batchWriter.TryComplete();

        // Return batches in the original chunk order
        return orderedBatches.OrderBy(kvp => kvp.Key).SelectMany(kvp => kvp.Value).ToList();
    }

    private static void ThrowDatabricksApiException(string responseBody)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize(
                responseBody,
                JsonContext.Default.ErrorResponse
            );
            throw new DatabricksApiException(
                errorResponse?.Message ?? "Unknown API error",
                errorResponse?.ErrorCode
            );
        }
        catch (JsonException)
        {
            throw new DatabricksApiException($"API request failed with response: {responseBody}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _httpClient.Dispose();
        _downloadClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
