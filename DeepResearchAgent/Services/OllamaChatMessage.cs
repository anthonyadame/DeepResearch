namespace DeepResearchAgent.Services;

/// <summary>
/// Represents a chat message in a conversation.
/// </summary>
public class OllamaChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}
