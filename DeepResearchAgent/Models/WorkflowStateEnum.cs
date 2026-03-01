using System;

namespace DeepResearchAgent.Model;

/// <summary>
/// Workflow lifecycle states for long-running process support.
/// Defines valid state transitions for pause/resume checkpointing.
/// </summary>
public enum WorkflowState
{
    /// <summary>Workflow created but not yet started.</summary>
    Queued = 0,

    /// <summary>Workflow is actively executing agents.</summary>
    Running = 1,

    /// <summary>Workflow paused; can be resumed from checkpoint.</summary>
    Paused = 2,

    /// <summary>Workflow completed successfully.</summary>
    Completed = 3,

    /// <summary>Workflow failed; may support retry from checkpoint.</summary>
    Failed = 4,

    /// <summary>Workflow cancelled by user or system.</summary>
    Cancelled = 5
}

/// <summary>
/// Represents valid state transitions for workflow lifecycle.
/// Enforces safe state machine semantics.
/// </summary>
public static class WorkflowStateTransitions
{
    /// <summary>Determine if a transition from currentState to nextState is valid.</summary>
    public static bool IsValidTransition(WorkflowState currentState, WorkflowState nextState)
    {
        return (currentState, nextState) switch
        {
            // Queued can transition to Running or Cancelled
            (WorkflowState.Queued, WorkflowState.Running) => true,
            (WorkflowState.Queued, WorkflowState.Cancelled) => true,

            // Running can transition to Paused, Completed, Failed, or Cancelled
            (WorkflowState.Running, WorkflowState.Paused) => true,
            (WorkflowState.Running, WorkflowState.Completed) => true,
            (WorkflowState.Running, WorkflowState.Failed) => true,
            (WorkflowState.Running, WorkflowState.Cancelled) => true,

            // Paused can transition to Running, Failed, or Cancelled
            (WorkflowState.Paused, WorkflowState.Running) => true,
            (WorkflowState.Paused, WorkflowState.Failed) => true,
            (WorkflowState.Paused, WorkflowState.Cancelled) => true,

            // Completed, Failed, Cancelled are terminal states
            _ => false
        };
    }

    /// <summary>Get human-readable description of current state.</summary>
    public static string GetDescription(WorkflowState state) => state switch
    {
        WorkflowState.Queued => "Workflow is queued and waiting to start",
        WorkflowState.Running => "Workflow is currently executing",
        WorkflowState.Paused => "Workflow is paused; can be resumed from checkpoint",
        WorkflowState.Completed => "Workflow completed successfully",
        WorkflowState.Failed => "Workflow failed",
        WorkflowState.Cancelled => "Workflow was cancelled",
        _ => "Unknown state"
    };
}
