using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

public class StreamState
{
    /// <summary>
    /// Gets or sets the current stream status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the research identifier.
    /// </summary>
    [JsonPropertyName("researchid")]
    public string ResearchId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original user query.
    /// </summary>
    [JsonPropertyName("userquery")]
    public string UserQuery { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the brief preview content.
    /// </summary>
    [JsonPropertyName("briefpreview")]
    public string BriefPreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the research brief content.
    /// </summary>
    [JsonPropertyName("researchbrief")]
    public string ResearchBrief { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the draft report content.
    /// </summary>
    [JsonPropertyName("draftreport")]
    public string DraftReport { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refined summary content.
    /// </summary>
    [JsonPropertyName("refinedsummary")]
    public string RefinedSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the final report content.
    /// </summary>
    [JsonPropertyName("finalreport")]
    public string FinalReport { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the supervisor update content.
    /// </summary>
    [JsonPropertyName("supervisorupdate")]
    public string SupervisorUpdate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the supervisor update count.
    /// </summary>
    [JsonPropertyName("supervisorupdatecount")]
    public int SupervisorUpdateCount { get; set; } = 0;
}