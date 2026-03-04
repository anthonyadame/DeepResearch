using Microsoft.AspNetCore.Mvc;
using DeepResearch.Api.Services;
using DeepResearch.Api.Services.ChatHistory;
using DeepResearch.Api.DTOs;
using DeepResearch.Api.DTOs.Requests.Chat;
using DeepResearch.Api.DTOs.Responses.Chat;
using DeepResearchAgent.Models;
using System.ComponentModel.DataAnnotations;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Chat Controller - Manages chat sessions and messaging for the UI
/// </summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatHistoryService _historyService;
    private readonly ChatIntegrationService _integrationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatHistoryService historyService,
        ChatIntegrationService integrationService,
        ILogger<ChatController> logger)
    {
        _historyService = historyService;
        _integrationService = integrationService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new chat session
    /// </summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(ChatSession), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatSession>> CreateSession(
        [FromBody] CreateSessionRequest? request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new chat session with title: {Title}", request?.Title);

        try
        {
            var session = await _historyService.CreateSessionAsync(request?.Title, cancellationToken);
            _logger.LogInformation("Session created: Id={SessionId}, Title={Title}", 
                session.Id, session.Title);
            
            return Created($"/api/chat/sessions/{session.Id}", session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return BadRequest(new { error = "Failed to create session", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all chat sessions
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<ChatSession>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatSession>>> GetSessions(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessions = await _historyService.GetSessionsAsync(includeArchived, cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sessions");
            return BadRequest(new { error = "Failed to retrieve sessions", details = ex.Message });
        }
    }

    /// <summary>
    /// Get sessions by category
    /// </summary>
    [HttpGet("sessions/category/{category}")]
    [ProducesResponseType(typeof(List<ChatSession>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatSession>>> GetSessionsByCategory(
        string category,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessions = await _historyService.GetSessionsByCategoryAsync(category, cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sessions by category {Category}", category);
            return BadRequest(new { error = "Failed to retrieve sessions", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific chat session
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(ChatSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatSession>> GetSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _historyService.GetSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                return NotFound(new { error = $"Session {sessionId} not found" });
            }
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to retrieve session", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a chat session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyService.DeleteSessionAsync(sessionId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to delete session", details = ex.Message });
        }
    }

    /// <summary>
    /// Archive a chat session
    /// </summary>
    [HttpPost("sessions/{sessionId}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyService.ArchiveSessionAsync(sessionId, cancellationToken);
            return Ok(new { message = "Session archived successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to archive session", details = ex.Message });
        }
    }

    /// <summary>
    /// Unarchive a chat session
    /// </summary>
    [HttpPost("sessions/{sessionId}/unarchive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnarchiveSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyService.UnarchiveSessionAsync(sessionId, cancellationToken);
            return Ok(new { message = "Session unarchived successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to unarchive session", details = ex.Message });
        }
    }

    /// <summary>
    /// Auto-categorize a chat session using AI
    /// </summary>
    [HttpPost("sessions/{sessionId}/categorize")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string[]>> CategorizeSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _historyService.CategorizeSessionAsync(sessionId, cancellationToken);
            return Ok(categories);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error categorizing session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to categorize session", details = ex.Message });
        }
    }

    /// <summary>
    /// Update categories for a chat session
    /// </summary>
    [HttpPut("sessions/{sessionId}/categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategories(
        string sessionId,
        [FromBody] string[] categories,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyService.UpdateCategoriesAsync(sessionId, categories, cancellationToken);
            return Ok(new { message = "Categories updated successfully", categories });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating categories for session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to update categories", details = ex.Message });
        }
    }

    /// <summary>
    /// Get chat history statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ChatHistoryStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatHistoryStatistics>> GetStatistics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _historyService.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat statistics");
            return BadRequest(new { error = "Failed to retrieve statistics", details = ex.Message });
        }
    }

    /// <summary>
    /// Send a message in a chat session
    /// Returns immediately with the user message, starts workflow in background
    /// </summary>
    [HttpPost("sessions/{sessionId}/query")]
    [ProducesResponseType(typeof(DTOs.ChatMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DTOs.ChatMessage>> SendMessage(
        [Required] string sessionId,
        [FromBody][Required] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📥 [CHAT] Request received: POST /api/chat/sessions/{SessionId}/query", sessionId);
        _logger.LogInformation("Sending message to session: {SessionId}", sessionId);

        try
        {
            // Add user message to session
            var userMessage = new DTOs.ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow,
                Metadata = null
            };

            await _historyService.AddMessageAsync(sessionId, userMessage, cancellationToken);
            _logger.LogInformation("✅ [CHAT] User message stored");

            // Start workflow in background (don't wait for it)
            // The UI can stream results separately via WebSocket or polling
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("🔄 [WORKFLOW] Starting async workflow for session {SessionId}", sessionId);
                    var assistantResponse = await _integrationService.ProcessChatMessageAsync(
                        sessionId,
                        request.Message,
                        request.Config
                    );

                    // Store the assistant response when it completes
                    var assistantMessage = new DTOs.ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = "assistant",
                        Content = assistantResponse,
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["config"] = request.Config ?? new ResearchConfig()
                        }
                    };

                    await _historyService.AddMessageAsync(sessionId, assistantMessage, CancellationToken.None);
                    _logger.LogInformation("✅ [WORKFLOW] Workflow completed for session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [WORKFLOW] Error in background workflow for session {SessionId}", sessionId);
                    
                    // Store error message
                    var errorMessage = new DTOs.ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = "assistant",
                        Content = $"Error: {ex.Message}",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object> { ["error"] = true }
                    };
                    
                    await _historyService.AddMessageAsync(sessionId, errorMessage, CancellationToken.None);
                }
            }, CancellationToken.None);

            // Return immediately with user message
            _logger.LogInformation("✅ [CHAT] Returning user message immediately (workflow in background)");
            return Ok(userMessage);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("❌ [CHAT] Session {SessionId} not found", sessionId);
            return NotFound(new { error = "Session not found", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [CHAT] Error processing message for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to process message", details = ex.Message });
        }
    }

    /// <summary>
    /// Execute a single step of the research workflow (step-by-step mode).
    /// UI passes the current AgentState and gets back the updated state with formatted content.
    /// Each call executes exactly one step based on the state's current progress.
    /// 
    /// Flow:
    /// 1. User submits query → AgentState with NeedsQualityRepair=true → step 1 (clarify)
    /// 2. If clarification needed: return question, else set NeedsQualityRepair=false → step 2 (brief)
    /// 3. Step 2 → returns ResearchBrief
    /// 4. Step 3 → returns DraftReport
    /// 5. Step 4 → returns RawNotes (supervisor refinement)
    /// 6. Step 5 → returns FinalReport
    /// </summary>
    [HttpPost("step")]
    [ProducesResponseType(typeof(ChatStepResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatStepResponse>> ExecuteStep(
        [FromBody][Required] ChatStepRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing workflow step - NeedsQualityRepair: {NeedsQualityRepair}", 
            request.CurrentState.NeedsQualityRepair);

        try
        {
            var response = await _integrationService.ProcessChatStepAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for step execution");
            return BadRequest(new { error = "Invalid request", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow step");
            return StatusCode(500, new { error = "Failed to execute step", details = ex.Message });
        }
    }

    /// <summary>
    /// Get chat history for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/history")]
    [ProducesResponseType(typeof(List<DTOs.ChatMessage>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DTOs.ChatMessage>>> GetHistory(
        [Required] string sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📥 [CHAT] Request received: GET /api/chat/sessions/{SessionId}/history", sessionId);

        try
        {
            _logger.LogInformation("🔍 [CHAT] Fetching history for session: {SessionId}", sessionId);
            var history = await _historyService.GetHistoryAsync(sessionId, cancellationToken);
            
            _logger.LogInformation("✅ [CHAT] Found {Count} messages for session {SessionId}", 
                history.Count, sessionId);
            return Ok(history);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("❌ [CHAT] Session {SessionId} not found", sessionId);
            return NotFound(new { error = "Session not found", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [CHAT] Error retrieving history for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve chat history", details = ex.Message });
        }
    }
}
