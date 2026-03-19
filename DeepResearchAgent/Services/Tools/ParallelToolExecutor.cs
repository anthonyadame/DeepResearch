using DeepResearchAgent.Models;
using DeepResearchAgent.Observability;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Tools;

/// <summary>
/// Parallel tool execution coordinator.
/// Executes independent tools concurrently using Task.WhenAll to reduce latency.
/// 
/// Performance Target: 13s → 9s per iteration (31% faster tool execution)
/// Strategy: Execute Summarize and ExtractFacts in parallel for each search result
/// </summary>
public class ParallelToolExecutor
{
    private readonly ToolInvocationService _toolService;
    private readonly ILogger<ParallelToolExecutor>? _logger;

    public ParallelToolExecutor(
        ToolInvocationService toolService,
        ILogger<ParallelToolExecutor>? logger = null)
    {
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
        _logger = logger;
    }

    /// <summary>
    /// Execute summarization and fact extraction in parallel for a search result.
    /// Both operations depend on the same search result content, so they can run concurrently.
    /// Extract facts from raw content to maintain independence from summarization.
    /// </summary>
    public async Task<(PageSummaryResult? Summary, FactExtractionResult? Facts)> 
        ExecuteSummarizeAndExtractAsync(
            WebSearchResult result,
            string topic,
            CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug("ParallelToolExecutor: Starting parallel summarize+extract for {Url}", 
                result.Url);

            // Create both tasks without awaiting immediately
            var summarizeTask = SummarizeAsync(result, cancellationToken);
            var extractTask = ExtractFactsAsync(result, topic, cancellationToken);

            // Execute both in parallel using Task.WhenAll
            await Task.WhenAll(summarizeTask, extractTask).ConfigureAwait(false);

            var summary = summarizeTask.Result as PageSummaryResult;
            var facts = extractTask.Result as FactExtractionResult;

            _logger?.LogDebug("ParallelToolExecutor: Completed parallel operations in {ElapsedMs}ms", 
                sw.Elapsed.TotalMilliseconds);

            return (summary, facts);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ParallelToolExecutor: Error during parallel execution");
            throw;
        }
        finally
        {
            DiagnosticConfig.ParallelExecutionDuration.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Execute multiple result processing operations in parallel.
    /// Each result gets its own parallel summarize+extract operation.
    /// </summary>
    public async Task<List<(WebSearchResult Result, PageSummaryResult? Summary, FactExtractionResult? Facts)>> 
        ExecuteResultsParallelAsync(
            List<WebSearchResult> results,
            string topic,
            CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger?.LogInformation("ParallelToolExecutor: Processing {count} results in parallel", 
                results.Count);

            // Create tasks for all results
            var processingTasks = results.Select(result =>
                ExecuteSummarizeAndExtractAsync(result, topic, cancellationToken)
                    .ContinueWith(t => (Result: result, Summary: t.Result.Summary, Facts: t.Result.Facts),
                        cancellationToken)
            ).ToList();

            // Wait for all to complete
            var allResults = await Task.WhenAll(processingTasks).ConfigureAwait(false);

            _logger?.LogInformation("ParallelToolExecutor: Processed {count} results in {ElapsedMs}ms", 
                results.Count, sw.Elapsed.TotalMilliseconds);

            DiagnosticConfig.ParallelBatchExecutions.Add(1);
            return allResults.ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ParallelToolExecutor: Error processing results batch");
            throw;
        }
        finally
        {
            DiagnosticConfig.ParallelBatchDuration.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Private helper: Execute summarization tool.
    /// </summary>
    private async Task<object?> SummarizeAsync(
        WebSearchResult result,
        CancellationToken cancellationToken)
    {
        var summaryParams = new Dictionary<string, object>
        {
            { "pageContent", result.Content },
            { "maxLength", 300 }
        };

        return await _toolService.InvokeToolAsync(
            "summarize", summaryParams, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Private helper: Execute fact extraction tool.
    /// </summary>
    private async Task<object?> ExtractFactsAsync(
        WebSearchResult result,
        string topic,
        CancellationToken cancellationToken)
    {
        // For parallel extraction, we need the summary first
        // So we'll extract from the content directly
        var factParams = new Dictionary<string, object>
        {
            { "content", result.Content },
            { "topic", topic }
        };

        return await _toolService.InvokeToolAsync(
            "extractfacts", factParams, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Options for parallel tool execution.
/// </summary>
public class ParallelToolExecutorOptions
{
    /// <summary>
    /// Enable parallel execution of independent tools.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Maximum degree of parallelism (default: processor count).
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Timeout for individual parallel operations (in milliseconds).
    /// </summary>
    public int OperationTimeoutMs { get; set; } = 60000; // 60 seconds
}
