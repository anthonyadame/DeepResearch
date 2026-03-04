namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for state management - Create or update agent state.
/// </summary>
public class StateManagementRequest
{
    /// <summary>
    /// Optional state ID. If null, creates new state.
    /// </summary>
    public string? StateId { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Agent identifier that owns this state.
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// State data as JSON object.
    /// </summary>
    public required Dictionary<string, object> StateData { get; set; }

    /// <summary>
    /// Optional state metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Whether to persist state to storage.
    /// </summary>
    public bool Persist { get; set; } = true;

    /// <summary>
    /// TTL for the state in seconds.
    /// </summary>
    public int? TimeToLiveSeconds { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>
/// Request to query existing state.
/// </summary>
public class StateQueryRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// State ID to retrieve.
    /// </summary>
    public string? StateId { get; set; }

    /// <summary>
    /// Agent ID to list states for.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Pagination parameters.
    /// </summary>
    public PaginationDto? Pagination { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
