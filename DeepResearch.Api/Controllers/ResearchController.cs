using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Research API Controller - Main entry point for research operations.
/// Provides endpoints for executing research pipelines and querying results.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ResearchController : ControllerBase
{
    private readonly MasterWorkflow _masterWorkflow;
    private readonly ResearcherAgent _researcherAgent;
    private readonly AnalystAgent _analystAgent;
    private readonly ReportAgent _reportAgent;
    private readonly StateTransitioner _transitioner;
    private readonly AgentErrorRecovery _errorRecovery;
    private readonly ILightningStateService _stateService;
    private readonly ILogger<ResearchController> _logger;

    public ResearchController(
        MasterWorkflow masterWorkflow,
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        StateTransitioner transitioner,
        AgentErrorRecovery errorRecovery,
        ILightningStateService stateService,
        ILogger<ResearchController> logger)
    {
        _masterWorkflow = masterWorkflow;
        _researcherAgent = researcherAgent;
        _analystAgent = analystAgent;
        _reportAgent = reportAgent;
        _transitioner = transitioner;
        _errorRecovery = errorRecovery;
        _stateService = stateService;
        _logger = logger;
    }

    /// <summary>
    /// Execute complete research pipeline.
    /// </summary>
    /// <param name="request">Research request with topic and brief</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete research report</returns>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(ResearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResearchResponse>> ExecuteResearch(
        [FromBody] ResearchRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing research for topic: {Topic}", request.Topic);

        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid request",
                    Details = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            // Execute pipeline
            var report = await _masterWorkflow.ExecuteFullPipelineAsync(
                _researcherAgent,
                _analystAgent,
                _reportAgent,
                _transitioner,
                _errorRecovery,
                request.Topic,
                request.ResearchBrief,
                _logger,
                cancellationToken
            );

            // Create response
            var response = new ResearchResponse
            {
                ResearchId = Guid.NewGuid().ToString(),
                Topic = request.Topic,
                Report = report,
                Status = "completed",
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Research completed for topic: {Topic}", request.Topic);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Research execution failed for topic: {Topic}", request.Topic);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Research execution failed",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Execute research with state persistence.
    /// </summary>
    /// <param name="request">Research request with topic and brief</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Research response with state ID</returns>
    [HttpPost("execute-with-state")]
    [ProducesResponseType(typeof(ResearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResearchResponse>> ExecuteResearchWithState(
        [FromBody] ResearchRequest request,
        CancellationToken cancellationToken)
    {
        var researchId = request.ResearchId ?? Guid.NewGuid().ToString();
        _logger.LogInformation("Executing research with state, ID: {ResearchId}", researchId);

        try
        {
            var report = await _masterWorkflow.ExecuteFullPipelineWithStateAsync(
                _researcherAgent,
                _analystAgent,
                _reportAgent,
                _transitioner,
                _errorRecovery,
                _stateService,
                request.Topic,
                request.ResearchBrief,
                researchId,
                _logger,
                cancellationToken
            );

            var response = new ResearchResponse
            {
                ResearchId = researchId,
                Topic = request.Topic,
                Report = report,
                Status = "completed",
                CreatedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Research execution failed, ID: {ResearchId}", researchId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Research execution failed",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get research state by ID.
    /// </summary>
    /// <param name="researchId">Research ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Research state</returns>
    [HttpGet("state/{researchId}")]
    [ProducesResponseType(typeof(ResearchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResearchStateResponse>> GetResearchState(
        string researchId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting research state for ID: {ResearchId}", researchId);

        try
        {
            var state = await _stateService.GetResearchStateAsync(researchId, cancellationToken);

            if (state == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "Research not found",
                    Details = new List<string> { $"No research found with ID: {researchId}" }
                });
            }

            var response = new ResearchStateResponse
            {
                ResearchId = state.ResearchId,
                Query = state.Query,
                Status = state.Status.ToString(),
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                IterationCount = state.IterationCount,
                Metadata = state.Metadata
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get research state for ID: {ResearchId}", researchId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to retrieve research state",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Validate research topic.
    /// </summary>
    /// <param name="topic">Topic to validate</param>
    /// <returns>Validation result</returns>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
    public ActionResult<ValidationResponse> ValidateTopic([FromQuery] string topic)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(topic))
        {
            errors.Add("Topic cannot be empty");
        }
        else if (topic.Length > 200)
        {
            errors.Add("Topic too long (max 200 characters)");
        }
        else if (topic.Length < 3)
        {
            errors.Add("Topic too short (min 3 characters)");
        }

        var response = new ValidationResponse
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };

        return Ok(response);
    }
}
