using DeepResearch.Api.DTOs;
using DeepResearchAgent.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepResearch.Api.Services.ChatHistory;

/// <summary>
/// Persistent chat session data model for LightningStore
/// Extends ChatSession with metadata for categorization and archiving
/// </summary>
public record PersistentChatSession
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public List<ChatMessage> Messages { get; init; } = new();
    public ResearchConfig? Config { get; init; }
    
    // Extended metadata
    public string[] Categories { get; init; } = Array.Empty<string>();
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public int MessageCount { get; init; }
    public long SizeBytes { get; init; }
    
    public ChatSession ToChatSession() => new()
    {
        Id = Id,
        Title = Title,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Messages = Messages,
        Config = Config
    };
}

/// <summary>
/// LightningStore-based implementation of chat history service
/// Supports AI categorization and automatic archiving
/// </summary>
public class LightningChatHistoryService : IChatHistoryService
{
    private readonly ICategorizationService _categorization;
    private readonly ILogger<LightningChatHistoryService> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    
    private const string ActiveTag = "active";
    private const string ArchivedTag = "archived";
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LightningChatHistoryService(
        ICategorizationService categorization,
        ILogger<LightningChatHistoryService> logger)
    {
        _categorization = categorization;
        _logger = logger;
    }

    public async Task<ChatSession> CreateSessionAsync(string? title, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        
        var session = new PersistentChatSession
        {
            Id = sessionId,
            Title = title ?? $"Chat {now:g}",
            CreatedAt = now,
            UpdatedAt = now,
            Messages = new List<ChatMessage>(),
            Config = null,
            Categories = Array.Empty<string>(),
            IsArchived = false,
            MessageCount = 0,
            SizeBytes = 0
        };

        await StoreSessionAsync(session, cancellationToken);
        
        _logger.LogInformation("✅ [STORAGE] Created session: {SessionId} | Title: {Title}", sessionId, session.Title);
        return session.ToChatSession();
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 [STORAGE] Loading session: {SessionId}", sessionId);
        var persistentSession = await LoadSessionAsync(sessionId, cancellationToken);
        
        if (persistentSession != null)
        {
            _logger.LogInformation("✅ [STORAGE] Loaded session: {SessionId}", sessionId);
        }
        else
        {
            _logger.LogWarning("❌ [STORAGE] Session not found: {SessionId}", sessionId);
        }
        
        return persistentSession?.ToChatSession();
    }

    public async Task<List<ChatSession>> GetSessionsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var tag = includeArchived ? "chat" : ActiveTag;
        _logger.LogInformation("🔍 [STORAGE] Searching sessions with tag: {Tag}", tag);
        
        var sessions = await SearchSessionsByTagAsync(tag, cancellationToken);
        
