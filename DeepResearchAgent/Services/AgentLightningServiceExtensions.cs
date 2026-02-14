using System.Net;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace DeepResearchAgent.Services;

/// <summary>
/// Extension methods and helpers for Agent-Lightning APO integration.
/// </summary>
public static class AgentLightningServiceExtensions
{
    /// <summary>
    /// Create retry policy based on APO configuration.
    /// Uses decorrelated jitter for exponential backoff to prevent thundering herd.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(
        LightningAPOConfig apo,
        Action<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context>? onRetry = null)
    {
        var retryCount = apo.Strategy switch
        {
            OptimizationStrategy.HighPerformance => 2,  // Fast fail
            OptimizationStrategy.Balanced => 3,
            OptimizationStrategy.LowResource => 5,      // More retries, less load
            OptimizationStrategy.CostOptimized => 4,
            _ => 3
        };

        var delays = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(250),
            retryCount: retryCount);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(delays, onRetry: onRetry ?? ((_, _, _, _) => { }));
    }

    /// <summary>
    /// Create concurrency gate (semaphore) based on APO configuration.
    /// </summary>
    public static SemaphoreSlim CreateConcurrencyGate(LightningAPOConfig apo)
    {
        var maxConcurrent = apo.ResourceLimits.MaxConcurrentTasks;
        return new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <summary>
    /// Determine if VERL verification should be performed based on APO strategy.
    /// </summary>
    public static bool ShouldVerify(this LightningAPOConfig apo, bool taskRequiresVerification)
    {
        if (!taskRequiresVerification) return false;

        return apo.Strategy switch
        {
            OptimizationStrategy.HighPerformance => false,  // Skip for max throughput
            OptimizationStrategy.Balanced => true,
            OptimizationStrategy.LowResource => true,
            OptimizationStrategy.CostOptimized => true,
            _ => true
        };
    }

    /// <summary>
    /// Get task priority based on APO strategy.
    /// </summary>
    public static int GetTaskPriority(this LightningAPOConfig apo, int? customPriority = null)
    {
        if (customPriority.HasValue) return customPriority.Value;

        return apo.Strategy switch
        {
            OptimizationStrategy.HighPerformance => 10,
            OptimizationStrategy.Balanced => 5,
            OptimizationStrategy.LowResource => 3,
            OptimizationStrategy.CostOptimized => 4,
            _ => 5
        };
    }
}

/// <summary>
/// Options for overriding APO behavior at function call level.
/// </summary>
public class ApoExecutionOptions
{
    /// <summary>
    /// Override optimization strategy for this execution.
    /// </summary>
    public OptimizationStrategy? StrategyOverride { get; set; }

    /// <summary>
    /// Force VERL verification regardless of strategy.
    /// </summary>
    public bool? ForceVerification { get; set; }

    /// <summary>
    /// Custom task priority (0-10).
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Custom timeout for this execution.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Disable APO for this specific execution.
    /// </summary>
    public bool DisableApo { get; set; }

    /// <summary>
    /// Create effective APO config by merging overrides with base config.
    /// </summary>
    public LightningAPOConfig MergeWith(LightningAPOConfig baseConfig)
    {
        if (DisableApo)
        {
            return new LightningAPOConfig { Enabled = false };
        }

        var merged = new LightningAPOConfig
        {
            Enabled = baseConfig.Enabled,
            Strategy = StrategyOverride ?? baseConfig.Strategy,
            ResourceLimits = new ResourceLimits
            {
                MaxCpuPercent = baseConfig.ResourceLimits.MaxCpuPercent,
                MaxMemoryMb = baseConfig.ResourceLimits.MaxMemoryMb,
                MaxConcurrentTasks = baseConfig.ResourceLimits.MaxConcurrentTasks,
                TaskTimeoutSeconds = Timeout.HasValue 
                    ? (int)Timeout.Value.TotalSeconds 
                    : baseConfig.ResourceLimits.TaskTimeoutSeconds,
                CacheSizeMb = baseConfig.ResourceLimits.CacheSizeMb
            },
            Metrics = baseConfig.Metrics,
            AutoScaling = baseConfig.AutoScaling
        };

        return merged;
    }
}
