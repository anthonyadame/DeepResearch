namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from DraftReportAgent - Initial draft report from research brief.
/// </summary>
public class DraftReportAgentResponse
{
    /// <summary>
    /// Unique draft identifier.
    /// </summary>
    public string DraftId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The draft report content.
    /// </summary>
    public string DraftContent { get; set; } = string.Empty;

    /// <summary>
    /// Quality score of the draft (0.0-1.0).
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Estimated completeness (0.0-1.0) for further refinement.
    /// </summary>
    public double Completeness { get; set; }

    /// <summary>
    /// Structure identified in the draft.
    /// </summary>
    public List<string> ReportSections { get; set; } = new();

    /// <summary>
    /// Identified areas that may need expansion.
    /// </summary>
    public List<string> AreasForExpansion { get; set; } = new();

    /// <summary>
    /// Identified gaps in coverage.
    /// </summary>
    public List<string> IdentifiedGaps { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement.
    /// </summary>
    public List<string> ImprovementSuggestions { get; set; } = new();

    /// <summary>
    /// Status: Success, Error.
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
