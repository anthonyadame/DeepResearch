using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DeepResearchAgent.Model.Api;

/// <summary>
/// Data Transfer Object for workflow status (response).
/// </summary>
public class WorkflowStatusDto
{
    public string WorkflowId { get; set; } = string.Empty;

    public string Status { get; set; } = "Queued"; // Queued, Running, Paused, Completed, Failed, Cancelled

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EstimatedCompletion { get; set; }

    public ProgressDto? Progress { get; set; }

    public string? LatestCheckpointId { get; set; }

    public DateTime? LatestCheckpointAt { get; set; }
}

/// <summary>
/// Progress information for a workflow.
/// </summary>
public class ProgressDto
{
    public int CurrentStep { get; set; }

    public int TotalSteps { get; set; }

    public string? CurrentAgent { get; set; }

    public int ElapsedSeconds { get; set; }

    public int? EstimatedRemainingSeconds { get; set; }

    public int ProgressPercent => TotalSteps > 0 ? (CurrentStep * 100) / TotalSteps : 0;
}

/// <summary>
/// Request to start a workflow.
/// </summary>
public class StartWorkflowRequestDto
{
    [Required]
    public string WorkflowType { get; set; } = string.Empty;

    public Dictionary<string, object>? Input { get; set; }

    public Dictionary<string, string>? Config { get; set; }
}

/// <summary>
/// Response when workflow is started.
/// </summary>
public class StartWorkflowResponseDto
{
    public string WorkflowId { get; set; } = string.Empty;

    public string Status { get; set; } = "Queued";

    public DateTime CreatedAt { get; set; }

    public string? Message { get; set; }
}

/// <summary>
/// Response for pause/resume/cancel operations.
/// </summary>
public class WorkflowActionResponseDto
{
    public string WorkflowId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty; // pause, resume, cancel

    public bool Success { get; set; }

    public string? Message { get; set; }

    public string? Status { get; set; }

    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Checkpoint information (response).
/// </summary>
public class CheckpointDto
{
    public string CheckpointId { get; set; } = string.Empty;

    public string WorkflowId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? AgentId { get; set; }

    public int StepIndex { get; set; }

    public long StateSizeBytes { get; set; }

    public string? Label { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Workflow execution log entry.
/// </summary>
public class WorkflowLogEntryDto
{
    public DateTime Timestamp { get; set; }

    public string Level { get; set; } = "Information"; // Debug, Information, Warning, Error

    public string Message { get; set; } = string.Empty;

    public Dictionary<string, string>? Context { get; set; }
}

/// <summary>
/// List response with pagination support.
/// </summary>
public class PagedResponseDto<T>
{
    public int Total { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; }

    public List<T> Items { get; set; } = new();
}

/// <summary>
/// Error response.
/// </summary>
public class ErrorResponseDto
{
    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public Dictionary<string, string>? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to resume from a specific checkpoint.
/// </summary>
public class ResumeWorkflowRequestDto
{
    public string? FromCheckpointId { get; set; }

    public bool StartFreshIfCheckpointMissing { get; set; } = false;
}
