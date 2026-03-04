namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;
using System.Text.Json.Serialization;

/// <summary>
/// Response from ReportAgent - Finalized synthesized report.
/// </summary>
public class ReportAgentResponse
{
    /// <summary>
    /// Unique report ID.
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Report title.
    /// </summary>
    public string ReportTitle { get; set; } = string.Empty;

    /// <summary>
    /// Finalized report content.
    /// </summary>
    public string ReportContent { get; set; } = string.Empty;

    /// <summary>
    /// Report format/style used.
    /// </summary>
    public string ReportStyle { get; set; } = "Standard";

    /// <summary>
    /// Report sections included.
    /// </summary>
    public List<string> ReportSections { get; set; } = new();

    /// <summary>
    /// Executive summary if included.
    /// </summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>
    /// Key findings highlighted in report.
    /// </summary>
    public List<string> KeyFindings { get; set; } = new();

    /// <summary>
    /// Recommendations if included.
    /// </summary>
    public List<string>? Recommendations { get; set; }

    /// <summary>
    /// Citation count.
    /// </summary>
    public int CitationCount { get; set; }

    /// <summary>
    /// Sources cited in report.
    /// </summary>
    public List<CitationDto> Citations { get; set; } = new();

    /// <summary>
    /// Overall quality score (0.0-1.0).
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Readability score (0.0-1.0).
    /// </summary>
    public double ReadabilityScore { get; set; }

    /// <summary>
    /// Estimated word count.
    /// </summary>
    public int EstimatedWordCount { get; set; }

    /// <summary>
    /// Estimated read time in minutes.
    /// </summary>
    public int EstimatedReadTimeMinutes { get; set; }

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

/// <summary>
/// Citation in the report.
/// </summary>
public class CitationDto
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Author { get; set; }
    public string? PublicationDate { get; set; }
    public int PageReferences { get; set; }
}
