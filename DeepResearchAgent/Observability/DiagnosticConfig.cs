using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Central configuration for OpenTelemetry diagnostics.
/// Provides ActivitySource for distributed tracing and Meter for metrics.
/// </summary>
public static class DiagnosticConfig
{
    /// <summary>
    /// Service name for OpenTelemetry
    /// </summary>
    public const string ServiceName = "DeepResearchAgent";

    /// <summary>
    /// Service version
    /// </summary>
    public const string ServiceVersion = "0.6.5";

    /// <summary>
    /// ActivitySource for distributed tracing
    /// Use this to create Activities (spans) for tracing execution flow
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    /// <summary>
    /// Meter for metrics collection
    /// Use this to create counters, histograms, and gauges
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Workflow Metrics
    public static readonly Counter<long> WorkflowStepsCounter = Meter.CreateCounter<long>(
        "deepresearch.workflow.steps.total",
        description: "Total number of workflow steps executed");

    public static readonly Histogram<double> WorkflowStepDuration = Meter.CreateHistogram<double>(
        "deepresearch.workflow.step.duration",
        unit: "ms",
        description: "Duration of workflow steps in milliseconds");

    public static readonly Histogram<double> WorkflowTotalDuration = Meter.CreateHistogram<double>(
        "deepresearch.workflow.total.duration",
        unit: "ms",
        description: "Total duration of complete workflow execution in milliseconds");

    public static readonly Counter<long> WorkflowErrors = Meter.CreateCounter<long>(
        "deepresearch.workflow.errors.total",
        description: "Total number of workflow errors");

    // Track active workflows (requires callback function to be set)
    private static int _activeWorkflowCount = 0;

    public static readonly ObservableGauge<int> ActiveWorkflows = Meter.CreateObservableGauge<int>(
        "deepresearch.workflow.active",
        observeValue: () => _activeWorkflowCount,
        description: "Number of currently active workflows");

    /// <summary>
    /// Increment active workflow count (call when workflow starts)
    /// </summary>
    public static void IncrementActiveWorkflows() => Interlocked.Increment(ref _activeWorkflowCount);

    /// <summary>
    /// Decrement active workflow count (call when workflow completes)
    /// </summary>
    public static void DecrementActiveWorkflows() => Interlocked.Decrement(ref _activeWorkflowCount);

    // LLM Metrics
    public static readonly Counter<long> LlmRequestsCounter = Meter.CreateCounter<long>(
        "deepresearch.llm.requests.total",
        description: "Total number of LLM requests");

    public static readonly Histogram<double> LlmRequestDuration = Meter.CreateHistogram<double>(
        "deepresearch.llm.request.duration",
        unit: "ms",
        description: "Duration of LLM requests in milliseconds");

    public static readonly Counter<long> LlmTokensUsed = Meter.CreateCounter<long>(
        "deepresearch.llm.tokens.used.total",
        description: "Total number of tokens used (prompt + completion)");

    public static readonly Counter<long> LlmTokensPrompt = Meter.CreateCounter<long>(
        "deepresearch.llm.tokens.prompt.total",
        description: "Total number of prompt tokens used");

    public static readonly Counter<long> LlmTokensCompletion = Meter.CreateCounter<long>(
        "deepresearch.llm.tokens.completion.total",
        description: "Total number of completion tokens used");

    public static readonly Counter<long> LlmErrors = Meter.CreateCounter<long>(
        "deepresearch.llm.errors.total",
        description: "Total number of LLM errors");

    // LLM Tier-Specific Metrics (Phase B1: Tiered Model Selection)
    public static readonly Counter<long> LlmRequestsByTier = Meter.CreateCounter<long>(
        "deepresearch.llm.requests.by_tier.total",
        description: "Total number of LLM requests by model tier (Fast/Balanced/Power)");

    public static readonly Histogram<double> LlmRequestDurationByTier = Meter.CreateHistogram<double>(
        "deepresearch.llm.request.duration.by_tier",
        unit: "ms",
        description: "Duration of LLM requests by model tier in milliseconds");

    public static readonly Counter<long> LlmTokensByTier = Meter.CreateCounter<long>(
        "deepresearch.llm.tokens.by_tier.total",
        description: "Total tokens used by model tier");

    // Tool Invocation Metrics
    public static readonly Counter<long> ToolInvocationsCounter = Meter.CreateCounter<long>(
        "deepresearch.tools.invocations.total",
        description: "Total number of tool invocations");

    public static readonly Histogram<double> ToolInvocationDuration = Meter.CreateHistogram<double>(
        "deepresearch.tools.invocation.duration",
        unit: "ms",
        description: "Duration of tool invocations in milliseconds");

    public static readonly Counter<long> ToolErrors = Meter.CreateCounter<long>(
        "deepresearch.tools.errors.total",
        description: "Total number of tool invocation errors");

    // Parallel Tool Execution Metrics (Phase B2: Parallel Tool Execution)
    public static readonly Histogram<double> ParallelExecutionDuration = Meter.CreateHistogram<double>(
        "deepresearch.tools.parallel.execution.duration",
        unit: "ms",
        description: "Duration of parallel tool execution (summarize + extract) in milliseconds");

    public static readonly Counter<long> ParallelBatchExecutions = Meter.CreateCounter<long>(
        "deepresearch.tools.parallel.batches.total",
        description: "Total number of parallel batch executions");

    public static readonly Histogram<double> ParallelBatchDuration = Meter.CreateHistogram<double>(
        "deepresearch.tools.parallel.batch.duration",
        unit: "ms",
        description: "Duration of parallel batch processing in milliseconds");

    // LLM Response Cache Metrics (Phase B3: LLM Response Caching)
    public static readonly Counter<long> LlmCacheHits = Meter.CreateCounter<long>(
        "deepresearch.llm.cache.hits.total",
        description: "Total number of LLM cache hits");

    public static readonly Counter<long> LlmCacheMisses = Meter.CreateCounter<long>(
        "deepresearch.llm.cache.misses.total",
        description: "Total number of LLM cache misses");

    public static readonly Counter<long> LlmCacheEntries = Meter.CreateCounter<long>(
        "deepresearch.llm.cache.entries.total",
        description: "Total number of LLM cache entries stored");

    // Search Metrics
    public static readonly Counter<long> SearchRequestsCounter = Meter.CreateCounter<long>(
        "deepresearch.search.requests.total",
        description: "Total number of search requests");

    public static readonly Histogram<double> SearchRequestDuration = Meter.CreateHistogram<double>(
        "deepresearch.search.request.duration",
        unit: "ms",
        description: "Duration of search requests in milliseconds");

    // State Management Metrics
    public static readonly Counter<long> StateOperationsCounter = Meter.CreateCounter<long>(
        "deepresearch.state.operations.total",
        description: "Total number of state operations");

    public static readonly Counter<long> StateCacheHits = Meter.CreateCounter<long>(
        "deepresearch.state.cache.hits.total",
        description: "Total number of state cache hits");

    public static readonly Counter<long> StateCacheMisses = Meter.CreateCounter<long>(
        "deepresearch.state.cache.misses.total",
        description: "Total number of state cache misses");
}
