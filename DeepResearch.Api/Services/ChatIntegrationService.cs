using DeepResearchAgent.Agents;
using DeepResearch.Api.DTOs;
using DeepResearch.Api.DTOs.Requests.Chat;
using DeepResearch.Api.DTOs.Responses.Chat;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using System.Diagnostics;
using ChatMessageDto = DeepResearch.Api.DTOs.ChatMessage;
using ChatMessageModel = DeepResearchAgent.Models.ChatMessage;

namespace DeepResearch.Api.Services;

/// <summary>
/// Service that integrates chat messages with the research workflow
/// </summary>
public class ChatIntegrationService
{
    private readonly MasterWorkflow _masterWorkflow;
    private readonly ResearcherAgent _researcherAgent;
    private readonly AnalystAgent _analystAgent;
    private readonly ReportAgent _reportAgent;
    private readonly StateTransitioner _transitioner;
    private readonly AgentErrorRecovery _errorRecovery;
    private readonly ILogger<ChatIntegrationService> _logger;

    public ChatIntegrationService(
        MasterWorkflow masterWorkflow,
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        StateTransitioner transitioner,
        AgentErrorRecovery errorRecovery,
        ILogger<ChatIntegrationService> logger)
    {
        _masterWorkflow = masterWorkflow;
        _researcherAgent = researcherAgent;
        _analystAgent = analystAgent;
        _reportAgent = reportAgent;
        _transitioner = transitioner;
        _errorRecovery = errorRecovery;
        _logger = logger;
    }

