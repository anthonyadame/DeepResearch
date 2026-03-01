using System;
using System.Collections.Generic;

namespace DeepResearchAgent.Model;

/// <summary>
/// Represents a saved checkpoint of workflow execution state.
/// Used for pause/resume functionality in long-running processes.
/// </summary>
public class WorkflowCheckpoint
{
    /// <summary>Unique checkpoint identifier (e.g., ckpt_20240115_abc123).</summary>
    public string CheckpointId { get; set; } = string.Empty;

    /// <summary>Parent workflow identifier.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Type of workflow (e.g., "ResearcherWorkflow", "MasterWorkflow").</summary>
    public string WorkflowType { get; set; } = string.Empty;

    /// <summary>Timestamp when checkpoint was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Identifier of the agent that was executing when checkpoint was saved.
    /// Example: "ResearchBriefAgent", "ResearcherAgent"
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Index of the current step/task in the agent pipeline.
    /// Used to resume from the exact position in workflow sequence.
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    /// Serialized workflow state snapshot (JSON).
    /// Contains minimal state necessary to resume execution.
    /// </summary>
    public string? StateSnapshot { get; set; }

    /// <summary>Schema version for forward/backward compatibility.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Metadata about checkpoint creation.</summary>
    public CheckpointMetadata Metadata { get; set; } = new();

    /// <summary>Size of serialized state in bytes (for optimization tracking).</summary>
    public long StateSizeBytes { get; set; }

    /// <summary>
    /// Human-readable label for the checkpoint.
    /// Example: "Before ResearchBriefAgent", "User pause at 50%"
    /// </summary>
    public string? Label { get; set; }
}

/// <summary>
/// Metadata about checkpoint creation and context.
/// </summary>
public class CheckpointMetadata
{
    /// <summary>Whether checkpoint was created automatically or by user action.</summary>
    public bool IsAutomated { get; set; }

    /// <summary>
    /// Reason for checkpoint creation.
    /// Example: "scheduled", "user-pause", "before-agent", "error-recovery"
    /// </summary>
    public string Reason { get; set; } = "user-initiated";

    /// <summary>User identifier if checkpoint was manually triggered.</summary>
    public string? UserId { get; set; }

    /// <summary>Additional context (e.g., error message if created during error recovery).</summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>List of agents that had completed at time of checkpoint.</summary>
    public List<string> CompletedAgents { get; set; } = new();
}

/// <summary>
/// Statistics about workflow checkpoints.
/// </summary>
public class CheckpointStatistics
{
    /// <summary>Total number of checkpoints in storage.</summary>
    public int TotalCheckpoints { get; set; }

    /// <summary>Average checkpoint size in bytes.</summary>
    public long AverageCheckpointSizeBytes { get; set; }

    /// <summary>Largest checkpoint size in bytes.</summary>
    public long LargestCheckpointSizeBytes { get; set; }

    /// <summary>Total storage used by all checkpoints.</summary>
    public long TotalStorageUsedBytes { get; set; }

    /// <summary>Number of checkpoints created in the last 24 hours.</summary>
    public int RecentCheckpointsCount { get; set; }

    /// <summary>Timestamp of oldest checkpoint.</summary>
    public DateTime? OldestCheckpointAt { get; set; }

    /// <summary>Timestamp of newest checkpoint.</summary>
    public DateTime? NewestCheckpointAt { get; set; }
}
