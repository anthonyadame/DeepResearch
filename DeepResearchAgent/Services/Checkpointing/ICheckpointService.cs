using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;

namespace DeepResearchAgent.Services.Checkpointing;

/// <summary>
/// High-level checkpoint service for workflow pause/resume support.
/// Abstracts checkpoint persistence and retrieval logic.
/// </summary>
public interface ICheckpointService
{
    /// <summary>Save a workflow checkpoint (before pause, or periodically during execution).</summary>
    Task<WorkflowCheckpoint> SaveCheckpointAsync(
        string workflowId,
        string workflowType,
        string? agentId,
        int stepIndex,
        string stateSnapshot,
        CheckpointMetadata? metadata = null,
        CancellationToken ct = default);

    /// <summary>Load a checkpoint by ID for resumption.</summary>
    Task<WorkflowCheckpoint?> LoadCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default);

    /// <summary>Get all checkpoints for a workflow, ordered by creation time (most recent first).</summary>
    Task<IReadOnlyList<WorkflowCheckpoint>> GetCheckpointsForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>Get the latest checkpoint for a workflow (most recent).</summary>
    Task<WorkflowCheckpoint?> GetLatestCheckpointAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>Delete a checkpoint by ID (for cleanup, retention policies).</summary>
    Task DeleteCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default);

    /// <summary>Delete all checkpoints for a workflow.</summary>
    Task DeleteCheckpointsForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>Get storage statistics for all checkpoints.</summary>
    Task<CheckpointStatistics> GetStatisticsAsync(
        CancellationToken ct = default);

    /// <summary>Validate a checkpoint before attempting to resume from it.</summary>
    Task<(bool isValid, string? errorMessage)> ValidateCheckpointAsync(
        string checkpointId,
        CancellationToken ct = default);
}

/// <summary>
/// Configuration options for checkpoint service.
/// </summary>
public class CheckpointServiceOptions
{
    /// <summary>Enable automatic periodic checkpoints during workflow execution.</summary>
    public bool EnableAutoCheckpoints { get; set; } = true;

    /// <summary>Interval between automatic checkpoints.</summary>
    public TimeSpan AutoCheckpointInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Enable checkpointing after each agent completes.</summary>
    public bool CheckpointAfterEachAgent { get; set; } = true;

    /// <summary>Maximum number of checkpoints to retain per workflow (0 = unlimited).</summary>
    public int MaxCheckpointsPerWorkflow { get; set; } = 10;

    /// <summary>Maximum checkpoint size in bytes (0 = unlimited).</summary>
    public long MaxCheckpointSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Checkpoint storage backend: "file" or "lightning-store".
    /// Falls back to file storage if lightning-store is unavailable.
    /// </summary>
    public string StorageBackend { get; set; } = "lightning-store";

    /// <summary>Local directory for file-based checkpoint storage.</summary>
    public string LocalStorageDirectory { get; set; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "checkpoints");

    /// <summary>Compress checkpoint state snapshots to reduce storage.</summary>
    public bool CompressSnapshots { get; set; } = false;

    /// <summary>Enable detailed checkpoint logging (can be verbose).</summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
