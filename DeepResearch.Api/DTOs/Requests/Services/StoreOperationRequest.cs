namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for data store operations - Key-value persistence.
/// </summary>
public class StoreOperationRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Operation type: Get, Store, Remove, List, Clear.
    /// </summary>
    public required string Operation { get; set; }

    /// <summary>
    /// Key for the operation.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Value to store (for Store operation).
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// TTL in seconds.
    /// </summary>
    public int? TimeToLiveSeconds { get; set; }

    /// <summary>
    /// Pattern for List operation.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
