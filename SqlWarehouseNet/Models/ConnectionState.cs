namespace SqlWarehouseNet.Models;

/// <summary>
/// Encapsulates the mutable connection state for the current Databricks session.
/// Replaces raw ref parameters passed through the call chain.
/// </summary>
public class ConnectionState
{
    public string? Host { get; set; }
    public string? Token { get; set; }
    public string? WarehouseId { get; set; }

    /// <summary>
    /// Returns true when all required connection details are present.
    /// </summary>
    public bool IsComplete =>
        !string.IsNullOrEmpty(Host)
        && !string.IsNullOrEmpty(Token)
        && !string.IsNullOrEmpty(WarehouseId);
}
