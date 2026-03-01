namespace DeepResearchAgent.Models;

/// <summary>
/// A structured model for adversarial feedback from the Red Team or other quality control agents.
/// </summary>
public record CritiqueState
{
    /// <summary>
    /// Tracks which agent generated the critique (e.g., "Red Team", "Safety Filter") for accountability.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// The specific logical fallacy, bias, or factual error that was found in the draft.
    /// </summary>
    public required string Concern { get; init; }

    /// <summary>
    /// A 1-10 score to quantify the severity of the issue, allowing the Supervisor agent to prioritize its actions.
    /// </summary>
    public required int Severity { get; init; }

    /// <summary>
    /// A flag to track whether a critique has been addressed in a subsequent revision of the draft.
    /// </summary>
    public bool Addressed { get; init; } = false;
}
