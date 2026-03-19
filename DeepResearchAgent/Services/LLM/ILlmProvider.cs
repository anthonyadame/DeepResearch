using DeepResearchAgent.Services;
using DeepResearchAgent.Configuration;

namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// Abstraction for LLM providers.
/// Enables switching between different LLM implementations (Ollama, LiteLLM, etc.).
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "ollama", "litellm").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the default model for this provider.
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Invoke the LLM with a list of chat messages.
    /// Returns the assistant's response as an OllamaChatMessage.
    /// </summary>
    /// <param name="messages">List of chat messages representing the conversation</param>
    /// <param name="model">Optional model override. Uses default if not specified.</param>
    /// <param name="tier">Optional model tier (Fast/Balanced/Power). Ignored if model is specified.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assistant's response message</returns>
    Task<OllamaChatMessage> InvokeAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream the LLM response as it's generated.
    /// Yields chunks of the response as they arrive.
    /// </summary>
    /// <param name="messages">List of chat messages representing the conversation</param>
    /// <param name="model">Optional model override. Uses default if not specified.</param>
    /// <param name="tier">Optional model tier (Fast/Balanced/Power). Ignored if model is specified.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> InvokeStreamingAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        CancellationToken cancellationToken = default);
}
