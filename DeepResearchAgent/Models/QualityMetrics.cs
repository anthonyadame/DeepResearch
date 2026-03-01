using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Quality metrics for evaluating clarification effectiveness.
/// Inspired by PromptWizard's feedback-driven evaluation approach.
/// </summary>
public record QualityMetrics
{
    /// <summary>
    /// Clarity score (0-100): Does the ask have sufficient detail?
    /// </summary>
    [JsonPropertyName("clarity_score")]
    public double ClarityScore { get; init; }

    /// <summary>
    /// Completeness score (0-100): Are all required dimensions present?
    /// </summary>
    [JsonPropertyName("completeness_score")]
    public double CompletenessScore { get; init; }

    /// <summary>
    /// Actionability score (0-100): Can research proceed confidently?
    /// </summary>
    [JsonPropertyName("actionability_score")]
    public double ActionabilityScore { get; init; }

    /// <summary>
    /// Overall combined quality score (0-100)
    /// </summary>
    [JsonPropertyName("overall_score")]
    public double OverallScore => (ClarityScore + CompletenessScore + ActionabilityScore) / 3.0;

    /// <summary>
    /// Identified gaps in the user's request
    /// </summary>
    [JsonPropertyName("identified_gaps")]
    public string[] IdentifiedGaps { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Reasoning behind the quality assessment
    /// </summary>
    [JsonPropertyName("critique_reasoning")]
    public string CritiqueReasoning { get; init; } = string.Empty;

    /// <summary>
    /// Whether the minimum quality threshold is met
    /// </summary>
    public bool MeetsThreshold(double threshold = 75.0) => OverallScore >= threshold;
}
