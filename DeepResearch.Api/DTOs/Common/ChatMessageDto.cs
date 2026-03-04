namespace DeepResearch.Api.DTOs.Common;

/// <summary>
/// Chat message for LLM conversations and agent interactions.
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// Message role: "user", "assistant", or "system".
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Message content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
