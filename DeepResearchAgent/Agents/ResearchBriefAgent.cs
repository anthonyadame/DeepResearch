using System.Globalization;
using System.Text.Json;
using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// ResearchBriefAgent: Transforms user intent into formal research brief.
/// 
/// Responsibility:
/// - Extract key research objectives from conversation
/// - Generate formal, unambiguous research brief
/// - Define scope constraints and boundaries
/// - Create the "guidance signal" for all research agents
/// 
/// Maps to Python's write_research_brief node (lines ~950 in rd-code.py)
/// </summary>
public class ResearchBriefAgent
{
    private readonly OllamaService _llmService;
    private readonly ILogger<ResearchBriefAgent>? _logger;

    protected virtual string AgentName => "ResearchBriefAgent";

    public ResearchBriefAgent(
        OllamaService llmService, 
        ILogger<ResearchBriefAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger;
    }

    /// <summary>
    /// Generate a formal research brief from conversation history.
    /// This brief is used as the guidance signal for all research work.
    /// </summary>
    public async Task<ResearchQuestion> GenerateResearchBriefAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var messagesText = FormatMessagesToString(conversationHistory);
            var currentDate = GetTodayString();
            
            var prompt = PromptTemplates.TransformMessagesIntoResearchTopicPrompt
                .Replace("{messages}", messagesText)
                .Replace("{date}", currentDate);
            
            _logger?.LogInformation("ResearchBriefAgent: Generating research brief from {MessageCount} messages", 
                conversationHistory.Count);
            
            var ollamaMessages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };
            
            // Primary path: expected schema
            ResearchQuestion? response = null;
            try
            {
                response = await _llmService.InvokeWithStructuredOutputAsync<ResearchQuestion>(
                    ollamaMessages,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Structured parse as ResearchQuestion failed; attempting fallback mapping");
            }

            if (response is not null)
            {
                _logger?.LogInformation(
                    "ResearchBriefAgent: Research brief generated with {ObjectiveCount} objectives",
                    response.Objectives?.Count ?? 0);
                
                return response;
            }

            // Fallback: map alternate shape like { question, scope:[], preferences:[] }
            var rawMessage = await _llmService.InvokeAsync(ollamaMessages, cancellationToken: cancellationToken);
            var rawJson = rawMessage.Content ?? "{}";

            // Strip accidental code fences
            if (rawJson.StartsWith("```"))
            {
                rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();
            }

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // Extract alternate fields
            var question = root.TryGetProperty("question", out var qEl) && qEl.ValueKind == JsonValueKind.String
                ? qEl.GetString()
                : null;

            // scope could be array or string
            string? scopeString = null;
            if (root.TryGetProperty("scope", out var scopeEl))
            {
                if (scopeEl.ValueKind == JsonValueKind.Array)
                {
                    var parts = scopeEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                    if (parts.Length > 0)
                        scopeString = string.Join("; ", parts);
                }
                else if (scopeEl.ValueKind == JsonValueKind.String)
                {
                    scopeString = scopeEl.GetString();
                }
            }

            // preferences can be appended to the brief as constraints
            string? prefsString = null;
            if (root.TryGetProperty("preferences", out var prefEl) && prefEl.ValueKind == JsonValueKind.Array)
            {
                var prefs = prefEl.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                if (prefs.Length > 0)
                    prefsString = "- Preferences: " + string.Join("; ", prefs);
            }

            // Build research_brief from available fields
            var briefParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(question))
                briefParts.Add(question!);
            if (!string.IsNullOrWhiteSpace(scopeString))
                briefParts.Add($"Scope: {scopeString}");
            if (!string.IsNullOrWhiteSpace(prefsString))
                briefParts.Add(prefsString!);

            var researchBrief = briefParts.Count > 0
                ? string.Join(" | ", briefParts)
                : "Formal research brief could not be extracted; proceed using conversation-derived context.";

            var normalized = new ResearchQuestion
            {
                ResearchBrief = researchBrief,
                Objectives = new List<string>
                {
                    "Define research scope and constraints from user intent",
                    "Identify key comparative dimensions and regulatory impacts",
                    "Plan sources prioritizing academic and official industry reports"
                },
                Scope = scopeString,
                CreatedAt = DateTime.UtcNow
            };

            _logger?.LogInformation(
                "ResearchBriefAgent: Fallback brief constructed with {ObjectiveCount} objectives",
                normalized.Objectives.Count);

            return normalized;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ResearchBriefAgent: Error generating research brief");
            throw new InvalidOperationException("Failed to generate research brief from conversation", ex);
        }
    }

    /// <summary>
    /// Format messages into a readable string representation for the LLM.
    /// Similar to Python's get_buffer_string().
    /// </summary>
    private static string FormatMessagesToString(List<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return "[No messages]";
        }

        var formattedMessages = new System.Text.StringBuilder();
        
        foreach (var msg in messages)
        {
            var roleLabel = msg.Role switch
            {
                "user" => "USER",
                "assistant" => "ASSISTANT",
                "system" => "SYSTEM",
                _ => msg.Role.ToUpper()
            };
            
            formattedMessages.AppendLine($"{roleLabel}: {msg.Content}");
            formattedMessages.AppendLine();
        }
        
        return formattedMessages.ToString().TrimEnd();
    }

    /// <summary>
    /// Get today's date in human-readable format.
    /// Similar to Python's get_today_str() function.
    /// </summary>
    private static string GetTodayString()
    {
        var today = DateTime.Now;
        // Format: "Mon Dec 25, 2024"
        return today.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
