using DeepResearch.Api.Services.ChatHistory;

namespace DeepResearch.Api.Services.BackgroundTasks;

/// <summary>
/// Background service for automatic chat session archiving based on time and size
/// </summary>
public class ChatArchiveBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatArchiveBackgroundService> _logger;
    private readonly TimeSpan _archiveInterval;
    private readonly int _daysBeforeArchive;
    private readonly int _maxMessagesBeforeArchive;

    public ChatArchiveBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ChatArchiveBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Load configuration with defaults
        _archiveInterval = TimeSpan.FromHours(
            configuration.GetValue("ChatHistory:ArchiveIntervalHours", 24)
        );
        _daysBeforeArchive = configuration.GetValue("ChatHistory:DaysBeforeArchive", 30);
        _maxMessagesBeforeArchive = configuration.GetValue("ChatHistory:MaxMessagesBeforeArchive", 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Chat Archive Background Service started. Interval: {Interval}, DaysBeforeArchive: {Days}, MaxMessages: {MaxMessages}",
            _archiveInterval, _daysBeforeArchive, _maxMessagesBeforeArchive
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_archiveInterval, stoppingToken);
                await PerformArchivingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Chat Archive Background Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Chat Archive Background Service");
                // Continue running even if one cycle fails
            }
        }
    }

    private async Task PerformArchivingAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var chatHistory = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();

        try
        {
            _logger.LogInformation("Starting automatic archiving cycle");

            // Archive old sessions
            var archivedByDate = await chatHistory.AutoArchiveOldSessionsAsync(
                _daysBeforeArchive,
                cancellationToken
            );

            // Archive large sessions
            var archivedBySize = await chatHistory.AutoArchiveLargeSessionsAsync(
                _maxMessagesBeforeArchive,
                cancellationToken
            );

            if (archivedByDate > 0 || archivedBySize > 0)
            {
                _logger.LogInformation(
                    "Archiving cycle completed: {ByDate} sessions by date, {BySize} sessions by size",
                    archivedByDate, archivedBySize
                );

                // Get updated statistics
                var stats = await chatHistory.GetStatisticsAsync(cancellationToken);
                _logger.LogInformation(
                    "Storage statistics: {Active} active, {Archived} archived, {Total} messages, {Size:N0} bytes",
                    stats.ActiveSessions, stats.ArchivedSessions, stats.TotalMessages, stats.StorageSizeBytes
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform automatic archiving");
        }
    }
}
