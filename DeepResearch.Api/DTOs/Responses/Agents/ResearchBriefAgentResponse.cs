namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from ResearchBriefAgent - Structured research brief from user query.
/// </summary>
public class ResearchBriefAgentResponse
{
    /// <summary>
    /// Unique brief identifier.
    /// </summary>
    public string BriefId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The original user query.
    /// </summary>
    public string OriginalQuery { get; set; } = string.Empty;

    /// <summary>
    /// Research objectives extracted from query.
    /// </summary>
    public List<string> ResearchObjectives { get; set; } = new();

    /// <summary>
    /// Key questions to address in research.
    /// </summary>
    public List<string> KeyQuestions { get; set; } = new();

    /// <summary>
    /// Scope of research.
    /// </summary>
    public string ResearchScope { get; set; } = string.Empty;

    /// <summary>
    /// Target audience identified.
    /// </summary>
    public string? TargetAudience { get; set; }

    /// <summary>
    /// Key topics to cover.
    /// </summary>
    public List<string> KeyTopics { get; set; } = new();

    /// <summary>
    /// Recommended research depth: Surface, Moderate, Deep.
    /// </summary>
    public string RecommendedDepth { get; set; } = "Moderate";

    /// <summary>
    /// Estimated required time for research.
    /// </summary>
    public string? EstimatedTimeRequired { get; set; }

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
