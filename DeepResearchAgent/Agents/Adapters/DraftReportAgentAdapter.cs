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
/// AIAgent adapter for DraftReportAgent.
/// Wraps DraftReportAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class DraftReportAgentAdapter : AgentAdapterBase
{
    private readonly DraftReportAgent _innerAgent;

    protected override string AgentName => "DraftReportAgent";

    public DraftReportAgentAdapter(
        OllamaService llmService,
        ILogger<DraftReportAgent>? logger = null)
    {
        _innerAgent = new DraftReportAgent(llmService, logger);
    }

    protected override async Task<string> ExecuteCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        CancellationToken cancellationToken)
    {
        // Convert Microsoft.Extensions.AI.ChatMessage to DeepResearchAgent.Models.ChatMessage
        var conversationHistory = messages.Select(m => new Models.ChatMessage
        {
            Role = m.Role.ToString().ToLower(),
            Content = m.Text ?? string.Empty
        }).ToList();

        // Extract research brief from the last user message or thread context
        var researchBrief = ExtractResearchBrief(conversationHistory);

        var result = await _innerAgent.GenerateDraftReportAsync(researchBrief, conversationHistory, cancellationToken);

        return result.Content;
    }

    private string ExtractResearchBrief(List<Models.ChatMessage> conversationHistory)
    {
        // Try to find a research brief in the conversation history
        var researchBriefMessage = conversationHistory
            .FirstOrDefault(m => m.Role == "assistant" && m.Content.Contains("Research Brief"));
        
        return researchBriefMessage?.Content ?? "Research Brief: Analyze the provided research findings.";
    }
}
