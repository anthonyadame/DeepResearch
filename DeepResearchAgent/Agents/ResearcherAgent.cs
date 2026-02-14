using System.Text.Json;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// ResearcherAgent: Orchestrates research workflow using tools.
/// 
/// Responsibilities:
/// 1. Plans research strategy based on topic
/// 2. Executes web searches for planned topics
/// 3. Summarizes search results
/// 4. Extracts facts from summaries
/// 5. Evaluates research quality
/// 6. Refines research if needed (iterative)
/// 
/// Returns: ResearchOutput with findings and metrics
/// </summary>
public class ResearcherAgent
{
    private readonly OllamaService _llmService;
    private readonly ToolInvocationService _toolService;
    private readonly ILogger<ResearcherAgent>? _logger;

    protected virtual string AgentName => "ResearcherAgent";

    public ResearcherAgent(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<ResearcherAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
        _logger = logger;
    }

    /// <summary>
    /// Execute research workflow: Plan → Search → Extract → Evaluate → Refine
    /// </summary>
    public async Task<ResearchOutput> ExecuteAsync(
        ResearchInput input,
        CancellationToken cancellationToken = default)
    {
        var output = new ResearchOutput();
        
        try
        {
            _logger?.LogInformation("ResearcherAgent: Starting research on topic: {Topic}", input.Topic);

            // Step 1: Plan research strategy
            var researchTopics = await PlanResearchTopicsAsync(input, cancellationToken);
            output.ResearchTopicsCovered = researchTopics;
            _logger?.LogInformation("ResearcherAgent: Planned research topics: {Topics}", 
                string.Join(", ", researchTopics));

            // Step 2-5: Execute iterative research
            for (int iteration = 0; iteration < input.MaxIterations; iteration++)
            {
                _logger?.LogInformation("ResearcherAgent: Iteration {Iter}/{Max}", 
                    iteration + 1, input.MaxIterations);

                // Execute search and extraction for each topic
                foreach (var topic in researchTopics)
                {
                    await ExecuteResearchTopicAsync(topic, output, cancellationToken);
                }

                // Evaluate quality
                var quality = await EvaluateFindingsAsync(output, cancellationToken);
                output.IterationsUsed = iteration + 1;
                _logger?.LogInformation("ResearcherAgent: Iteration {Iter} quality: {Quality:F1}", 
                    iteration + 1, quality);

                // Check if we should refine
                if (quality >= input.MinQualityThreshold)
                {
                    _logger?.LogInformation("ResearcherAgent: Quality threshold reached ({Quality:F1} >= {Threshold:F1})", 
                        quality, input.MinQualityThreshold);
                    output.CompletionStatus = "completed_quality_threshold";
                    break;
                }

                if (iteration == input.MaxIterations - 1)
                {
                    _logger?.LogInformation("ResearcherAgent: Max iterations reached");
                    output.CompletionStatus = "completed_max_iterations";
                }
            }

            // Calculate average quality
            output.AverageQuality = output.Findings.Any() 
                ? (float)output.Findings.Average(f => f.Facts.Average(fact => fact.Confidence))
                : 0f;

            _logger?.LogInformation("ResearcherAgent: Research complete. Facts extracted: {Count}, Quality: {Quality:F2}", 
                output.TotalFactsExtracted, output.AverageQuality);

            return output;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ResearcherAgent: Research failed");
            output.CompletionStatus = "failed";
            throw;
        }
    }

