using Apache.Arrow;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public interface IExportService
{
    void ExportToCsv(string path, StatementResultResponse status, List<RecordBatch> batches);
    void ExportToJson(string path, StatementResultResponse status, List<RecordBatch> batches);
}
