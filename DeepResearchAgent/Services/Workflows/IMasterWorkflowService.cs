using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>
/// Workflow execution states for state machine tracking.
/// </summary>
public enum WorkflowExecutionState
{
    Queued,           // Workflow created, waiting to start
    Initializing,     // Loading checkpoint or preparing state
    Running,          // Active execution
    Paused,           // Paused at checkpoint
    Evaluating,       // Checking quality, deciding next step
    Completed,        // Successfully completed
    Failed,           // Failed with error
    Cancelled         // User cancelled
}

/// <summary>
/// Represents a single step in a workflow.
/// </summary>
public class WorkflowStep
{
    /// <summary>Unique identifier for this step.</summary>
    public string StepId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Name of the agent to execute (clarify, brief, researcher, etc.).</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Input data for the agent.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>IDs of steps that must complete before this one (for parallel execution).</summary>
    public List<string> DependsOn { get; set; } = new();

    /// <summary>
    /// Conditional branching: key is condition expression, value is next step ID.
    /// Example: "quality < 0.5" => "researcher_step_2"
    /// </summary>
    public Dictionary<string, string> ConditionalNextSteps { get; set; } = new();

    /// <summary>Maximum number of retry attempts if step fails.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Delay in milliseconds between retries.</summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>Timeout in seconds for step execution.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Optional checkpoint flag - save checkpoint after this step.</summary>
    public bool SaveCheckpointAfter { get; set; } = false;
}

/// <summary>
/// Defines a complete workflow with multiple steps.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>Unique name identifier for the workflow.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Ordered list of workflow steps.</summary>
    public List<WorkflowStep> Steps { get; set; } = new();

    /// <summary>When this workflow was created/registered.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Version number for tracking updates.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Optional tags for categorization.</summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Represents a single execution of a workflow.
/// </summary>
public class WorkflowExecution
{
    /// <summary>Unique execution ID.</summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The workflow definition being executed.</summary>
    public WorkflowDefinition WorkflowDefinition { get; set; } = new();

    /// <summary>Current execution state.</summary>
    public WorkflowExecutionState State { get; set; } = WorkflowExecutionState.Queued;

    /// <summary>Input context passed to the workflow.</summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>Current step being executed (or last executed step).</summary>
    public string? CurrentStepId { get; set; }

    /// <summary>Accumulated state from all executed steps.</summary>
    public Dictionary<string, object> AccumulatedState { get; set; } = new();

    /// <summary>Execution history (log of all steps completed).</summary>
    public List<StepExecutionRecord> ExecutionHistory { get; set; } = new();

    /// <summary>When the workflow started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the workflow completed (successfully, failed, or cancelled).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total duration of execution.</summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>Final quality score (if workflow performs quality assessment).</summary>
    public double? QualityScore { get; set; }

    /// <summary>Error message if workflow failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Associated checkpoint ID (if paused).</summary>
    public string? CheckpointId { get; set; }

    /// <summary>Number of times workflow has been retried.</summary>
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// Record of a single step execution within a workflow.
/// </summary>
public class StepExecutionRecord
{
    /// <summary>Step ID that was executed.</summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>Execution number (1 for first, 2 for retry, etc.).</summary>
    public int ExecutionNumber { get; set; } = 1;

    /// <summary>When the step started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the step completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Step execution status.</summary>
    public string Status { get; set; } = "Running"; // Running, Completed, Failed, Skipped

    /// <summary>Output from the agent.</summary>
    public string? AgentOutput { get; set; }

    /// <summary>Quality score for the output (if assessed).</summary>
    public double? QualityScore { get; set; }

    /// <summary>Error message if step failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Duration of execution.</summary>
    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : null;
}

/// <summary>
/// Analytics for a completed workflow execution.
/// </summary>
public class WorkflowAnalytics
{
    public string ExecutionId { get; set; } = string.Empty;
    public int TotalStepsPlanned { get; set; }
    public int StepsCompleted { get; set; }
    public int StepsFailed { get; set; }
    public double AverageQualityScore { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public Dictionary<string, TimeSpan> StepExecutionTimes { get; set; } = new();
    public int TotalRetries { get; set; }
    public double SuccessRate { get; set; }
    public List<string> BottleneckSteps { get; set; } = new();
}

/// <summary>
/// Master Workflow Service interface for orchestrating multi-agent workflows.
/// </summary>
public interface IMasterWorkflowService
{
    // Workflow Execution
    /// <summary>Execute a complete workflow with the given definition and context.</summary>
    Task<WorkflowExecution> ExecuteWorkflowAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>Get the current status of a workflow execution.</summary>
    Task<WorkflowExecution?> GetWorkflowStatusAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>Pause a running workflow at the next checkpoint.</summary>
    Task PauseWorkflowAsync(
        string executionId,
        string reason,
        CancellationToken ct = default);

    /// <summary>Resume a paused workflow.</summary>
    Task ResumeWorkflowAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>Cancel a workflow execution.</summary>
    Task CancelWorkflowAsync(
        string executionId,
        string reason,
        CancellationToken ct = default);

    // Workflow History & Analysis
    /// <summary>Get execution history for a workflow type or agent.</summary>
    Task<IReadOnlyList<WorkflowExecution>> GetWorkflowHistoryAsync(
        string? workflowName = null,
        string? agentFilter = null,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>Get analytics for a completed workflow.</summary>
    Task<WorkflowAnalytics> GetWorkflowAnalyticsAsync(
        string executionId,
        CancellationToken ct = default);

    // Workflow Definition Management
    /// <summary>Register a new workflow definition.</summary>
    Task<WorkflowDefinition> RegisterWorkflowAsync(
        string name,
        string description,
        List<WorkflowStep> steps,
        List<string>? tags = null,
        CancellationToken ct = default);

    /// <summary>Get a registered workflow definition.</summary>
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(
        string workflowName,
        CancellationToken ct = default);

    /// <summary>List all registered workflow definitions.</summary>
    Task<IReadOnlyList<WorkflowDefinition>> ListWorkflowDefinitionsAsync(
        CancellationToken ct = default);

    /// <summary>Delete a workflow definition.</summary>
    Task DeleteWorkflowDefinitionAsync(
        string workflowName,
        CancellationToken ct = default);
}
