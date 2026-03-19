using DeepResearchAgent;
using DeepResearchAgent.Configuration;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Caching;
using DeepResearchAgent.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("=== Deep Research Agent - C# Implementation ===");
Console.WriteLine("Multi-agent research system with modern workflow architecture");
Console.WriteLine("Powered by Microsoft Agent-Lightning\n");

// Get configuration values
var configValues = ServiceProviderConfiguration.GetConfigurationValues();
var (
    configuration,
    ollamaBaseUrl,
    ollamaDefaultModel,
    searxngBaseUrl,
    crawl4aiBaseUrl,
    lightningServerUrl,
    rmptConfig,
    vectorDbEnabled,
    qdrantBaseUrl,
    qdrantCollectionName,
    qdrantVectorDimension,
    embeddingModel,
    embeddingApiUrl
) = configValues;

// Print configuration summary
PrintConfigurationSummary(
    ollamaBaseUrl,
    searxngBaseUrl,
    crawl4aiBaseUrl,
    lightningServerUrl,
    rmptConfig,
    vectorDbEnabled,
    qdrantBaseUrl,
    embeddingModel);

// Build service provider
var serviceProvider = ServiceProviderConfiguration.BuildServiceProvider();

// Initialize ActivityScope with observability configuration (required for distributed tracing to work)
var observabilityConfig = new ObservabilityConfiguration
{
    EnableTracing = true,
    TraceSamplingRate = 1.0,  // 100% sampling for full visibility
    EnableMetrics = true,
    UseAsyncMetrics = false
};
ActivityScope.Configure(observabilityConfig);

// Start all hosted services (including MetricsHostedService)
var hostedServices = serviceProvider.GetServices<IHostedService>();
var cancellationTokenSource = new CancellationTokenSource();

foreach (var hostedService in hostedServices)
{
    await hostedService.StartAsync(cancellationTokenSource.Token);
}

Console.WriteLine("\n✓ Observability services started");
Console.WriteLine("  • Metrics endpoint: http://localhost:5000/metrics/");
Console.WriteLine("  • Distributed tracing enabled: OTLP → http://localhost:4317 (Jaeger)");
Console.WriteLine("  • Trace sampling: 100% (all requests traced)");
Console.WriteLine("  • Note: Traces appear in Jaeger after StreamStateAsync is called\n");

// Run console host
var consoleHost = new ConsoleHost(
    serviceProvider,
    ollamaBaseUrl,
    searxngBaseUrl,
    crawl4aiBaseUrl,
    lightningServerUrl,
    serviceProvider.GetRequiredService<LlmResponseCache>());

try
{
    await consoleHost.RunAsync();
}
finally
{
    // Force flush OpenTelemetry traces/metrics before exit to ensure delivery to Jaeger
    Console.WriteLine("\nFlushing observability data...");
    TelemetryExtensions.ForceFlush();

    // Stop hosted services on exit
    Console.WriteLine("Stopping services...");
    foreach (var hostedService in hostedServices)
    {
        await hostedService.StopAsync(cancellationTokenSource.Token);
    }
}

// Helper method to print configuration summary
static void PrintConfigurationSummary(
    string ollamaBaseUrl,
    string searxngBaseUrl,
    string crawl4aiBaseUrl,
    string lightningServerUrl,
    LightningRMPTConfig rmptConfig,
    bool vectorDbEnabled,
    string qdrantBaseUrl,
    string embeddingModel)
{
    Console.WriteLine($"✓ Ollama connection configured ({ollamaBaseUrl})");
    Console.WriteLine($"✓ Web search + scraping configured (SearXNG: {searxngBaseUrl}, Crawl4AI: {crawl4aiBaseUrl})");
    Console.WriteLine("✓ Knowledge persistence configured (LightningStore)");
    if (vectorDbEnabled)
    {
        Console.WriteLine($"✓ Vector database configured (Qdrant: {qdrantBaseUrl})");
        Console.WriteLine($"✓ Embedding service configured ({embeddingModel})");
    }
    else
    {
        Console.WriteLine("ℹ Vector database disabled (configure VectorDatabase:Enabled to enable)");
    }
    Console.WriteLine($"✓ Agent-Lightning integration configured ({lightningServerUrl})");

    // Print RMPT configuration - Review the Roadmap for the latest RMPT features and configuration options
    //Console.WriteLine($"✓ RMPT (Resource Management Performance Tuning) enabled - Strategy: {rmptConfig.Strategy}");
    //Console.WriteLine($"✓ RMPT Resource Limits - MaxConcurrent: {rmptConfig.ResourceLimits.MaxConcurrentTasks}, Timeout: {rmptConfig.ResourceLimits.TaskTimeoutSeconds}s");
    //if (rmptConfig.AutoScaling.Enabled)
    //{
    //    Console.WriteLine($"✓ RMPT Auto-Scaling enabled - {rmptConfig.AutoScaling.MinInstances}-{rmptConfig.AutoScaling.MaxInstances} instances");
    //}
    //Console.WriteLine("✓ RLCS (Reasoning Layer Confidence Scoring) enabled\n");
}