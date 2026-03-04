namespace DeepResearch.Api.DTOs.Requests.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for AnalystAgent - Analyzes research findings and synthesizes insights.
/// </summary>
public class AnalystAgentRequest
{
    /// <summary>
    /// List of research findings/facts to analyze.
    /// </summary>
    public required List<FindingDto> Findings { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Research context for analysis guidance.
    /// </summary>
    public string? ResearchContext { get; set; }

    /// <summary>
    /// Specific analysis focus areas (comma-separated).
    /// </summary>
    public string? AnalysisFocus { get; set; }

    /// <summary>
    /// Enable contradiction detection.
    /// </summary>
    public bool DetectContradictions { get; set; } = true;

    /// <summary>
    /// Enable theme identification.
    /// </summary>
    public bool IdentifyThemes { get; set; } = true;

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
