using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents.Adapters;

/// <summary>
/// AIAgent adapter for ResearcherAgent with streaming support.
/// Wraps ResearcherAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class ResearcherAgentAdapter : AgentAdapterBase
{
    private readonly ResearcherAgent _innerAgent;
    private readonly ILogger<ResearcherAgent>? _logger;

    protected override string AgentName => "ResearcherAgent";

    public ResearcherAgentAdapter(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<ResearcherAgent>? logger = null)
    {
        _innerAgent = new ResearcherAgent(llmService, toolService, logger);
        _logger = logger;
    }

    protected override async Task<string> ExecuteCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        CancellationToken cancellationToken)
    {
        // Extract research parameters from messages
        var topic = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        var researchBrief = topic; // Use topic as brief for now

        var input = new ResearchInput
        {
            Topic = topic,
            ResearchBrief = researchBrief,
            MaxIterations = 3,
            MinQualityThreshold = 7.0f
        };

        var result = await _innerAgent.ExecuteAsync(input, cancellationToken);

        return $"Research completed: {result.TotalFactsExtracted} facts extracted across {result.Findings.Count} findings. Quality: {result.AverageQuality:F2}/10";
    }

    /// <summary>
    /// Streaming implementation for ResearcherAgent.
    /// Yields progress updates as research progresses.
    /// </summary>
    protected override async IAsyncEnumerable<string> ExecuteCoreStreamingAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topic = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;

        yield return $"[ResearcherAgent] Starting research on: {topic}\n";

        var input = new ResearchInput
        {
            Topic = topic,
            ResearchBrief = topic,
            MaxIterations = 3,
            MinQualityThreshold = 7.0f
        };

        yield return "[ResearcherAgent] Planning research strategy...\n";

        // Execute research
        var result = await _innerAgent.ExecuteAsync(input, cancellationToken);

        yield return $"[ResearcherAgent] Research complete: {result.TotalFactsExtracted} facts extracted\n";
        yield return $"[ResearcherAgent] Quality: {result.AverageQuality:F2}/10\n";
        yield return $"[ResearcherAgent] Status: {result.CompletionStatus}\n";
    }
}
