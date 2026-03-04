namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from AnalystAgent - Analysis of research findings.
/// </summary>
public class AnalystAgentResponse
{
    /// <summary>
    /// Unique analysis ID.
    /// </summary>
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Themes identified in findings.
    /// </summary>
    public List<ThemeDto> ThemesIdentified { get; set; } = new();

    /// <summary>
    /// Contradictions detected in findings.
    /// </summary>
    public List<ContradictionDto> ContradictionsDetected { get; set; } = new();

    /// <summary>
    /// Importance scores for each finding.
    /// </summary>
    public Dictionary<string, double> FactImportanceScores { get; set; } = new();

    /// <summary>
    /// Synthesized key insights.
    /// </summary>
    public List<InsightDto> KeyInsights { get; set; } = new();

    /// <summary>
    /// Pattern analysis results.
    /// </summary>
    public List<PatternDto> PatternsIdentified { get; set; } = new();

    /// <summary>
    /// Confidence metrics for analysis.
    /// </summary>
    public AnalysisConfidenceDto? ConfidenceMetrics { get; set; }

    /// <summary>
    /// Recommended research areas for expansion.
    /// </summary>
    public List<string> RecommendedExpansionAreas { get; set; } = new();

    /// <summary>
    /// Identified knowledge gaps.
    /// </summary>
    public List<string> KnowledgeGaps { get; set; } = new();

    /// <summary>
    /// Overall quality score (0.0-1.0).
    /// </summary>
    public double OverallQualityScore { get; set; }

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
/// Confidence metrics for analysis.
/// </summary>
public class AnalysisConfidenceDto
{
    public double ThemeIdentificationConfidence { get; set; }
    public double ContradictionDetectionConfidence { get; set; }
    public double InsightSynthesisConfidence { get; set; }
    public double OverallAnalysisConfidence { get; set; }
}
