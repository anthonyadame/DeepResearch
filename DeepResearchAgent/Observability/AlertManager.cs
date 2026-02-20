using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Alert model for AlertManager.
/// </summary>
public class Alert
{
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Annotations { get; set; } = new();
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string GeneratorURL { get; set; } = string.Empty;
}

/// <summary>
/// AlertManager client for sending alerts.
/// Integrates with Prometheus AlertManager for workflow and checkpoint alerts.
/// </summary>
public class AlertManagerClient
{
    private readonly HttpClient _httpClient;
    private readonly string _alertManagerUrl;
    private readonly ILogger<AlertManagerClient> _logger;

    public AlertManagerClient(
        HttpClient httpClient,
        string alertManagerUrl,
        ILogger<AlertManagerClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _alertManagerUrl = alertManagerUrl ?? throw new ArgumentNullException(nameof(alertManagerUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Send alert to AlertManager.
    /// </summary>
    public async Task SendAlertAsync(
        string alertName,
        AlertSeverity severity,
        string summary,
        string description,
        Dictionary<string, string>? additionalLabels = null,
        CancellationToken ct = default)
    {
        var alert = new Alert
        {
            Labels = new Dictionary<string, string>
            {
                { "alertname", alertName },
                { "severity", severity.ToString().ToLower() },
                { "service", "deepresearch-workflows" }
            },
            Annotations = new Dictionary<string, string>
            {
                { "summary", summary },
                { "description", description }
            },
            StartsAt = DateTime.UtcNow
        };

        if (additionalLabels != null)
        {
            foreach (var kvp in additionalLabels)
            {
                alert.Labels[kvp.Key] = kvp.Value;
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(new[] { alert });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                $"{_alertManagerUrl}/api/v2/alerts",
                content,
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Alert sent to AlertManager: {AlertName} ({Severity})",
                    alertName, severity);
            }
            else
            {
                _logger.LogWarning("Failed to send alert to AlertManager: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert to AlertManager: {AlertName}", alertName);
        }
    }

    /// <summary>
    /// Send workflow failure alert.
    /// </summary>
    public Task SendWorkflowFailureAlertAsync(
        string workflowId,
        string workflowType,
        string errorMessage,
        CancellationToken ct = default)
    {
        return SendAlertAsync(
            "WorkflowFailed",
            AlertSeverity.Critical,
            $"Workflow {workflowId} failed",
            $"Workflow {workflowId} of type {workflowType} failed with error: {errorMessage}",
            new Dictionary<string, string>
            {
                { "workflow_id", workflowId },
                { "workflow_type", workflowType }
            },
            ct);
    }

    /// <summary>
    /// Send workflow long-running alert.
    /// </summary>
    public Task SendWorkflowLongRunningAlertAsync(
        string workflowId,
        string workflowType,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        return SendAlertAsync(
            "WorkflowLongRunning",
            AlertSeverity.Warning,
            $"Workflow {workflowId} running for {duration.TotalMinutes:F1} minutes",
            $"Workflow {workflowId} of type {workflowType} has been running longer than expected",
            new Dictionary<string, string>
            {
                { "workflow_id", workflowId },
                { "workflow_type", workflowType },
                { "duration_minutes", duration.TotalMinutes.ToString("F0") }
            },
            ct);
    }

    /// <summary>
    /// Send checkpoint storage alert.
    /// </summary>
    public Task SendCheckpointStorageAlertAsync(
        long storageBytes,
        long thresholdBytes,
        CancellationToken ct = default)
    {
        return SendAlertAsync(
            "CheckpointStorageHigh",
            AlertSeverity.Warning,
            $"Checkpoint storage usage: {storageBytes / 1024 / 1024} MB",
            $"Checkpoint storage has exceeded threshold: {storageBytes / 1024 / 1024} MB > {thresholdBytes / 1024 / 1024} MB",
            new Dictionary<string, string>
            {
                { "storage_mb", (storageBytes / 1024 / 1024).ToString() },
                { "threshold_mb", (thresholdBytes / 1024 / 1024).ToString() }
            },
            ct);
    }

    /// <summary>
    /// Send checkpoint validation failure alert.
    /// </summary>
    public Task SendCheckpointValidationFailureAlertAsync(
        string checkpointId,
        string workflowId,
        string errorMessage,
        CancellationToken ct = default)
    {
        return SendAlertAsync(
            "CheckpointValidationFailed",
            AlertSeverity.Warning,
            $"Checkpoint {checkpointId} validation failed",
            $"Checkpoint {checkpointId} for workflow {workflowId} failed validation: {errorMessage}",
            new Dictionary<string, string>
            {
                { "checkpoint_id", checkpointId },
                { "workflow_id", workflowId }
            },
            ct);
    }
}

/// <summary>
/// Observer that sends alerts to AlertManager for critical events.
/// </summary>
public class AlertManagerWorkflowObserver : IWorkflowObserver
{
    private readonly AlertManagerClient _alertManager;
    private readonly ILogger<AlertManagerWorkflowObserver> _logger;
    private readonly Dictionary<string, DateTime> _workflowStartTimes = new();
    private readonly TimeSpan _longRunningThreshold = TimeSpan.FromMinutes(30);
    private readonly object _lock = new();

    public AlertManagerWorkflowObserver(
        AlertManagerClient alertManager,
        ILogger<AlertManagerWorkflowObserver> logger)
    {
        _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnWorkflowStateChangedAsync(WorkflowStateChangedEventArgs e, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (e.NewState == WorkflowState.Running && e.PreviousState == WorkflowState.Queued)
            {
                _workflowStartTimes[e.WorkflowId] = e.Timestamp;
            }
        }

        switch (e.NewState)
        {
            case WorkflowState.Failed:
                await _alertManager.SendWorkflowFailureAlertAsync(
                    e.WorkflowId,
                    e.WorkflowType,
                    e.Reason ?? "Unknown error",
                    ct);
                
                lock (_lock)
                {
                    _workflowStartTimes.Remove(e.WorkflowId);
                }
                break;

            case WorkflowState.Running:
                // Check if workflow is running too long
                DateTime startTime;
                lock (_lock)
                {
                    if (!_workflowStartTimes.TryGetValue(e.WorkflowId, out startTime))
                    {
                        break;
                    }
                }

                var duration = e.Timestamp - startTime;
                if (duration > _longRunningThreshold)
                {
                    await _alertManager.SendWorkflowLongRunningAlertAsync(
                        e.WorkflowId,
                        e.WorkflowType,
                        duration,
                        ct);
                }
                break;

            case WorkflowState.Completed:
            case WorkflowState.Cancelled:
                lock (_lock)
                {
                    _workflowStartTimes.Remove(e.WorkflowId);
                }
                break;
        }
    }
}

/// <summary>
/// Observer that sends alerts to AlertManager for checkpoint events.
/// </summary>
public class AlertManagerCheckpointObserver : ICheckpointObserver
{
    private readonly AlertManagerClient _alertManager;
    private readonly ILogger<AlertManagerCheckpointObserver> _logger;
    private readonly long _storageThresholdBytes = 1024L * 1024 * 1024 * 5; // 5 GB
    private long _currentStorageBytes = 0;

    public AlertManagerCheckpointObserver(
        AlertManagerClient alertManager,
        ILogger<AlertManagerCheckpointObserver> logger)
    {
        _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnCheckpointEventAsync(CheckpointEventArgs e, CancellationToken ct = default)
    {
        switch (e.EventType)
        {
            case CheckpointEventType.Created:
                if (e.SizeBytes.HasValue)
                {
                    _currentStorageBytes += e.SizeBytes.Value;
                    
                    if (_currentStorageBytes > _storageThresholdBytes)
                    {
                        await _alertManager.SendCheckpointStorageAlertAsync(
                            _currentStorageBytes,
                            _storageThresholdBytes,
                            ct);
                    }
                }
                break;

            case CheckpointEventType.Deleted:
                if (e.SizeBytes.HasValue)
                {
                    _currentStorageBytes -= e.SizeBytes.Value;
                }
                break;

            case CheckpointEventType.Failed:
                await _alertManager.SendCheckpointValidationFailureAlertAsync(
                    e.CheckpointId,
                    e.WorkflowId,
                    e.Reason ?? "Unknown error",
                    ct);
                break;
        }
    }
}
