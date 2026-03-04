namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from state management operations.
/// </summary>
public class StateManagementResponse
{
    /// <summary>
    /// State ID.
    /// </summary>
    public string StateId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent ID that owns the state.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// The state data.
    /// </summary>
    public Dictionary<string, object> StateData { get; set; } = new();

    /// <summary>
    /// State metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// When state was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When state was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When state will expire (if TTL set).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether state is persisted.
    /// </summary>
    public bool IsPersisted { get; set; }

    /// <summary>
    /// Operation status.
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Operation metadata.
    /// </summary>
    public ApiMetadata? OperationMetadata { get; set; }
}

/// <summary>
/// Response for state query operations.
/// </summary>
public class StateQueryResponse
{
    /// <summary>
    /// List of states matching query.
    /// </summary>
    public PaginatedResponse<StateManagementResponse>? States { get; set; }

    /// <summary>
    /// Status of query.
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }
}
