using DeepResearchAgent;
using DeepResearchAgent.Configuration;
using DeepResearchAgent.Services;

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

// Run console host
var consoleHost = new ConsoleHost(
    serviceProvider,
    ollamaBaseUrl,
    searxngBaseUrl,
    crawl4aiBaseUrl,
    lightningServerUrl);

await consoleHost.RunAsync();

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