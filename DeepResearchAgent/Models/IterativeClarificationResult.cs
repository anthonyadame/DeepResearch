using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Enhanced clarification result with iterative refinement tracking.
/// Extends base ClarificationResult with quality metrics and iteration history.
/// </summary>
public record IterativeClarificationResult : ClarificationResult
{
    /// <summary>
    /// Quality metrics for this clarification
    /// </summary>
    [JsonPropertyName("quality_metrics")]
    public QualityMetrics? QualityMetrics { get; init; }

    /// <summary>
    /// Number of refinement iterations performed
    /// </summary>
    [JsonPropertyName("iteration_count")]
    public int IterationCount { get; init; }

    /// <summary>
    /// Critique feedback from the last iteration
    /// </summary>
    [JsonPropertyName("last_critique")]
    public CritiqueFeedback? LastCritique { get; init; }

    /// <summary>
    /// Total time spent on iterative clarification (milliseconds)
    /// </summary>
    [JsonPropertyName("total_duration_ms")]
    public double TotalDurationMs { get; init; }

    /// <summary>
    /// History of iterations (for debugging/analysis)
    /// </summary>
    [JsonPropertyName("iteration_history")]
    public List<IterationSnapshot> IterationHistory { get; init; } = new();
}

/// <summary>
/// Snapshot of a single iteration in the clarification process
/// </summary>
public record IterationSnapshot
{
    [JsonPropertyName("iteration")]
    public int Iteration { get; init; }

    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("quality_score")]
    public double QualityScore { get; init; }

    [JsonPropertyName("critique_summary")]
    public string CritiqueSummary { get; init; } = string.Empty;

    [JsonPropertyName("duration_ms")]
    public double DurationMs { get; init; }
}
