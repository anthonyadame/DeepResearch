using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Feedback from critique step analyzing a clarification question.
/// Implements PromptWizard's critique-and-refine mechanism.
/// </summary>
public record CritiqueFeedback
{
    /// <summary>
    /// Whether the clarification question is sufficiently focused
    /// </summary>
    [JsonPropertyName("is_sufficiently_focused")]
    public bool IsSufficientlyFocused { get; init; }

    /// <summary>
    /// Identified weaknesses in the clarification question
    /// </summary>
    [JsonPropertyName("weaknesses")]
    public string[] Weaknesses { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Suggested improvements to the clarification approach
    /// </summary>
    [JsonPropertyName("suggested_improvements")]
    public string[] SuggestedImprovements { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Quality dimensions that need strengthening
    /// </summary>
    [JsonPropertyName("dimensions_to_improve")]
    public string[] DimensionsToImprove { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Overall critique reasoning
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// Confidence in this critique (0.0-1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}
