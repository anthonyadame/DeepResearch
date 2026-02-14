using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.VectorDatabase;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Examples;

/// <summary>
/// Example demonstrating vector database integration with ResearcherWorkflow.
/// Shows how to use Qdrant for semantic search and fact indexing.
/// </summary>
public class VectorDatabaseExample
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Vector Database Integration Example ===\n");

        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup services with vector database support
        var services = new ServiceCollection();
        
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Configuration from appsettings.json
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var ollamaDefaultModel = configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b";
        var searxngBaseUrl = configuration["SearXNG:BaseUrl"] ?? "http://localhost:8080";
        var crawl4aiBaseUrl = configuration["Crawl4AI:BaseUrl"] ?? "http://localhost:11235";
        var lightningServerUrl = configuration["Lightning:ServerUrl"] ?? "http://localhost:8090";

        var vectorDbEnabled = configuration.GetValue("VectorDatabase:Enabled", true);
        var qdrantBaseUrl = configuration["VectorDatabase:Qdrant:BaseUrl"] ?? "http://localhost:6333";
        var qdrantCollectionName = configuration["VectorDatabase:Qdrant:CollectionName"] ?? "research-example";
        var embeddingModel = configuration["VectorDatabase:EmbeddingModel"] ?? "nomic-embed-text";

        // Register core services
        services.AddMemoryCache();
        services.AddSingleton(new OllamaService(baseUrl: ollamaBaseUrl, defaultModel: ollamaDefaultModel));
        services.AddSingleton(new HttpClient());
        services.AddSingleton(new SearCrawl4AIService(
            new HttpClient(),
            searxngBaseUrl,
            crawl4aiBaseUrl
        ));
        services.AddSingleton(new LightningStore());

        // Register vector database services
        services.AddSingleton<IEmbeddingService>(sp => new OllamaEmbeddingService(
            sp.GetRequiredService<HttpClient>(),
            baseUrl: ollamaBaseUrl,
            model: embeddingModel,
            dimension: 384,
            logger: sp.GetService<Microsoft.Extensions.Logging.ILogger>()
        ));

        if (vectorDbEnabled)
        {
            services.AddSingleton<IVectorDatabaseService>(sp => new QdrantVectorDatabaseService(
                sp.GetRequiredService<HttpClient>(),
                new QdrantConfig
                {
                    BaseUrl = qdrantBaseUrl,
                    CollectionName = qdrantCollectionName,
                    VectorDimension = 384
                },
                sp.GetRequiredService<IEmbeddingService>(),
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger>()
            ));
        }

        // Register Lightning and workflows
        services.AddSingleton<ILightningStateService, LightningStateService>();
        services.AddSingleton<ResearcherWorkflow>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<VectorDatabaseExample>>();

        try
        {
            logger.LogInformation("Vector Database Integration Example Started");

            // Get services
            var vectorDb = serviceProvider.GetService<IVectorDatabaseService>();
            var embeddingService = serviceProvider.GetRequiredService<IEmbeddingService>();
            var researcherWorkflow = serviceProvider.GetRequiredService<ResearcherWorkflow>();

            if (vectorDb == null)
            {
                logger.LogWarning("Vector database not configured. Enable it in appsettings.json");
                return;
            }

            // Example 1: Health check
            logger.LogInformation("\n--- Example 1: Vector Database Health Check ---");
            var isHealthy = await vectorDb.HealthCheckAsync();
            logger.LogInformation($"Vector Database Health: {(isHealthy ? "✓ Healthy" : "✗ Unhealthy")}");

            if (!isHealthy)
            {
                logger.LogError("Vector database is not accessible. Please ensure Qdrant is running at {qdrantUrl}", qdrantBaseUrl);
                return;
            }

            // Example 2: Clear previous collection
            logger.LogInformation("\n--- Example 2: Clear Previous Collection ---");
            try
            {
                await vectorDb.ClearAsync();
                logger.LogInformation("Collection cleared successfully");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not clear collection (may not exist yet)");
            }

            // Example 3: Embed and index sample facts
            logger.LogInformation("\n--- Example 3: Embed and Index Sample Facts ---");
            var sampleFacts = new[]
            {
                "Machine learning is a subset of artificial intelligence",
                "Deep learning uses neural networks with multiple layers",
                "Natural language processing helps computers understand human language",
                "Transformers revolutionized the field of NLP",
                "GPT models are large language models trained on text data"
            };

            var embeddings = await embeddingService.EmbedBatchAsync(sampleFacts);
            
            for (int i = 0; i < sampleFacts.Length; i++)
            {
                var factId = $"fact_{i}";
                var metadata = new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["category"] = "AI/ML"
                };

                await vectorDb.UpsertAsync(factId, sampleFacts[i], embeddings[i], metadata);
                logger.LogInformation($"  ✓ Indexed: {sampleFacts[i].Substring(0, Math.Min(50, sampleFacts[i].Length))}...");
            }

            // Example 4: Search by vector
            logger.LogInformation("\n--- Example 4: Semantic Search by Vector ---");
            var queryEmbedding = await embeddingService.EmbedAsync("What is artificial intelligence?");
            var searchResults = await vectorDb.SearchAsync(queryEmbedding, topK: 3);

            logger.LogInformation($"Search Results for 'What is artificial intelligence?' (Top {searchResults.Count}):");
            foreach (var result in searchResults)
            {
                logger.LogInformation($"  Score: {result.Score:F3} | {result.Content}");
            }

            // Example 5: Search by content
            logger.LogInformation("\n--- Example 5: Semantic Search by Content ---");
            var contentQuery = "neural networks and deep learning";
            var contentSearchResults = await vectorDb.SearchByContentAsync(contentQuery, topK: 2);

            logger.LogInformation($"Search Results for '{contentQuery}' (Top {contentSearchResults.Count}):");
            foreach (var result in contentSearchResults)
            {
                logger.LogInformation($"  Score: {result.Score:F3} | {result.Content}");
            }

            // Example 6: Get database statistics
            logger.LogInformation("\n--- Example 6: Database Statistics ---");
            var stats = await vectorDb.GetStatsAsync();
            logger.LogInformation($"Total Documents: {stats.DocumentCount}");
            logger.LogInformation($"Total Size: {stats.SizeBytes} bytes");
            logger.LogInformation($"Status: {stats.Status}");

            // Example 7: Research workflow with semantic search
            logger.LogInformation("\n--- Example 7: Research Workflow with Vector DB ---");
            logger.LogInformation("ResearcherWorkflow now automatically indexes facts to vector database");
            logger.LogInformation("Use researcherWorkflow.SearchSimilarFactsAsync() to find related findings");

            // Example 8: Delete a document
            logger.LogInformation("\n--- Example 8: Delete Document ---");
            await vectorDb.DeleteAsync("fact_0");
            logger.LogInformation("Deleted 'fact_0' from vector database");

            var updatedStats = await vectorDb.GetStatsAsync();
            logger.LogInformation($"Documents after deletion: {updatedStats.DocumentCount}");

            logger.LogInformation("\n=== Example Completed Successfully ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Example failed with error");
            throw;
        }
    }
}