    /// <summary>
    /// Process a single step of the research workflow.
    /// The UI controls step progression by calling this method with the current AgentState.
    /// Each call executes exactly one step and returns the updated state.
    /// </summary>
    public async Task<ChatStepResponse> ProcessChatStepAsync(
        ChatStepRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== ProcessChatStepAsync ENTRY ===");
        _logger.LogInformation("Processing chat step - NeedsQualityRepair: {NeedsQualityRepair}", 
            request.CurrentState.NeedsQualityRepair);
        _logger.LogInformation("Messages count: {Count}", request.CurrentState.Messages?.Count ?? 0);
        _logger.LogInformation("First message: {Content}", request.CurrentState.Messages?.FirstOrDefault()?.Content ?? "NONE");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Handle clarification response: if user provided a response, update the state
            if (!string.IsNullOrEmpty(request.UserResponse) && request.CurrentState.NeedsQualityRepair)
            {
                _logger.LogInformation("Processing clarification response from user: {Response}", request.UserResponse);
                
                // Update the first message with the clarification
                if (request.CurrentState.Messages.Any())
                {
                    request.CurrentState.Messages[0] = new ChatMessageModel
                    {
                        Role = "user",
                        Content = request.UserResponse,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    request.CurrentState.Messages.Add(new ChatMessageModel
                    {
                        Role = "user",
                        Content = request.UserResponse,
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Clear the repair flag so next step processes normally
                request.CurrentState.NeedsQualityRepair = false;
            }

            // Execute the step
            _logger.LogInformation("=== CALLING MasterWorkflow.ExecuteByStepAsync ===");
            var updatedState = await _masterWorkflow.ExecuteByStepAsync(request.CurrentState, cancellationToken);
            _logger.LogInformation("=== MasterWorkflow.ExecuteByStepAsync RETURNED ===");
            _logger.LogInformation("Updated state - NeedsQualityRepair: {NeedsQualityRepair}", updatedState.NeedsQualityRepair);
            _logger.LogInformation("Updated state - ResearchBrief: {ResearchBrief}", updatedState.ResearchBrief);
            _logger.LogInformation("Updated state - DraftReport: {DraftReport}", updatedState.DraftReport);
            _logger.LogInformation("Updated state - FinalReport: {FinalReport}", updatedState.FinalReport);

            stopwatch.Stop();

            // Determine which step just completed and format response
            var (currentStep, displayContent, statusMessage) = FormatStepResponse(updatedState);
            var isClarificationNeeded = updatedState.NeedsQualityRepair && updatedState.ResearchBrief?.Contains("Clarification needed:") == true;
            var clarificationQuestion = isClarificationNeeded ? ExtractClarificationQuestion(updatedState.ResearchBrief) : null;

            return new ChatStepResponse
            {
                // Map all AgentState properties
                Messages = ConvertMessages(updatedState.Messages),
                ResearchBrief = updatedState.ResearchBrief,
                SupervisorMessages = ConvertMessages(updatedState.SupervisorMessages),
                RawNotes = updatedState.RawNotes,
                Notes = updatedState.Notes,
                DraftReport = updatedState.DraftReport,
                FinalReport = updatedState.FinalReport,
                SupervisorState = updatedState.SupervisorState,
                NeedsQualityRepair = updatedState.NeedsQualityRepair,
                
                // UI metadata
                CurrentStep = currentStep,
                ClarificationQuestion = clarificationQuestion,
                IsComplete = !string.IsNullOrEmpty(updatedState.FinalReport),
                StatusMessage = statusMessage,
                DisplayContent = displayContent,
                Metrics = new Dictionary<string, object>
                {
                    { "duration_ms", stopwatch.ElapsedMilliseconds },
                    { "content_length", displayContent.Length }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat step");
            stopwatch.Stop();
            
            return new ChatStepResponse
            {
                // Map current state (unchanged)
                Messages = ConvertMessages(request.CurrentState.Messages),
                ResearchBrief = request.CurrentState.ResearchBrief,
                SupervisorMessages = ConvertMessages(request.CurrentState.SupervisorMessages),
                RawNotes = request.CurrentState.RawNotes,
                Notes = request.CurrentState.Notes,
                DraftReport = request.CurrentState.DraftReport,
                FinalReport = request.CurrentState.FinalReport,
                SupervisorState = request.CurrentState.SupervisorState,
                NeedsQualityRepair = request.CurrentState.NeedsQualityRepair,
                
                // Error metadata
                CurrentStep = 0,
                DisplayContent = $"Error: {ex.Message}",
                StatusMessage = "An error occurred during processing. Please try again.",
                IsComplete = false,
                Metrics = new Dictionary<string, object> 
                { 
                    { "error", ex.Message },
                    { "duration_ms", stopwatch.ElapsedMilliseconds }
                }
            };
        }
    }

    /// <summary>
    /// Convert Models.ChatMessage list to DTOs.ChatMessage list
    /// </summary>
    private List<ChatMessageDto> ConvertMessages(List<ChatMessageModel> messages)
    {
        if (messages == null) return new List<ChatMessageDto>();
        
        return messages.Select(m => new ChatMessageDto
        {
            Id = Guid.NewGuid().ToString(),
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp == default ? DateTime.UtcNow : m.Timestamp,
            Metadata = null
        }).ToList();
    }

    /// <summary>
    /// Determine which step was just completed and format the response content.
    /// Returns: (step number, display content, status message)
    /// </summary>
    private (int Step, string DisplayContent, string StatusMessage) FormatStepResponse(AgentState state)
    {
        // Determine which step just completed based on what properties are filled
        if (state.NeedsQualityRepair && state.ResearchBrief?.Contains("Clarification needed:") == true)
        {
            // Step 1: Clarification needed
            return (1, state.ResearchBrief, "Clarification required");
        }

        if (!string.IsNullOrEmpty(state.ResearchBrief) && string.IsNullOrEmpty(state.DraftReport))
        {
            // Step 2: Research brief generated
            var preview = TruncateForDisplay(state.ResearchBrief, 250);
            return (2, preview, "Research brief generated. Click 'Continue' to generate the initial draft.");
        }

        if (!string.IsNullOrEmpty(state.DraftReport) && (state.SupervisorMessages == null || !state.SupervisorMessages.Any()))
        {
            // Step 3: Draft report generated
            var preview = TruncateForDisplay(state.DraftReport, 250);
            return (3, preview, "Initial draft generated. Click 'Continue' to refine findings with the supervisor.");
        }

        if (state.SupervisorMessages?.Any() == true && string.IsNullOrEmpty(state.FinalReport))
        {
            // Step 4: Supervisor refinement complete
            var rawNotesPreview = state.RawNotes.Any() 
                ? $"Refined findings: {string.Join("; ", state.RawNotes.Take(2))}"
                : "Findings refined through supervisor loop";
            return (4, rawNotesPreview, "Findings refined. Click 'Continue' to generate the final polished report.");
        }

        if (!string.IsNullOrEmpty(state.FinalReport))
        {
            // Step 5: Final report generated
            return (5, state.FinalReport, "Research complete! Your final report is ready.");
        }

        return (0, "Starting workflow...", "Initializing research");
    }

    /// <summary>
    /// Extract the clarification question from the ResearchBrief message.
    /// Message format: "Clarification needed: {question}"
    /// </summary>
    private string ExtractClarificationQuestion(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        
        const string prefix = "Clarification needed: ";
        if (message.StartsWith(prefix))
        {
            return message.Substring(prefix.Length);
        }

        return message;
    }

    /// <summary>
    /// Truncate content for display, adding ellipsis if truncated.
    /// </summary>
    private string TruncateForDisplay(string content, int maxLength = 250)
    {
        if (string.IsNullOrEmpty(content)) return "";
        
        if (content.Length <= maxLength)
            return content;

        return content.Substring(0, maxLength).TrimEnd() + "...";
    }

    /// <summary>
    /// Process a chat message through the research workflow (full execution).
    /// Used by the /chat/{sessionId}/query endpoint for complete research execution.
    /// </summary>
    public async Task<string> ProcessChatMessageAsync(
        string sessionId,
        string userMessage,
        ResearchConfig? config)
    {
        _logger.LogInformation("Processing chat message for session {SessionId}", sessionId);

        try
        {
            // Execute the simple 5-step master workflow (not the complex agent pipeline)
            var finalReport = await _masterWorkflow.RunAsync(
                userMessage,
                CancellationToken.None
            );

            _logger.LogInformation("Research completed for session {SessionId}", sessionId);

            return finalReport;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message for session {SessionId}", sessionId);
            
            // Return error message to user
            return $"I encountered an error while processing your request: {ex.Message}. " +
                   "Please try rephrasing your question or contact support if the issue persists.";
        }
    }

    /// <summary>
    /// Format the research report for chat display
    /// </summary>
    private string FormatResponseForChat(ReportOutput report, string originalQuery)
    {
        var response = new System.Text.StringBuilder();
        
        response.AppendLine($"# {report.Title}");
        response.AppendLine();
        response.AppendLine($"**Your Query:** {originalQuery}");
        response.AppendLine();
        response.AppendLine("---");
        response.AppendLine();
        
        // Executive Summary
        response.AppendLine("## Executive Summary");
        response.AppendLine(report.ExecutiveSummary);
        response.AppendLine();
        
        // Sections
        foreach (var section in report.Sections)
        {
            response.AppendLine($"## {section.Heading}");
            response.AppendLine(section.Content);
            response.AppendLine();
        }
        
        // Citations
        if (report.Citations != null && report.Citations.Any())
        {
            response.AppendLine("## References");
            foreach (var citation in report.Citations)
            {
                response.AppendLine($"[{citation.Index}] {citation.Source}");
                if (!string.IsNullOrEmpty(citation.Url))
                {
                    response.AppendLine($"   URL: {citation.Url}");
                }
            }
            response.AppendLine();
        }
        
        // Quality Score
        response.AppendLine($"*Quality Score: {report.QualityScore:F2}/10*");
        
        return response.ToString();
    }
}
