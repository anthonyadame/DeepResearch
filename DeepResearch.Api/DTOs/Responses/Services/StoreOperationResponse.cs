namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from store operations.
/// </summary>
public class StoreOperationResponse
{
    /// <summary>
    /// Operation performed.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Key used in operation.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Value retrieved or stored.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// For List operation - all keys.
    /// </summary>
    public List<string>? Keys { get; set; }

    /// <summary>
    /// Whether operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of items affected.
    /// </summary>
    public int ItemsAffected { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