/// <summary>
/// Advanced example showing how to implement a custom vector database.
/// </summary>
public class CustomVectorDatabaseExample
{
    /// <summary>
    /// Example of a minimal custom vector database implementation.
    /// This could be extended to support any vector database.
    /// </summary>
    public class InMemoryVectorDatabase : IVectorDatabaseService
    {
        private readonly Dictionary<string, (string content, float[] embedding, Dictionary<string, object>? metadata)> _documents = new();

        public string Name => "InMemory";

        public Task<string> UpsertAsync(
            string id,
            string content,
            float[] embedding,
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            _documents[id] = (content, embedding, metadata);
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            float[] embedding,
            int topK = 5,
            double? scoreThreshold = null,
            CancellationToken cancellationToken = default)
        {
            var results = _documents
                .Select(kvp =>
                {
                    var score = CosineSimilarity(embedding, kvp.Value.embedding);
                    return new VectorSearchResult
                    {
                        Id = kvp.Key,
                        Content = kvp.Value.content,
                        Score = score,
                        Metadata = kvp.Value.metadata
                    };
                })
                .Where(r => (scoreThreshold == null || r.Score >= scoreThreshold))
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();

            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchByContentAsync(
            string content,
            int topK = 5,
            double? scoreThreshold = null,
            CancellationToken cancellationToken = default)
        {
            // In a real implementation, you'd embed the content
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(new List<VectorSearchResult>());
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _documents.Remove(id);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _documents.Clear();
            return Task.CompletedTask;
        }

        public Task<VectorDatabaseStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VectorDatabaseStats
            {
                DocumentCount = _documents.Count,
                IsHealthy = true,
                Status = "Operational"
            });
        }

        public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;
            
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denominator == 0 ? 0 : dotProduct / denominator;
        }
    }
}
