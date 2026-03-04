using DeepResearch.Api.Models.Chat;
using System.Collections.Concurrent;

namespace DeepResearch.Api.Services.Chat;

/// <summary>
/// Simple in-memory chat history service for WebUI
/// </summary>
public interface IChatHistoryService
{
    Task<ChatSession> CreateSessionAsync(string? title, CancellationToken cancellationToken = default);
    Task<List<ChatSession>> GetSessionsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default);
    Task AddMessageAsync(string sessionId, ChatMessage message, CancellationToken cancellationToken = default);
    Task UpdateSessionConfigAsync(string sessionId, ResearchConfig config, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of chat history service
/// </summary>
public class InMemoryChatHistoryService : IChatHistoryService
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
    private readonly ILogger<InMemoryChatHistoryService> _logger;

    public InMemoryChatHistoryService(ILogger<InMemoryChatHistoryService> logger)
    {
        _logger = logger;
    }

    public Task<ChatSession> CreateSessionAsync(string? title, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new ChatSession
        {
            Id = sessionId,
            Title = title ?? $"Chat {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Messages = new List<ChatMessage>(),
            Config = null,
            Categories = Array.Empty<string>(),
            IsArchived = false
        };

        _sessions[sessionId] = session;
        _logger.LogInformation("Created chat session: {SessionId}", sessionId);

        return Task.FromResult(session);
    }

    public Task<List<ChatSession>> GetSessionsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values
            .Where(s => includeArchived || !s.IsArchived)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();

        return Task.FromResult(sessions);
    }

    public Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryRemove(sessionId, out _))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        _logger.LogInformation("Deleted chat session: {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task<List<ChatMessage>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        return Task.FromResult(session.Messages);
    }

    public Task AddMessageAsync(string sessionId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var messages = new List<ChatMessage>(session.Messages) { message };
        var updatedSession = session with
        {
            Messages = messages,
            UpdatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = updatedSession;
        _logger.LogInformation("Added message to session {SessionId}", sessionId);

        return Task.CompletedTask;
    }

    public Task UpdateSessionConfigAsync(string sessionId, ResearchConfig config, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var updatedSession = session with
        {
            Config = config,
            UpdatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = updatedSession;
        _logger.LogInformation("Updated config for session {SessionId}", sessionId);

        return Task.CompletedTask;
    }
}
