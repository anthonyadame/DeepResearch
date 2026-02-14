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
/// AIAgent adapter for ResearchBriefAgent.
/// Wraps ResearchBriefAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class ResearchBriefAgentAdapter : AgentAdapterBase
{
    private readonly ResearchBriefAgent _innerAgent;

    protected override string AgentName => "ResearchBriefAgent";

    public ResearchBriefAgentAdapter(
        OllamaService llmService,
        ILogger<ResearchBriefAgent>? logger = null)
    {
        _innerAgent = new ResearchBriefAgent(llmService, logger);
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

        var result = await _innerAgent.GenerateResearchBriefAsync(conversationHistory, cancellationToken);

        return result.ResearchBrief;
    }
}
