namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// Configuration options for LLM providers
/// </summary>
public class LlmProviderOptions
{
    /// <summary>
    /// Gets or sets the default LLM provider to use (e.g., "ollama", "litellm")
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// Gets or sets the request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Ollama provider configuration
    /// </summary>
    public OllamaProviderConfig Ollama { get; set; } = new();

    /// <summary>
    /// LiteLLM provider configuration
    /// </summary>
    public LiteLlmProviderConfig LiteLLM { get; set; } = new();
}

/// <summary>
/// Ollama-specific configuration
/// </summary>
public class OllamaProviderConfig
{
    /// <summary>
    /// Gets or sets the Ollama API base URL
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the default model to use
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-oss:20b";
}

/// <summary>
/// LiteLLM-specific configuration
/// </summary>
public class LiteLlmProviderConfig
{
    /// <summary>
    /// Gets or sets the LiteLLM proxy base URL
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:4000";

    /// <summary>
    /// Gets or sets the default model to use
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4";

    /// <summary>
    /// Gets or sets the API key (if required)
    /// </summary>
    public string? ApiKey { get; set; }
}
