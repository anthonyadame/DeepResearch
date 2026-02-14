using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.VectorDatabase;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Workflows;

/// <summary>
/// Phase 5 Extension: Adds ResearcherAgent integration to ResearcherWorkflow.
/// Provides enhanced research capabilities using complex agent orchestration.
/// </summary>
public static class ResearcherWorkflowExtensions
{
    /// <summary>
    /// Execute research using the Phase 4 ResearcherAgent for complex orchestration.
    /// This delegates to ResearcherAgent instead of using the ReAct loop.
    /// </summary>
    public static async Task<IReadOnlyList<Models.FactState>> ResearchWithAgentAsync(
        this ResearcherWorkflow workflow,
        ResearcherAgent agent,
        string topic,
        string? researchBrief = null,
        int maxIterations = 3,
        float minQualityThreshold = 7.0f,
        CancellationToken cancellationToken = default)
    {
        // Create input for ResearcherAgent
        var agentInput = new ResearchInput
        {
            Topic = topic,
            ResearchBrief = researchBrief ?? topic,
            MaxIterations = maxIterations,
            MinQualityThreshold = minQualityThreshold
        };

        // Execute agent research
        var agentOutput = await agent.ExecuteAsync(agentInput, cancellationToken);

        // Convert agent output to FactState list
        var facts = MapResearchOutputToFactStates(agentOutput);

        return facts;
    }

    /// <summary>
    /// Execute research using ResearcherAgent with full workflow integration.
    /// Includes state management, metrics tracking, and vector database indexing.
    /// </summary>
    public static async Task<IReadOnlyList<Models.FactState>> ResearchWithAgentIntegratedAsync(
        this ResearcherWorkflow workflow,
        ResearcherAgent agent,
        LightningStore store,
        ILightningStateService stateService,
        IVectorDatabaseService? vectorDb,
        IEmbeddingService? embeddingService,
        string topic,
        string? researchBrief = null,
        string? researchId = null,
        Microsoft.Extensions.Logging.ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var localResearchId = researchId ?? Guid.NewGuid().ToString();
        logger?.LogInformation("ResearchWithAgentIntegrated: Starting for topic: {topic}", topic);

        try
        {
            // Initialize research state
            var researchState = new ResearchStateModel
            {
                ResearchId = localResearchId,
                Query = topic,
                Status = ResearchStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };

            await stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);

            // Execute research using ResearcherAgent
            var agentInput = new ResearchInput
            {
                Topic = topic,
                ResearchBrief = researchBrief ?? topic,
                MaxIterations = 3,
                MinQualityThreshold = 7.0f
            };

            var agentOutput = await agent.ExecuteAsync(agentInput, cancellationToken);

            // Convert to FactState
            var facts = MapResearchOutputToFactStates(agentOutput);

            // Persist facts
            if (facts.Any())
            {
                await store.SaveFactsAsync(facts.AsEnumerable(), cancellationToken);
                logger?.LogInformation("Persisted {count} facts from ResearcherAgent", facts.Count);

                // Index to vector database if available
                if (vectorDb != null && embeddingService != null)
                {
                    await IndexFactsToVectorDatabaseAsync(
                        facts, 
                        vectorDb, 
                        embeddingService, 
                        logger, 
                        cancellationToken);
                }
            }

            // Update research state
            researchState.Status = ResearchStatus.Completed;
            researchState.CompletedAt = DateTime.UtcNow;
            await stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);

            logger?.LogInformation("ResearchWithAgentIntegrated: Complete - {count} facts extracted", facts.Count);

            return facts;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "ResearchWithAgentIntegrated failed");
            throw;
        }
    }

    /// <summary>
    /// Map ResearchOutput from ResearcherAgent to list of FactState.
    /// Converts Phase 4 agent output to workflow-compatible fact format.
    /// </summary>
    private static List<Models.FactState> MapResearchOutputToFactStates(ResearchOutput output)
    {
        var factStates = new List<Models.FactState>();

        if (output?.Findings == null)
            return factStates;

        foreach (var finding in output.Findings)
        {
            if (finding?.Facts == null)
                continue;

            foreach (var fact in finding.Facts)
            {
                factStates.Add(new Models.FactState
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = fact.Statement ?? string.Empty,
                    SourceUrl = fact.Source ?? "ResearcherAgent",
                    Confidence = fact.Confidence,
                    ExtractedAt = DateTime.UtcNow,
                    IsDisputed = false,
                    Tags = new List<string> 
                    { 
                        fact.Category ?? "research",
                        "phase5",
                        "researcher-agent"
                    }
                });
            }
        }

        return factStates;
    }

    /// <summary>
    /// Index facts to vector database for semantic search.
    /// Helper method for vector database integration.
    /// </summary>
    private static async Task IndexFactsToVectorDatabaseAsync(
        List<Models.FactState> facts,
        IVectorDatabaseService vectorDb,
        IEmbeddingService embeddingService,
        Microsoft.Extensions.Logging.ILogger? logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger?.LogDebug("Indexing {count} facts to vector database", facts.Count);

            // Generate embeddings for each fact's content
            var embeddings = await embeddingService.EmbedBatchAsync(
                facts.Select(f => f.Content).ToList(),
                cancellationToken);

            // Index each fact with its embedding
            int indexedCount = 0;
            for (int i = 0; i < facts.Count && i < embeddings.Count; i++)
            {
                try
                {
                    var metadata = new Dictionary<string, object>
                    {
                        ["factId"] = facts[i].Id,
                        ["sourceUrl"] = facts[i].SourceUrl,
                        ["confidence"] = facts[i].Confidence,
                        ["extractedAt"] = facts[i].ExtractedAt.ToString("O"),
                        ["tags"] = string.Join(",", facts[i].Tags)
                    };

                    await vectorDb.UpsertAsync(
                        facts[i].Id,
                        facts[i].Content,
                        embeddings[i],
                        metadata,
                        cancellationToken);

                    indexedCount++;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to index fact {id} to vector database", facts[i].Id);
                }
            }

            logger?.LogInformation("Successfully indexed {count}/{total} facts to vector database",
                indexedCount, facts.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to index facts to vector database");
        }
    }

    /// <summary>
    /// Get research quality metrics from ResearchOutput.
    /// Provides quality score and statistics from agent execution.
    /// </summary>
    public static (float averageQuality, int totalFacts, int iterations) GetResearchMetrics(
        this ResearchOutput output)
    {
        if (output == null)
            return (0f, 0, 0);

        var totalFacts = output.Findings?.Sum(f => f.Facts?.Count ?? 0) ?? 0;
        var averageQuality = output.AverageQuality;
        var iterations = output.IterationsUsed;

        return (averageQuality, totalFacts, iterations);
    }

    /// <summary>
    /// Create FactState from ExtractedFact.
    /// Helper for individual fact conversion.
    /// </summary>
    public static Models.FactState ToFactState(this ExtractedFact fact, string? category = null)
    {
        return new Models.FactState
        {
            Id = Guid.NewGuid().ToString(),
            Content = fact.Statement ?? string.Empty,
            SourceUrl = fact.Source ?? "Unknown",
            Confidence = fact.Confidence,
            ExtractedAt = DateTime.UtcNow,
            IsDisputed = false,
            Tags = new List<string> { category ?? fact.Category ?? "research" }
        };
    }

    /// <summary>
    /// Convert list of ExtractedFact to list of FactState.
    /// Batch conversion helper.
    /// </summary>
    public static List<Models.FactState> ToFactStates(this IEnumerable<ExtractedFact> facts, string? category = null)
    {
        return facts.Select(f => f.ToFactState(category)).ToList();
    }
}
