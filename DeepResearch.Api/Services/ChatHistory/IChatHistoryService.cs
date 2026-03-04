using DeepResearch.Api.DTOs;

namespace DeepResearch.Api.Services.ChatHistory;

/// <summary>
/// Service for managing chat session persistence, categorization, and archiving
/// </summary>
public interface IChatHistoryService
{
    // Session Management
    Task<ChatSession> CreateSessionAsync(string? title, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<List<ChatSession>> GetSessionsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<List<ChatSession>> GetSessionsByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    // Message Management
    Task<ChatMessage> AddMessageAsync(string sessionId, ChatMessage message, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default);
    
    // Categorization
    Task<string[]> CategorizeSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task UpdateCategoriesAsync(string sessionId, string[] categories, CancellationToken cancellationToken = default);
    
    // Archiving
    Task ArchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task UnarchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<int> AutoArchiveOldSessionsAsync(int daysOld = 30, CancellationToken cancellationToken = default);
    Task<int> AutoArchiveLargeSessionsAsync(int maxMessages = 1000, CancellationToken cancellationToken = default);
    
    // Statistics
    Task<ChatHistoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about chat history storage
/// </summary>
public record ChatHistoryStatistics
{
    public int TotalSessions { get; init; }
    public int ActiveSessions { get; init; }
    public int ArchivedSessions { get; init; }
    public int TotalMessages { get; init; }
    public Dictionary<string, int> CategoryCounts { get; init; } = new();
    public long StorageSizeBytes { get; init; }
    public DateTime? OldestSession { get; init; }
    public DateTime? NewestSession { get; init; }
}
