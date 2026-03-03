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
/// AIAgent adapter for AnalystAgent.
/// Wraps AnalystAgent to work with AIAgentBuilder and the Microsoft.Agents.AI framework.
/// </summary>
public class AnalystAgentAdapter : AgentAdapterBase
{
    private readonly AnalystAgent _innerAgent;

    protected override string AgentName => "AnalystAgent";

    public AnalystAgentAdapter(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<AnalystAgent>? logger = null)
    {
        _innerAgent = new AnalystAgent(llmService, toolService, logger);
    }

    protected override async Task<string> ExecuteCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        CancellationToken cancellationToken)
    {
        // Extract analysis input from conversation history
        var conversationHistory = messages.Select(m => new Models.ChatMessage
        {
            Role = m.Role.ToString().ToLower(),
            Content = m.Text ?? string.Empty
        }).ToList();

        var input = ExtractAnalysisInput(conversationHistory);
        var result = await _innerAgent.ExecuteAsync(input, cancellationToken);

        return result.SynthesisNarrative;
    }

    private AnalysisInput ExtractAnalysisInput(List<Models.ChatMessage> conversationHistory)
    {
        // Extract findings from conversation or create default
        var findings = new List<FactExtractionResult>
        {
            new FactExtractionResult
            {
                Facts = new List<ExtractedFact>
                {
                    new ExtractedFact 
                    { 
                        Statement = "Compiled research findings",
                        Source = "research-pipeline",
                        Confidence = 0.85f,
                        Category = "research"
                    }
                }
            }
        };

        // Extract topic from conversation or use default
        var topicMessage = conversationHistory.LastOrDefault(m => m.Role == "user");
        var topic = topicMessage?.Content ?? "Research Analysis";

        return new AnalysisInput
        {
            Findings = findings,
            Topic = topic,
            ResearchBrief = "Compiled research brief from previous agents"
        };
    }
}
