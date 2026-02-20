using DeepResearchAgent.Model;
using DeepResearchAgent.Model.Api;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using DeepResearch.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// API controller for managing long-running workflow execution.
/// Provides endpoints for starting, monitoring, pausing, resuming, and cancelling workflows.
/// Requires JWT authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowPauseResumeService _pauseResumeService;
    private readonly ICheckpointService _checkpointService;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        IWorkflowPauseResumeService pauseResumeService,
        ICheckpointService checkpointService,
        ILogger<WorkflowsController> logger)
    {
        _pauseResumeService = pauseResumeService ?? throw new ArgumentNullException(nameof(pauseResumeService));
        _checkpointService = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start a new workflow execution.
    /// </summary>
    /// <param name="request">Workflow start request (type, input, config).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Started workflow information.</returns>
    /// <response code="200">Workflow started successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartWorkflowResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    public async Task<IActionResult> StartWorkflow(
        [FromBody] StartWorkflowRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // TODO: Implement workflow instantiation logic
            // This is a skeleton; actual implementation depends on workflow factory/registry

            var workflowId = $"wf_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}".Substring(0, 40);

            await _pauseResumeService.TransitionWorkflowStateAsync(
                workflowId,
                WorkflowState.Queued,
                "workflow-started",
                ct);

            var response = new StartWorkflowResponseDto
            {
                WorkflowId = workflowId,
                Status = "Queued",
                CreatedAt = DateTime.UtcNow,
                Message = $"Workflow {request.WorkflowType} started with ID {workflowId}"
            };

            _logger.LogInformation("Workflow started: {WorkflowId} (type: {WorkflowType})", workflowId, request.WorkflowType);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow");
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to start workflow",
                Details = new() { { "error", ex.Message } }
            });
        }
    }

    /// <summary>
    /// Get the current status of a workflow.
    /// </summary>
    /// <param name="id">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current workflow status.</returns>
    /// <response code="200">Status retrieved successfully.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(WorkflowStatusDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 404)]
    public async Task<IActionResult> GetWorkflowStatus(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var executionState = await _pauseResumeService.GetWorkflowStateAsync(id, ct);

            var dto = new WorkflowStatusDto
            {
                WorkflowId = id,
                Status = executionState.State.ToString(),
                CreatedAt = DateTime.UtcNow, // TODO: retrieve actual creation time
                StartedAt = executionState.StartedAt,
                Progress = new ProgressDto
                {
                    CurrentStep = executionState.CurrentStepIndex,
                    TotalSteps = 10, // TODO: get actual total steps
                    CurrentAgent = executionState.CurrentAgentId,
                    ElapsedSeconds = (int)executionState.ElapsedTime.TotalSeconds,
                    EstimatedRemainingSeconds = null // TODO: calculate based on historical data
                },
                LatestCheckpointId = executionState.LatestCheckpointId
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow status for {WorkflowId}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to retrieve workflow status",
                Details = new() { { "workflowId", id } }
            });
        }
    }

    /// <summary>
    /// Pause a running workflow at the next safe checkpoint.
    /// </summary>
    /// <param name="id">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pause operation result.</returns>
    /// <response code="200">Pause request accepted.</response>
    /// <response code="404">Workflow not found.</response>
    /// <response code="409">Cannot pause workflow in current state.</response>
    [HttpPut("{id}/pause")]
    [ProducesResponseType(typeof(WorkflowActionResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 409)]
    public async Task<IActionResult> PauseWorkflow(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var currentState = await _pauseResumeService.GetWorkflowStateAsync(id, ct);

            if (currentState.State != WorkflowState.Running)
                return Conflict(new ErrorResponseDto
                {
                    StatusCode = 409,
                    Message = $"Cannot pause workflow in {currentState.State} state"
                });

            await _pauseResumeService.RequestPauseAsync(id, ct);

            var response = new WorkflowActionResponseDto
            {
                WorkflowId = id,
                Action = "pause",
                Success = true,
                Message = "Pause request accepted; workflow will pause at next checkpoint",
                Status = "Pausing",
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Pause requested for workflow {WorkflowId}", id);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to pause workflow",
                Details = new() { { "workflowId", id } }
            });
        }
    }

    /// <summary>
    /// Resume a paused workflow from its latest checkpoint.
    /// </summary>
    /// <param name="id">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resume operation result.</returns>
    /// <response code="200">Resume request accepted.</response>
    /// <response code="404">Workflow or checkpoint not found.</response>
    /// <response code="409">Cannot resume workflow in current state.</response>
    [HttpPut("{id}/resume")]
    [ProducesResponseType(typeof(WorkflowActionResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 409)]
    public async Task<IActionResult> ResumeWorkflow(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var currentState = await _pauseResumeService.GetWorkflowStateAsync(id, ct);

            if (currentState.State != WorkflowState.Paused)
                return Conflict(new ErrorResponseDto
                {
                    StatusCode = 409,
                    Message = $"Cannot resume workflow in {currentState.State} state"
                });

            if (string.IsNullOrEmpty(currentState.LatestCheckpointId))
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = 404,
                    Message = "No checkpoint found to resume from"
                });

            await _pauseResumeService.OnWorkflowResumedAsync(
                id,
                await _checkpointService.LoadCheckpointAsync(currentState.LatestCheckpointId, ct)
                    ?? throw new InvalidOperationException("Checkpoint not found"),
                ct);

            var response = new WorkflowActionResponseDto
            {
                WorkflowId = id,
                Action = "resume",
                Success = true,
                Message = $"Workflow resuming from checkpoint {currentState.LatestCheckpointId}",
                Status = "Running",
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Resume requested for workflow {WorkflowId} from checkpoint {CheckpointId}",
                id, currentState.LatestCheckpointId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to resume workflow",
                Details = new() { { "workflowId", id } }
            });
        }
    }

    /// <summary>
    /// Cancel a running or paused workflow.
    /// </summary>
    /// <param name="id">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cancel operation result.</returns>
    /// <response code="200">Cancel request accepted.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpPut("{id}/cancel")]
    [ProducesResponseType(typeof(WorkflowActionResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 404)]
    public async Task<IActionResult> CancelWorkflow(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            await _pauseResumeService.RequestCancellationAsync(id, ct);

            var response = new WorkflowActionResponseDto
            {
                WorkflowId = id,
                Action = "cancel",
                Success = true,
                Message = "Cancellation requested; workflow will terminate",
                Status = "Cancelling",
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Cancellation requested for workflow {WorkflowId}", id);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to cancel workflow",
                Details = new() { { "workflowId", id } }
            });
        }
    }

    /// <summary>
    /// List all workflows with optional filtering.
    /// </summary>
    /// <param name="status">Filter by status (Running, Paused, Completed, Failed).</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of workflows.</returns>
    /// <response code="200">Workflows retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WorkflowListResponseDto), 200)]
    public async Task<IActionResult> ListWorkflows(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            // TODO: Implement workflow listing from persistence layer
            // For now, return empty list as placeholder

            var response = new WorkflowListResponseDto
            {
                Workflows = new List<WorkflowSummaryDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
                FilteredStatus = status
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workflows");
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "Failed to list workflows"
            });
        }
    }
}
