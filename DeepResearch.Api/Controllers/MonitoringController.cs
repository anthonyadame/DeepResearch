using DeepResearch.Api.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// API controller for workflow monitoring and dashboard data.
/// Provides aggregated metrics and real-time workflow status information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitoringController : ControllerBase
{
    private readonly IWorkflowPauseResumeService _pauseResumeService;
    private readonly ICheckpointService _checkpointService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IWorkflowPauseResumeService pauseResumeService,
        ICheckpointService checkpointService,
        ILogger<MonitoringController> logger)
    {
        _pauseResumeService = pauseResumeService ?? throw new ArgumentNullException(nameof(pauseResumeService));
        _checkpointService = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get comprehensive monitoring data for dashboard.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Monitoring data including active workflows, metrics, and checkpoints.</returns>
    /// <response code="200">Monitoring data retrieved successfully.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(WorkflowMonitoringResponseDto), 200)]
    public async Task<IActionResult> GetDashboardData(CancellationToken ct = default)
    {
        try
        {
            // TODO: Implement actual workflow tracking
            // For now, return placeholder data

            var checkpointStats = await _checkpointService.GetStatisticsAsync(ct);

            var response = new WorkflowMonitoringResponseDto
            {
                ActiveWorkflows = new(),
                PausedWorkflows = new(),
                Metrics = new WorkflowMetricsDto
                {
                    TotalWorkflows = 0,
                    ActiveWorkflows = 0,
                    PausedWorkflows = 0,
                    CompletedWorkflows = 0,
                    FailedWorkflows = 0,
                    AverageExecutionTime = TimeSpan.Zero,
                    CheckpointsCreatedToday = checkpointStats.RecentCheckpointsCount,
                    TotalCheckpointStorage = checkpointStats.TotalStorageUsedBytes
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard data");
            return StatusCode(500, new { error = "Failed to retrieve dashboard data" });
        }
    }

    /// <summary>
    /// Get workflow execution metrics.
    /// </summary>
    /// <param name="timeRange">Time range for metrics (1h, 24h, 7d, 30d).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Workflow metrics.</returns>
    /// <response code="200">Metrics retrieved successfully.</response>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(WorkflowMetricsDto), 200)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] string timeRange = "24h",
        CancellationToken ct = default)
    {
        try
        {
            var checkpointStats = await _checkpointService.GetStatisticsAsync(ct);

            var metrics = new WorkflowMetricsDto
            {
                TotalWorkflows = 0, // TODO: Track from workflow registry
                ActiveWorkflows = 0,
                PausedWorkflows = 0,
                CompletedWorkflows = 0,
                FailedWorkflows = 0,
                AverageExecutionTime = TimeSpan.Zero,
                CheckpointsCreatedToday = checkpointStats.RecentCheckpointsCount,
                TotalCheckpointStorage = checkpointStats.TotalStorageUsedBytes
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Get health check status for monitoring systems.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health status.</returns>
    /// <response code="200">System healthy.</response>
    /// <response code="503">System unhealthy.</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthCheckResponseDto), 200)]
    [ProducesResponseType(typeof(HealthCheckResponseDto), 503)]
    public async Task<IActionResult> HealthCheck(CancellationToken ct = default)
    {
        try
        {
            // Check checkpoint service health
            var stats = await _checkpointService.GetStatisticsAsync(ct);
            
            var response = new HealthCheckResponseDto
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Services = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "checkpoint-service", "healthy" },
                    { "pause-resume-service", "healthy" }
                },
                Metrics = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "total-checkpoints", stats.TotalCheckpoints },
                    { "storage-used-bytes", stats.TotalStorageUsedBytes }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            var response = new HealthCheckResponseDto
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Services = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "checkpoint-service", "unhealthy" }
                },
                Error = ex.Message
            };

            return StatusCode(503, response);
        }
    }

    /// <summary>
    /// Get system-wide statistics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>System statistics.</returns>
    /// <response code="200">Statistics retrieved successfully.</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(SystemStatisticsDto), 200)]
    public async Task<IActionResult> GetSystemStatistics(CancellationToken ct = default)
    {
        try
        {
            var checkpointStats = await _checkpointService.GetStatisticsAsync(ct);

            var response = new SystemStatisticsDto
            {
                Checkpoints = new CheckpointStatisticsResponseDto
                {
                    TotalCheckpoints = checkpointStats.TotalCheckpoints,
                    AverageCheckpointSizeBytes = checkpointStats.AverageCheckpointSizeBytes,
                    LargestCheckpointSizeBytes = checkpointStats.LargestCheckpointSizeBytes,
                    TotalStorageUsedBytes = checkpointStats.TotalStorageUsedBytes,
                    RecentCheckpointsCount = checkpointStats.RecentCheckpointsCount,
                    OldestCheckpointAt = checkpointStats.OldestCheckpointAt,
                    NewestCheckpointAt = checkpointStats.NewestCheckpointAt
                },
                Workflows = new WorkflowStatisticsDto
                {
                    TotalWorkflows = 0,
                    ActiveCount = 0,
                    PausedCount = 0,
                    CompletedCount = 0,
                    FailedCount = 0
                },
                System = new SystemInfoDto
                {
                    Uptime = TimeSpan.Zero, // TODO: Track actual uptime
                    Version = "1.0.0",
                    Environment = "Development"
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }
}

/// <summary>
/// Health check response model.
/// </summary>
public class HealthCheckResponseDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public System.Collections.Generic.Dictionary<string, string> Services { get; set; } = new();
    public System.Collections.Generic.Dictionary<string, object> Metrics { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// System statistics response model.
/// </summary>
public class SystemStatisticsDto
{
    public CheckpointStatisticsResponseDto Checkpoints { get; set; } = new();
    public WorkflowStatisticsDto Workflows { get; set; } = new();
    public SystemInfoDto System { get; set; } = new();
}

/// <summary>
/// Workflow statistics model.
/// </summary>
public class WorkflowStatisticsDto
{
    public int TotalWorkflows { get; set; }
    public int ActiveCount { get; set; }
    public int PausedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// System information model.
/// </summary>
public class SystemInfoDto
{
    public TimeSpan Uptime { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}
