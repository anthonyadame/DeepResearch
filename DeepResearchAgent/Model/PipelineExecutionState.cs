using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace DeepResearchAgent.Model;

/// <summary>
/// Represents the detailed execution state of an agent pipeline for checkpointing.
/// Captures message history, intermediate results, and agent progress.
/// </summary>
public class PipelineExecutionState
{
    /// <summary>Unique workflow identifier.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Type of workflow (e.g., "ResearchWorkflow", "AnalysisWorkflow").</summary>
    public string WorkflowType { get; set; } = string.Empty;

    /// <summary>Original user query that initiated the workflow.</summary>
    public string UserQuery { get; set; } = string.Empty;

    /// <summary>Current step/agent index in the pipeline (0-based).</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Name of the agent currently executing or next to execute.</summary>
    public string? CurrentAgentId { get; set; }

    /// <summary>List of agents that have completed execution.</summary>
    public List<string> CompletedAgents { get; set; } = new();

    /// <summary>Conversation history (chat messages).</summary>
    public List<ChatMessageState> Messages { get; set; } = new();

    /// <summary>Intermediate results from each agent.</summary>
    public Dictionary<string, string> AgentResults { get; set; } = new();

    /// <summary>Workflow start time (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Time when workflow was paused (UTC), null if not paused.</summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>Reason for pausing (e.g., "user-request", "error", "checkpoint").</summary>
    public string? PauseReason { get; set; }

    /// <summary>Whether the workflow is currently paused.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Additional metadata for the workflow.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Serializable representation of a chat message for checkpoint persistence.
/// </summary>
public class ChatMessageState
{
    /// <summary>Role of the message sender (User, Assistant, System, Tool).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Text content of the message.</summary>
    public string? Content { get; set; }

    /// <summary>Timestamp when message was created (UTC).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Agent ID that generated this message (for Assistant role).</summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Convert from Microsoft.Extensions.AI.ChatMessage to serializable state.
    /// </summary>
    public static ChatMessageState FromChatMessage(Microsoft.Extensions.AI.ChatMessage message, string? agentId = null)
    {
        return new ChatMessageState
        {
            Role = message.Role.ToString(),
            Content = message.Text,
            Timestamp = DateTime.UtcNow,
            AgentId = agentId
        };
    }

    /// <summary>
    /// Convert serializable state back to Microsoft.Extensions.AI.ChatMessage.
    /// </summary>
    public Microsoft.Extensions.AI.ChatMessage ToChatMessage()
    {
        var role = Role.ToLowerInvariant() switch
        {
            "user" => ChatRole.User,
            "assistant" => ChatRole.Assistant,
            "system" => ChatRole.System,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User
        };

        return new Microsoft.Extensions.AI.ChatMessage(role, Content ?? string.Empty);
    }
}
