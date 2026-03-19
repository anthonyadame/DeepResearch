using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DeepResearchAgent.Observability;
using DeepResearchAgent.Workflows;
using System.Diagnostics;

namespace DeepResearchAgent.Tests.Benchmarks;

/// <summary>
/// Baseline performance benchmarks for workflow execution.
/// Measures the impact of observability instrumentation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class WorkflowPerformanceBenchmark
{
    private const int SimulatedLlmDelayMs = 100; // Simulate LLM call

    [GlobalSetup]
    public void Setup()
    {
        // Configure observability for different scenarios
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            EnableMetrics = true,
            EnableDetailedTracing = true,
            TraceSamplingRate = 1.0,
            SlowOperationThresholdMs = 0,
            UseAsyncMetrics = false
        });
    }

    [Benchmark(Baseline = true)]
    public async Task NoTelemetry_SimulatedWorkflowStep()
    {
        // Simulate a workflow step without any telemetry
        await Task.Delay(SimulatedLlmDelayMs);
    }

    [Benchmark]
    public async Task WithActivity_SimulatedWorkflowStep()
    {
        // Simulate a workflow step with Activity tracing
        using var activity = ActivityScope.Start("TestStep", ActivityKind.Internal);
        activity.AddTag("step.number", 1);
        activity.AddTag("step.name", "Test");

        await Task.Delay(SimulatedLlmDelayMs);

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    [Benchmark]
    public async Task WithActivityAndMetrics_SimulatedWorkflowStep()
    {
        // Simulate a workflow step with full telemetry
        using var activity = ActivityScope.Start("TestStep", ActivityKind.Internal);
        activity.AddTag("step.number", 1);

        var sw = Stopwatch.StartNew();
        await Task.Delay(SimulatedLlmDelayMs);
        sw.Stop();

        // Record metrics synchronously
        DiagnosticConfig.WorkflowStepDuration.Record(
            sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("workflow", "Test"),
            new KeyValuePair<string, object?>("step", "test"));

        DiagnosticConfig.WorkflowStepsCounter.Add(1,
            new KeyValuePair<string, object?>("workflow", "Test"),
            new KeyValuePair<string, object?>("step", "test"),
            new KeyValuePair<string, object?>("status", "completed"));

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    [Benchmark]
    public async Task WithSampling_SimulatedWorkflowStep()
    {
        // Reconfigure for 10% sampling
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            TraceSamplingRate = 0.1 // 10% sampling
        });

        using var activity = ActivityScope.Start("TestStep", ActivityKind.Internal);
        activity.AddTag("step.number", 1);

        await Task.Delay(SimulatedLlmDelayMs);

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    [Benchmark]
    public async Task TelemetryDisabled_SimulatedWorkflowStep()
    {
        // Reconfigure to disable all telemetry
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = false,
            EnableMetrics = false
        });

        using var activity = ActivityScope.Start("TestStep", ActivityKind.Internal);

        await Task.Delay(SimulatedLlmDelayMs);

        activity.SetStatus(ActivityStatusCode.Ok);
    }
}
