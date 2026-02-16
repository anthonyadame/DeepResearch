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
    apoConfig,
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
    apoConfig,
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
    LightningAPOConfig apoConfig,
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

    // Print APO configuration - Review the Roadmap for the latest APO features and configuration options
    //Console.WriteLine($"✓ APO (Automatic Performance Optimization) enabled - Strategy: {apoConfig.Strategy}");
    //Console.WriteLine($"✓ APO Resource Limits - MaxConcurrent: {apoConfig.ResourceLimits.MaxConcurrentTasks}, Timeout: {apoConfig.ResourceLimits.TaskTimeoutSeconds}s");
    //if (apoConfig.AutoScaling.Enabled)
    //{
    //    Console.WriteLine($"✓ APO Auto-Scaling enabled - {apoConfig.AutoScaling.MinInstances}-{apoConfig.AutoScaling.MaxInstances} instances");
    //}
    //Console.WriteLine("✓ VERL (Verification and Reasoning Layer) enabled\n");
}