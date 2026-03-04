using DeepResearch.Api.Models.Chat;
using DeepResearch.Api.Services.Chat;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Chat Controller - Manages chat sessions and messaging for the WebUI
/// Does not require authentication for easier WebUI integration
/// </summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatHistoryService _historyService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatHistoryService historyService,
        ILogger<ChatController> logger)
    {
        _historyService = historyService;
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
    /// Send a message in a chat session
    /// Returns immediately with echo message (simplified for WebUI)
    /// </summary>
    [HttpPost("sessions/{sessionId}/query")]
    [ProducesResponseType(typeof(ChatMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessage>> SendMessage(
        [Required] string sessionId,
        [FromBody][Required] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending message to session: {SessionId}", sessionId);

        try
        {
            // Add user message to session
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow,
                Metadata = null
            };

            await _historyService.AddMessageAsync(sessionId, userMessage, cancellationToken);
            _logger.LogInformation("User message stored for session {SessionId}", sessionId);

            // For now, return a simple acknowledgment
            // In a full implementation, this would trigger workflow processing
            var assistantMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "assistant",
                Content = "Message received. Workflow processing would happen here in a full implementation.",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { ["placeholder"] = true }
            };

            await _historyService.AddMessageAsync(sessionId, assistantMessage, cancellationToken);

            return Ok(userMessage);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to process message", details = ex.Message });
        }
    }

    /// <summary>
    /// Stream a query response (Server-Sent Events)
    /// </summary>
    [HttpPost("sessions/{sessionId}/stream")]
    [Produces("text/event-stream")]
    public async Task StreamQuery(
        [Required] string sessionId,
        [FromBody][Required] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        try
        {
            // Add user message
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow,
                Metadata = null
            };

            await _historyService.AddMessageAsync(sessionId, userMessage, cancellationToken);

            // Simulate streaming response
            var responseText = $"Processing your query: {request.Message}";
            var chunks = responseText.Split(' ');

            foreach (var chunk in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await Response.WriteAsync($"data: {chunk} \n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(100, cancellationToken); // Simulate processing
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming for session {SessionId}", sessionId);
            await Response.WriteAsync($"data: {{\"error\": \"{ex.Message}\"}}\n\n", cancellationToken);
        }
    }

    /// <summary>
    /// Get chat history for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/history")]
    [ProducesResponseType(typeof(List<ChatMessage>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChatMessage>>> GetHistory(
        [Required] string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _historyService.GetHistoryAsync(sessionId, cancellationToken);
            return Ok(history);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve chat history", details = ex.Message });
        }
    }

    /// <summary>
    /// Update session configuration
    /// </summary>
    [HttpPut("sessions/{sessionId}/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfig(
        [Required] string sessionId,
        [FromBody][Required] ResearchConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyService.UpdateSessionConfigAsync(sessionId, config, cancellationToken);
            return Ok(new { message = "Configuration updated successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating config for session {SessionId}", sessionId);
            return BadRequest(new { error = "Failed to update configuration", details = ex.Message });
        }
    }
}
