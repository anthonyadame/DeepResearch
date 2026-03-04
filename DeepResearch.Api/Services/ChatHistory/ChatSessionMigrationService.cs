using DeepResearch.Api.DTOs;
using DeepResearch.Api.Services.ChatHistory;

namespace DeepResearch.Api.Services;

/// <summary>
/// Migration utility to convert existing in-memory sessions to LightningStore
/// </summary>
public class ChatSessionMigrationService
{
    private readonly ChatSessionService _inMemoryService;
    private readonly IChatHistoryService _lightningService;
    private readonly ILogger<ChatSessionMigrationService> _logger;

    public ChatSessionMigrationService(
        ChatSessionService inMemoryService,
        IChatHistoryService lightningService,
        ILogger<ChatSessionMigrationService> logger)
    {
        _inMemoryService = inMemoryService;
        _lightningService = lightningService;
        _logger = logger;
    }

    /// <summary>
    /// Migrate all in-memory sessions to LightningStore
    /// </summary>
    public async Task<MigrationResult> MigrateAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting migration of in-memory sessions to LightningStore");
        
        var result = new MigrationResult();
        
        try
        {
            var inMemorySessions = await _inMemoryService.GetSessionsAsync();
            result.TotalSessions = inMemorySessions.Count;
            
            _logger.LogInformation("Found {Count} in-memory sessions to migrate", inMemorySessions.Count);

            foreach (var session in inMemorySessions)
            {
                try
                {
                    await MigrateSessionAsync(session, cancellationToken);
                    result.MigratedSessions++;
                    _logger.LogInformation("Migrated session {SessionId}: {Title}", session.Id, session.Title);
                }
                catch (Exception ex)
                {
                    result.FailedSessions++;
                    result.Errors.Add($"Session {session.Id}: {ex.Message}");
                    _logger.LogError(ex, "Failed to migrate session {SessionId}", session.Id);
                }
            }

            result.Success = result.FailedSessions == 0;
            _logger.LogInformation(
                "Migration completed: {Migrated} migrated, {Failed} failed out of {Total} total",
                result.MigratedSessions, result.FailedSessions, result.TotalSessions
            );
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Migration failed: {ex.Message}");
            _logger.LogError(ex, "Migration process failed");
        }

        return result;
    }

    /// <summary>
    /// Migrate a single session
    /// </summary>
    private async Task MigrateSessionAsync(ChatSession session, CancellationToken cancellationToken)
    {
        // Create new session in LightningStore with same ID
        var newSession = await _lightningService.CreateSessionAsync(session.Title, cancellationToken);
        
        // Copy all messages
        foreach (var message in session.Messages)
        {
            await _lightningService.AddMessageAsync(newSession.Id, message, cancellationToken);
        }

        // Auto-categorize the migrated session
        await _lightningService.CategorizeSessionAsync(newSession.Id, cancellationToken);
    }
}

/// <summary>
/// Result of chat session migration
/// </summary>
public record MigrationResult
{
    public bool Success { get; set; }
    public int TotalSessions { get; set; }
    public int MigratedSessions { get; set; }
    public int FailedSessions { get; set; }
    public List<string> Errors { get; init; } = new();
}
