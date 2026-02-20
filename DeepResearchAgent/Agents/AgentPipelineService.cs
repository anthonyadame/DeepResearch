using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Agents.Adapters;
using DeepResearchAgent.Agents.Middleware;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// Service class demonstrating AIAgent integration with adapters and middleware.
/// Provides production-ready agent instances with logging, timing, and retry capabilities.
/// Now includes checkpoint-based pause/resume functionality.
/// </summary>
public class AgentPipelineService
{
    private readonly ILogger<AgentPipelineService> _logger;
    private readonly ICheckpointService? _checkpointService;
    private readonly Dictionary<string, PipelineExecutionState> _activeWorkflows = new();
    private readonly object _workflowLock = new();
    
    public AgentPipelineService(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<AgentPipelineService> logger,
        ICheckpointService? checkpointService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkpointService = checkpointService;
        
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
    /// Supports checkpoint-based pause/resume functionality.
    /// </summary>
    public async Task<string> ExecuteResearchWorkflowAsync(
        string userQuery,
        string? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        // Generate workflow ID if not provided
        workflowId ??= GenerateWorkflowId();
        
        _logger.LogInformation("Starting research workflow {WorkflowId} for query: {Query}", workflowId, userQuery);
        
        // Initialize workflow state
        var state = new PipelineExecutionState
        {
            WorkflowId = workflowId,
            WorkflowType = "ResearchWorkflow",
            UserQuery = userQuery,
            StartedAt = DateTime.UtcNow,
            CurrentStepIndex = 0
        };
        
        TrackWorkflow(state);
        
        try
        {
            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.User, userQuery)
            };
            state.Messages.Add(ChatMessageState.FromChatMessage(messages[0]));
            
            // Step 1: Clarify intent
            state.CurrentStepIndex = 0;
            state.CurrentAgentId = "ClarifyAgent";
            await SaveCheckpointAsync(state, "before-clarify-agent", cancellationToken);
            
            _logger.LogInformation("Step 1: Clarifying user intent");
            var clarifyResponse = await ClarifyAgent.RunAsync(messages, cancellationToken: cancellationToken);
            var clarification = clarifyResponse.Messages[0].Text ?? string.Empty;
            
            state.CompletedAgents.Add("ClarifyAgent");
            state.AgentResults["ClarifyAgent"] = clarification;
            state.Messages.Add(ChatMessageState.FromChatMessage(clarifyResponse.Messages[0], "ClarifyAgent"));
            await SaveCheckpointAsync(state, "after-clarify-agent", cancellationToken);
            
            if (clarification.Contains("Clarification needed", StringComparison.OrdinalIgnoreCase))
            {
                return clarification;
            }
            
            // Step 2: Generate research brief
            state.CurrentStepIndex = 1;
            state.CurrentAgentId = "ResearchBriefAgent";
            await SaveCheckpointAsync(state, "before-research-brief-agent", cancellationToken);
            
            _logger.LogInformation("Step 2: Generating research brief");
            var briefResponse = await ResearchBriefAgent.RunAsync(messages, cancellationToken: cancellationToken);
            var brief = briefResponse.Messages[0].Text ?? string.Empty;
            
            state.CompletedAgents.Add("ResearchBriefAgent");
            state.AgentResults["ResearchBriefAgent"] = brief;
            state.Messages.Add(ChatMessageState.FromChatMessage(briefResponse.Messages[0], "ResearchBriefAgent"));
            await SaveCheckpointAsync(state, "after-research-brief-agent", cancellationToken);
            
            // Step 3: Conduct research
            state.CurrentStepIndex = 2;
            state.CurrentAgentId = "ResearcherAgent";
            await SaveCheckpointAsync(state, "before-researcher-agent", cancellationToken);
            
            _logger.LogInformation("Step 3: Conducting research");
            var researchResponse = await ResearcherAgent.RunAsync(messages, cancellationToken: cancellationToken);
            var research = researchResponse.Messages[0].Text ?? string.Empty;
            
            state.CompletedAgents.Add("ResearcherAgent");
            state.AgentResults["ResearcherAgent"] = research;
            state.Messages.Add(ChatMessageState.FromChatMessage(researchResponse.Messages[0], "ResearcherAgent"));
            await SaveCheckpointAsync(state, "workflow-complete", cancellationToken);
            
            _logger.LogInformation("Research workflow {WorkflowId} completed successfully", workflowId);
            UntrackWorkflow(workflowId);
            
            return research;
        }
        catch (WorkflowPauseException ex)
        {
            _logger.LogInformation("Workflow {WorkflowId} paused at checkpoint {CheckpointId}: {Reason}", 
                ex.WorkflowId, ex.CheckpointId, ex.PauseReason);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in research workflow {WorkflowId}", workflowId);
            
            // Save error checkpoint for recovery
            if (state != null)
            {
                await SaveCheckpointAsync(state, $"error-recovery: {ex.Message}", cancellationToken);
            }
            
            throw;
        }
    }

    /// <summary>
    /// Pause an active workflow and save checkpoint.
    /// </summary>
    public async Task<string> PauseWorkflowAsync(
        string workflowId,
        string reason = "user-request",
        CancellationToken cancellationToken = default)
    {
        if (_checkpointService == null)
        {
            throw new InvalidOperationException("CheckpointService is not configured. Cannot pause workflow.");
        }

        var state = GetWorkflowState(workflowId);
        if (state == null)
        {
            throw new InvalidOperationException($"Workflow '{workflowId}' not found or not active.");
        }

        state.IsPaused = true;
        state.PausedAt = DateTime.UtcNow;
        state.PauseReason = reason;

        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: workflowId,
            workflowType: state.WorkflowType,
            agentId: state.CurrentAgentId,
            stepIndex: state.CurrentStepIndex,
            stateSnapshot: JsonSerializer.Serialize(state),
            metadata: new CheckpointMetadata
            {
                IsAutomated = false,
                Reason = $"pause: {reason}",
                UserId = null,
                CompletedAgents = state.CompletedAgents,
                Context = new Dictionary<string, string>
                {
                    { "pausedAt", state.PausedAt.Value.ToString("O") },
                    { "currentAgent", state.CurrentAgentId ?? "none" },
                    { "stepIndex", state.CurrentStepIndex.ToString() }
                }
            },
            ct: cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} paused at checkpoint {CheckpointId}. Reason: {Reason}",
            workflowId, checkpoint.CheckpointId, reason);

        return checkpoint.CheckpointId;
    }

    /// <summary>
    /// Resume a paused workflow from checkpoint.
    /// </summary>
    public async Task<string> ResumeWorkflowAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        if (_checkpointService == null)
        {
            throw new InvalidOperationException("CheckpointService is not configured. Cannot resume workflow.");
        }

        _logger.LogInformation("Resuming workflow from checkpoint {CheckpointId}", checkpointId);

        var checkpoint = await _checkpointService.LoadCheckpointAsync(checkpointId, cancellationToken);
        if (checkpoint == null)
        {
            throw new InvalidOperationException($"Checkpoint '{checkpointId}' not found.");
        }

        var state = JsonSerializer.Deserialize<PipelineExecutionState>(checkpoint.StateSnapshot!);
        if (state == null)
        {
            throw new InvalidOperationException($"Failed to deserialize workflow state from checkpoint '{checkpointId}'.");
        }

        state.IsPaused = false;
        state.PausedAt = null;
        state.PauseReason = null;

        _logger.LogInformation("Resuming workflow {WorkflowId} from step {StepIndex}, agent: {AgentId}",
            state.WorkflowId, state.CurrentStepIndex, state.CurrentAgentId);

        TrackWorkflow(state);

        // Reconstruct message history
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
        foreach (var msgState in state.Messages)
        {
            messages.Add(msgState.ToChatMessage());
        }

        // Resume from current step
        try
        {
            switch (state.CurrentStepIndex)
            {
                case 0: // Resume from ClarifyAgent
                    if (!state.CompletedAgents.Contains("ClarifyAgent"))
                    {
                        var clarifyResponse = await ClarifyAgent.RunAsync(messages, cancellationToken: cancellationToken);
                        state.CompletedAgents.Add("ClarifyAgent");
                        state.AgentResults["ClarifyAgent"] = clarifyResponse.Messages[0].Text ?? string.Empty;
                        state.Messages.Add(ChatMessageState.FromChatMessage(clarifyResponse.Messages[0], "ClarifyAgent"));
                        await SaveCheckpointAsync(state, "resumed-after-clarify", cancellationToken);
                    }
                    goto case 1;

                case 1: // Resume from ResearchBriefAgent
                    if (!state.CompletedAgents.Contains("ResearchBriefAgent"))
                    {
                        state.CurrentStepIndex = 1;
                        state.CurrentAgentId = "ResearchBriefAgent";
                        var briefResponse = await ResearchBriefAgent.RunAsync(messages, cancellationToken: cancellationToken);
                        state.CompletedAgents.Add("ResearchBriefAgent");
                        state.AgentResults["ResearchBriefAgent"] = briefResponse.Messages[0].Text ?? string.Empty;
                        state.Messages.Add(ChatMessageState.FromChatMessage(briefResponse.Messages[0], "ResearchBriefAgent"));
                        await SaveCheckpointAsync(state, "resumed-after-brief", cancellationToken);
                    }
                    goto case 2;

                case 2: // Resume from ResearcherAgent
                    if (!state.CompletedAgents.Contains("ResearcherAgent"))
                    {
                        state.CurrentStepIndex = 2;
                        state.CurrentAgentId = "ResearcherAgent";
                        var researchResponse = await ResearcherAgent.RunAsync(messages, cancellationToken: cancellationToken);
                        state.CompletedAgents.Add("ResearcherAgent");
                        state.AgentResults["ResearcherAgent"] = researchResponse.Messages[0].Text ?? string.Empty;
                        state.Messages.Add(ChatMessageState.FromChatMessage(researchResponse.Messages[0], "ResearcherAgent"));
                        await SaveCheckpointAsync(state, "resumed-workflow-complete", cancellationToken);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Invalid step index: {state.CurrentStepIndex}");
            }

            _logger.LogInformation("Workflow {WorkflowId} resumed and completed successfully", state.WorkflowId);
            UntrackWorkflow(state.WorkflowId);

            // Return final research result
            return state.AgentResults.GetValueOrDefault("ResearcherAgent") ?? "Research completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow {WorkflowId} from checkpoint {CheckpointId}",
                state.WorkflowId, checkpointId);
            await SaveCheckpointAsync(state, $"resume-error: {ex.Message}", cancellationToken);
            throw;
        }
    }

    // ==================== Private Helper Methods ====================

    private async Task SaveCheckpointAsync(
        PipelineExecutionState state,
        string reason,
        CancellationToken cancellationToken)
    {
        if (_checkpointService == null || !ShouldSaveCheckpoint(reason))
        {
            return;
        }

        try
        {
            await _checkpointService.SaveCheckpointAsync(
                workflowId: state.WorkflowId,
                workflowType: state.WorkflowType,
                agentId: state.CurrentAgentId,
                stepIndex: state.CurrentStepIndex,
                stateSnapshot: JsonSerializer.Serialize(state),
                metadata: new CheckpointMetadata
                {
                    IsAutomated = true,
                    Reason = reason,
                    CompletedAgents = state.CompletedAgents
                },
                ct: cancellationToken);

            _logger.LogDebug("Checkpoint saved for workflow {WorkflowId}: {Reason}",
                state.WorkflowId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save checkpoint for workflow {WorkflowId}: {Reason}",
                state.WorkflowId, reason);
            // Don't fail workflow execution if checkpoint fails
        }
    }

    private bool ShouldSaveCheckpoint(string reason)
    {
        // Save checkpoints before/after agents and on pause
        return reason.Contains("before-") ||
               reason.Contains("after-") ||
               reason.Contains("pause") ||
               reason.Contains("error") ||
               reason.Contains("complete");
    }

    private string GenerateWorkflowId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var randomSuffix = Guid.NewGuid().ToString("N")[..8];
        return $"wf_{timestamp}_{randomSuffix}";
    }

    private void TrackWorkflow(PipelineExecutionState state)
    {
        lock (_workflowLock)
        {
            _activeWorkflows[state.WorkflowId] = state;
        }
    }

    private void UntrackWorkflow(string workflowId)
    {
        lock (_workflowLock)
        {
            _activeWorkflows.Remove(workflowId);
        }
    }

    private PipelineExecutionState? GetWorkflowState(string workflowId)
    {
        lock (_workflowLock)
        {
            _activeWorkflows.TryGetValue(workflowId, out var state);
            return state;
        }
    }
}
