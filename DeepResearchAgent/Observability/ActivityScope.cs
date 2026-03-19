using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Provides automatic activity (span) creation and disposal for method tracing.
/// Supports configuration-based control of observability features.
/// Usage: using var scope = ActivityScope.Start();
/// </summary>
public sealed class ActivityScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly Stopwatch? _stopwatch;
    private readonly string _operationName;
    private bool _disposed;

    // Static configuration (set once at startup)
    private static ObservabilityConfiguration _config = new();

    /// <summary>
    /// Configure observability settings (call once at startup)
    /// </summary>
    public static void Configure(ObservabilityConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate(); // Validate configuration on setup
    }

    /// <summary>
    /// Get current configuration (for testing/diagnostics)
    /// </summary>
    public static ObservabilityConfiguration GetConfiguration() => _config;

    private ActivityScope(Activity? activity, string operationName)
    {
        _activity = activity;
        _operationName = operationName;
        _stopwatch = activity != null ? Stopwatch.StartNew() : null;
    }

    /// <summary>
    /// Start a new activity scope with configuration-aware behavior.
    /// Automatically captures method name and creates a distributed trace span.
    /// </summary>
    /// <param name="operationName">Optional operation name (defaults to caller member name)</param>
    /// <param name="kind">Activity kind (defaults to Internal)</param>
    /// <param name="tags">Optional tags to add to the activity</param>
    /// <param name="memberName">Auto-populated with caller member name</param>
    /// <returns>Disposable activity scope</returns>
    public static ActivityScope Start(
        string? operationName = null,
        ActivityKind kind = ActivityKind.Internal,
        IDictionary<string, object?>? tags = null,
        [CallerMemberName] string memberName = "")
    {
        var activityName = operationName ?? memberName;

        // Fast path: If tracing disabled, return no-op scope
        if (!_config.EnableTracing)
        {
            return new ActivityScope(null, activityName);
        }

        // Sampling: Skip trace based on configured rate
        if (_config.TraceSamplingRate < 1.0)
        {
            if (Random.Shared.NextDouble() > _config.TraceSamplingRate)
            {
                return new ActivityScope(null, activityName);
            }
        }

        // Create activity
        var activity = DiagnosticConfig.ActivitySource.StartActivity(activityName, kind);

        if (activity != null)
        {
            // Add standard tags
            activity.SetTag("code.function", activityName);
            activity.SetTag("thread.id", Environment.CurrentManagedThreadId);

            // Add custom tags if provided
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }
        }

        return new ActivityScope(activity, activityName);
    }

    /// <summary>
    /// Add a tag to the current activity
    /// </summary>
    public ActivityScope AddTag(string key, object? value)
    {
        _activity?.SetTag(key, value);
        return this;
    }

    /// <summary>
    /// Add an event to the current activity (respects EnableActivityEvents config)
    /// </summary>
    public ActivityScope AddEvent(string name, IDictionary<string, object?>? tags = null)
    {
        // Check configuration: skip if events are disabled
        if (!_config.EnableActivityEvents || _activity == null)
        {
            return this;
        }

        if (tags != null && tags.Count > 0)
        {
            var tagsCollection = new ActivityTagsCollection();
            foreach (var tag in tags)
            {
                tagsCollection.Add(tag.Key, tag.Value);
            }
            _activity.AddEvent(new ActivityEvent(name, tags: tagsCollection));
        }
        else
        {
            _activity.AddEvent(new ActivityEvent(name));
        }

        return this;
    }

    /// <summary>
    /// Record an exception in the current activity (respects EnableExceptionRecording config)
    /// </summary>
    public ActivityScope RecordException(Exception exception)
    {
        // Check configuration: skip if exception recording is disabled
        if (!_config.EnableExceptionRecording || _activity == null)
        {
            return this;
        }

        _activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        _activity.RecordException(exception);
        return this;
    }

    /// <summary>
    /// Set the status of the activity
    /// </summary>
    public ActivityScope SetStatus(ActivityStatusCode status, string? description = null)
    {
        _activity?.SetStatus(status, description);
        return this;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _stopwatch?.Stop();

        // Check if we should record this activity based on duration threshold
        if (_activity != null && _stopwatch != null)
        {
            var durationMs = _stopwatch.Elapsed.TotalMilliseconds;

            // Only record if duration exceeds threshold (0 = record all)
            if (_config.SlowOperationThresholdMs == 0 || durationMs >= _config.SlowOperationThresholdMs)
            {
                _activity.SetTag("duration.ms", durationMs);

                // Add slow operation marker if threshold was exceeded
                if (_config.SlowOperationThresholdMs > 0 && durationMs >= _config.SlowOperationThresholdMs)
                {
                    _activity.SetTag("slow_operation", true);
                    _activity.SetTag("threshold.ms", _config.SlowOperationThresholdMs);
                }
            }
        }

        _activity?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Extension methods for Activity
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Record an exception on an activity with standard tags
    /// </summary>
    public static Activity RecordException(this Activity activity, Exception exception)
    {
        var tags = new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.StackTrace }
        };

        if (exception.InnerException != null)
        {
            tags.Add("exception.inner", exception.InnerException.Message);
        }

        activity.AddEvent(new ActivityEvent("exception", tags: tags));
        return activity;
    }
}
