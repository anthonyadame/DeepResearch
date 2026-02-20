using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Service for managing workflow pause/resume state and checkpoint integration.
/// Coordinates with AgentPipelineService for safe checkpointing at agent boundaries.
/// </summary>
public interface IWorkflowPauseResumeService
{
    /// <summary>Request pause of the current workflow at the next safe point.</summary>
    Task RequestPauseAsync(string workflowId, CancellationToken ct = default);

    /// <summary>Request cancellation of the current workflow.</summary>
    Task RequestCancellationAsync(string workflowId, CancellationToken ct = default);

    /// <summary>Check if a pause/cancellation has been requested for a workflow.</summary>
    PauseResumeSignal GetSignal(string workflowId);

    /// <summary>Get the cancellation token for a workflow (respects pause/cancel requests).</summary>
    CancellationToken GetCancellationToken(string workflowId);

    /// <summary>Notify service that a checkpoint has been saved at an agent boundary.</summary>
    Task OnCheckpointSavedAsync(string workflowId, WorkflowCheckpoint checkpoint, CancellationToken ct = default);

    /// <summary>Notify service that workflow has resumed from a checkpoint.</summary>
    Task OnWorkflowResumedAsync(string workflowId, WorkflowCheckpoint checkpoint, CancellationToken ct = default);

    /// <summary>Transition workflow to a new state and log the transition.</summary>
    Task TransitionWorkflowStateAsync(
        string workflowId,
        WorkflowState newState,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>Get current state of a workflow.</summary>
    Task<WorkflowExecutionState> GetWorkflowStateAsync(string workflowId, CancellationToken ct = default);
}

/// <summary>
/// Signal indicating pause or cancellation request for a workflow.
/// </summary>
public class PauseResumeSignal
{
    /// <summary>Whether pause has been requested.</summary>
    public bool PauseRequested { get; set; }

    /// <summary>Whether cancellation has been requested.</summary>
    public bool CancellationRequested { get; set; }

    /// <summary>Timestamp when signal was last updated.</summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>User-provided reason for pause/cancel.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Execution state of a workflow for monitoring and control.
/// </summary>
public class WorkflowExecutionState
{
    /// <summary>Unique workflow identifier.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Current state (Queued, Running, Paused, etc.).</summary>
    public WorkflowState State { get; set; }

    /// <summary>Current agent being executed (if Running).</summary>
    public string? CurrentAgentId { get; set; }

    /// <summary>Current step in agent pipeline.</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>List of agents that have completed.</summary>
    public List<string> CompletedAgents { get; set; } = new();

    /// <summary>When workflow started execution.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When workflow was paused (if applicable).</summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>Latest checkpoint ID (for resumption).</summary>
    public string? LatestCheckpointId { get; set; }

    /// <summary>Elapsed execution time.</summary>
    public TimeSpan ElapsedTime
    {
        get
        {
            if (StartedAt == null)
                return TimeSpan.Zero;

            var endTime = State == WorkflowState.Paused ? PausedAt : DateTime.UtcNow;
            return endTime.HasValue ? endTime.Value - StartedAt.Value : TimeSpan.Zero;
        }
    }

    /// <summary>Progress percentage (0-100).</summary>
    public int ProgressPercent
    {
        get
        {
            // TODO: calculate based on total expected agents and completed count
            return 0;
        }
    }
}

/// <summary>
/// Implementation of workflow pause/resume service.
/// Manages state transitions, signals, and checkpoint integration.
/// </summary>
public class WorkflowPauseResumeService : IWorkflowPauseResumeService
{
    private readonly ICheckpointService _checkpointService;
    private readonly ILogger<WorkflowPauseResumeService> _logger;
    private readonly Dictionary<string, PauseResumeSignal> _signals = new();
    private readonly Dictionary<string, WorkflowExecutionState> _executionStates = new();
    private readonly Dictionary<string, CancellationTokenSource> _cancellationSources = new();
    private readonly object _lock = new();

