using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Generic;

namespace DeepResearchAgent.Observability;

/// <summary>
/// OpenTelemetry instrumentation for workflow checkpoint operations.
/// Provides metrics, traces, and monitoring for checkpoint lifecycle.
/// </summary>
public class CheckpointTelemetry : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _checkpointsSaved;
    private readonly Counter<long> _checkpointsLoaded;
    private readonly Counter<long> _checkpointsDeleted;
    private readonly Counter<long> _checkpointValidations;
    private readonly Counter<long> _checkpointErrors;
    
    // Histograms
    private readonly Histogram<double> _checkpointSaveLatency;
    private readonly Histogram<double> _checkpointLoadLatency;
    private readonly Histogram<long> _checkpointSize;
    
    // Gauges (using ObservableGauges)
    private readonly ObservableGauge<long> _activeCheckpoints;
    private readonly ObservableGauge<long> _totalStorageBytes;
    
    private long _currentActiveCheckpoints;
    private long _currentStorageBytes;

    public CheckpointTelemetry()
    {
        _activitySource = new ActivitySource("DeepResearch.Checkpoints", "1.0.0");
        _meter = new Meter("DeepResearch.Checkpoints", "1.0.0");
        
        // Initialize counters
        _checkpointsSaved = _meter.CreateCounter<long>(
            "checkpoint.saved.total",
            description: "Total number of checkpoints saved");
            
        _checkpointsLoaded = _meter.CreateCounter<long>(
            "checkpoint.loaded.total",
            description: "Total number of checkpoints loaded");
            
        _checkpointsDeleted = _meter.CreateCounter<long>(
            "checkpoint.deleted.total",
            description: "Total number of checkpoints deleted");
            
        _checkpointValidations = _meter.CreateCounter<long>(
            "checkpoint.validations.total",
            description: "Total number of checkpoint validations performed");
            
        _checkpointErrors = _meter.CreateCounter<long>(
            "checkpoint.errors.total",
            description: "Total number of checkpoint errors");
        
        // Initialize histograms
        _checkpointSaveLatency = _meter.CreateHistogram<double>(
            "checkpoint.save.duration.seconds",
            unit: "s",
            description: "Checkpoint save operation latency");
            
        _checkpointLoadLatency = _meter.CreateHistogram<double>(
            "checkpoint.load.duration.seconds",
            unit: "s",
            description: "Checkpoint load operation latency");
            
        _checkpointSize = _meter.CreateHistogram<long>(
            "checkpoint.size.bytes",
            unit: "bytes",
            description: "Checkpoint size distribution");
        
        // Initialize observable gauges
        _activeCheckpoints = _meter.CreateObservableGauge<long>(
            "checkpoint.active.count",
            () => _currentActiveCheckpoints,
            description: "Number of active checkpoints in storage");
            
        _totalStorageBytes = _meter.CreateObservableGauge<long>(
            "checkpoint.storage.bytes",
            () => _currentStorageBytes,
            description: "Total storage used by checkpoints");
    }

    /// <summary>
    /// Start tracing a checkpoint save operation.
    /// </summary>
    public Activity? StartSaveActivity(string workflowId, string checkpointId)
    {
        var activity = _activitySource.StartActivity("CheckpointSave", ActivityKind.Internal);
        activity?.SetTag("workflow.id", workflowId);
        activity?.SetTag("checkpoint.id", checkpointId);
        activity?.SetTag("operation", "save");
        return activity;
    }

    /// <summary>
    /// Record checkpoint save metrics.
    /// </summary>
    public void RecordCheckpointSaved(
        string workflowId,
        string workflowType,
        long sizeBytes,
        double durationSeconds,
        bool isAutomated)
    {
        var tags = new TagList
        {
            { "workflow.type", workflowType },
            { "automated", isAutomated.ToString() }
        };
        
        _checkpointsSaved.Add(1, tags);
        _checkpointSaveLatency.Record(durationSeconds, tags);
        _checkpointSize.Record(sizeBytes, tags);
        
        _currentActiveCheckpoints++;
        _currentStorageBytes += sizeBytes;
    }

    /// <summary>
    /// Start tracing a checkpoint load operation.
    /// </summary>
    public Activity? StartLoadActivity(string checkpointId)
    {
        var activity = _activitySource.StartActivity("CheckpointLoad", ActivityKind.Internal);
        activity?.SetTag("checkpoint.id", checkpointId);
        activity?.SetTag("operation", "load");
        return activity;
    }

    /// <summary>
    /// Record checkpoint load metrics.
    /// </summary>
    public void RecordCheckpointLoaded(
        string checkpointId,
        string workflowType,
        double durationSeconds,
        bool found)
    {
        var tags = new TagList
        {
            { "workflow.type", workflowType },
            { "found", found.ToString() }
        };
        
        _checkpointsLoaded.Add(1, tags);
        _checkpointLoadLatency.Record(durationSeconds, tags);
    }

    /// <summary>
    /// Record checkpoint deletion.
    /// </summary>
    public void RecordCheckpointDeleted(string workflowId, long sizeBytes)
    {
        _checkpointsDeleted.Add(1);
        _currentActiveCheckpoints--;
        _currentStorageBytes -= sizeBytes;
    }

    /// <summary>
    /// Record checkpoint validation.
    /// </summary>
    public void RecordValidation(string checkpointId, bool isValid, string? errorReason = null)
    {
        var tags = new TagList
        {
            { "valid", isValid.ToString() }
        };
        
        if (!isValid && errorReason != null)
        {
            tags.Add("error.reason", errorReason);
        }
        
        _checkpointValidations.Add(1, tags);
    }

    /// <summary>
    /// Record checkpoint error.
    /// </summary>
    public void RecordError(string operation, string errorType, string errorMessage)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "error.type", errorType }
        };
        
        _checkpointErrors.Add(1, tags);
    }

    /// <summary>
    /// Update storage metrics.
    /// </summary>
    public void UpdateStorageMetrics(long activeCount, long totalBytes)
    {
        _currentActiveCheckpoints = activeCount;
        _currentStorageBytes = totalBytes;
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}

