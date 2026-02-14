using System.Globalization;
using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// ClarifyAgent: Gatekeeper for research process initiation.
/// 
/// Responsibility:
/// - Analyze the conversation history to determine if the user's request is sufficiently detailed
/// - If clarification is needed, ask a focused question
/// - If sufficient detail exists, confirm and move to research brief generation
/// 
/// Maps to Python's clarify_with_user node (lines 870-920 in rd-code.py)
/// </summary>
public class ClarifyAgent
{
    protected readonly OllamaService _llmService;
    protected readonly ILogger<ClarifyAgent>? _logger;

    protected virtual string AgentName => "ClarifyAgent";

    public ClarifyAgent(
        OllamaService llmService, 
        ILogger<ClarifyAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger;
    }

    /// <summary>
    /// Analyze messages and determine if clarification is needed.
    /// 
    /// Returns:
    /// - need_clarification=true, question="..." → User must respond before proceeding
    /// - need_clarification=false, verification="..." → Ready to proceed to research brief
    /// </summary>
    public async Task<ClarificationResult> ClarifyAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messagesText = FormatMessagesToString(conversationHistory);
            var currentDate = GetTodayString();
            
            var prompt = PromptTemplates.ClarifyWithUserInstructions
                .Replace("{messages}", messagesText)
                .Replace("{date}", currentDate);
            
            _logger?.LogInformation("ClarifyAgent: Analyzing {MessageCount} messages for clarification need", 
                conversationHistory.Count);
            
            // Convert ChatMessage to OllamaChatMessage for the service
            var ollamaMessages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };
            
            var response = await _llmService.InvokeWithStructuredOutputAsync<ClarificationResult>(
                ollamaMessages, 
                cancellationToken: cancellationToken);
            
            _logger?.LogInformation(
                "ClarifyAgent: Decision made - NeedClarification={NeedClarification}", 
                response.NeedClarification);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ClarifyAgent: Error during clarification analysis");
            throw new InvalidOperationException("Failed to analyze user intent for clarification", ex);
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
