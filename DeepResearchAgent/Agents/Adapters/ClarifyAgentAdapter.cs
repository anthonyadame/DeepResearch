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
/// AIAgent adapter for ClarifyAgent.
/// Wraps ClarifyAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class ClarifyAgentAdapter : AgentAdapterBase
{
    private readonly ClarifyAgent _innerAgent;

    protected override string AgentName => "ClarifyAgent";

    public ClarifyAgentAdapter(
        OllamaService llmService,
        ILogger<ClarifyAgent>? logger = null)
    {
        _innerAgent = new ClarifyAgent(llmService, logger);
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

        var result = await _innerAgent.ClarifyAsync(conversationHistory, cancellationToken);

        return result.NeedClarification ? result.Question : result.Verification;
    }
}
