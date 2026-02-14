using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services;

/// <summary>
/// Represents a chat message in a conversation.
/// </summary>
public class OllamaChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// Service wrapper for LLM integration with Ollama.
/// Provides chat functionality using local Ollama models via HTTP.
/// </summary>
public class OllamaService
{
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService>? _logger;

    public string DefaultModel => _defaultModel;

    public OllamaService(
        string baseUrl = "http://localhost:11434",
        string defaultModel = "gpt-oss:20b",
        HttpClient? httpClient = null,
        ILogger<OllamaService>? logger = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
    }

    /// <summary>
    /// Invoke the LLM with a list of chat messages.
    /// Returns the assistant's response as an OllamaChatMessage.
    /// </summary>
    public async Task<OllamaChatMessage> InvokeAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedModel = model ?? _defaultModel;

            _logger?.LogDebug("Invoking LLM with {model} model and {messageCount} messages", 
                selectedModel, messages.Count);

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
                _logger?.LogError("Ollama API error: {statusCode}", response.StatusCode);
                throw new HttpRequestException($"Ollama API returned {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonDocument.Parse(responseBody);
            
            var messageContent = jsonResponse.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "[No response from LLM]";

            _logger?.LogDebug("LLM response received: {length} characters", messageContent.Length);

            return new OllamaChatMessage
            {
                Role = "assistant",
                Content = messageContent
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error connecting to Ollama at {url}", _baseUrl);
            throw new InvalidOperationException(
                $"Failed to connect to Ollama at {_baseUrl}. Ensure Ollama is running. Error: {ex.Message}", 
                ex);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Error parsing Ollama response as JSON");
            throw new InvalidOperationException(
                "Failed to parse response from Ollama. Check that the model is installed.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error invoking Ollama LLM");
            throw;
        }
    }

    /// <summary>
    /// Stream the LLM response as it's generated.
    /// Yields chunks of the response as they arrive.
    /// </summary>
    public async IAsyncEnumerable<string> InvokeStreamingAsync(
        List<OllamaChatMessage> messages,
        string? model = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedModel = model ?? _defaultModel;

        _logger?.LogDebug("Starting streaming LLM invocation with {model} model", selectedModel);

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

        HttpResponseMessage response = null;
        
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
            _logger?.LogError(ex, "HTTP error in streaming LLM");
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

        _logger?.LogDebug("Streaming LLM response completed");
    }

    /// <summary>
    /// Invoke the LLM with structured output expectation.
    /// Attempts to parse the response as JSON matching the provided schema.
    /// </summary>
    public async Task<T> InvokeWithStructuredOutputAsync<T>(
        List<OllamaChatMessage> messages,
        string? model = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            // Add instruction to return JSON
            var instructionMessage = new OllamaChatMessage
            {
                Role = "system",
                Content = $"You must respond with valid JSON only. No markdown code blocks, just raw JSON. The JSON should be deserializable to a {typeof(T).Name} object."
            };

            var messagesWithInstruction = new List<OllamaChatMessage> { instructionMessage };
            messagesWithInstruction.AddRange(messages);

            var response = await InvokeAsync(messagesWithInstruction, model, cancellationToken);

            // Try to parse as JSON
            var responseContent = response.Content ?? "{}";
            
            // Remove markdown code blocks if present
            if (responseContent.StartsWith("```json"))
            {
                responseContent = responseContent.Replace("```json", "").Replace("```", "").Trim();
            }
            else if (responseContent.StartsWith("```"))
            {
                responseContent = responseContent.Replace("```", "").Trim();
            }

            var result = JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                Converters = { new JsonStringEnumConverter() }
            });

            if (result == null)
            {
                _logger?.LogWarning("Failed to deserialize LLM response to {type}: {content}", 
                    typeof(T).Name, responseContent);
                throw new InvalidOperationException(
                    $"Failed to parse LLM response as {typeof(T).Name}");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON parsing error in structured output");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in structured output invocation");
            throw;
        }
    }

    /// <summary>
    /// Check if Ollama is running and accessible.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Health check: connecting to Ollama");
            
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/tags",
                cancellationToken
            );
            
            var isHealthy = response.IsSuccessStatusCode;
            _logger?.LogInformation("Ollama health check: {status}", 
                isHealthy ? "Healthy" : $"HTTP {response.StatusCode}");
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ollama health check failed");
            return false;
        }
    }

    /// <summary>
    /// Get list of available models on the Ollama server.
    /// </summary>
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Fetching available models from Ollama");
            
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/tags",
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to fetch models: HTTP {status}", response.StatusCode);
                return new List<string>();
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonDocument.Parse(responseBody);
            
            var models = jsonResponse.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString())
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .ToList();
            
            _logger?.LogInformation("Found {count} available models", models.Count);
            
            return models;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching available models");
            return new List<string>();
        }
    }
}
