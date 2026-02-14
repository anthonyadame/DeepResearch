using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Result of the user clarification decision.
/// Structured output from the Clarify agent to determine if additional information is needed.
/// Maps to Python's ClarifyWithUser class.
/// </summary>
public record ClarificationResult
{
    /// <summary>
    /// Whether clarification is needed from the user.
    /// If true, the system will ask a clarifying question and pause execution.
    /// If false, the system will proceed with research using the provided information.
    /// </summary>
    [JsonPropertyName("need_clarification")]
    public required bool NeedClarification { get; init; }

    /// <summary>
    /// The clarifying question to ask the user.
    /// Only populated when NeedClarification is true.
    /// </summary>
    [JsonPropertyName("question")]
    public required string Question { get; init; }

    /// <summary>
    /// Verification message confirming that research will begin.
    /// Only populated when NeedClarification is false.
    /// Acknowledges the user's input and confirms the research process will start.
    /// </summary>
    [JsonPropertyName("verification")]
    public required string Verification { get; init; }
}