/// <summary>
/// OpenTelemetry instrumentation for workflow operations.
/// Provides metrics and traces for workflow lifecycle.
/// </summary>
public class WorkflowTelemetry : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _workflowsStarted;
    private readonly Counter<long> _workflowsCompleted;
    private readonly Counter<long> _workflowsFailed;
    private readonly Counter<long> _workflowsPaused;
    private readonly Counter<long> _workflowsResumed;
    private readonly Counter<long> _workflowsCancelled;
    
    // Histograms
    private readonly Histogram<double> _workflowDuration;
    private readonly Histogram<double> _pauseLatency;
    private readonly Histogram<double> _resumeLatency;
    
    // Gauges
    private readonly ObservableGauge<long> _activeWorkflows;
    private readonly ObservableGauge<long> _pausedWorkflows;
    
    private long _currentActiveWorkflows;
    private long _currentPausedWorkflows;

    public WorkflowTelemetry()
    {
        _activitySource = new ActivitySource("DeepResearch.Workflows", "1.0.0");
        _meter = new Meter("DeepResearch.Workflows", "1.0.0");
        
        // Initialize counters
        _workflowsStarted = _meter.CreateCounter<long>(
            "workflow.started.total",
            description: "Total number of workflows started");
            
        _workflowsCompleted = _meter.CreateCounter<long>(
            "workflow.completed.total",
            description: "Total number of workflows completed successfully");
            
        _workflowsFailed = _meter.CreateCounter<long>(
            "workflow.failed.total",
            description: "Total number of workflows that failed");
            
        _workflowsPaused = _meter.CreateCounter<long>(
            "workflow.paused.total",
            description: "Total number of workflows paused");
            
        _workflowsResumed = _meter.CreateCounter<long>(
            "workflow.resumed.total",
            description: "Total number of workflows resumed");
            
        _workflowsCancelled = _meter.CreateCounter<long>(
            "workflow.cancelled.total",
            description: "Total number of workflows cancelled");
        
        // Initialize histograms
        _workflowDuration = _meter.CreateHistogram<double>(
            "workflow.duration.seconds",
            unit: "s",
            description: "Workflow execution duration");
            
        _pauseLatency = _meter.CreateHistogram<double>(
            "workflow.pause.duration.seconds",
            unit: "s",
            description: "Time taken to pause a workflow");
            
        _resumeLatency = _meter.CreateHistogram<double>(
            "workflow.resume.duration.seconds",
            unit: "s",
            description: "Time taken to resume a workflow");
        
        // Initialize observable gauges
        _activeWorkflows = _meter.CreateObservableGauge<long>(
            "workflow.active.count",
            () => _currentActiveWorkflows,
            description: "Number of currently running workflows");
            
        _pausedWorkflows = _meter.CreateObservableGauge<long>(
            "workflow.paused.count",
            () => _currentPausedWorkflows,
            description: "Number of currently paused workflows");
    }

    /// <summary>
    /// Start tracing a workflow execution.
    /// </summary>
    public Activity? StartWorkflowActivity(string workflowId, string workflowType)
    {
        var activity = _activitySource.StartActivity("WorkflowExecution", ActivityKind.Internal);
        activity?.SetTag("workflow.id", workflowId);
        activity?.SetTag("workflow.type", workflowType);
        return activity;
    }

    /// <summary>
    /// Record workflow start.
    /// </summary>
    public void RecordWorkflowStarted(string workflowType)
    {
        var tags = new TagList { { "workflow.type", workflowType } };
        _workflowsStarted.Add(1, tags);
        _currentActiveWorkflows++;
    }

    /// <summary>
    /// Record workflow completion.
    /// </summary>
    public void RecordWorkflowCompleted(string workflowType, double durationSeconds)
    {
        var tags = new TagList { { "workflow.type", workflowType } };
        _workflowsCompleted.Add(1, tags);
        _workflowDuration.Record(durationSeconds, tags);
        _currentActiveWorkflows--;
    }

    /// <summary>
    /// Record workflow failure.
    /// </summary>
    public void RecordWorkflowFailed(string workflowType, string errorType, double durationSeconds)
    {
        var tags = new TagList
        {
            { "workflow.type", workflowType },
            { "error.type", errorType }
        };
        
        _workflowsFailed.Add(1, tags);
        _workflowDuration.Record(durationSeconds, tags);
        _currentActiveWorkflows--;
    }

    /// <summary>
    /// Record workflow pause.
    /// </summary>
    public void RecordWorkflowPaused(string workflowType, double pauseLatencySeconds, string reason)
    {
        var tags = new TagList
        {
            { "workflow.type", workflowType },
            { "pause.reason", reason }
        };
        
        _workflowsPaused.Add(1, tags);
        _pauseLatency.Record(pauseLatencySeconds, tags);
        _currentActiveWorkflows--;
        _currentPausedWorkflows++;
    }

    /// <summary>
    /// Record workflow resume.
    /// </summary>
    public void RecordWorkflowResumed(string workflowType, double resumeLatencySeconds)
    {
        var tags = new TagList { { "workflow.type", workflowType } };
        _workflowsResumed.Add(1, tags);
        _resumeLatency.Record(resumeLatencySeconds, tags);
        _currentPausedWorkflows--;
        _currentActiveWorkflows++;
    }

    /// <summary>
    /// Record workflow cancellation.
    /// </summary>
    public void RecordWorkflowCancelled(string workflowType, double durationSeconds)
    {
        var tags = new TagList { { "workflow.type", workflowType } };
        _workflowsCancelled.Add(1, tags);
        _currentActiveWorkflows--;
    }

    /// <summary>
    /// Update workflow count metrics.
    /// </summary>
    public void UpdateWorkflowCounts(long active, long paused)
    {
        _currentActiveWorkflows = active;
        _currentPausedWorkflows = paused;
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}
