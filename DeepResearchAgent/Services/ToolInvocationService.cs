using DeepResearchAgent.Tools;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services.WebSearch;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Tool Invocation Service: Manages execution of research tools.
/// Provides a registry and execution framework for all tools.
/// </summary>
public class ToolInvocationService
{
    private readonly ResearchToolsImplementation _tools;
    private readonly ILogger<ToolInvocationService>? _logger;

    public ToolInvocationService(
        IWebSearchProvider searchProvider,
        OllamaService llmService,
        ILogger<ToolInvocationService>? logger = null)
    {
        _tools = new ResearchToolsImplementation(searchProvider, llmService, null);
        _logger = logger;
    }

    /// <summary>
    /// Execute a tool by name with the provided input.
    /// Routes to the appropriate tool method.
    /// </summary>
    public async Task<object> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Invoking tool: {ToolName} with {ParameterCount} parameters",
                toolName, parameters.Count);

            var result = toolName.ToLowerInvariant() switch
            {
                "websearch" => await WebSearchToolAsync(parameters, cancellationToken),
                "qualityevaluation" => await QualityEvaluationToolAsync(parameters, cancellationToken),
                "summarize" => await WebpageSummarizationToolAsync(parameters, cancellationToken),
                "extractfacts" => await FactExtractionToolAsync(parameters, cancellationToken),
                "refinedraft" => await RefineDraftToolAsync(parameters, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };

            _logger?.LogInformation("Tool {ToolName} executed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool invocation failed for: {ToolName}", toolName);
            throw new InvalidOperationException($"Tool execution failed: {toolName}", ex);
        }
    }

    /// <summary>
    /// Get list of available tools with descriptions.
    /// </summary>
    public List<ToolDescriptor> GetAvailableTools()
    {
        return new List<ToolDescriptor>
        {
            new ToolDescriptor
            {
                Name = "websearch",
                DisplayName = "Web Search",
                Description = "Search the web for information on a specific topic",
                Parameters = new Dictionary<string, string>
                {
                    { "query", "string: The search query" },
                    { "maxResults", "int: Maximum number of results (default: 10)" }
                }
            },
            new ToolDescriptor
            {
                Name = "qualityevaluation",
                DisplayName = "Quality Evaluation",
                Description = "Evaluate the quality of research content on multiple dimensions",
                Parameters = new Dictionary<string, string>
                {
                    { "content", "string: The content to evaluate" },
                    { "dimensions", "string[]: Dimensions to evaluate (default: accuracy, completeness, relevance, clarity)" }
                }
            },
            new ToolDescriptor
            {
                Name = "summarize",
                DisplayName = "Webpage Summarization",
                Description = "Summarize the content of a web page",
                Parameters = new Dictionary<string, string>
                {
                    { "pageContent", "string: The webpage content to summarize" },
                    { "maxLength", "int: Maximum summary length (default: 500)" }
                }
            },
            new ToolDescriptor
            {
                Name = "extractfacts",
                DisplayName = "Fact Extraction",
                Description = "Extract structured facts from research content",
                Parameters = new Dictionary<string, string>
                {
                    { "content", "string: The content to extract facts from" },
                    { "topic", "string: The topic or domain for context" }
                }
            },
            new ToolDescriptor
            {
                Name = "refinedraft",
                DisplayName = "Refine Draft",
                Description = "Refine and improve a draft report",
                Parameters = new Dictionary<string, string>
                {
                    { "draftReport", "string: The current draft report" },
                    { "feedback", "string: Feedback or new information to incorporate" },
                    { "iterationNumber", "int: Iteration number for tracking" }
                }
            }
        };
    }

    #region Tool Executors

    private async Task<object> WebSearchToolAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var query = GetRequiredParameter<string>(parameters, "query");
        var maxResults = GetOptionalParameter(parameters, "maxResults", 10);
        var topics = GetOptionalParameter(parameters, "topics", (List<string>?)null);

        return await _tools.WebSearchAsync(query, maxResults, topics, cancellationToken);
    }

    private async Task<object> QualityEvaluationToolAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var content = GetRequiredParameter<string>(parameters, "content");
        var dimensions = GetOptionalParameter(parameters, "dimensions", (string[])null);

        return await _tools.EvaluateQualityAsync(content, dimensions, cancellationToken);
    }

    private async Task<object> WebpageSummarizationToolAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var pageContent = GetRequiredParameter<string>(parameters, "pageContent");
        var maxLength = GetOptionalParameter(parameters, "maxLength", 500);

        return await _tools.SummarizePageAsync(pageContent, maxLength, cancellationToken);
    }

    private async Task<object> FactExtractionToolAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var content = GetRequiredParameter<string>(parameters, "content");
        var topic = GetOptionalParameter(parameters, "topic", "general");

        return await _tools.ExtractFactsAsync(content, topic, cancellationToken);
    }

    private async Task<object> RefineDraftToolAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var draftReport = GetRequiredParameter<string>(parameters, "draftReport");
        var feedback = GetRequiredParameter<string>(parameters, "feedback");
        var iterationNumber = GetOptionalParameter(parameters, "iterationNumber", 1);

        return await _tools.RefineDraftAsync(draftReport, feedback, iterationNumber, cancellationToken);
    }

    #endregion

    #region Helper Methods

    private T GetRequiredParameter<T>(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"Required parameter missing: {key}");
        }

        if (value is not T typedValue)
        {
            throw new InvalidOperationException($"Parameter {key} is not of type {typeof(T).Name}");
        }

        return typedValue;
    }

    private T GetOptionalParameter<T>(Dictionary<string, object> parameters, string key, T defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value is T typedValue ? typedValue : defaultValue;
    }

    #endregion
}

/// <summary>
/// Descriptor for a tool, including metadata and parameters.
/// </summary>
public class ToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}
