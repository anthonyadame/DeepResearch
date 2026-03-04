namespace DeepResearch.Api.DTOs.Requests.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for ResearchBriefAgent - Transforms user query into structured research brief.
/// </summary>
public class ResearchBriefAgentRequest
{
    /// <summary>
    /// The user's research query to transform into brief.
    /// </summary>
    public required string UserQuery { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Optional background context or constraints.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
