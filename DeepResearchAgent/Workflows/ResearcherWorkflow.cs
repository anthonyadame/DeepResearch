using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.VectorDatabase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using OllamaSharp.Models.Chat;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DeepResearchAgent.Workflows;

/// <summary>
/// Researcher Workflow: Conducts focused research using ReAct loop (LLM → Tools → Loop).
/// 
/// Core Functions:
/// 1. LLM Brain: Decides what to search for
/// 2. Tool Execution: Search, scrape, think
/// 3. Research Compression: Synthesize findings
/// 4. Fact Persistence: Store in knowledge base
/// 5. Vector Database Indexing: Store embeddings for semantic search
/// 
/// Maps to Python lines 390-470 (Re Act loop pattern)
/// </summary>
public class ResearcherWorkflow
{
    private readonly ILightningStateService _stateService;
    private readonly SearCrawl4AIService _searchService;
    private readonly OllamaService _llmService;
    private readonly LightningStore _store;
    private readonly IVectorDatabaseService? _vectorDb;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ILogger<ResearcherWorkflow>? _logger;
    private readonly IAgentLightningService? _lightningService;
    private readonly LightningAPOConfig? _apoConfig;

    public ResearcherWorkflow(
        ILightningStateService stateService,
        SearCrawl4AIService searchService,
        OllamaService llmService,
        LightningStore store,
        IVectorDatabaseService? vectorDb = null,
        IEmbeddingService? embeddingService = null,
        ILogger<ResearcherWorkflow>? logger = null,
        IAgentLightningService? lightningService = null,
        LightningAPOConfig? apoConfig = null)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _vectorDb = vectorDb;
        _embeddingService = embeddingService;
        _logger = logger;
        _lightningService = lightningService;
        _apoConfig = apoConfig;
    }

    /// <summary>
    /// Execute focused research on a topic using ReAct loop.
    /// Returns list of extracted facts.
    /// </summary>
    public async Task<IReadOnlyList<Models.FactState>> ResearchAsync(
        string topic,
        string? researchId = null,
        CancellationToken cancellationToken = default,
        ApoExecutionOptions? apoOptions = null)
    {
        var localResearchId = researchId ?? Guid.NewGuid().ToString();
        _logger?.LogInformation("ResearcherWorkflow starting for topic: {topic}, Research ID: {researchId}", topic, localResearchId);

        // Submit APO task if Lightning integration is enabled
        if (_lightningService != null && _apoConfig?.Enabled == true)
        {
            var apoTask = new AgentTask
            {
                Name = "researcher",
                Description = $"Research topic: {topic}",
                Input = new Dictionary<string, object> 
                { 
                    ["topic"] = topic, 
                    ["researchId"] = localResearchId 
                },
                VerificationRequired = _apoConfig.Strategy != OptimizationStrategy.HighPerformance
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _lightningService.SubmitTaskAsync("researcher", apoTask, apoOptions);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to submit APO task for research");
                }
            }, cancellationToken);
        }

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

            await _stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);
            _logger?.LogDebug("Research state initialized: {localResearchId}", localResearchId);

            var researcherState = CreateResearcherState(topic);
            
            // Execute ReAct loop: LLM → Tools → Loop
            const int maxIterations = 5;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                _logger?.LogDebug("Research iteration {iter}/{max}", iteration + 1, maxIterations);

                // Step 1: LLM Call - Decide next research action
                var llmResponse = await LLMCallAsync(researcherState, cancellationToken);
                researcherState.ResearcherMessages.Add(llmResponse);

                // Step 2: Check if we should continue (loop or compress)
                if (!ShouldContinue(researcherState, iteration, maxIterations))
                {
                    _logger?.LogDebug("Research loop ending - should compress");
                    break;
                }

                // Step 3: Execute tools (search, scrape, think)
                await ToolExecutionAsync(researcherState, llmResponse, cancellationToken);

                // Update progress
                var progressQuality = CalculateResearchQuality(researcherState);
                await _stateService.UpdateResearchProgressAsync(
                     localResearchId,
                     iteration + 1,
                     progressQuality,
                     cancellationToken
                 );
                 _logger?.LogDebug("Research progress updated: iteration {iter}, quality: {quality:P}", iteration + 1, progressQuality);

                _logger?.LogDebug("Research iteration {iter} completed, quality: {quality:P}", iteration + 1, progressQuality);
            }

            // Step 4: Compress research into coherent summary
            var compressedFindings = await CompressResearchAsync(researcherState, cancellationToken);
            researcherState.CompressedResearch = compressedFindings;

            // Step 5: Extract and persist facts
            var facts = ExtractFactsFromFindings(compressedFindings);
            if (facts.Count > 0)
            {
                await _store.SaveFactsAsync(facts.AsEnumerable(), cancellationToken);
                _logger?.LogInformation("Persisted {count} facts to knowledge base", facts.Count);

                // Step 5a: Index facts to vector database (if available)
                if (_vectorDb != null && _embeddingService != null)
                {
                    await IndexFactsToVectorDatabaseAsync(facts, cancellationToken);
                }
            }

            // Update final research state
            researchState.Status = ResearchStatus.Completed;
            researchState.CompletedAt = DateTime.UtcNow;
            await _stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);
            _logger?.LogDebug("Research state updated to completed: {localResearchId}", localResearchId);

            _logger?.LogInformation("Research complete for topic: {topic} - {count} facts extracted", 
                topic, facts.Count);
            
            return facts;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Research error");
            throw;
        }
    }

    /// <summary>
    /// Stream research progress for real-time output.
    /// </summary>
    public async IAsyncEnumerable<string> StreamResearchAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("ResearcherWorkflow streaming for topic: {topic}", topic);
        yield return $"[researcher] starting research on: {topic}";

        var researcherState = CreateResearcherState(topic);
        int iteration = 0;
        const int maxIterations = 5;

        for (iteration = 0; iteration < maxIterations; iteration++)
        {
            // LLM Call (with error handling outside yield)
            var llmDecision = "";
            try
            {
                var llmResponse = await LLMCallAsync(researcherState, cancellationToken);
                researcherState.ResearcherMessages.Add(llmResponse);
                llmDecision = llmResponse.Content;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LLM call failed");
                llmDecision = $"ERROR: {ex.Message}";
            }

            yield return $"[researcher] iteration {iteration + 1}/{maxIterations}";
            if (llmDecision.StartsWith("ERROR"))
            {
                yield return llmDecision;
                yield break;
            }

            var preview = llmDecision.Substring(0, Math.Min(60, llmDecision.Length));
            yield return $"[researcher] llm: {preview}...";

            // Should continue check
            if (!ShouldContinue(researcherState, iteration, maxIterations))
            {
                yield return "[researcher] converging to compression phase";
                break;
            }

            // Tool execution (with error handling outside yield)
            int notesGathered = 0;
            try
            {
                await ToolExecutionAsync(researcherState, 
                    new Models.ChatMessage { Role = "assistant", Content = llmDecision }, 
                    cancellationToken);
                notesGathered = researcherState.RawNotes.Count;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Tool execution failed");
                notesGathered = -1;
            }

            if (notesGathered < 0)
            {
                yield return "[researcher] error during tool execution, stopping";
                break;
            }

            yield return $"[researcher] tools: gathered {notesGathered} notes";
        }

        // Compression phase (with error handling outside yield)
        string compressedFindings = "";
        int extractedFacts = 0;
        try
        {
            compressedFindings = await CompressResearchAsync(researcherState, cancellationToken);
            researcherState.CompressedResearch = compressedFindings;
            
            var facts = ExtractFactsFromFindings(compressedFindings);
            if (facts.Count > 0)
            {
                await _store.SaveFactsAsync(facts.AsEnumerable(), cancellationToken);
                extractedFacts = facts.Count;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Compression failed");
            compressedFindings = $"ERROR: {ex.Message}";
        }

        yield return "[researcher] compressing findings...";
        if (compressedFindings.StartsWith("ERROR"))
        {
            yield return compressedFindings;
            yield break;
        }

        yield return $"[researcher] compressed summary: {compressedFindings.Length} chars";
        if (extractedFacts > 0)
        {
            yield return $"[researcher] extracted and persisted {extractedFacts} facts";
        }

        yield return $"[researcher] research complete - {iteration + 1} iterations";
    }

    /// <summary>
    /// Step 1: LLM Call - Decide what research action to take next.
    /// Maps to Python lines 390-400
    /// </summary>
    public async Task<Models.ChatMessage> LLMCallAsync(
        ResearcherState state,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("LLMCall: deciding research direction");

            var currentDate = GetTodayString();
            
            // Build context for LLM
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("=== RESEARCH CONTEXT ===");
            contextBuilder.AppendLine($"Date: {currentDate}");
            contextBuilder.AppendLine($"Research Topic: {state.ResearchTopic}");
            contextBuilder.AppendLine($"Iteration: {state.ToolCallIterations}");
            contextBuilder.AppendLine();

            if (state.RawNotes.Any())
            {
                contextBuilder.AppendLine("=== GATHERED NOTES ===");
                foreach (var note in state.RawNotes.Take(3))
                {
                    var preview = note.Substring(0, Math.Min(100, note.Length));
                    contextBuilder.AppendLine($"- {preview}...");
                }
                contextBuilder.AppendLine();
            }

            string task = @"
            Your task:
            1. Assess what you've learned so far (if anything)
            2. Identify the most important next search or question
            3. Decide whether to:
               - Search for a specific aspect
               - Ask a clarifying question (reflect)
               - Stop researching (you have enough)

            Be concise and strategic. Focus on filling knowledge gaps.
            ";

            
            var systemPrompt = PromptTemplates.ResearchAgentPrompt
                .Replace("{context}", contextBuilder.ToString())
                .Replace("{task}", task)
                .Replace("{date}", currentDate);

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            // Add research history
            foreach (var msg in state.ResearcherMessages)
            {
                messages.Add(new OllamaChatMessage 
                { 
                    Role = msg.Role, 
                    Content = msg.Content 
                });
            }

            var response = await _llmService.InvokeAsync(messages, cancellationToken: cancellationToken);
            
            _logger?.LogDebug("LLM decision: {length} chars", response.Content.Length);
            
            // Convert to Models.ChatMessage
            return new Models.ChatMessage 
            { 
                Role = response.Role, 
                Content = response.Content 
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "LLMCall failed - using fallback");
            return new Models.ChatMessage 
            { 
                Role = "assistant", 
                Content = $"Search for more information about: {state.ResearchTopic}" 
            };
        }
    }

    /// <summary>
    /// Step 2: Tool Execution - Execute search, scraping, and semantic knowledge search.
    /// Maps to Python lines 400-420
    /// Now includes vector database search for external knowledge resources.
    /// </summary>
    public async Task ToolExecutionAsync(
        ResearcherState state,
        Models.ChatMessage llmResponse,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("ToolExecution: executing LLM decision");

            // Extract research queries from LLM response
            var queries = ExtractSearchQueries(llmResponse.Content, state.ResearchTopic);

            if (queries.Count == 0)
            {
                _logger?.LogDebug("No search queries identified");
                return;
            }

            // Execute web searches
            var webSearchTasks = queries
                .Take(2) // Max 2 concurrent web searches
                .Select(q => ExecuteSearchAsync(q,cancellationToken))
                ;

            var webSearchResults = await Task.WhenAll(webSearchTasks);

            // Add web search results to raw notes
            foreach (var result in webSearchResults)
            {
                if (result.Any())
                {
                    state.RawNotes.AddRange(result);
                }
            }

            // Execute vector database searches (if available)
            if (_vectorDb != null && _embeddingService != null)
            {
                var vectorSearchTasks = queries
                    .Take(2) // Max 2 concurrent vector database searches
                    .Select(q => ExecuteVectorDatabaseSearchAsync(q, cancellationToken))
                    .ToList();

                var vectorSearchResults = await Task.WhenAll(vectorSearchTasks);

                // Add vector search results to raw notes (flatten the lists)
                foreach (var resultList in vectorSearchResults)
                {
                    foreach (var result in resultList)
                    {
                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            state.RawNotes.Add(result);
                        }
                    }
                }
            }

            // Record tool execution in messages
            var searchSourceInfo = _vectorDb != null && _embeddingService != null 
                ? "web and vector database"
                : "web search";
            
            state.ResearcherMessages.Add(new Models.ChatMessage
            {
                Role = "tool",
                Content = $"Searched {queries.Count} topics across {searchSourceInfo} and gathered {state.RawNotes.Count} pieces of information."
            });

            state.ToolCallIterations++;

            _logger?.LogInformation("ToolExecution: gathered {count} notes from combined sources", state.RawNotes.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ToolExecution failed");
        }
    }

    /// <summary>
    /// Execute a single search and scrape operation.
    /// </summary>
    private async Task<List<string>> ExecuteSearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Searching for: {query}", query);
            
            // 1. Execute the search
            var results = await _searchService.SearchAndScrapeAsync(
                query,
                maxResults: 3,
                cancellationToken: cancellationToken
            );

            // 2. Deduplicate the results.
            var uniqueResults = DeduplicateSearchResults(results, cancellationToken);

            // 3. Process and summarize the content.
            var processedResults = await ProcessSearchResults(uniqueResults, cancellationToken);

            //4. Format the final output.
            var formattedOutput = FormatSearchOutput(processedResults, cancellationToken);

            _logger?.LogDebug("Search found {count} results", uniqueResults.Count);

            var notes = new List<string>() { formattedOutput };

            return notes;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Search for '{query}' failed", query);
            return new List<string>();
        }
    }

    private List<ScrapedContent> DeduplicateSearchResults(
        List<ScrapedContent> scrapedContents,
        CancellationToken cancellationToken)
    {
        var deduplicatedContents = new List<ScrapedContent>();

        foreach (var scrapedContent in scrapedContents)
        {
            // Simple deduplication based on URL or Title
            bool isDuplicate = deduplicatedContents.Any(other =>
                    other.Url == scrapedContent.Url);

            if (isDuplicate)
            {
                _logger?.LogDebug("Deduplicated search result: {url}", scrapedContent.Url);
                scrapedContents.Remove(scrapedContent);
            }
            else
            {
                deduplicatedContents.Add(scrapedContent);
            }
        }

        return deduplicatedContents;
    }

    private async Task<Dictionary<string, (string, string)>> ProcessSearchResults(
        List<ScrapedContent> uniqueContents,
        CancellationToken cancellationToken)
    {
        try
        {
            var summarizeTasks = uniqueContents
                .Select(async c =>
                {
                    var summary = !string.IsNullOrWhiteSpace(c.Html)
                        ? await SummarizeContent(c.Html, cancellationToken)
                        : c.Html ?? string.Empty;
                    return new { c.Url, c.Title, Content = summary };
                });

            var results = await Task.WhenAll(summarizeTasks);

            var resultDict = results.ToDictionary(
                r => r.Url,
                r => (r.Title, r.Content) // Both summary and keyExcerpts set to Content for now
            );
            return resultDict;

        }
        catch (Exception ex) 
        {
            _logger?.LogWarning(ex, "Processing search results failed");
            return new Dictionary<string, (string, string)>();
        }
        
    }

    private string FormatSearchOutput(
        Dictionary<string, (string, string)> summarizedResults,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Search results: \n\n");
        
        int i = 1;
        foreach (var kvp in summarizedResults)
        {
            string url = kvp.Key;
            var (title, content) = kvp.Value;

            sb.AppendLine();                       // blank line before each source
            sb.AppendLine($"--- SOURCE {i}: {title} ---");
            sb.AppendLine($"URL: {url}");
            sb.AppendLine();                       // blank line after the URL
            sb.AppendLine("SUMMARY:");
            sb.AppendLine(content);
            sb.AppendLine();                       // blank line after the content
            sb.AppendLine(new string('-', 80));     // 80 hyphens as a separator
            sb.AppendLine();                       // optional final blank line
            i++;
        }
        return sb.ToString();
    }



    private async Task<string> SummarizeContent(
        string content,
        CancellationToken cancellationToken)
    {
        
        try
        {
            var summary = "";
            var currentDate = GetTodayString();

            var userPrompt = PromptTemplates.SummarizeWebpagePrompt
                    .Replace("{webpage_content}", content)
                    .Replace("{date}", currentDate);

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = userPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, cancellationToken: cancellationToken);

            if (response != null && !string.IsNullOrEmpty(response.Content))
            {
                var jsonClean = response.Content.Substring(1, response.Content.Length - 1).Replace("```", "").Replace("json", "").ReplaceLineEndings();
                var responseJson = JsonDocument.Parse(jsonClean);

                summary = "<summary>\n{summary}\n</summary>\n\n<key_excerpts>\n{key_excerpts}\n</key_excerpts>";

                if (responseJson.RootElement.TryGetProperty("summary", out var summaryElement))
                {
                    summary = summary.Replace("{summary}", summaryElement.GetString() ?? "");
                }

                if (responseJson.RootElement.TryGetProperty("key_excerpts", out var keyExcerpts))
                {
                    summary = summary.Replace("{key_excerpts}", keyExcerpts.GetString() ?? "");
                }
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "SummarizeContent '{content}' failed", content);
            return string.Empty;
        }
        
    }


    /// <summary>
    /// Execute a vector database search for existing knowledge resources.
    /// Searches pre-existing or external knowledge bases for related information.
    /// Similar to ExecuteSearchAsync but queries semantic embeddings instead of web.
    /// </summary>
    private async Task<List<string>> ExecuteVectorDatabaseSearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Vector database search for: {query}", query);

            if (_vectorDb == null || _embeddingService == null)
            {
                _logger?.LogDebug("Vector database not configured - skipping vector search");
                return new List<string>();
            }

            // Search for similar facts in vector database
            // Use higher threshold (0.6) for quality relevance in knowledge bases
            var searchResults = await _vectorDb.SearchByContentAsync(
                query,
                topK: 5,
                scoreThreshold: 0.6,
                cancellationToken: cancellationToken);

            if (searchResults.Count == 0)
            {
                _logger?.LogDebug("Vector database search returned no results for: {query}", query);
                return new List<string>();
            }

            // Convert vector search results to notes for research synthesis
            var notes = searchResults
                .Select(result => FormatVectorSearchResult(result))
                .Where(content => !string.IsNullOrWhiteSpace(content))
                .ToList();

            _logger?.LogDebug("Vector database search found {count} relevant facts", notes.Count);

            return notes;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Vector database search for '{query}' failed", query);
            return new List<string>();
        }
    }

    /// <summary>
    /// Format vector search result into note content with source attribution.
    /// Includes the fact content, relevance score, and metadata for research context.
    /// </summary>
    private static string FormatVectorSearchResult(VectorSearchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Content))
            return string.Empty;

        var formatted = new StringBuilder();
        formatted.AppendLine($"[Knowledge Base - Relevance: {result.Score:P0}]");
        formatted.AppendLine(result.Content);

        if (result.Metadata != null)
        {
            if (result.Metadata.TryGetValue("sourceUrl", out var sourceUrl) && sourceUrl != null)
            {
                formatted.AppendLine($"Source: {sourceUrl}");
            }

            if (result.Metadata.TryGetValue("confidence", out var confidence) && confidence != null)
            {
                formatted.AppendLine($"Confidence: {confidence}");
            }
        }

        var text = formatted.ToString();
        // Trim to reasonable length (consistent with web search results)
        return text.Length > 280 ? text[..280] + "..." : text;
    }

    /// <summary>
    /// Step 3: Check if research should continue or move to compression.
    /// Maps to Python lines 420-430
    /// </summary>
    public static bool ShouldContinue(
        ResearcherState state,
        int currentIteration,
        int maxIterations)
    {
        // Continue if:
        // 1. Haven't hit max iterations
        // 2. We still have notes to process
        // 3. LLM didn't suggest stopping
        
        if (currentIteration >= maxIterations - 1)
            return false; // Last iteration, compress

        if (state.RawNotes.Count == 0)
            return false; // No data to compress

        // Check if LLM suggested stopping (heuristic)
        var lastMessage = state.ResearcherMessages.LastOrDefault()?.Content ?? "";
        if (lastMessage.Contains("enough", StringComparison.OrdinalIgnoreCase) ||
            lastMessage.Contains("sufficient", StringComparison.OrdinalIgnoreCase) ||
            lastMessage.Contains("stop", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Step 4: Compress research findings into coherent summary.
    /// Maps to Python lines 430-460
    /// </summary>
    public async Task<string> CompressResearchAsync(
        ResearcherState state,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("CompressResearch: synthesizing findings");

            var currentDate = GetTodayString();

            var compressionSystemPrompt = PromptTemplates.CompressResearchSystemPrompt
                .Replace("{date}", currentDate);

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = compressionSystemPrompt }
            };

            // Convert ChatMessage to OllamaChatMessage
            foreach (var msg in state.ResearcherMessages)
            {
                messages.Add(new OllamaChatMessage 
                { 
                    Role = msg.Role, 
                    Content = msg.Content 
                });
            }
            
            var compressResearchHumanMessage = PromptTemplates.CompressResearchHumanMessage
                .Replace("{research_topic}", "")
                .Replace("{research_query}", "")
                .Replace("{date}", currentDate);


            messages.Add(new() { Role = "user", Content = compressResearchHumanMessage });


            var response = await _llmService.InvokeAsync(messages, cancellationToken: cancellationToken);
            var compressed = response.Content ?? "Unable to compress research findings.";


            // Aggregate raw notes
            var aggregatedNotes = string.Join("\n\n", state.RawNotes.Take(15));


            _logger?.LogInformation("CompressResearch: {length} chars", compressed.Length);

            return aggregatedNotes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CompressResearch failed");
            return string.Empty;
        }
    }

    /// <summary>
    /// Extract search queries from LLM decision.
    /// Simple heuristic: parse for search suggestions.
    /// </summary>
    private static List<string> ExtractSearchQueries(string llmDecision, string researchTopic)
    {
        var queries = new List<string>();

        // Always include the main topic
        queries.Add(researchTopic);

        // Look for specific aspects mentioned
        if (llmDecision.Contains("search for", StringComparison.OrdinalIgnoreCase))
        {
            // Extract words after "search for"
            var idx = llmDecision.IndexOf("search for", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var portion = llmDecision.Substring(idx + 10, Math.Min(100, llmDecision.Length - idx - 10));
                var words = portion.Split(new[] { ' ', ',', '.', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Take(3)
                    .Where(w => w.Length > 2);
                
                if (words.Any())
                {
                    queries.Add($"{researchTopic} {string.Join(" ", words)}");
                }
            }
        }

        // Add variations
        if (researchTopic.Length > 10)
        {
            queries.Add($"{researchTopic} applications");
            queries.Add($"{researchTopic} benefits");
        }

        return queries.Distinct().Take(3).ToList();
    }

    /// <summary>
    /// Extract facts from compressed research findings.
    /// </summary>
    private static List<Models.FactState> ExtractFactsFromFindings(string compressedFindings)
    {
        var facts = new List<Models.FactState>();

        if (string.IsNullOrWhiteSpace(compressedFindings))
            return facts;

        // Split by sentences or paragraphs
        var paragraphs = compressedFindings
            .Split(new[] { "\n\n", "\n", ". " }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 20)
            .Take(20); // Max 20 facts

        int idx = 1;
        foreach (var paragraph in paragraphs)
        {
            var fact = StateFactory.CreateFact(
                paragraph.Trim(),
                "compressed_research",
                confidenceScore: 75  // Confidence from compression
            );
            facts.Add(fact);
            idx++;
        }

        return facts;
    }

    /// <summary>
    /// Create initial researcher state.
    /// </summary>
    private static ResearcherState CreateResearcherState(string topic)
    {
        return new ResearcherState
        {
            ResearchTopic = topic,
            ResearcherMessages = new List<Models.ChatMessage>(),
            RawNotes = new List<string>(),
            CompressedResearch = string.Empty,
            ToolCallIterations = 0
        };
    }

    /// <summary>
    /// Build fact content from scraped web result.
    /// </summary>
    private static string BuildFactContent(ScrapedContent content)
    {
        var text = !string.IsNullOrWhiteSpace(content.Markdown)
            ? content.Markdown
            : content.CleanedHtml;

        if (string.IsNullOrWhiteSpace(text))
        {
            text = content.Html ?? string.Empty;
        }

        return text.Length > 280 ? text[..280] + "..." : text;
    }

    /// <summary>
    /// Calculate current research quality based on gathered notes and messages.
    /// Returns a value between 0 and 1.
    /// </summary>
    private static double CalculateResearchQuality(ResearcherState state)
    {
        double quality = 0.0;

        // Factor 1: Number of notes gathered (0-0.3)
        var notesFactor = Math.Min(state.RawNotes.Count / 10.0, 0.3);
        quality += notesFactor;

        // Factor 2: Number of researcher messages (0-0.3)
        var messageFactor = Math.Min(state.ResearcherMessages.Count / 5.0, 0.3);
        quality += messageFactor;

        // Factor 3: Tool call iterations (0-0.2)
        var iterationFactor = Math.Min(state.ToolCallIterations / 5.0, 0.2);
        quality += iterationFactor;

        // Factor 4: Length of compressed research (0-0.2)
        var contentLength = state.CompressedResearch?.Length ?? 0;
        var contentFactor = Math.Min(contentLength / 5000.0, 0.2);
        quality += contentFactor;

        return Math.Min(quality, 1.0);  // Cap at 1.0
    }

    /// <summary>
    /// Format today's date for prompt consistency.
    /// </summary>
    private static string GetTodayString()
    {
        return DateTime.Now.ToString("ddd MMM d, yyyy");
    }

    /// <summary>
    /// Step 5a: Index facts to vector database for semantic search.
    /// This stores document embeddings for later similarity-based retrieval.
    /// </summary>
    private async Task IndexFactsToVectorDatabaseAsync(
        List<Models.FactState> facts,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Indexing {count} facts to vector database", facts.Count);

            // Generate embeddings for each fact's content
            var embeddings = await _embeddingService!.EmbedBatchAsync(
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
                        ["extractedAt"] = facts[i].ExtractedAt.ToString("O")
                    };

                    await _vectorDb!.UpsertAsync(
                        facts[i].Id,
                        facts[i].Content,
                        embeddings[i],
                        metadata,
                        cancellationToken);

                    indexedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to index fact {id} to vector database", facts[i].Id);
                }
            }

            _logger?.LogInformation("Successfully indexed {count}/{total} facts to vector database", 
                indexedCount, facts.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to index facts to vector database");
        }
    }

    /// <summary>
    /// Search the vector database for similar facts.
    /// Useful for finding related research findings and avoiding redundant work.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarFactsAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (_vectorDb == null || _embeddingService == null)
        {
            _logger?.LogWarning("Vector database not configured - cannot search similar facts");
            return Array.Empty<VectorSearchResult>();
        }

        try
        {
            _logger?.LogDebug("Searching for similar facts to: {query}", query);

            var results = await _vectorDb.SearchByContentAsync(
                query,
                topK,
                scoreThreshold: 0.5,
                cancellationToken: cancellationToken);

            _logger?.LogInformation("Found {count} similar facts", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search similar facts");
            return Array.Empty<VectorSearchResult>();
        }
    }
}
