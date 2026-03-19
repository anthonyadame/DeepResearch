using DeepResearchAgent.Observability;
using DeepResearchAgent.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// Ollama LLM provider implementation.
/// Provides chat functionality using local Ollama models via HTTP.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmProvider>? _logger;
    private readonly LlmModelTierConfiguration? _tierConfig;

    public string ProviderName => "ollama";
    public string DefaultModel => _defaultModel;

    public OllamaLlmProvider(
        string baseUrl,
        string defaultModel,
        HttpClient httpClient,
        ILogger<OllamaLlmProvider>? logger = null,
        LlmModelTierConfiguration? tierConfig = null)
    {
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        _tierConfig = tierConfig;
    }

    /// <summary>
    /// Invoke the LLM with a list of chat messages.
    /// Returns the assistant's response as an OllamaChatMessage.
    /// </summary>
    public async Task<OllamaChatMessage> InvokeAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        CancellationToken cancellationToken = default)
    {
        DiagnosticConfig.LlmRequestsCounter.Add(1);

        // Record tier-specific metrics if tier is enabled
        if (tier.HasValue && _tierConfig?.EnableTierMetrics == true)
        {
            var tierTag = new KeyValuePair<string, object?>("tier", tier.Value.ToString());
            DiagnosticConfig.LlmRequestsByTier.Add(1, tierTag);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var selectedModel = ResolveModel(model, tier);

            _logger?.LogDebug("[Ollama] Invoking LLM with {model} model (tier: {tier}) and {messageCount} messages", 
                selectedModel, tier?.ToString() ?? "none", messages.Count);

            // Build request for Ollama API
            var request = new
            {
                model = selectedModel,
                messages = messages.Select(m => new
                {
                    role = m.Role.ToLowerInvariant(),
                    content = m.Content
                }).ToList(),
                stream = false
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/chat",
                content,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("[Ollama] API error: {statusCode}", response.StatusCode);
                throw new HttpRequestException($"Ollama API returned {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonDocument.Parse(responseBody);

            var messageContent = jsonResponse.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "[No response from LLM]";

            _logger?.LogDebug("[Ollama] LLM response received: {length} characters", messageContent.Length);

            if (jsonResponse.RootElement.TryGetProperty("prompt_eval_count", out var promptTokensEl))
            {
                var promptTokens = promptTokensEl.GetInt32();
                var completionTokens = jsonResponse.RootElement.TryGetProperty("eval_count", out var evalEl)
                    ? evalEl.GetInt32() : 0;
                var totalTokens = promptTokens + completionTokens;

                DiagnosticConfig.LlmTokensPrompt.Add(promptTokens);
                DiagnosticConfig.LlmTokensCompletion.Add(completionTokens);
                DiagnosticConfig.LlmTokensUsed.Add(totalTokens);

                // Record tier-specific token metrics
                if (tier.HasValue && _tierConfig?.EnableTierMetrics == true)
                {
                    var tierTag = new KeyValuePair<string, object?>("tier", tier.Value.ToString());
                    DiagnosticConfig.LlmTokensByTier.Add(totalTokens, tierTag);
                }
            }

            return new OllamaChatMessage
            {
                Role = "assistant",
                Content = messageContent
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "[Ollama] HTTP error connecting to Ollama at {url}", _baseUrl);
            DiagnosticConfig.LlmErrors.Add(1);
            throw new InvalidOperationException(
                $"Failed to connect to Ollama at {_baseUrl}. Ensure Ollama is running. Error: {ex.Message}", 
                ex);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "[Ollama] Error parsing Ollama response as JSON");
            DiagnosticConfig.LlmErrors.Add(1);
            throw new InvalidOperationException(
                "Failed to parse response from Ollama. Check that the model is installed.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Ollama] Error invoking Ollama LLM");
            DiagnosticConfig.LlmErrors.Add(1);
            throw;
        }
        finally
        {
            var duration = sw.Elapsed.TotalMilliseconds;
            DiagnosticConfig.LlmRequestDuration.Record(duration);

            // Record tier-specific duration metrics
            if (tier.HasValue && _tierConfig?.EnableTierMetrics == true)
            {
                var tierTag = new KeyValuePair<string, object?>("tier", tier.Value.ToString());
                DiagnosticConfig.LlmRequestDurationByTier.Record(duration, tierTag);
            }
        }
    }

    /// <summary>
    /// Stream the LLM response as it's generated.
    /// Yields chunks of the response as they arrive.
    /// </summary>
    public async IAsyncEnumerable<string> InvokeStreamingAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedModel = ResolveModel(model, tier);

        _logger?.LogDebug("[Ollama] Starting streaming LLM invocation with {model} model (tier: {tier})", 
            selectedModel, tier?.ToString() ?? "none");

        // Build request for Ollama API
        var request = new
        {
            model = selectedModel,
            messages = messages.Select(m => new
            {
                role = m.Role.ToLowerInvariant(),
                content = m.Content
            }).ToList(),
            stream = true
        };

        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        // Make the request and handle errors
        try
        {
            response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/chat",
                content,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Ollama API returned {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "[Ollama] HTTP error in streaming LLM");
            DiagnosticConfig.LlmErrors.Add(1);
            throw new InvalidOperationException(
                $"Failed to stream from Ollama at {_baseUrl}. Error: {ex.Message}", 
                ex);
        }

        // Stream processing - outside try-catch block to allow yield
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var jsonResponse = JsonDocument.Parse(line);
            if (!jsonResponse.RootElement.TryGetProperty("message", out var messageElement))
                continue;

            if (!messageElement.TryGetProperty("content", out var contentElement))
                continue;

            var chunk = contentElement.GetString();
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }

        _logger?.LogDebug("[Ollama] Streaming LLM response completed");
    }

    /// <summary>
    /// Check if Ollama is running and accessible.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("[Ollama] Health check: connecting to Ollama");

            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/tags",
                cancellationToken
            );

            var isHealthy = response.IsSuccessStatusCode;
            _logger?.LogInformation("[Ollama] Health check: {status}", 
                isHealthy ? "Healthy" : $"HTTP {response.StatusCode}");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Ollama] Health check failed");
            return false;
        }
    }

    /// <summary>
    /// Resolve model name from explicit model or tier configuration.
    /// Priority: explicit model > tier selection > default model
    /// </summary>
    private string ResolveModel(string? model, LlmModelTier? tier)
    {
        // Priority 1: Explicit model override
        if (!string.IsNullOrWhiteSpace(model))
            return model;

        // Priority 2: Tier-based selection (if enabled and configured)
        if (tier.HasValue && _tierConfig != null && _tierConfig.EnableTierSelection)
        {
            return _tierConfig.GetModelForTier(tier.Value);
        }

        // Priority 3: Default model
        return _defaultModel;
    }
}
