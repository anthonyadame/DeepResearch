using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Event arguments for workflow state changes.
/// </summary>
public class WorkflowStateChangedEventArgs : EventArgs
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public WorkflowState PreviousState { get; set; }
    public WorkflowState NewState { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Event arguments for checkpoint events.
/// </summary>
public class CheckpointEventArgs : EventArgs
{
    public string CheckpointId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public CheckpointEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public long? SizeBytes { get; set; }
    public string? Reason { get; set; }
}

public enum CheckpointEventType
{
    Created,
    Loaded,
    Deleted,
    Validated,
    Failed
}

/// <summary>
/// Observer interface for workflow state changes.
/// </summary>
public interface IWorkflowObserver
{
    Task OnWorkflowStateChangedAsync(WorkflowStateChangedEventArgs e, CancellationToken ct = default);
}

/// <summary>
/// Observer interface for checkpoint events.
/// </summary>
public interface ICheckpointObserver
{
    Task OnCheckpointEventAsync(CheckpointEventArgs e, CancellationToken ct = default);
}

/// <summary>
/// Subject for workflow state changes implementing observer pattern.
/// Notifies registered observers of workflow state transitions.
/// </summary>
public class WorkflowStateSubject
{
    private readonly List<IWorkflowObserver> _observers = new();
    private readonly ILogger<WorkflowStateSubject> _logger;
    private readonly object _lock = new();

    public WorkflowStateSubject(ILogger<WorkflowStateSubject> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribe an observer to workflow state changes.
    /// </summary>
    public void Subscribe(IWorkflowObserver observer)
    {
        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
                _logger.LogDebug("Observer subscribed: {ObserverType}", observer.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Unsubscribe an observer from workflow state changes.
    /// </summary>
    public void Unsubscribe(IWorkflowObserver observer)
    {
        lock (_lock)
        {
            _observers.Remove(observer);
            _logger.LogDebug("Observer unsubscribed: {ObserverType}", observer.GetType().Name);
        }
    }

    /// <summary>
    /// Notify all observers of a workflow state change.
    /// </summary>
    public async Task NotifyAsync(WorkflowStateChangedEventArgs e, CancellationToken ct = default)
    {
        List<IWorkflowObserver> observersCopy;
        lock (_lock)
        {
            observersCopy = new List<IWorkflowObserver>(_observers);
        }

        _logger.LogInformation(
            "Workflow state changed: {WorkflowId} {PreviousState} → {NewState}",
            e.WorkflowId, e.PreviousState, e.NewState);

        foreach (var observer in observersCopy)
        {
            try
            {
                await observer.OnWorkflowStateChangedAsync(e, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error notifying observer {ObserverType} of workflow state change",
                    observer.GetType().Name);
            }
        }
    }
}

/// <summary>
/// Subject for checkpoint events implementing observer pattern.
/// Notifies registered observers of checkpoint lifecycle events.
/// </summary>
public class CheckpointEventSubject
{
    private readonly List<ICheckpointObserver> _observers = new();
    private readonly ILogger<CheckpointEventSubject> _logger;
    private readonly object _lock = new();

    public CheckpointEventSubject(ILogger<CheckpointEventSubject> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribe an observer to checkpoint events.
    /// </summary>
    public void Subscribe(ICheckpointObserver observer)
    {
        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
                _logger.LogDebug("Checkpoint observer subscribed: {ObserverType}", observer.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Unsubscribe an observer from checkpoint events.
    /// </summary>
    public void Unsubscribe(ICheckpointObserver observer)
    {
        lock (_lock)
        {
            _observers.Remove(observer);
            _logger.LogDebug("Checkpoint observer unsubscribed: {ObserverType}", observer.GetType().Name);
        }
    }

    /// <summary>
    /// Notify all observers of a checkpoint event.
    /// </summary>
    public async Task NotifyAsync(CheckpointEventArgs e, CancellationToken ct = default)
    {
        List<ICheckpointObserver> observersCopy;
        lock (_lock)
        {
            observersCopy = new List<ICheckpointObserver>(_observers);
        }

        _logger.LogInformation(
            "Checkpoint event: {EventType} for checkpoint {CheckpointId}",
            e.EventType, e.CheckpointId);

        foreach (var observer in observersCopy)
        {
            try
            {
                await observer.OnCheckpointEventAsync(e, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error notifying checkpoint observer {ObserverType}",
                    observer.GetType().Name);
            }
        }
    }
}

/// <summary>
/// Telemetry observer that records metrics for workflow state changes.
/// </summary>
public class TelemetryWorkflowObserver : IWorkflowObserver
{
    private readonly WorkflowTelemetry _telemetry;
    private readonly Dictionary<string, DateTime> _workflowStartTimes = new();
    private readonly object _lock = new();

    public TelemetryWorkflowObserver(WorkflowTelemetry telemetry)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public Task OnWorkflowStateChangedAsync(WorkflowStateChangedEventArgs e, CancellationToken ct = default)
    {
        lock (_lock)
        {
            switch (e.NewState)
            {
                case WorkflowState.Running:
                    if (e.PreviousState == WorkflowState.Queued)
                    {
                        _telemetry.RecordWorkflowStarted(e.WorkflowType);
                        _workflowStartTimes[e.WorkflowId] = e.Timestamp;
                    }
                    else if (e.PreviousState == WorkflowState.Paused)
                    {
                        var pauseDuration = GetElapsedSeconds(e.WorkflowId, e.Timestamp);
                        _telemetry.RecordWorkflowResumed(e.WorkflowType, pauseDuration);
                    }
                    break;

                case WorkflowState.Paused:
                    var pauseLatency = GetElapsedSeconds(e.WorkflowId, e.Timestamp);
                    _telemetry.RecordWorkflowPaused(e.WorkflowType, pauseLatency, e.Reason ?? "unknown");
                    break;

                case WorkflowState.Completed:
                    var duration = GetElapsedSeconds(e.WorkflowId, e.Timestamp);
                    _telemetry.RecordWorkflowCompleted(e.WorkflowType, duration);
                    _workflowStartTimes.Remove(e.WorkflowId);
                    break;

                case WorkflowState.Failed:
                    var failedDuration = GetElapsedSeconds(e.WorkflowId, e.Timestamp);
                    _telemetry.RecordWorkflowFailed(e.WorkflowType, e.Reason ?? "unknown", failedDuration);
                    _workflowStartTimes.Remove(e.WorkflowId);
                    break;

                case WorkflowState.Cancelled:
                    var cancelledDuration = GetElapsedSeconds(e.WorkflowId, e.Timestamp);
                    _telemetry.RecordWorkflowCancelled(e.WorkflowType, cancelledDuration);
                    _workflowStartTimes.Remove(e.WorkflowId);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private double GetElapsedSeconds(string workflowId, DateTime endTime)
    {
        if (_workflowStartTimes.TryGetValue(workflowId, out var startTime))
        {
            return (endTime - startTime).TotalSeconds;
        }
        return 0;
    }
}

/// <summary>
/// Telemetry observer that records metrics for checkpoint events.
/// </summary>
public class TelemetryCheckpointObserver : ICheckpointObserver
{
    private readonly CheckpointTelemetry _telemetry;

    public TelemetryCheckpointObserver(CheckpointTelemetry telemetry)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public Task OnCheckpointEventAsync(CheckpointEventArgs e, CancellationToken ct = default)
    {
        switch (e.EventType)
        {
            case CheckpointEventType.Deleted:
                if (e.SizeBytes.HasValue)
                {
                    _telemetry.RecordCheckpointDeleted(e.WorkflowId, e.SizeBytes.Value);
                }
                break;

            case CheckpointEventType.Validated:
                _telemetry.RecordValidation(e.CheckpointId, true);
                break;

            case CheckpointEventType.Failed:
                _telemetry.RecordError("checkpoint", "failure", e.Reason ?? "unknown");
                break;
        }

        return Task.CompletedTask;
    }
}
