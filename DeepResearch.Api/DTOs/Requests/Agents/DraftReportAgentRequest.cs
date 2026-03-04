namespace DeepResearch.Api.DTOs.Requests.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for DraftReportAgent - Generates initial draft report from research brief.
/// </summary>
public class DraftReportAgentRequest
{
    /// <summary>
    /// The structured research brief to create draft from.
    /// </summary>
    public required string ResearchBrief { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Optional writing style or tone guidance.
    /// </summary>
    public string? WritingStyle { get; set; }

    /// <summary>
    /// Optional length preference: Brief, Standard, Detailed.
    /// </summary>
    public string? LengthPreference { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
