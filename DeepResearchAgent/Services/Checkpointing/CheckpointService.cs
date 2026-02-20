using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Checkpointing;

/// <summary>
/// Implementation of checkpoint persistence using LightningStore with file-based fallback.
/// Supports pause/resume functionality for long-running workflows.
/// </summary>
public class CheckpointService : ICheckpointService
{
    private readonly ILightningStore _lightningStore;
    private readonly ILogger<CheckpointService> _logger;
    private readonly CheckpointServiceOptions _options;
    private readonly object _lockObject = new();

    public CheckpointService(
        ILightningStore lightningStore,
        CheckpointServiceOptions options,
        ILogger<CheckpointService> logger)
    {
        _lightningStore = lightningStore ?? throw new ArgumentNullException(nameof(lightningStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure local storage directory exists
        if (!Directory.Exists(_options.LocalStorageDirectory))
        {
            Directory.CreateDirectory(_options.LocalStorageDirectory);
            _logger.LogInformation(
                "Created checkpoint storage directory: {Directory}",
                _options.LocalStorageDirectory);
        }
    }

    public async Task<WorkflowCheckpoint> SaveCheckpointAsync(
        string workflowId,
        string workflowType,
        string? agentId,
        int stepIndex,
        string stateSnapshot,
        CheckpointMetadata? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("Workflow ID cannot be null or empty", nameof(workflowId));
        if (string.IsNullOrWhiteSpace(workflowType))
            throw new ArgumentException("Workflow type cannot be null or empty", nameof(workflowType));
        if (string.IsNullOrWhiteSpace(stateSnapshot))
            throw new ArgumentException("State snapshot cannot be null or empty", nameof(stateSnapshot));

        // Validate snapshot size
        var snapshotBytes = Encoding.UTF8.GetByteCount(stateSnapshot);
        if (_options.MaxCheckpointSizeBytes > 0 && snapshotBytes > _options.MaxCheckpointSizeBytes)
        {
            var error = $"Checkpoint snapshot exceeds maximum size: {snapshotBytes} > {_options.MaxCheckpointSizeBytes}";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = GenerateCheckpointId(),
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            CreatedAt = DateTime.UtcNow,
            AgentId = agentId,
            StepIndex = stepIndex,
            StateSnapshot = stateSnapshot,
            StateSizeBytes = snapshotBytes,
            Metadata = metadata ?? new CheckpointMetadata()
        };

        // Check storage backend preference
        if (_options.StorageBackend == "file")
        {
            // Use file storage directly
            await SaveToFileSystemAsync(checkpoint, ct);
        }
        else
        {
            // Try LightningStore (primary), fallback to file
            try
            {
                await SaveToLightningStoreAsync(checkpoint, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to save checkpoint to LightningStore; falling back to file storage. WorkflowId={WorkflowId}",
                    workflowId);

                // Fallback to file storage
                await SaveToFileSystemAsync(checkpoint, ct);
            }
        }

        _logger.LogInformation(
            "Checkpoint saved: {CheckpointId} for workflow {WorkflowId} at step {StepIndex}",
            checkpoint.CheckpointId,
            workflowId,
            stepIndex);

        return checkpoint;
    }

    public async Task<WorkflowCheckpoint?> LoadCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
            throw new ArgumentException("Checkpoint ID cannot be null or empty", nameof(checkpointId));

        // Check storage backend preference
        if (_options.StorageBackend == "file")
        {
            // Load from file storage directly
            try
            {
                var checkpoint = await LoadFromFileSystemAsync(checkpointId, ct);
                if (checkpoint != null)
                {
                    _logger.LogInformation("Checkpoint loaded from file storage: {CheckpointId}", checkpointId);
                    return checkpoint;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load checkpoint from file storage: {CheckpointId}", checkpointId);
            }
        }
        else
        {
            // Try LightningStore first, fallback to file
            try
            {
                var checkpoint = await LoadFromLightningStoreAsync(checkpointId, ct);
                if (checkpoint != null)
                {
                    _logger.LogInformation("Checkpoint loaded from LightningStore: {CheckpointId}", checkpointId);
                    return checkpoint;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load checkpoint from LightningStore: {CheckpointId}", checkpointId);
            }

            try
            {
                // Fallback to file storage
                var checkpoint = await LoadFromFileSystemAsync(checkpointId, ct);
                if (checkpoint != null)
                {
                    _logger.LogInformation("Checkpoint loaded from file storage: {CheckpointId}", checkpointId);
                    return checkpoint;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load checkpoint from file storage: {CheckpointId}", checkpointId);
            }
        }

        _logger.LogWarning("Checkpoint not found: {CheckpointId}", checkpointId);
        return null;
    }

    public async Task<IReadOnlyList<WorkflowCheckpoint>> GetCheckpointsForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("Workflow ID cannot be null or empty", nameof(workflowId));

        var checkpoints = new List<WorkflowCheckpoint>();

        // Try file storage (primary for now)
        try
        {
            var fileCheckpoints = await GetCheckpointsFromFileSystemAsync(workflowId, ct);
            checkpoints.AddRange(fileCheckpoints);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve checkpoints from file storage for workflow {WorkflowId}", workflowId);
        }

        return checkpoints.OrderByDescending(c => c.CreatedAt).ToList().AsReadOnly();
    }

    public async Task<WorkflowCheckpoint?> GetLatestCheckpointAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var checkpoints = await GetCheckpointsForWorkflowAsync(workflowId, ct);
        return checkpoints.FirstOrDefault();
    }

    public async Task DeleteCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
            throw new ArgumentException("Checkpoint ID cannot be null or empty", nameof(checkpointId));

        try
        {
            await DeleteFromFileSystemAsync(checkpointId, ct);
            _logger.LogInformation("Checkpoint deleted: {CheckpointId}", checkpointId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete checkpoint: {CheckpointId}", checkpointId);
            throw;
        }
    }

    public async Task DeleteCheckpointsForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("Workflow ID cannot be null or empty", nameof(workflowId));

        var checkpoints = await GetCheckpointsForWorkflowAsync(workflowId, ct);
        var deleteTaskss = checkpoints.Select(cp => DeleteCheckpointAsync(cp.CheckpointId, ct));
        await Task.WhenAll(deleteTaskss);

        _logger.LogInformation("Deleted {Count} checkpoints for workflow {WorkflowId}", checkpoints.Count, workflowId);
    }

    public async Task<CheckpointStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        // Implementation: scan all checkpoint files and aggregate stats
        var stats = new CheckpointStatistics();

        try
        {
            if (!Directory.Exists(_options.LocalStorageDirectory))
                return stats;

            var checkpointFiles = Directory.GetFiles(_options.LocalStorageDirectory, "*.json");
            stats.TotalCheckpoints = checkpointFiles.Length;

            if (checkpointFiles.Length > 0)
            {
                var fileSizes = checkpointFiles.Select(f => new FileInfo(f).Length).ToList();
                stats.AverageCheckpointSizeBytes = (long)(fileSizes.Average());
                stats.LargestCheckpointSizeBytes = fileSizes.Max();
                stats.TotalStorageUsedBytes = fileSizes.Sum();

                var now = DateTime.UtcNow;
                var recentCutoff = now.AddDays(-1);
                stats.RecentCheckpointsCount = checkpointFiles
                    .Count(f => new FileInfo(f).LastWriteTimeUtc > recentCutoff);

                var oldestFile = checkpointFiles
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                stats.OldestCheckpointAt = oldestFile?.LastWriteTimeUtc;

                var newestFile = checkpointFiles
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                stats.NewestCheckpointAt = newestFile?.LastWriteTimeUtc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute checkpoint statistics");
        }

        return stats;
    }

    public async Task<(bool isValid, string? errorMessage)> ValidateCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default)
    {
        try
        {
            // First check if file exists (for file-based storage)
            if (_options.StorageBackend == "file")
            {
                var filePath = Path.Combine(_options.LocalStorageDirectory, $"{checkpointId}.json");
                if (!File.Exists(filePath))
                    return (false, "Checkpoint not found");

                // Try to read and parse the file directly for validation
                try
                {
                    var json = await File.ReadAllTextAsync(filePath, ct);
                    
                    // Try to deserialize as WorkflowCheckpoint
                    var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);
                    if (checkpoint == null)
                        return (false, "Checkpoint deserialization returned null");

                    if (string.IsNullOrWhiteSpace(checkpoint.StateSnapshot))
                        return (false, "Checkpoint has empty state snapshot");

                    // Try to deserialize state snapshot as JSON (basic validation)
                    try
                    {
                        JsonDocument.Parse(checkpoint.StateSnapshot);
                    }
                    catch (JsonException ex)
                    {
                        return (false, $"State snapshot is not valid JSON: {ex.Message}");
                    }

                    return (true, null);
                }
                catch (JsonException ex)
                {
                    return (false, $"Checkpoint file is not valid JSON: {ex.Message}");
                }
            }
            else
            {
                // For Lightning Store, use standard load
                var checkpoint = await LoadCheckpointAsync(checkpointId, ct);
                if (checkpoint == null)
                    return (false, "Checkpoint not found");

                if (string.IsNullOrWhiteSpace(checkpoint.StateSnapshot))
                    return (false, "Checkpoint has empty state snapshot");

                // Try to deserialize state snapshot as JSON (basic validation)
                try
                {
                    JsonDocument.Parse(checkpoint.StateSnapshot);
                }
                catch (JsonException ex)
                {
                    return (false, $"State snapshot is not valid JSON: {ex.Message}");
                }

                return (true, null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Validation error: {ex.Message}");
        }
    }

    // ==================== Private Helper Methods ====================

    private string GenerateCheckpointId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var randomSuffix = Guid.NewGuid().ToString("N")[..8];
        return $"ckpt_{timestamp}_{randomSuffix}";
    }