    /// <summary>
    /// Use LLM to plan research topics based on the input topic and brief.
    /// </summary>
    private async Task<List<string>> PlanResearchTopicsAsync(
        ResearchInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var planPrompt = $@"You are a research planning agent. Plan a research strategy for the following:

Topic: {input.Topic}
Research Brief: {input.ResearchBrief}

Based on this, identify 3-5 specific sub-topics or angles that should be researched to build a comprehensive understanding.

Return ONLY a JSON array of strings (the topic names), nothing else.
Example: [""subtopic1"", ""subtopic2"", ""subtopic3""]";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = planPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            
            // Parse JSON response
            var trimmedResponse = response.Content.Trim();
            if (trimmedResponse.StartsWith("[") && trimmedResponse.EndsWith("]"))
            {
                var topics = JsonSerializer.Deserialize<List<string>>(trimmedResponse) ?? new();
                _logger?.LogDebug("Planned research topics: {Count}", topics.Count);
                return topics;
            }

            // Fallback: extract topic from response
            _logger?.LogWarning("Could not parse topic list, using default");
            return new List<string> { input.Topic };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to plan research topics, using default");
            return new List<string> { input.Topic };
        }
    }

    /// <summary>
    /// Execute research for a single topic: Search → Summarize → Extract Facts
    /// </summary>
    private async Task ExecuteResearchTopicAsync(
        string topic,
        ResearchOutput output,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("ResearcherAgent: Researching topic: {Topic}", topic);

            // Step 1: Web Search
            var searchParams = new Dictionary<string, object>
            {
                { "query", topic },
                { "maxResults", 3 }
            };

            var searchResults = await _toolService.InvokeToolAsync(
                "websearch", searchParams, cancellationToken);

            if (searchResults is not List<WebSearchResult> results || !results.Any())
            {
                _logger?.LogDebug("No search results for topic: {Topic}", topic);
                return;
            }

            _logger?.LogDebug("Found {Count} search results for: {Topic}", results.Count, topic);

            // Step 2-3: For each result, summarize and extract facts
            foreach (var result in results)
            {
                try
                {
                    // Summarize
                    var summaryParams = new Dictionary<string, object>
                    {
                        { "pageContent", result.Content },
                        { "maxLength", 400 }
                    };

                    var summarized = await _toolService.InvokeToolAsync(
                        "summarize", summaryParams, cancellationToken);

                    if (summarized is not PageSummaryResult summary)
                    {
                        _logger?.LogWarning("Summarization returned unexpected type for: {Url}", result.Url);
                        continue;
                    }

                    // Extract facts
                    var factParams = new Dictionary<string, object>
                    {
                        { "content", summary.Summary },
                        { "topic", topic }
                    };

                    var factResult = await _toolService.InvokeToolAsync(
                        "extractfacts", factParams, cancellationToken);

                    if (factResult is FactExtractionResult extracted)
                    {
                        output.Findings.Add(extracted);
                        _logger?.LogDebug("Extracted {Count} facts from: {Url}", 
                            extracted.Facts.Count, result.Url);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to process search result: {Url}", result.Url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to research topic: {Topic}", topic);
        }
    }

    /// <summary>
    /// Evaluate the quality of extracted findings using LLM.
    /// </summary>
    private async Task<float> EvaluateFindingsAsync(
        ResearchOutput output,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!output.Findings.Any() || !output.Findings.Any(f => f.Facts.Any()))
            {
                _logger?.LogDebug("No findings to evaluate");
                return 0f;
            }

            var findingsSummary = string.Join("\n", 
                output.Findings.SelectMany(f => f.Facts.Take(5).Select(fact => fact.Statement)));

            var evalPrompt = $@"Evaluate the quality of these research findings on a scale of 0-10:

Findings:
{findingsSummary}

Consider:
1. Relevance to the research topic
2. Credibility and sourcing
3. Comprehensiveness (are multiple angles covered?)
4. Freshness and timeliness
5. Specificity and detail

Respond with ONLY a single number between 0 and 10 (e.g., 7.5)";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = evalPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);

            if (float.TryParse(response.Content.Trim(), out var quality))
            {
                return Math.Clamp(quality, 0f, 10f);
            }

            _logger?.LogWarning("Could not parse quality score: {Response}", response);
            return 5f; // Default middle quality
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to evaluate findings quality");
            return 5f;
        }
    }
}
