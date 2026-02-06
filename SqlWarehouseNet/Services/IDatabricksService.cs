using System.Threading.Channels;
using Apache.Arrow;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public interface IDatabricksService : IDisposable
{
    void UpdateConnection(string? host, string? token);
    Task<string> ExecuteStatementAsync(
        string warehouseId,
        string query,
        CancellationToken cancellationToken = default
    );
    Task<StatementResultResponse> PollForResultAsync(
        string statementId,
        CancellationToken cancellationToken = default
    );
    Task<List<RecordBatch>> FetchArrowResultsStreamingAsync(
        StatementResultResponse status,
        ChannelWriter<RecordBatch> batchWriter,
        CancellationToken cancellationToken = default
    );
}
