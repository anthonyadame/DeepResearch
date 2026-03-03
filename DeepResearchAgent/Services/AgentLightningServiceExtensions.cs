using System.Net;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace DeepResearchAgent.Services;

/// <summary>
/// Extension methods and helpers for Agent-Lightning RMPT integration.
/// </summary>
public static class AgentLightningServiceExtensions
{
    /// <summary>
    /// Create retry policy based on RMPT configuration.
    /// Uses decorrelated jitter for exponential backoff to prevent thundering herd.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(
        LightningRMPTConfig rmpt,
        Action<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context>? onRetry = null)
    {
        var retryCount = rmpt.Strategy switch
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
    /// Create concurrency gate (semaphore) based on RMPT configuration.
    /// </summary>
    public static SemaphoreSlim CreateConcurrencyGate(LightningRMPTConfig rmpt)
    {
        var maxConcurrent = rmpt.ResourceLimits.MaxConcurrentTasks;
        return new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <summary>
    /// Determine if RLCS verification should be performed based on RMPT strategy.
    /// </summary>
    public static bool ShouldVerify(this LightningRMPTConfig rmpt, bool taskRequiresVerification)
    {
        if (!taskRequiresVerification) return false;

        return rmpt.Strategy switch
        {
            OptimizationStrategy.HighPerformance => false,  // Skip for max throughput
            OptimizationStrategy.Balanced => true,
            OptimizationStrategy.LowResource => true,
            OptimizationStrategy.CostOptimized => true,
            _ => true
        };
    }

    /// <summary>
    /// Get task priority based on RMPT strategy.
    /// </summary>
    public static int GetTaskPriority(this LightningRMPTConfig rmpt, int? customPriority = null)
    {
        if (customPriority.HasValue) return customPriority.Value;

        return rmpt.Strategy switch
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
/// Options for overriding RMPT behavior at function call level.
/// </summary>
public class RmptExecutionOptions
{
    /// <summary>
    /// Override optimization strategy for this execution.
    /// </summary>
    public OptimizationStrategy? StrategyOverride { get; set; }

    /// <summary>
    /// Force RLCS verification regardless of strategy.
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
    /// Disable RMPT for this specific execution.
    /// </summary>
    public bool DisableRmpt { get; set; }

    /// <summary>
    /// Create effective RMPT config by merging overrides with base config.
    /// </summary>
    public LightningRMPTConfig MergeWith(LightningRMPTConfig baseConfig)
    {
        if (DisableRmpt)
        {
            return new LightningRMPTConfig { Enabled = false };
        }

        var merged = new LightningRMPTConfig
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
