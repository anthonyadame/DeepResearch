using System.ComponentModel;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Tools;

/// <summary>
/// Phase 3 Tool Implementations: Core research tools
/// These are the actual implementations that will be used by agents.
/// </summary>
public class ResearchToolsImplementation
{
    private readonly IWebSearchProvider _searchProvider;
    private readonly OllamaService _llmService;
    private readonly ILogger<ResearchToolsImplementation>? _logger;

    public ResearchToolsImplementation(
        IWebSearchProvider searchProvider,
        OllamaService llmService,
        ILogger<ResearchToolsImplementation>? logger = null)
    {
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger;
    }

    /// <summary>
    /// WebSearchTool: Search the web for information on a topic.
    /// Integrates with the configured web search provider to fetch results.
    /// </summary>
    [Description("Search the web for information on a specific topic")]
    public async Task<List<WebSearchResult>> WebSearchAsync(
        [Description("The search query to find information about")]
        string query,
        [Description("Maximum number of results to return (default: 10)")]
        int maxResults = 10,
        [Description("Optional list of topics to constrain the search")]
        List<string>? topics = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation(
                "WebSearchTool: Searching for '{Query}' with max {MaxResults} results using provider '{Provider}'. Topics: {Topics}",
                query,
                maxResults,
                _searchProvider.ProviderName,
                topics != null ? string.Join(", ", topics) : "none");

            var results = await _searchProvider.SearchAsync(query, maxResults, topics, cancellationToken);

            _logger?.LogInformation("WebSearchTool: Found {ResultCount} results", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WebSearchTool: Search failed for query '{Query}'", query);
            throw new InvalidOperationException($"Web search failed for query: {query}", ex);
        }
    }

    /// <summary>
    /// QualityEvaluationTool: Evaluate the quality of content.
    /// Uses LLM to score content on multiple dimensions.
    /// </summary>
    [Description("Evaluate the quality of research content")]
    public async Task<QualityEvaluationResult> EvaluateQualityAsync(
        [Description("The content to evaluate")]
        string content,
        [Description("Dimension names to evaluate (e.g., 'accuracy', 'completeness', 'relevance')")]
        string[] dimensions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("QualityEvaluationTool: Evaluating content of {Length} characters", 
                content.Length);

            // Default dimensions if not provided
            dimensions ??= new[] { "accuracy", "completeness", "relevance", "clarity" };

            // Create evaluation prompt
            var dimensionList = string.Join(", ", dimensions);
            var prompt = $@"Evaluate the following content on these dimensions: {dimensionList}

Content:
{content}

For each dimension, provide a score from 0-10 and a brief explanation.
Then provide an overall quality score.

Return your evaluation in this JSON format:
{{
  ""dimension_scores"": {{
    ""dimension_name"": {{
      ""score"": 8,
      ""reason"": ""explanation""
    }}
  }},
  ""overall_score"": 7.5,
  ""summary"": ""overall assessment""
}}";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };

            var response = await _llmService.InvokeWithStructuredOutputAsync<QualityEvaluationResult>(
                messages, cancellationToken: cancellationToken);

            _logger?.LogInformation("QualityEvaluationTool: Overall score = {Score}", 
                response.OverallScore);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "QualityEvaluationTool: Evaluation failed");
            throw new InvalidOperationException("Quality evaluation failed", ex);
        }
    }

    /// <summary>
    /// WebpageSummarizationTool: Summarize a web page's content.
    /// Uses LLM to extract key points and create a concise summary.
    /// </summary>
    [Description("Summarize the content of a web page")]
    public async Task<PageSummaryResult> SummarizePageAsync(
        [Description("The webpage content to summarize")]
        string pageContent,
        [Description("Maximum length of summary in characters (default: 500)")]
        int maxLength = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("WebpageSummarizationTool: Summarizing {Length} characters", 
                pageContent.Length);

            var prompt = $@"Summarize the following webpage content in {maxLength} characters or less.

Content:
{pageContent}

Provide:
1. A concise summary (max {maxLength} chars)
2. 3-5 key points (bullet list)

Return as JSON with:
- ""summary"": concise summary
- ""key_points"": array of key points";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };

            var response = await _llmService.InvokeWithStructuredOutputAsync<PageSummaryResult>(
                messages, cancellationToken: cancellationToken);

            _logger?.LogInformation("WebpageSummarizationTool: Summary created ({Length} chars)", 
                response.Summary.Length);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WebpageSummarizationTool: Summarization failed");
            throw new InvalidOperationException("Page summarization failed", ex);
        }
    }

    /// <summary>
    /// FactExtractionTool: Extract structured facts from content.
    /// Uses LLM to identify and structure facts from research content.
    /// </summary>
    [Description("Extract structured facts from research content")]
    public async Task<FactExtractionResult> ExtractFactsAsync(
        [Description("The content to extract facts from")]
        string content,
        [Description("The topic or domain for fact extraction")]
        string topic = "general",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("FactExtractionTool: Extracting facts for topic '{Topic}'", topic);

            var prompt = $@"Extract structured facts from the following content about {topic}.

Content:
{content}

Extract facts in this JSON format:
{{
  ""facts"": [
    {{
      ""statement"": ""fact statement"",
      ""confidence"": 0.9,
      ""source"": ""where it came from in the content"",
      ""category"": ""fact category""
    }}
  ],
  ""total_facts"": 5
}}";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };

            var response = await _llmService.InvokeWithStructuredOutputAsync<FactExtractionResult>(
                messages, cancellationToken: cancellationToken);

            _logger?.LogInformation("FactExtractionTool: Extracted {FactCount} facts", 
                response.Facts.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FactExtractionTool: Fact extraction failed");
            throw new InvalidOperationException("Fact extraction failed", ex);
        }
    }

    /// <summary>
    /// RefineDraftReportTool: Iteratively improve draft reports.
    /// Uses LLM to refine drafts based on feedback or new information.
    /// </summary>
    [Description("Refine and improve a draft report")]
    public async Task<RefinedDraftResult> RefineDraftAsync(
        [Description("The current draft report")]
        string draftReport,
        [Description("Feedback or new information to incorporate")]
        string feedback,
        [Description("Iteration number for tracking")]
        int iterationNumber = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("RefineDraftReportTool: Refining draft (iteration {Iteration})", 
                iterationNumber);

            var prompt = $@"Improve this research draft report based on the feedback provided.

Current Draft:
{draftReport}

Feedback/New Information:
{feedback}

Task:
1. Incorporate the feedback into the draft
2. Maintain professional structure and formatting
3. Improve clarity and completeness
4. Identify key changes made

Return as JSON:
{{
  ""refined_report"": ""improved report text"",
  ""changes_made"": [""change 1"", ""change 2"", ...],
  ""improvement_score"": 0.85
}}";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };

            var response = await _llmService.InvokeWithStructuredOutputAsync<RefinedDraftResult>(
                messages, cancellationToken: cancellationToken);

            _logger?.LogInformation("RefineDraftReportTool: Improvement score = {Score}", 
                response.ImprovementScore);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RefineDraftReportTool: Refinement failed");
            throw new InvalidOperationException("Draft refinement failed", ex);
        }
    }
}
