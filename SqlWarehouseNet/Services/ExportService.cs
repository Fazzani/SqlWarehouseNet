using System.Text;
using System.Text.Json;
using Apache.Arrow;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public class ExportService
{
    public void ExportToCsv(string path, StatementResultResponse status, List<RecordBatch> batches)
    {
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        
        if (status.Manifest?.Schema?.Columns != null)
        {
            writer.WriteLine(string.Join(",", status.Manifest.Schema.Columns.Select(c => $"\"{c.Name.Replace("\"", "\"\"")}\"")));
        }

        foreach (var batch in batches)
        {
            for (int i = 0; i < batch.Length; i++)
            {
                var values = new List<string>();
                for (int col = 0; col < batch.ColumnCount; col++)
                {
                    var val = ArrowUtils.GetValueFromArrowColumn(batch.Column(col), i);
                    var strVal = val?.ToString() ?? "";
                    values.Add($"\"{strVal.Replace("\"", "\"\"")}\"");
                }
                writer.WriteLine(string.Join(",", values));
            }
        }
    }

    public void ExportToJson(string path, StatementResultResponse status, List<RecordBatch> batches)
    {
        var rows = new List<Dictionary<string, object?>>();
        var colNames = status.Manifest?.Schema?.Columns?.Select(c => c.Name).ToList() ?? new List<string>();

        foreach (var batch in batches)
        {
            for (int i = 0; i < batch.Length; i++)
            {
                var row = new Dictionary<string, object?>();
                for (int col = 0; col < batch.ColumnCount; col++)
                {
                    var colName = col < colNames.Count ? colNames[col] : $"col_{col}";
                    row[colName] = ArrowUtils.GetValueFromArrowColumn(batch.Column(col), i);
                }
                rows.Add(row);
            }
        }

        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}
