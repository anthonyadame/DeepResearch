namespace DeepResearch.Api.DTOs.Requests.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for ResearcherAgent - Plans and executes research on a topic.
/// </summary>
public class ResearcherAgentRequest
{
    /// <summary>
    /// The research topic to investigate.
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Maximum iterations for research refinement.
    /// </summary>
    public int MaxIterations { get; set; } = 3;

    /// <summary>
    /// Optional research context or constraints.
    /// </summary>
    public string? ResearchContext { get; set; }

    /// <summary>
    /// Whether to focus on specific aspects (comma-separated).
    /// </summary>
    public string? FocusAreas { get; set; }

    /// <summary>
    /// Whether to run asynchronously.
    /// </summary>
    public bool RunAsync { get; set; } = false;

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
