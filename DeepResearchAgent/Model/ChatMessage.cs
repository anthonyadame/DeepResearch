using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Represents a message in the agent conversation.
/// Used across all workflows for message accumulation and state management.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The role of the message sender (e.g., "user", "assistant", "system").
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Optional timestamp for message tracking.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
