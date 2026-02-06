using System.Text.Json.Serialization;

namespace SqlWarehouseNet.Models;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(StatementExecuteRequest))]
[JsonSerializable(typeof(StatementExecuteResponse))]
[JsonSerializable(typeof(StatementResultResponse))]
[JsonSerializable(typeof(ExternalLink))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ProfileConfig))]
[JsonSerializable(typeof(Dictionary<string, DatabricksProfile>))]
[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
internal partial class JsonContext : JsonSerializerContext { }
