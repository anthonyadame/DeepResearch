using System.ComponentModel.DataAnnotations;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Configuration for OpenTelemetry observability features.
/// Allows fine-grained control over tracing and metrics collection.
/// </summary>
public class ObservabilityConfiguration
{
    /// <summary>
    /// Master switch: Enable/disable distributed tracing (Activities)
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Master switch: Enable/disable metrics collection
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable/disable detailed per-step tracing.
    /// When false, only workflow-level activities are created.
    /// </summary>
    public bool EnableDetailedTracing { get; set; } = true;

    /// <summary>
    /// Sampling rate for traces (0.0 to 1.0).
    /// 1.0 = trace all requests (development).
    /// 0.1 = trace 10% of requests (production).
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "TraceSamplingRate must be between 0.0 and 1.0")]
    public double TraceSamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Only trace operations that exceed this duration (ms).
    /// 0 = trace all operations.
    /// 5000 = only trace operations > 5 seconds.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "SlowOperationThresholdMs must be non-negative")]
    public int SlowOperationThresholdMs { get; set; } = 0;

    /// <summary>
    /// Enable async background processing for metrics.
    /// Reduces synchronous overhead from ~3ms to ~0.3ms.
    /// </summary>
    public bool UseAsyncMetrics { get; set; } = false;

    /// <summary>
    /// Maximum size of async metrics queue.
    /// Only used if UseAsyncMetrics = true.
    /// </summary>
    [Range(100, 1000000, ErrorMessage = "AsyncMetricsQueueSize must be between 100 and 1,000,000")]
    public int AsyncMetricsQueueSize { get; set; } = 10000;

    /// <summary>
    /// Enable activity events (AddEvent calls).
    /// Events add ~0.2ms overhead each.
    /// </summary>
    public bool EnableActivityEvents { get; set; } = true;

    /// <summary>
    /// Enable exception recording in activities.
    /// </summary>
    public bool EnableExceptionRecording { get; set; } = true;

    /// <summary>
    /// Validate configuration on startup
    /// </summary>
    public void Validate()
    {
        if (TraceSamplingRate < 0.0 || TraceSamplingRate > 1.0)
        {
            throw new ArgumentException(
                $"TraceSamplingRate must be between 0.0 and 1.0. Current value: {TraceSamplingRate}");
        }

        if (SlowOperationThresholdMs < 0)
        {
            throw new ArgumentException(
                $"SlowOperationThresholdMs must be non-negative. Current value: {SlowOperationThresholdMs}");
        }

        if (AsyncMetricsQueueSize < 100 || AsyncMetricsQueueSize > 1000000)
        {
            throw new ArgumentException(
                $"AsyncMetricsQueueSize must be between 100 and 1,000,000. Current value: {AsyncMetricsQueueSize}");
        }
    }

    /// <summary>
    /// Get a summary of the current configuration
    /// </summary>
    public string GetSummary()
    {
        return $"""
            Observability Configuration:
            - Tracing: {(EnableTracing ? "Enabled" : "Disabled")}
            - Metrics: {(EnableMetrics ? "Enabled" : "Disabled")}
            - Detailed Tracing: {(EnableDetailedTracing ? "Enabled" : "Disabled")}
            - Sampling Rate: {TraceSamplingRate:P0}
            - Slow Operation Threshold: {SlowOperationThresholdMs}ms
            - Async Metrics: {(UseAsyncMetrics ? "Enabled" : "Disabled")}
            - Metrics Queue Size: {AsyncMetricsQueueSize:N0}
            - Activity Events: {(EnableActivityEvents ? "Enabled" : "Disabled")}
            - Exception Recording: {(EnableExceptionRecording ? "Enabled" : "Disabled")}
            """;
    }

    /// <summary>
    /// Create development configuration (full observability)
    /// </summary>
    public static ObservabilityConfiguration Development() => new()
    {
        EnableTracing = true,
        EnableMetrics = true,
        EnableDetailedTracing = true,
        TraceSamplingRate = 1.0,
        SlowOperationThresholdMs = 0,
        UseAsyncMetrics = false,
        EnableActivityEvents = true,
        EnableExceptionRecording = true
    };

    /// <summary>
    /// Create production configuration (minimal overhead)
    /// </summary>
    public static ObservabilityConfiguration Production() => new()
    {
        EnableTracing = true,
        EnableMetrics = true,
        EnableDetailedTracing = false,
        TraceSamplingRate = 0.1, // 10% sampling
        SlowOperationThresholdMs = 10000,
        UseAsyncMetrics = true,
        AsyncMetricsQueueSize = 50000,
        EnableActivityEvents = false,
        EnableExceptionRecording = true
    };

    /// <summary>
    /// Create staging configuration (balanced)
    /// </summary>
    public static ObservabilityConfiguration Staging() => new()
    {
        EnableTracing = true,
        EnableMetrics = true,
        EnableDetailedTracing = true,
        TraceSamplingRate = 0.5, // 50% sampling
        SlowOperationThresholdMs = 5000,
        UseAsyncMetrics = true,
        AsyncMetricsQueueSize = 25000,
        EnableActivityEvents = true,
        EnableExceptionRecording = true
    };
}
