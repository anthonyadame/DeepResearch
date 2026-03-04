namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from ResearcherAgent - Research execution results with findings.
/// </summary>
public class ResearcherAgentResponse
{
    /// <summary>
    /// Unique research execution ID.
    /// </summary>
    public string ResearchExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Topic that was researched.
    /// </summary>
    public string ResearchTopic { get; set; } = string.Empty;

    /// <summary>
    /// Extracted facts and findings.
    /// </summary>
    public List<FactResultDto> Facts { get; set; } = new();

    /// <summary>
    /// Research topics covered.
    /// </summary>
    public List<string> TopicsCovered { get; set; } = new();

    /// <summary>
    /// Quality scores from each iteration.
    /// </summary>
    public List<double> IterationQualityScores { get; set; } = new();

    /// <summary>
    /// Overall quality score (0.0-1.0).
    /// </summary>
    public double OverallQualityScore { get; set; }

    /// <summary>
    /// Number of iterations completed.
    /// </summary>
    public int IterationsCompleted { get; set; }

    /// <summary>
    /// Number of web searches performed.
    /// </summary>
    public int SearchesPerformed { get; set; }

    /// <summary>
    /// Number of unique sources consulted.
    /// </summary>
    public int SourcesConsulted { get; set; }

    /// <summary>
    /// Key themes identified in research.
    /// </summary>
    public List<string> KeyThemes { get; set; } = new();

    /// <summary>
    /// Contradictions found during research (if any).
    /// </summary>
    public List<string> ContradictionsFound { get; set; } = new();

    /// <summary>
    /// Recommendations from research.
    /// </summary>
    public List<string>? Recommendations { get; set; }

    /// <summary>
    /// Status: Completed, InProgress, Failed.
    /// </summary>
    public string Status { get; set; } = "Completed";

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
/// A fact result from research.
/// </summary>
public class FactResultDto
{
    /// <summary>
    /// Unique fact ID.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The fact statement.
    /// </summary>
    public string Statement { get; set; } = string.Empty;

    /// <summary>
    /// Source URL.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Confidence score (0.0-1.0).
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Topic category.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// When extracted.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