    private async Task SaveToLightningStoreAsync(WorkflowCheckpoint checkpoint, CancellationToken ct)
    {
        // TODO: Implement LightningStore integration
        // For now, this is a placeholder
        await Task.CompletedTask;
    }

    private async Task SaveToFileSystemAsync(WorkflowCheckpoint checkpoint, CancellationToken ct)
    {
        var filePath = Path.Combine(_options.LocalStorageDirectory, $"{checkpoint.CheckpointId}.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(checkpoint, options);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    private async Task<WorkflowCheckpoint?> LoadFromLightningStoreAsync(string checkpointId, CancellationToken ct)
    {
        // TODO: Implement LightningStore integration
        return await Task.FromResult<WorkflowCheckpoint?>(null);
    }

    private async Task<WorkflowCheckpoint?> LoadFromFileSystemAsync(string checkpointId, CancellationToken ct)
    {
        var filePath = Path.Combine(_options.LocalStorageDirectory, $"{checkpointId}.json");

        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, ct);
        var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        return checkpoint;
    }

    private async Task<IReadOnlyList<WorkflowCheckpoint>> GetCheckpointsFromFileSystemAsync(
        string workflowId,
        CancellationToken ct)
    {
        var checkpoints = new List<WorkflowCheckpoint>();

        if (!Directory.Exists(_options.LocalStorageDirectory))
            return checkpoints.AsReadOnly();

        var checkpointFiles = Directory.GetFiles(_options.LocalStorageDirectory, "*.json");

        foreach (var filePath in checkpointFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, ct);
                var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

                if (checkpoint?.WorkflowId == workflowId)
                {
                    checkpoints.Add(checkpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize checkpoint file: {FilePath}", filePath);
            }
        }

        return checkpoints.AsReadOnly();
    }

    private async Task DeleteFromFileSystemAsync(string checkpointId, CancellationToken ct)
    {
        var filePath = Path.Combine(_options.LocalStorageDirectory, $"{checkpointId}.json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        await Task.CompletedTask;
    }
}
