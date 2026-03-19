using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Asynchronous metrics collector that buffers metrics in a background queue
/// to minimize performance impact on the main workflow execution.
/// </summary>
public class AsyncMetricsCollector : BackgroundService
{
    private readonly BlockingCollection<MetricEntry> _metricsQueue;
    private readonly ObservabilityConfiguration _config;
    private readonly ILogger<AsyncMetricsCollector> _logger;

    // Health monitoring
    private long _totalEnqueued;
    private long _totalProcessed;
    private long _totalDropped;
    private DateTime _lastProcessedTime;

    public AsyncMetricsCollector(
        ObservabilityConfiguration config,
        ILogger<AsyncMetricsCollector> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _metricsQueue = new BlockingCollection<MetricEntry>(
            new ConcurrentQueue<MetricEntry>(),
            _config.AsyncMetricsQueueSize);

        _lastProcessedTime = DateTime.UtcNow;

        _logger.LogInformation(
            "AsyncMetricsCollector initialized with queue size: {QueueSize}",
            _config.AsyncMetricsQueueSize);
    }

    /// <summary>
    /// Health statistics for monitoring
    /// </summary>
    public MetricsQueueHealth GetHealth()
    {
        return new MetricsQueueHealth
        {
            QueueDepth = _metricsQueue.Count,
            QueueCapacity = _config.AsyncMetricsQueueSize,
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalProcessed = Interlocked.Read(ref _totalProcessed),
            TotalDropped = Interlocked.Read(ref _totalDropped),
            LastProcessedTime = _lastProcessedTime,
            IsHealthy = _metricsQueue.Count < _config.AsyncMetricsQueueSize * 0.8
        };
    }

    /// <summary>
    /// Enqueue a counter increment operation
    /// </summary>
    public void RecordCounter<T>(Counter<T> counter, T value, params KeyValuePair<string, object?>[] tags)
        where T : struct
    {
        if (!_config.UseAsyncMetrics || !_config.EnableMetrics)
        {
            // Fallback to synchronous recording
            counter.Add(value, tags);
            return;
        }

        var entry = new MetricEntry
        {
            Type = MetricType.Counter,
            Counter = counter as Counter<long>,
            CounterValue = Convert.ToInt64(value),
            Tags = tags,
            Timestamp = DateTime.UtcNow
        };

        EnqueueMetric(entry);
    }

    /// <summary>
    /// Enqueue a histogram recording operation
    /// </summary>
    public void RecordHistogram(Histogram<double> histogram, double value, params KeyValuePair<string, object?>[] tags)
    {
        if (!_config.UseAsyncMetrics || !_config.EnableMetrics)
        {
            // Fallback to synchronous recording
            histogram.Record(value, tags);
            return;
        }

        var entry = new MetricEntry
        {
            Type = MetricType.Histogram,
            Histogram = histogram,
            HistogramValue = value,
            Tags = tags,
            Timestamp = DateTime.UtcNow
        };

        EnqueueMetric(entry);
    }

    private void EnqueueMetric(MetricEntry entry)
    {
        try
        {
            if (_metricsQueue.TryAdd(entry, millisecondsTimeout: 0))
            {
                Interlocked.Increment(ref _totalEnqueued);
            }
            else
            {
                // Queue is full, drop the metric
                Interlocked.Increment(ref _totalDropped);

                // Log warning if drop rate exceeds threshold (e.g., every 100 drops)
                var dropped = Interlocked.Read(ref _totalDropped);
                if (dropped % 100 == 0)
                {
                    _logger.LogWarning(
                        "Metrics queue full, dropped {DroppedCount} metrics so far. Queue depth: {QueueDepth}/{QueueCapacity}",
                        dropped,
                        _metricsQueue.Count,
                        _config.AsyncMetricsQueueSize);
                }
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalDropped);
            _logger.LogError(ex, "Error enqueueing metric");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AsyncMetricsCollector background service started");

        try
        {
            foreach (var entry in _metricsQueue.GetConsumingEnumerable(stoppingToken))
            {
                try
                {
                    ProcessMetric(entry);
                    Interlocked.Increment(ref _totalProcessed);
                    _lastProcessedTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing metric of type {MetricType}", entry.Type);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            _logger.LogInformation("AsyncMetricsCollector background service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AsyncMetricsCollector background loop");
        }

        await Task.CompletedTask;
    }

    private void ProcessMetric(MetricEntry entry)
    {
        switch (entry.Type)
        {
            case MetricType.Counter:
                entry.Counter?.Add(entry.CounterValue, entry.Tags ?? Array.Empty<KeyValuePair<string, object?>>());
                break;

            case MetricType.Histogram:
                entry.Histogram?.Record(entry.HistogramValue, entry.Tags ?? Array.Empty<KeyValuePair<string, object?>>());
                break;

            default:
                _logger.LogWarning("Unknown metric type: {MetricType}", entry.Type);
                break;
        }
    }

    public override void Dispose()
    {
        var health = GetHealth();
        _logger.LogInformation(
            "AsyncMetricsCollector disposing. Final stats - Enqueued: {Enqueued}, Processed: {Processed}, Dropped: {Dropped}, Queue Depth: {QueueDepth}",
            health.TotalEnqueued,
            health.TotalProcessed,
            health.TotalDropped,
            health.QueueDepth);

        _metricsQueue?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Represents a queued metric entry
/// </summary>
internal class MetricEntry
{
    public MetricType Type { get; set; }
    public Counter<long>? Counter { get; set; }
    public long CounterValue { get; set; }
    public Histogram<double>? Histogram { get; set; }
    public double HistogramValue { get; set; }
    public KeyValuePair<string, object?>[]? Tags { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Metric type enumeration
/// </summary>
internal enum MetricType
{
    Counter,
    Histogram
}

/// <summary>
/// Health status of the metrics queue
/// </summary>
public class MetricsQueueHealth
{
    public int QueueDepth { get; set; }
    public int QueueCapacity { get; set; }
    public long TotalEnqueued { get; set; }
    public long TotalProcessed { get; set; }
    public long TotalDropped { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public bool IsHealthy { get; set; }

    public double UtilizationPercent => QueueCapacity > 0 
        ? (double)QueueDepth / QueueCapacity * 100 
        : 0;

    public double DropRatePercent => TotalEnqueued > 0 
        ? (double)TotalDropped / TotalEnqueued * 100 
        : 0;
}
