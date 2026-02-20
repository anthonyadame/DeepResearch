using System;

namespace DeepResearchAgent.Agents;

/// <summary>
/// Exception thrown when a workflow is paused.
/// Contains checkpoint information for resumption.
/// </summary>
public class WorkflowPauseException : Exception
{
    /// <summary>Checkpoint ID where workflow was paused.</summary>
    public string CheckpointId { get; }

    /// <summary>Workflow ID that was paused.</summary>
    public string WorkflowId { get; }

    /// <summary>Reason for pausing.</summary>
    public string PauseReason { get; }

    public WorkflowPauseException(string workflowId, string checkpointId, string pauseReason)
        : base($"Workflow '{workflowId}' paused at checkpoint '{checkpointId}'. Reason: {pauseReason}")
    {
        WorkflowId = workflowId;
        CheckpointId = checkpointId;
        PauseReason = pauseReason;
    }

    public WorkflowPauseException(string workflowId, string checkpointId, string pauseReason, Exception innerException)
        : base($"Workflow '{workflowId}' paused at checkpoint '{checkpointId}'. Reason: {pauseReason}", innerException)
    {
        WorkflowId = workflowId;
        CheckpointId = checkpointId;
        PauseReason = pauseReason;
    }
}
