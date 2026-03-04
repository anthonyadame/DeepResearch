using DeepResearchAgent.Services;

namespace DeepResearch.Api.Services.ChatHistory;

/// <summary>
/// AI-powered categorization service using Ollama for chat session classification
/// </summary>
public interface ICategorizationService
{
    Task<string[]> CategorizeAsync(string title, string[] messageSnippets, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of AI-based categorization using Ollama
/// </summary>
public class OllamaCategorizationService : ICategorizationService
{
    private readonly OllamaService _ollama;
    private readonly ILogger<OllamaCategorizationService> _logger;
    
    private const string CategorizationPrompt = @"Analyze this chat conversation and suggest 2-3 relevant category tags.
Tags should be:
- Short (1-2 words)
- Lowercase
- Descriptive of the main topic
- Use hyphens for multi-word tags

Conversation Title: {0}

Recent Messages:
{1}

Respond with ONLY the tags separated by commas. Example: web-development, api-design, authentication";

    public OllamaCategorizationService(
        OllamaService ollama,
        ILogger<OllamaCategorizationService> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<string[]> CategorizeAsync(
        string title,
        string[] messageSnippets,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snippets = string.Join("\n", messageSnippets.Select((msg, idx) => $"{idx + 1}. {msg}"));
            var prompt = string.Format(CategorizationPrompt, title, snippets);
            
            _logger.LogInformation("Categorizing conversation: {Title}", title);
            
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = prompt }
            };
            
            var response = await _ollama.InvokeAsync(
                messages,
                cancellationToken: cancellationToken
            );

            var categories = ParseCategories(response.Content);
            _logger.LogInformation("Generated categories for '{Title}': {Categories}", 
                title, string.Join(", ", categories));
            
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to categorize conversation: {Title}", title);
            // Fallback to basic categorization
            return new[] { "general" };
        }
    }

    private static string[] ParseCategories(string response)
    {
        var categories = response
            .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Where(c => c.Length > 2 && c.Length < 30) // Sanity check
            .Take(3) // Maximum 3 categories
            .ToArray();

        return categories.Length > 0 ? categories : new[] { "general" };
    }
}
