namespace DeepResearch.Api.DTOs.Requests.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for ReportAgent - Generates final synthesized report from research and analysis.
/// </summary>
public class ReportAgentRequest
{
    /// <summary>
    /// The research findings to include in report.
    /// </summary>
    public required string ResearchContent { get; set; }

    /// <summary>
    /// The analysis insights to synthesize.
    /// </summary>
    public required string AnalysisContent { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Optional report title.
    /// </summary>
    public string? ReportTitle { get; set; }

    /// <summary>
    /// Optional report style/format: Academic, Executive, Technical, General.
    /// </summary>
    public string? ReportStyle { get; set; }

    /// <summary>
    /// Whether to include citations.
    /// </summary>
    public bool IncludeCitations { get; set; } = true;

    /// <summary>
    /// Whether to include table of contents.
    /// </summary>
    public bool IncludeTableOfContents { get; set; } = true;

    /// <summary>
    /// Whether to include recommendations section.
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Whether to include summary section.
    /// </summary>
    public bool IncludeSummary { get; set; } = true;

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
