using System.Collections.Concurrent;
using System.Diagnostics;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Collects and tracks performance metrics for methods.
/// Provides real-time visibility into execution history and performance.
/// </summary>
public class MetricsCollector
{
    private static readonly ConcurrentDictionary<string, MethodMetrics> _methodMetrics = new();
    private static readonly ConcurrentQueue<ExecutionRecord> _executionHistory = new();
    private const int MaxHistorySize = 1000;

    /// <summary>
    /// Start tracking execution of a method
    /// </summary>
    public static IDisposable TrackExecution(
        string methodName,
        string? workflow = null,
        string? step = null,
        Dictionary<string, object>? metadata = null)
    {
        var key = BuildKey(methodName, workflow, step);
        var record = new ExecutionRecord
        {
            MethodName = methodName,
            Workflow = workflow,
            Step = step,
            StartTime = DateTime.UtcNow,
            ThreadId = Environment.CurrentManagedThreadId,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        return new ExecutionTracker(key, record);
    }

    /// <summary>
    /// Get metrics for a specific method
    /// </summary>
    public static MethodMetrics? GetMetrics(string methodName, string? workflow = null, string? step = null)
    {
        var key = BuildKey(methodName, workflow, step);
        return _methodMetrics.TryGetValue(key, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Get recent execution history (up to maxCount records)
    /// </summary>
    public static List<ExecutionRecord> GetExecutionHistory(int maxCount = 100)
    {
        return _executionHistory.Reverse().Take(maxCount).ToList();
    }

    /// <summary>
    /// Get execution history filtered by workflow or method
    /// </summary>
    public static List<ExecutionRecord> GetExecutionHistory(string? workflow = null, string? methodName = null, int maxCount = 100)
    {
        return _executionHistory.Reverse()
            .Where(r => (workflow == null || r.Workflow == workflow) &&
                       (methodName == null || r.MethodName == methodName))
            .Take(maxCount)
            .ToList();
    }

    /// <summary>
    /// Get all tracked method metrics
    /// </summary>
    public static Dictionary<string, MethodMetrics> GetAllMetrics()
    {
        return new Dictionary<string, MethodMetrics>(_methodMetrics);
    }

    /// <summary>
    /// Clear all metrics and history
    /// </summary>
    public static void Clear()
    {
        _methodMetrics.Clear();
        _executionHistory.Clear();
    }

    private static string BuildKey(string methodName, string? workflow, string? step)
    {
        if (workflow != null && step != null)
            return $"{workflow}.{step}.{methodName}";
        if (workflow != null)
            return $"{workflow}.{methodName}";
        return methodName;
    }

    private static void RecordExecution(string key, ExecutionRecord record)
    {
        // Update method metrics
        _methodMetrics.AddOrUpdate(
            key,
            _ => new MethodMetrics
            {
                MethodName = record.MethodName,
                TotalExecutions = 1,
                TotalDurationMs = record.DurationMs,
                MinDurationMs = record.DurationMs,
                MaxDurationMs = record.DurationMs,
                AvgDurationMs = record.DurationMs,
                LastExecutionTime = record.StartTime,
                SuccessCount = record.Success ? 1 : 0,
                FailureCount = record.Success ? 0 : 1
            },
            (_, existing) =>
            {
                existing.TotalExecutions++;
                existing.TotalDurationMs += record.DurationMs;
                existing.MinDurationMs = Math.Min(existing.MinDurationMs, record.DurationMs);
                existing.MaxDurationMs = Math.Max(existing.MaxDurationMs, record.DurationMs);
                existing.AvgDurationMs = existing.TotalDurationMs / existing.TotalExecutions;
                existing.LastExecutionTime = record.StartTime;
                if (record.Success)
                    existing.SuccessCount++;
                else
                    existing.FailureCount++;
                return existing;
            });

        // Add to execution history
        _executionHistory.Enqueue(record);

        // Trim history if too large
        while (_executionHistory.Count > MaxHistorySize)
        {
            _executionHistory.TryDequeue(out _);
        }
    }

    private class ExecutionTracker : IDisposable
    {
        private readonly string _key;
        private readonly ExecutionRecord _record;
        private readonly Stopwatch _stopwatch;

        public ExecutionTracker(string key, ExecutionRecord record)
        {
            _key = key;
            _record = record;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _record.EndTime = DateTime.UtcNow;
            _record.DurationMs = _stopwatch.Elapsed.TotalMilliseconds;
            _record.Success = true; // Assume success unless exception thrown

            RecordExecution(_key, _record);
        }
    }
}

/// <summary>
/// Metrics for a specific method
/// </summary>
public class MethodMetrics
{
    public string MethodName { get; set; } = string.Empty;
    public long TotalExecutions { get; set; }
    public double TotalDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double AvgDurationMs { get; set; }
    public DateTime LastExecutionTime { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }

    public double SuccessRate => TotalExecutions > 0
        ? (double)SuccessCount / TotalExecutions * 100
        : 0;
}

/// <summary>
/// Record of a single method execution
/// </summary>
public class ExecutionRecord
{
    public string MethodName { get; set; } = string.Empty;
    public string? Workflow { get; set; }
    public string? Step { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationMs { get; set; }
    public int ThreadId { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
