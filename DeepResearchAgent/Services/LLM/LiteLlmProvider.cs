using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepResearchAgent.Configuration;

namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// LiteLLM provider implementation.
/// Provides chat functionality using LiteLLM proxy for multi-provider LLM support.
/// LiteLLM supports OpenAI-compatible API format.
/// </summary>
public class LiteLlmProvider : ILlmProvider
{
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiteLlmProvider>? _logger;

    public string ProviderName => "litellm";
    public string DefaultModel => _defaultModel;

    public LiteLlmProvider(
        string baseUrl,
        string defaultModel,
        HttpClient httpClient,
        string? apiKey = null,
        ILogger<LiteLlmProvider>? logger = null)
    {
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey;
        _logger = logger;
    }

    /// <summary>
    /// Invoke the LLM with a list of chat messages.
    /// Uses OpenAI-compatible API format that LiteLLM supports.
    /// </summary>
    public async Task<OllamaChatMessage> InvokeAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedModel = model ?? _defaultModel;

            _logger?.LogDebug("[LiteLLM] Invoking LLM with {model} model (tier: {tier}) and {messageCount} messages", 
                selectedModel, tier?.ToString() ?? "none", messages.Count);

            // Build request for LiteLLM (OpenAI-compatible format)
            if (!messages.Where(x => x.Role.ToLowerInvariant() == "user").Any())
            {
                var usermsg = new OllamaChatMessage
                {
                    Role = "user",
                    Content = "..."
                };

                messages.Add(usermsg);
            }


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

            // Add authorization header if API key is provided
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(_apiKey))
            {
                requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
            }

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("[LiteLLM] API error: {statusCode}, Body: {errorBody}", 
                    response.StatusCode, errorBody);
                throw new HttpRequestException($"LiteLLM API returned {response.StatusCode}: {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonDocument.Parse(responseBody);

            // Parse OpenAI-compatible response format
            var messageContent = jsonResponse.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "[No response from LLM]";

            _logger?.LogDebug("[LiteLLM] LLM response received: {length} characters", messageContent.Length);

            return new OllamaChatMessage
            {
                Role = "assistant",
                Content = messageContent
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "[LiteLLM] HTTP error connecting to LiteLLM at {url}", _baseUrl);
            throw new InvalidOperationException(
                $"Failed to connect to LiteLLM at {_baseUrl}. Ensure LiteLLM proxy is running. Error: {ex.Message}", 
                ex);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "[LiteLLM] Error parsing LiteLLM response as JSON");
            throw new InvalidOperationException(
                "Failed to parse response from LiteLLM. Check the response format.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[LiteLLM] Error invoking LiteLLM");
            throw;
        }
    }

    /// <summary>
    /// Stream the LLM response as it's generated.
    /// Uses OpenAI-compatible streaming format.
    /// </summary>
    public async IAsyncEnumerable<string> InvokeStreamingAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedModel = model ?? _defaultModel;

        _logger?.LogDebug("[LiteLLM] Starting streaming LLM invocation with {model} model (tier: {tier})", 
            selectedModel, tier?.ToString() ?? "none");

        // Build request for LiteLLM (OpenAI-compatible format)
        if (!messages.Where(x => x.Role.ToLowerInvariant() == "user").Any())
        {
            var usermsg = new OllamaChatMessage
            {
                Role = "user",
                Content = "..."
            };

            messages.Add(usermsg);
        }

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

        // Add authorization header if API key is provided
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
        }

        HttpResponseMessage response;

        // Make the request and handle errors
        try
        {
            response = await _httpClient.SendAsync(
                requestMessage, 
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"LiteLLM API returned {response.StatusCode}: {errorBody}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "[LiteLLM] HTTP error in streaming LLM");
            throw new InvalidOperationException(
                $"Failed to stream from LiteLLM at {_baseUrl}. Error: {ex.Message}", 
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

            // OpenAI streaming format: "data: {json}"
            if (!line.StartsWith("data: "))
                continue;

            var jsonData = line.Substring(6); // Remove "data: " prefix

            // Check for stream end marker
            if (jsonData.Trim() == "[DONE]")
                break;

            // Parse JSON outside try-catch to allow yield
            JsonDocument? jsonResponse = null;
            try
            {
                jsonResponse = JsonDocument.Parse(jsonData);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "[LiteLLM] Error parsing streaming chunk: {line}", line);
                continue; // Skip to next line on parse error
            }

            // Parse OpenAI streaming chunk format
            if (jsonResponse.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentElement))
                {
                    var chunk = contentElement.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk;
                    }
                }
            }
        }

        _logger?.LogDebug("[LiteLLM] Streaming LLM response completed");
    }

    /// <summary>
    /// Check if LiteLLM proxy is running and accessible.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("[LiteLLM] Health check: connecting to LiteLLM");

            // LiteLLM typically has a /health endpoint
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/health",
                cancellationToken
            );

            var isHealthy = response.IsSuccessStatusCode;
            _logger?.LogInformation("[LiteLLM] Health check: {status}", 
                isHealthy ? "Healthy" : $"HTTP {response.StatusCode}");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[LiteLLM] Health check failed");
            return false;
        }
    }
}
