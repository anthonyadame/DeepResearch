using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Agents.Adapters;
using DeepResearchAgent.Agents.Middleware;
using DeepResearchAgent.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// Service class demonstrating AIAgent integration with adapters and middleware.
/// Provides production-ready agent instances with logging, timing, and retry capabilities.
/// </summary>
public class AgentPipelineService
{
    private readonly ILogger<AgentPipelineService> _logger;
    
    public AgentPipelineService(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<AgentPipelineService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Build agents with middleware
        ClarifyAgent = BuildClarifyAgent(llmService);
        ResearchBriefAgent = BuildResearchBriefAgent(llmService);
        ResearcherAgent = BuildResearcherAgent(llmService, toolService);
    }
    
    public AIAgent ClarifyAgent { get; }
    public AIAgent ResearchBriefAgent { get; }
    public AIAgent ResearcherAgent { get; }
    
    /// <summary>
    /// Build ClarifyAgent with middleware pipeline:
    /// Logging → Timing → Retry
    /// </summary>
    private AIAgent BuildClarifyAgent(OllamaService llmService)
    {
        var baseAgent = new ClarifyAgentAdapter(llmService);
        
        // Add logging middleware
        var withLogging = new LoggingAgentMiddleware(baseAgent, _logger);
        
        // Add timing middleware (warn if > 5 seconds)
        var withTiming = new TimingAgentMiddleware(
            withLogging, 
            _logger, 
            TimeSpan.FromSeconds(5));
        
        // Add retry middleware (max 2 attempts)
        var withRetry = new RetryAgentMiddleware(
            withTiming, 
            maxAttempts: 2, 
            logger: _logger);
        
        return withRetry;
    }
    
    /// <summary>
    /// Build ResearchBriefAgent with middleware pipeline:
    /// Logging → Timing
    /// </summary>
    private AIAgent BuildResearchBriefAgent(OllamaService llmService)
    {
        var baseAgent = new ResearchBriefAgentAdapter(llmService);
        
        var withLogging = new LoggingAgentMiddleware(baseAgent, _logger);
        
        var withTiming = new TimingAgentMiddleware(
            withLogging, 
            _logger, 
            TimeSpan.FromSeconds(10));
        
        return withTiming;
    }
    
    /// <summary>
    /// Build ResearcherAgent with middleware pipeline:
    /// Logging → Timing (60s threshold) → Retry (3 attempts)
    /// </summary>
    private AIAgent BuildResearcherAgent(OllamaService llmService, ToolInvocationService toolService)
    {
        var baseAgent = new ResearcherAgentAdapter(llmService, toolService);
        
        var withLogging = new LoggingAgentMiddleware(baseAgent, _logger);
        
        var withTiming = new TimingAgentMiddleware(
            withLogging, 
            _logger, 
            TimeSpan.FromSeconds(60));
        
        var withRetry = new RetryAgentMiddleware(
            withTiming, 
            maxAttempts: 3, 
            logger: _logger);
        
        return withRetry;
    }
    
    /// <summary>
    /// Execute complete research workflow using AIAgent pipeline.
    /// </summary>
    public async Task<string> ExecuteResearchWorkflowAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting research workflow for query: {Query}", userQuery);
        
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, userQuery)
        };
        
        // Step 1: Clarify intent
        _logger.LogInformation("Step 1: Clarifying user intent");
        var clarifyResponse = await ClarifyAgent.RunAsync(messages, cancellationToken: cancellationToken);
        var clarification = clarifyResponse.Messages[0].Text ?? string.Empty;
        
        if (clarification.Contains("Clarification needed", StringComparison.OrdinalIgnoreCase))
        {
            return clarification;
        }
        
        // Step 2: Generate research brief
        _logger.LogInformation("Step 2: Generating research brief");
        var briefResponse = await ResearchBriefAgent.RunAsync(messages, cancellationToken: cancellationToken);
        var brief = briefResponse.Messages[0].Text ?? string.Empty;
        
        // Step 3: Conduct research
        _logger.LogInformation("Step 3: Conducting research");
        var researchResponse = await ResearcherAgent.RunAsync(messages, cancellationToken: cancellationToken);
        var research = researchResponse.Messages[0].Text ?? string.Empty;
        
        _logger.LogInformation("Research workflow completed successfully");
        return research;
    }
    
    /// <summary>
    /// Execute research workflow with streaming support.
    /// TODO: Improve streaming once AgentRunResponseUpdate API is stable
    /// </summary>
    public async IAsyncEnumerable<string> ExecuteResearchWorkflowStreamingAsync(
        string userQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return $"[Workflow] Starting research on: {userQuery}\n";
        
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, userQuery)
        };
        
        // Stream research progress
        await foreach (var update in ResearcherAgent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            // For now, just yield a status message
            yield return "[Update received]\n";
        }
        
        yield return "[Workflow] Research workflow complete\n";
    }
}
