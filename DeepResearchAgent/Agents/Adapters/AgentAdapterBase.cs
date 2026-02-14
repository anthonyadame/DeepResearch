using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepResearchAgent.Agents.Adapters;

/// <summary>
/// Base adapter class for wrapping existing agents to implement the AIAgent interface.
/// This allows our agents to work with the Microsoft.Agents.AI framework and AIAgentBuilder.
/// </summary>
public abstract class AgentAdapterBase : AIAgent
{
    protected abstract string AgentName { get; }
    
    /// <summary>
    /// Core execution logic to be implemented by derived adapters.
    /// </summary>
    protected abstract Task<string> ExecuteCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        CancellationToken cancellationToken);

    /// <summary>
    /// Optional streaming implementation.
    /// Default implementation converts non-streaming result to a single chunk.
    /// </summary>
    protected virtual async IAsyncEnumerable<string> ExecuteCoreStreamingAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var result = await ExecuteCoreAsync(messages, thread, cancellationToken);
        yield return result;
    }

    /// <summary>
    /// AIAgent implementation - delegates to ExecuteCoreAsync.
    /// </summary>
    protected override async Task<AgentRunResponse> RunCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCoreAsync(messages, thread, cancellationToken);
        
        return new AgentRunResponse
        {
            Messages = new[]
            {
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, result)
            }
        };
    }

    /// <summary>
    /// AIAgent streaming implementation - not fully implemented.
    /// NOTE: Streaming support will be enhanced once AgentRunResponseUpdate API stabilizes.
    /// For now, use non-streaming RunAsync method.
    /// </summary>
    protected override async IAsyncEnumerable<AgentRunResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Convert to non-streaming for now
        var response = await RunCoreAsync(messages, thread, options, cancellationToken);
        
        // Return a single update with the full response
        // TODO: Implement true streaming when AgentRunResponseUpdate constructor/factory is available
        yield return default!; // Placeholder - will be replaced when API is stable
    }

    /// <summary>
    /// Create a new thread for this agent.
    /// </summary>
    public override AgentThread GetNewThread()
    {
        // Return null - let the framework create the thread
        return null!;
    }

    /// <summary>
    /// Deserialize a thread from JSON.
    /// </summary>
    public override AgentThread DeserializeThread(
        JsonElement threadJson,
        JsonSerializerOptions? options = null)
    {
        // Return null - not implemented for now
        return null!;
    }
}
