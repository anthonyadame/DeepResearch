using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services;

/// <summary>
/// Configuration for Agent-Lightning APO (Automatic Performance Optimization).
/// Manages performance tuning, resource allocation, and optimization strategies.
/// </summary>
public class LightningAPOConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("optimizationStrategy")]
    public OptimizationStrategy Strategy { get; set; } = OptimizationStrategy.Balanced;

    [JsonPropertyName("resourceLimits")]
    public ResourceLimits ResourceLimits { get; set; } = new();

    [JsonPropertyName("performanceMetrics")]
    public PerformanceMetrics Metrics { get; set; } = new();

    [JsonPropertyName("autoScaling")]
    public AutoScalingConfig AutoScaling { get; set; } = new();

    [JsonPropertyName("circuitBreaker")]
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

public enum OptimizationStrategy
{
    /// <summary>Prioritize speed over resource usage</summary>
    HighPerformance,
    
    /// <summary>Balanced approach between speed and resources</summary>
    Balanced,
    
    /// <summary>Minimize resource usage, accept slower execution</summary>
    LowResource,
    
    /// <summary>Optimize for cost efficiency</summary>
    CostOptimized
}

public class ResourceLimits
{
    [JsonPropertyName("maxCpuPercent")]
    public int MaxCpuPercent { get; set; } = 80;

    [JsonPropertyName("maxMemoryMb")]
    public int MaxMemoryMb { get; set; } = 2048;

    [JsonPropertyName("maxConcurrentTasks")]
    public int MaxConcurrentTasks { get; set; } = 10;

    [JsonPropertyName("taskTimeoutSeconds")]
    public int TaskTimeoutSeconds { get; set; } = 300;

    [JsonPropertyName("cacheSizeMb")]
    public int CacheSizeMb { get; set; } = 512;
}

public class PerformanceMetrics
{
    [JsonPropertyName("trackingEnabled")]
    public bool TrackingEnabled { get; set; } = true;

    [JsonPropertyName("metricsInterval")]
    public int MetricsIntervalSeconds { get; set; } = 10;

    [JsonPropertyName("performanceLoggingLevel")]
    public string PerformanceLoggingLevel { get; set; } = "Info";

    [JsonPropertyName("enableTracing")]
    public bool EnableTracing { get; set; } = true;

    [JsonPropertyName("enableProfiling")]
    public bool EnableProfiling { get; set; } = false;
}

public class AutoScalingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("minInstances")]
    public int MinInstances { get; set; } = 1;

    [JsonPropertyName("maxInstances")]
    public int MaxInstances { get; set; } = 5;

    [JsonPropertyName("scaleUpThreshold")]
    public int ScaleUpThresholdPercent { get; set; } = 70;

    [JsonPropertyName("scaleDownThreshold")]
    public int ScaleDownThresholdPercent { get; set; } = 30;

    [JsonPropertyName("cooldownSeconds")]
    public int CooldownSeconds { get; set; } = 60;
}

/// <summary>
/// Circuit breaker configuration for Lightning server failures.
/// Prevents cascading failures and provides graceful degradation.
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Enable circuit breaker pattern (recommended for production).
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening circuit.
    /// </summary>
    [JsonPropertyName("failureThreshold")]
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Minimum throughput (requests/sec) required before circuit breaker activates.
    /// Prevents circuit breaking on low-traffic scenarios.
    /// </summary>
    [JsonPropertyName("minimumThroughput")]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Time window for measuring failure rate (seconds).
    /// </summary>
    [JsonPropertyName("samplingDurationSeconds")]
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Percentage of failed requests that triggers circuit open (0-100).
    /// </summary>
    [JsonPropertyName("failureRateThreshold")]
    public double FailureRateThreshold { get; set; } = 50;

    /// <summary>
    /// How long circuit stays open before attempting recovery (seconds).
    /// </summary>
    [JsonPropertyName("breakDurationSeconds")]
    public int BreakDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Enable fallback to local execution when circuit is open.
    /// </summary>
    [JsonPropertyName("enableFallback")]
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Log circuit state changes (Open, HalfOpen, Closed).
    /// </summary>
    [JsonPropertyName("logStateChanges")]
    public bool LogStateChanges { get; set; } = true;
}