namespace DeepResearchAgent.Models;

/// <summary>
/// A structure for storing a snapshot of the draft's quality at a specific iteration.
/// </summary>
public record QualityMetric
{
    /// <summary>
    /// The programmatic quality score calculated by our self-evolution evaluator.
    /// </summary>
    public required float Score { get; init; }

    /// <summary>
    /// The textual feedback from the evaluator explaining the score.
    /// </summary>
    public required string Feedback { get; init; }

    /// <summary>
    /// The iteration number at which this score was recorded, for tracking progress over time.
    /// </summary>
    public required int Iteration { get; init; }
}
