using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DeepResearch.Api.Models;

/// <summary>
/// Response for checkpoint operations.
/// </summary>
public class CheckpointResponseDto
{
    public string CheckpointId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? AgentId { get; set; }
    public int StepIndex { get; set; }
    public long StateSizeBytes { get; set; }
    public string? Label { get; set; }
    public CheckpointMetadataDto Metadata { get; set; } = new();
}

/// <summary>
/// Metadata about checkpoint.
/// </summary>
public class CheckpointMetadataDto
{
    public bool IsAutomated { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
    public List<string> CompletedAgents { get; set; } = new();
}

/// <summary>
/// Response for listing checkpoints.
/// </summary>
public class CheckpointListResponseDto
{
    public List<CheckpointResponseDto> Checkpoints { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Request to create a checkpoint.
/// </summary>
public class CreateCheckpointRequestDto
{
    [Required]
    public string WorkflowId { get; set; } = string.Empty;
    
    public string Reason { get; set; } = "user-request";
    
    public string? Label { get; set; }
}

/// <summary>
/// Response for pause workflow operation.
/// </summary>
public class PauseWorkflowResponseDto
{
    public string WorkflowId { get; set; } = string.Empty;
    public string CheckpointId { get; set; } = string.Empty;
    public DateTime PausedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to resume a workflow.
/// </summary>
public class ResumeWorkflowRequestDto
{
    [Required]
    public string CheckpointId { get; set; } = string.Empty;
}

/// <summary>
/// Response for resume workflow operation.
/// </summary>
public class ResumeWorkflowResponseDto
{
    public string WorkflowId { get; set; } = string.Empty;
    public string CheckpointId { get; set; } = string.Empty;
    public DateTime ResumedAt { get; set; }
    public int ResumedFromStep { get; set; }
    public List<string> SkippedAgents { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Checkpoint statistics response.
/// </summary>
public class CheckpointStatisticsResponseDto
{
    public int TotalCheckpoints { get; set; }
    public long AverageCheckpointSizeBytes { get; set; }
    public long LargestCheckpointSizeBytes { get; set; }
    public long TotalStorageUsedBytes { get; set; }
    public int RecentCheckpointsCount { get; set; }
    public DateTime? OldestCheckpointAt { get; set; }
    public DateTime? NewestCheckpointAt { get; set; }
}

/// <summary>
/// Workflow status response.
/// </summary>
public class WorkflowStatusResponseDto
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentAgentId { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public int ProgressPercent { get; set; }
    public List<string> CompletedAgents { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
    public string? LatestCheckpointId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Workflow monitoring data for dashboard.
/// </summary>
public class WorkflowMonitoringResponseDto
{
    public List<WorkflowStatusResponseDto> ActiveWorkflows { get; set; } = new();
    public List<WorkflowStatusResponseDto> PausedWorkflows { get; set; } = new();
    public WorkflowMetricsDto Metrics { get; set; } = new();
}

/// <summary>
/// Workflow execution metrics.
/// </summary>
public class WorkflowMetricsDto
{
    public int TotalWorkflows { get; set; }
    public int ActiveWorkflows { get; set; }
    public int PausedWorkflows { get; set; }
    public int CompletedWorkflows { get; set; }
    public int FailedWorkflows { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public int CheckpointsCreatedToday { get; set; }
    public long TotalCheckpointStorage { get; set; }
}

/// <summary>
/// Request to validate a checkpoint.
/// </summary>
public class ValidateCheckpointRequestDto
{
    [Required]
    public string CheckpointId { get; set; } = string.Empty;
}

/// <summary>
/// Response for checkpoint validation.
/// </summary>
public class ValidateCheckpointResponseDto
{
    public string CheckpointId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
}

/// <summary>
/// Request to delete checkpoints.
/// </summary>
public class DeleteCheckpointsRequestDto
{
    public List<string> CheckpointIds { get; set; } = new();
    public string? WorkflowId { get; set; }
    public bool DeleteAllForWorkflow { get; set; }
}

/// <summary>
/// Response for delete operation.
/// </summary>
public class DeleteCheckpointsResponseDto
{
    public int DeletedCount { get; set; }
    public List<string> DeletedCheckpointIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Workflow list response.
/// </summary>
public class WorkflowListResponseDto
{
    public List<WorkflowSummaryDto> Workflows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? FilteredStatus { get; set; }
}

/// <summary>
/// Workflow summary for list views.
/// </summary>
public class WorkflowSummaryDto
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercent { get; set; }
    public string? CurrentAgentId { get; set; }
}
