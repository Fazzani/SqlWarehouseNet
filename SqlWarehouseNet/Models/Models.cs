using System.Text.Json.Serialization;

namespace SqlWarehouseNet.Models;

public class StatementExecuteRequest
{
    [JsonPropertyName("warehouse_id")]
    public string WarehouseId { get; set; } = string.Empty;

    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("wait_timeout")]
    public string WaitTimeout { get; set; } = "10s";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "ARROW_STREAM";

    [JsonPropertyName("disposition")]
    public string Disposition { get; set; } = "EXTERNAL_LINKS";
}

public class StatementExecuteResponse
{
    [JsonPropertyName("statement_id")]
    public string StatementId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public StatementStatus Status { get; set; } = new();
}

public class StatementResultResponse
{
    [JsonPropertyName("statement_id")]
    public string StatementId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public StatementStatus Status { get; set; } = new();

    [JsonPropertyName("manifest")]
    public StatementManifest? Manifest { get; set; }

    [JsonPropertyName("result")]
    public StatementResult? Result { get; set; }
}

public class StatementStatus
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public StatementError? Error { get; set; }
}

public class StatementError
{
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class StatementManifest
{
    [JsonPropertyName("schema")]
    public StatementSchema? Schema { get; set; }
}

public class StatementSchema
{
    [JsonPropertyName("columns")]
    public List<StatementColumn>? Columns { get; set; }
}

public class StatementColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type_text")]
    public string? TypeText { get; set; }
}

public class StatementResult
{
    [JsonPropertyName("external_links")]
    public List<ExternalLink>? ExternalLinks { get; set; }

    [JsonPropertyName("row_count")]
    public long? RowCount { get; set; }
}

public class ExternalLink
{
    [JsonPropertyName("external_link")]
    public string Url { get; set; } = string.Empty;
}

public class ErrorResponse
{
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class DatabricksProfile
{
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;
    public string? Host { get; set; }
    public string? Token { get; set; }
    public string? WarehouseId { get; set; }
}

public class ProfileConfig
{
    public string DefaultProfile { get; set; } = string.Empty;
    public Dictionary<string, DatabricksProfile> Profiles { get; set; } = new();
}

public class DatabricksApiException : Exception
{
    public string? ErrorCode { get; }

    public DatabricksApiException(string message, string? errorCode = null) 
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
