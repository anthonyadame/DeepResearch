using DeepResearch.Api.DTOs;
using System.Collections.Concurrent;

namespace DeepResearch.Api.Services;

/// <summary>
/// Interface for chat session management
/// </summary>
public interface IChatSessionService
{
    Task<ChatSession> CreateSessionAsync(string? title);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<List<ChatSession>> GetSessionsAsync();
    Task DeleteSessionAsync(string sessionId);
    Task<ChatMessage> AddMessageAsync(string sessionId, ChatMessage message);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId);
}

/// <summary>
/// In-memory implementation of chat session service
/// TODO: Replace with persistent storage (database, Lightning, etc.)
/// </summary>
public class ChatSessionService : IChatSessionService
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
    private readonly ILogger<ChatSessionService> _logger;

    public ChatSessionService(ILogger<ChatSessionService> logger)
    {
        _logger = logger;
    }

    public Task<ChatSession> CreateSessionAsync(string? title)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new ChatSession
        {
            Id = sessionId,
            Title = title ?? $"Chat {DateTime.Now:g}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Messages = new List<ChatMessage>(),
            Config = null
        };

        if (!_sessions.TryAdd(sessionId, session))
        {
            _logger.LogWarning("Failed to add session {SessionId} to dictionary", sessionId);
            throw new InvalidOperationException("Failed to create session");
        }

        _logger.LogInformation("Created session {SessionId} with title: {Title}", sessionId, session.Title);
        return Task.FromResult(session);
    }

    public Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<List<ChatSession>> GetSessionsAsync()
    {
        var sessions = _sessions.Values
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();
        
        _logger.LogInformation("Retrieved {Count} sessions", sessions.Count);
        return Task.FromResult(sessions);
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var removed))
        {
            _logger.LogWarning("Session {SessionId} not found for deletion", sessionId);
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        _logger.LogInformation("Deleted session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task<ChatMessage> AddMessageAsync(string sessionId, ChatMessage message)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Session {SessionId} not found when adding message", sessionId);
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Update session with new message
        var updatedMessages = new List<ChatMessage>(session.Messages) { message };
        var updatedSession = session with
        {
            Messages = updatedMessages,
            UpdatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = updatedSession;

        _logger.LogInformation("Added {Role} message to session {SessionId}", message.Role, sessionId);
        return Task.FromResult(message);
    }

    public Task<List<ChatMessage>> GetHistoryAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Session {SessionId} not found when retrieving history", sessionId);
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        _logger.LogInformation("Retrieved {Count} messages for session {SessionId}", 
            session.Messages.Count, sessionId);
        return Task.FromResult(session.Messages);
    }
}