        var filtered = sessions
            .Where(s => includeArchived || !s.IsArchived)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => s.ToChatSession())
            .ToList();
        
        _logger.LogInformation("✅ [STORAGE] Found {Count} sessions", filtered.Count);
        return filtered;
    }

    public async Task<List<ChatSession>> GetSessionsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var allSessions = await SearchSessionsByTagAsync(category, cancellationToken);
        
        return allSessions
            .Where(s => s.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => s.ToChatSession())
            .ToList();
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Actually delete the session file
        var dataDir = "data/chat-sessions";
        var filePath = Path.Combine(dataDir, $"{sessionId}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Attempted to delete non-existent session: {SessionId}", sessionId);
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        File.Delete(filePath);
        _logger.LogInformation("✓ Deleted session file: {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public async Task<ChatMessage> AddMessageAsync(string sessionId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("📝 [STORAGE] Adding {Role} message to session {SessionId}", message.Role, sessionId);
            
            var session = await LoadSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("❌ [STORAGE] Session {SessionId} not found when adding message", sessionId);
                throw new KeyNotFoundException($"Session {sessionId} not found");
            }

            var updatedMessages = new List<ChatMessage>(session.Messages) { message };
            var messageJson = JsonSerializer.Serialize(message, _jsonOptions);
            
            var updatedSession = session with
            {
                Messages = updatedMessages,
                UpdatedAt = DateTime.UtcNow,
                MessageCount = updatedMessages.Count,
                SizeBytes = session.SizeBytes + messageJson.Length
            };

            await StoreSessionAsync(updatedSession, cancellationToken);
            
            _logger.LogInformation("✅ [STORAGE] Added {Role} message to session {SessionId} | Total: {Count}", 
                message.Role, sessionId, updatedMessages.Count);
            return message;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<List<ChatMessage>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found when retrieving history", sessionId);
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        _logger.LogInformation("✓ Retrieved {Count} messages for session {SessionId}", 
            session.Messages.Count, sessionId);
        return session.Messages;
    }

    public async Task<string[]> CategorizeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Get first 5 messages for categorization
        var messageSnippets = session.Messages
            .Take(5)
            .Select(m => m.Content.Length > 200 ? m.Content[..200] : m.Content)
            .ToArray();

        var categories = await _categorization.CategorizeAsync(
            session.Title,
            messageSnippets,
            cancellationToken
        );

        await UpdateCategoriesAsync(sessionId, categories, cancellationToken);
        return categories;
    }

    public async Task UpdateCategoriesAsync(string sessionId, string[] categories, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var updatedSession = session with
        {
            Categories = categories,
            UpdatedAt = DateTime.UtcNow
        };

        await StoreSessionAsync(updatedSession, cancellationToken);
        _logger.LogInformation("Updated categories for session {SessionId}: {Categories}", 
            sessionId, string.Join(", ", categories));
    }

    public async Task ArchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var archivedSession = session with
        {
            IsArchived = true,
            ArchivedAt = DateTime.UtcNow
        };

        await StoreSessionAsync(archivedSession, cancellationToken);
        _logger.LogInformation("Archived session {SessionId}", sessionId);
    }

    public async Task UnarchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        var unarchivedSession = session with
        {
            IsArchived = false,
            ArchivedAt = null
        };

        await StoreSessionAsync(unarchivedSession, cancellationToken);
        _logger.LogInformation("Unarchived session {SessionId}", sessionId);
    }

    public async Task<int> AutoArchiveOldSessionsAsync(int daysOld = 30, CancellationToken cancellationToken = default)
    {
        var allSessions = await SearchSessionsByTagAsync(ActiveTag, cancellationToken);
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        
        var oldSessions = allSessions
            .Where(s => !s.IsArchived && s.UpdatedAt < cutoffDate)
            .ToList();

        foreach (var session in oldSessions)
        {
            await ArchiveSessionAsync(session.Id, cancellationToken);
        }

        _logger.LogInformation("Auto-archived {Count} sessions older than {Days} days", 
            oldSessions.Count, daysOld);
        return oldSessions.Count;
    }

    public async Task<int> AutoArchiveLargeSessionsAsync(int maxMessages = 1000, CancellationToken cancellationToken = default)
    {
        var allSessions = await SearchSessionsByTagAsync(ActiveTag, cancellationToken);
        
        var largeSessions = allSessions
            .Where(s => !s.IsArchived && s.MessageCount > maxMessages)
            .ToList();

        foreach (var session in largeSessions)
        {
            await ArchiveSessionAsync(session.Id, cancellationToken);
        }

        _logger.LogInformation("Auto-archived {Count} sessions with more than {MaxMessages} messages", 
            largeSessions.Count, maxMessages);
        return largeSessions.Count;
    }

    public async Task<ChatHistoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var allSessions = await SearchSessionsByTagAsync("chat", cancellationToken);
        
        var activeSessions = allSessions.Where(s => !s.IsArchived).ToList();
        var archivedSessions = allSessions.Where(s => s.IsArchived).ToList();
        
        var categoryCounts = allSessions
            .SelectMany(s => s.Categories)
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ChatHistoryStatistics
        {
            TotalSessions = allSessions.Count,
            ActiveSessions = activeSessions.Count,
            ArchivedSessions = archivedSessions.Count,
            TotalMessages = allSessions.Sum(s => s.MessageCount),
            CategoryCounts = categoryCounts,
            StorageSizeBytes = allSessions.Sum(s => s.SizeBytes),
            OldestSession = allSessions.Any() ? allSessions.Min(s => s.CreatedAt) : null,
            NewestSession = allSessions.Any() ? allSessions.Max(s => s.CreatedAt) : null
        };
    }

    // Private helper methods
    
    private async Task StoreSessionAsync(PersistentChatSession session, CancellationToken cancellationToken)
    {
        try
        {
            // Use file-based storage for chat sessions (simpler and more reliable)
            // LightningStore is designed for facts/research data, not chat sessions
            var dataDir = "data/chat-sessions";
            Directory.CreateDirectory(dataDir);
            
            var filePath = Path.Combine(dataDir, $"{session.Id}.json");
            var sessionJson = JsonSerializer.Serialize(session, _jsonOptions);
            
            await File.WriteAllTextAsync(filePath, sessionJson, cancellationToken);
            
            _logger.LogInformation("✓ Stored session {SessionId} to file", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store session {SessionId}", session.Id);
            throw;
        }
    }

    private async Task<PersistentChatSession?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var dataDir = "data/chat-sessions";
            var filePath = Path.Combine(dataDir, $"{sessionId}.json");
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Session file not found: {SessionId}", sessionId);
                return null;
            }

            var sessionJson = await File.ReadAllTextAsync(filePath, cancellationToken);
            var session = JsonSerializer.Deserialize<PersistentChatSession>(sessionJson, _jsonOptions);
            
            _logger.LogInformation("✓ Loaded session {SessionId} from file", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {SessionId}", sessionId);
            return null;
        }
    }

    private async Task<List<PersistentChatSession>> SearchSessionsByTagAsync(string tag, CancellationToken cancellationToken)
    {
        try
        {
            var dataDir = "data/chat-sessions";
            Directory.CreateDirectory(dataDir);
            
            var sessions = new List<PersistentChatSession>();
            var sessionFiles = Directory.GetFiles(dataDir, "*.json");
            
            _logger.LogInformation("Searching {FileCount} session files for tag: {Tag}", sessionFiles.Length, tag);
            
            foreach (var file in sessionFiles)
            {
                try
                {
                    var sessionJson = await File.ReadAllTextAsync(file, cancellationToken);
                    var session = JsonSerializer.Deserialize<PersistentChatSession>(sessionJson, _jsonOptions);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize session from {File}", file);
                }
            }

            _logger.LogInformation("✓ Found {Count} sessions", sessions.Count);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search sessions by tag: {Tag}", tag);
            return new List<PersistentChatSession>();
        }
    }
}
