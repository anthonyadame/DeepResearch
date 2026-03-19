using System.Text.Json;
using System.Text.Json.Serialization;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Caching;
using DeepResearchAgent.Configuration;

namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// Extension methods for ILlmProvider
/// </summary>
public static class LlmProviderExtensions
{
    /// <summary>
    /// Invoke the LLM with structured output expectation and optional caching.
    /// Attempts to parse the response as JSON matching the provided schema.
    /// Caches successful results if cache is provided.
    /// </summary>
    public static async Task<T> InvokeWithStructuredOutputAsync<T>(
        this ILlmProvider llmProvider,
        List<OllamaChatMessage> messages,
        string? model = null,
        LlmModelTier? tier = null,
        LlmResponseCache? cache = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        // Check cache first if provided
        if (cache != null && messages.Count > 0)
        {
            var firstUserMessage = messages.FirstOrDefault(m => m.Role?.ToLowerInvariant() == "user")?.Content ?? "";
            if (!string.IsNullOrWhiteSpace(firstUserMessage))
            {
                var tierStr = tier?.ToString();
                if (cache.TryGetCached<T>(firstUserMessage, model, tierStr, out var cached) && cached != null)
                {
                    return cached;
                }
            }
        }

        // Add instruction to return JSON
        var instructionMessage = new OllamaChatMessage
        {
            Role = "system",
            Content = $"You must respond with valid JSON only. No markdown code blocks, just raw JSON. The JSON should be deserializable to a {typeof(T).Name} object."
        };

        var messagesWithInstruction = new List<OllamaChatMessage> { instructionMessage };
        messagesWithInstruction.AddRange(messages);

        if (!messagesWithInstruction.Where(x => x.Role.ToLowerInvariant() == "user").Any())
        {
            var usermsg = new OllamaChatMessage
            {
                Role = "user",
                Content = "..."
            };

            messagesWithInstruction.Add(usermsg);
        }

        var responseContent = string.Empty;

        try 
        {
            var response = await llmProvider.InvokeAsync(messagesWithInstruction, model, tier, cancellationToken);
            responseContent = response.Content ?? "{}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"LLM invocation failed: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(responseContent) || responseContent == "{}")
        { 
            throw new InvalidOperationException($"LLM response is empty or invalid for type {typeof(T).Name}");
        }

        // Remove markdown code blocks if present
        if (responseContent.Contains("```json"))
        {
            responseContent = responseContent.Replace("```json", "").Replace("```", "").Trim();
        }
        else if (responseContent.StartsWith("```"))
        {
            responseContent = responseContent.Replace("```", "").Trim();
        }
        else if (responseContent.EndsWith("```"))
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
            throw new InvalidOperationException(
                $"Failed to parse LLM response as {typeof(T).Name}");
        }

        // Cache the result if cache is provided
        if (cache != null && messages.Count > 0)
        {
            var firstUserMessage = messages.FirstOrDefault(m => m.Role?.ToLowerInvariant() == "user")?.Content ?? "";
            if (!string.IsNullOrWhiteSpace(firstUserMessage))
            {
                var tierStr = tier?.ToString();
                cache.CacheResponse<T>(firstUserMessage, model, tierStr, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Check if the LLM provider is healthy and can respond to requests.
    /// </summary>
    public static async Task<bool> IsHealthyAsync(
        this ILlmProvider llmProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testMessages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = "Reply with 'OK'" }
            };

            var response = await llmProvider.InvokeAsync(testMessages, cancellationToken: cancellationToken);
            return !string.IsNullOrWhiteSpace(response.Content);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the list of available models from the provider.
    /// For now, returns a default list based on the provider type.
    /// </summary>
    public static Task<List<string>> GetAvailableModelsAsync(
        this ILlmProvider llmProvider,
        CancellationToken cancellationToken = default)
    {
        // Return common models based on provider name
        var models = llmProvider.ProviderName.ToLowerInvariant() switch
        {
            "ollama" => new List<string>
            {
                "gpt-oss:20b",
                "llama3.1:8b",
                "llama2:13b",
                "mistral:7b",
                "mixtral:8x7b"
            },
            "litellm" => new List<string>
            {
                "qwen3.5-2b",
                "qwen3.5-4b",
                "gpt-3.5-turbo",
                "gpt-4"
            },
            _ => new List<string> { llmProvider.DefaultModel }
        };

        return Task.FromResult(models);
    }
}
