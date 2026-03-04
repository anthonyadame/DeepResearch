namespace DeepResearch.Api.DTOs;

/// <summary>
/// Represents a chat session with messages and configuration
/// </summary>
public record ChatSession
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public List<ChatMessage> Messages { get; init; } = new();
    public ResearchConfig? Config { get; init; }
}

/// <summary>
/// Represents a chat message in a session
/// </summary>
public record ChatMessage
{
    public required string Id { get; init; }
    public required string Role { get; init; } // "user", "assistant", "system"
    public required string Content { get; init; }
    public required DateTime Timestamp { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Request to create a new chat session
/// </summary>
public record CreateSessionRequest
{
    public string? Title { get; init; }
}

/// <summary>
/// Request to send a message in a chat session
/// </summary>
public record SendMessageRequest
{
    public required string Message { get; init; }
    public ResearchConfig? Config { get; init; }
}

/// <summary>
/// Research configuration for a chat session
/// </summary>
public record ResearchConfig
{
    public string Language { get; init; } = "English";
    public List<string> LlmModels { get; init; } = new();
    public List<string> IncludedWebsites { get; init; } = new();
    public List<string> ExcludedWebsites { get; init; } = new();
    public List<string> Topics { get; init; } = new();
    public int? MaxDepth { get; init; }
    public int? TimeoutSeconds { get; init; }
}

/// <summary>
/// Response for file upload
/// </summary>
public record FileUploadResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required string ContentType { get; init; }
    public required DateTime UploadedAt { get; init; }
}
