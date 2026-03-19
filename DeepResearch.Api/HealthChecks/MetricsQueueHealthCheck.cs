using DeepResearchAgent.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepResearch.Api.HealthChecks;

/// <summary>
/// Health check for monitoring the async metrics queue
/// </summary>
public class MetricsQueueHealthCheck : IHealthCheck
{
    private readonly AsyncMetricsCollector? _metricsCollector;
    private readonly ObservabilityConfiguration _config;

    public MetricsQueueHealthCheck(
        ObservabilityConfiguration config,
        AsyncMetricsCollector? metricsCollector = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metricsCollector = metricsCollector;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // If async metrics is disabled, health check is not applicable
        if (!_config.UseAsyncMetrics)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Async metrics is disabled, no queue monitoring needed"));
        }

        // If collector is not available, report degraded
        if (_metricsCollector == null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "AsyncMetricsCollector is not registered or available"));
        }

        try
        {
            var health = _metricsCollector.GetHealth();

            // Define thresholds
            const double warningUtilizationThreshold = 80.0; // 80% queue utilization
            const double criticalUtilizationThreshold = 95.0; // 95% queue utilization
            const double criticalDropRateThreshold = 5.0; // 5% drop rate
            const int staleProcessingThresholdSeconds = 60; // 1 minute without processing

            var data = new Dictionary<string, object>
            {
                { "QueueDepth", health.QueueDepth },
                { "QueueCapacity", health.QueueCapacity },
                { "QueueUtilization%", Math.Round(health.UtilizationPercent, 2) },
                { "TotalEnqueued", health.TotalEnqueued },
                { "TotalProcessed", health.TotalProcessed },
                { "TotalDropped", health.TotalDropped },
                { "DropRate%", Math.Round(health.DropRatePercent, 2) },
                { "LastProcessedTime", health.LastProcessedTime },
                { "SecondsSinceLastProcessed", (DateTime.UtcNow - health.LastProcessedTime).TotalSeconds }
            };

            // Check for unhealthy conditions
            var issues = new List<string>();

            if (health.UtilizationPercent >= criticalUtilizationThreshold)
            {
                issues.Add($"Queue critically full: {health.UtilizationPercent:F1}% (threshold: {criticalUtilizationThreshold}%)");
            }

            if (health.DropRatePercent >= criticalDropRateThreshold)
            {
                issues.Add($"High drop rate: {health.DropRatePercent:F2}% (threshold: {criticalDropRateThreshold}%)");
            }

            var secondsSinceLastProcessed = (DateTime.UtcNow - health.LastProcessedTime).TotalSeconds;
            if (health.TotalEnqueued > 0 && secondsSinceLastProcessed > staleProcessingThresholdSeconds)
            {
                issues.Add($"Processing stalled: {secondsSinceLastProcessed:F0}s since last metric processed");
            }

            // Determine health status
            if (issues.Count > 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Metrics queue issues detected: {string.Join("; ", issues)}",
                    data: data));
            }

            // Check for degraded conditions (warnings)
            var warnings = new List<string>();

            if (health.UtilizationPercent >= warningUtilizationThreshold && 
                health.UtilizationPercent < criticalUtilizationThreshold)
            {
                warnings.Add($"Queue utilization high: {health.UtilizationPercent:F1}%");
            }

            if (health.TotalDropped > 0)
            {
                warnings.Add($"Some metrics dropped: {health.TotalDropped} ({health.DropRatePercent:F2}%)");
            }

            if (warnings.Count > 0)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Metrics queue warnings: {string.Join("; ", warnings)}",
                    data: data));
            }

            // Everything is healthy
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Metrics queue healthy: {health.QueueDepth}/{health.QueueCapacity} items, " +
                $"{health.TotalProcessed} processed, {health.UtilizationPercent:F1}% utilization",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Error checking metrics queue health",
                exception: ex));
        }
    }
}