    public WorkflowPauseResumeService(
        ICheckpointService checkpointService,
        ILogger<WorkflowPauseResumeService> logger)
    {
        _checkpointService = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RequestPauseAsync(string workflowId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_signals.ContainsKey(workflowId))
                _signals[workflowId] = new();

            _signals[workflowId].PauseRequested = true;
            _signals[workflowId].LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Pause requested for workflow {WorkflowId}", workflowId);
        }

        return Task.CompletedTask;
    }

    public Task RequestCancellationAsync(string workflowId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_signals.ContainsKey(workflowId))
                _signals[workflowId] = new();

            _signals[workflowId].CancellationRequested = true;
            _signals[workflowId].LastUpdated = DateTime.UtcNow;

            // Trigger cancellation token
            if (_cancellationSources.ContainsKey(workflowId))
                _cancellationSources[workflowId].Cancel();

            _logger.LogInformation("Cancellation requested for workflow {WorkflowId}", workflowId);
        }

        return Task.CompletedTask;
    }

    public PauseResumeSignal GetSignal(string workflowId)
    {
        lock (_lock)
        {
            if (_signals.TryGetValue(workflowId, out var signal))
                return signal;

            return new PauseResumeSignal();
        }
    }

    public CancellationToken GetCancellationToken(string workflowId)
    {
        lock (_lock)
        {
            if (!_cancellationSources.ContainsKey(workflowId))
                _cancellationSources[workflowId] = new CancellationTokenSource();

            return _cancellationSources[workflowId].Token;
        }
    }

    public async Task OnCheckpointSavedAsync(string workflowId, WorkflowCheckpoint checkpoint, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_executionStates.TryGetValue(workflowId, out var state))
            {
                state.LatestCheckpointId = checkpoint.CheckpointId;
            }
        }

        _logger.LogInformation(
            "Checkpoint saved: {CheckpointId} for workflow {WorkflowId}",
            checkpoint.CheckpointId,
            workflowId);

        await Task.CompletedTask;
    }

    public async Task OnWorkflowResumedAsync(string workflowId, WorkflowCheckpoint checkpoint, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_executionStates.TryGetValue(workflowId, out var state))
            {
                state.State = WorkflowState.Running;
                state.PausedAt = null;
            }
        }

        _logger.LogInformation(
            "Workflow resumed from checkpoint {CheckpointId} for workflow {WorkflowId}",
            checkpoint.CheckpointId,
            workflowId);

        await Task.CompletedTask;
    }

    public async Task TransitionWorkflowStateAsync(
        string workflowId,
        WorkflowState newState,
        string? reason = null,
        CancellationToken ct = default)
    {
        WorkflowExecutionState? state;

        lock (_lock)
        {
            if (!_executionStates.TryGetValue(workflowId, out state))
                return;

            var oldState = state.State;

            // Validate transition
            if (!WorkflowStateTransitions.IsValidTransition(oldState, newState))
            {
                _logger.LogError(
                    "Invalid state transition for workflow {WorkflowId}: {OldState} -> {NewState}",
                    workflowId,
                    oldState,
                    newState);
                return;
            }

            state.State = newState;

            if (newState == WorkflowState.Running && state.StartedAt == null)
                state.StartedAt = DateTime.UtcNow;

            if (newState == WorkflowState.Paused)
                state.PausedAt = DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Workflow state transitioned: {WorkflowId} {OldState} -> {NewState}. Reason: {Reason}",
            workflowId,
            state?.State,
            newState,
            reason ?? "unspecified");

        await Task.CompletedTask;
    }

    public async Task<WorkflowExecutionState> GetWorkflowStateAsync(string workflowId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_executionStates.TryGetValue(workflowId, out var state))
                return state;

            // Initialize if not exists
            var newState = new WorkflowExecutionState
            {
                WorkflowId = workflowId,
                State = WorkflowState.Queued
            };

            _executionStates[workflowId] = newState;
            return newState;
        }
    }
}
