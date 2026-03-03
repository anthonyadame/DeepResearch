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
/// AIAgent adapter for ReportAgent.
/// Wraps ReportAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class ReportAgentAdapter : AgentAdapterBase
{
    private readonly ReportAgent _innerAgent;

    protected override string AgentName => "ReportAgent";

    public ReportAgentAdapter(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<ReportAgent>? logger = null)
    {
        _innerAgent = new ReportAgent(llmService, toolService, logger);
    }

    protected override async Task<string> ExecuteCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        CancellationToken cancellationToken)
    {
        // Extract report input from conversation history
        var conversationHistory = messages.Select(m => new Models.ChatMessage
        {
            Role = m.Role.ToString().ToLower(),
            Content = m.Text ?? string.Empty
        }).ToList();

        var input = ExtractReportInput(conversationHistory);
        var result = await _innerAgent.ExecuteAsync(input, cancellationToken);

        return result.ExecutiveSummary;
    }

    private ReportInput ExtractReportInput(List<Models.ChatMessage> conversationHistory)
    {
        // Extract topic from conversation or use default
        var topicMessage = conversationHistory.LastOrDefault(m => m.Role == "user");
        var topic = topicMessage?.Content ?? "Research Report";

        return new ReportInput
        {
            Topic = topic,
            Research = new ResearchOutput { CompletionStatus = "complete" },
            Analysis = new AnalysisOutput { SynthesisNarrative = "Research analysis" }
        };
    }
}
