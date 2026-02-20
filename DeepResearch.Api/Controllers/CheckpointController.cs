using DeepResearch.Api.Models;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// API controller for managing workflow checkpoints.
/// Provides endpoints for listing, creating, validating, and deleting checkpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckpointController : ControllerBase
{
    private readonly ICheckpointService _checkpointService;
    private readonly ILogger<CheckpointController> _logger;

    public CheckpointController(
        ICheckpointService checkpointService,
        ILogger<CheckpointController> logger)
    {
        _checkpointService = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all checkpoints for a specific workflow.
    /// </summary>
    /// <param name="workflowId">Workflow ID to get checkpoints for.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of checkpoints.</returns>
    /// <response code="200">Checkpoints retrieved successfully.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpGet("workflow/{workflowId}")]
    [ProducesResponseType(typeof(CheckpointListResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCheckpointsForWorkflow(
        string workflowId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId, ct);
            
            var paginatedCheckpoints = checkpoints
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            var response = new CheckpointListResponseDto
            {
                Checkpoints = paginatedCheckpoints,
                TotalCount = checkpoints.Count,
                Page = page,
                PageSize = pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoints for workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { error = "Failed to retrieve checkpoints" });
        }
    }

    /// <summary>
    /// Get a specific checkpoint by ID.
    /// </summary>
    /// <param name="checkpointId">Checkpoint ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Checkpoint details.</returns>
    /// <response code="200">Checkpoint found.</response>
    /// <response code="404">Checkpoint not found.</response>
    [HttpGet("{checkpointId}")]
    [ProducesResponseType(typeof(CheckpointResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCheckpoint(
        string checkpointId,
        CancellationToken ct = default)
    {
        try
        {
            var checkpoint = await _checkpointService.LoadCheckpointAsync(checkpointId, ct);
            
            if (checkpoint == null)
            {
                return NotFound(new { error = $"Checkpoint '{checkpointId}' not found" });
            }

            return Ok(MapToDto(checkpoint));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoint {CheckpointId}", checkpointId);
            return StatusCode(500, new { error = "Failed to retrieve checkpoint" });
        }
    }

    /// <summary>
    /// Get the latest checkpoint for a workflow.
    /// </summary>
    /// <param name="workflowId">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Latest checkpoint.</returns>
    /// <response code="200">Latest checkpoint found.</response>
    /// <response code="404">No checkpoints found for workflow.</response>
    [HttpGet("workflow/{workflowId}/latest")]
    [ProducesResponseType(typeof(CheckpointResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLatestCheckpoint(
        string workflowId,
        CancellationToken ct = default)
    {
        try
        {
            var checkpoint = await _checkpointService.GetLatestCheckpointAsync(workflowId, ct);
            
            if (checkpoint == null)
            {
                return NotFound(new { error = $"No checkpoints found for workflow '{workflowId}'" });
            }

            return Ok(MapToDto(checkpoint));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest checkpoint for workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { error = "Failed to retrieve latest checkpoint" });
        }
    }

    /// <summary>
    /// Validate a checkpoint.
    /// </summary>
    /// <param name="checkpointId">Checkpoint ID to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    /// <response code="200">Checkpoint validated.</response>
    [HttpPost("{checkpointId}/validate")]
    [ProducesResponseType(typeof(ValidateCheckpointResponseDto), 200)]
    public async Task<IActionResult> ValidateCheckpoint(
        string checkpointId,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, errorMessage) = await _checkpointService.ValidateCheckpointAsync(checkpointId, ct);
            
            var response = new ValidateCheckpointResponseDto
            {
                CheckpointId = checkpointId,
                IsValid = isValid,
                ErrorMessage = errorMessage,
                ValidationMessages = isValid 
                    ? new List<string> { "Checkpoint is valid" }
                    : new List<string> { errorMessage ?? "Unknown error" }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating checkpoint {CheckpointId}", checkpointId);
            return StatusCode(500, new { error = "Failed to validate checkpoint" });
        }
    }

    /// <summary>
    /// Delete a checkpoint.
    /// </summary>
    /// <param name="checkpointId">Checkpoint ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Delete confirmation.</returns>
    /// <response code="200">Checkpoint deleted.</response>
    /// <response code="404">Checkpoint not found.</response>
    [HttpDelete("{checkpointId}")]
    [ProducesResponseType(typeof(DeleteCheckpointsResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCheckpoint(
        string checkpointId,
        CancellationToken ct = default)
    {
        try
        {
            await _checkpointService.DeleteCheckpointAsync(checkpointId, ct);
            
            var response = new DeleteCheckpointsResponseDto
            {
                DeletedCount = 1,
                DeletedCheckpointIds = new List<string> { checkpointId },
                Message = $"Checkpoint '{checkpointId}' deleted successfully"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checkpoint {CheckpointId}", checkpointId);
            return StatusCode(500, new { error = "Failed to delete checkpoint" });
        }
    }

    /// <summary>
    /// Delete all checkpoints for a workflow.
    /// </summary>
    /// <param name="workflowId">Workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Delete confirmation.</returns>
    /// <response code="200">Checkpoints deleted.</response>
    [HttpDelete("workflow/{workflowId}")]
    [ProducesResponseType(typeof(DeleteCheckpointsResponseDto), 200)]
    public async Task<IActionResult> DeleteCheckpointsForWorkflow(
        string workflowId,
        CancellationToken ct = default)
    {
        try
        {
            var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId, ct);
            var checkpointIds = checkpoints.Select(cp => cp.CheckpointId).ToList();
            
            await _checkpointService.DeleteCheckpointsForWorkflowAsync(workflowId, ct);
            
            var response = new DeleteCheckpointsResponseDto
            {
                DeletedCount = checkpointIds.Count,
                DeletedCheckpointIds = checkpointIds,
                Message = $"Deleted {checkpointIds.Count} checkpoints for workflow '{workflowId}'"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checkpoints for workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { error = "Failed to delete checkpoints" });
        }
    }

    /// <summary>
    /// Get checkpoint storage statistics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Storage statistics.</returns>
    /// <response code="200">Statistics retrieved.</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(CheckpointStatisticsResponseDto), 200)]
    public async Task<IActionResult> GetStatistics(CancellationToken ct = default)
    {
        try
        {
            var stats = await _checkpointService.GetStatisticsAsync(ct);
            
            var response = new CheckpointStatisticsResponseDto
            {
                TotalCheckpoints = stats.TotalCheckpoints,
                AverageCheckpointSizeBytes = stats.AverageCheckpointSizeBytes,
                LargestCheckpointSizeBytes = stats.LargestCheckpointSizeBytes,
                TotalStorageUsedBytes = stats.TotalStorageUsedBytes,
                RecentCheckpointsCount = stats.RecentCheckpointsCount,
                OldestCheckpointAt = stats.OldestCheckpointAt,
                NewestCheckpointAt = stats.NewestCheckpointAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoint statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    // Helper method to map domain model to DTO
    private static CheckpointResponseDto MapToDto(WorkflowCheckpoint checkpoint)
    {
        return new CheckpointResponseDto
        {
            CheckpointId = checkpoint.CheckpointId,
            WorkflowId = checkpoint.WorkflowId,
            WorkflowType = checkpoint.WorkflowType,
            CreatedAt = checkpoint.CreatedAt,
            AgentId = checkpoint.AgentId,
            StepIndex = checkpoint.StepIndex,
            StateSizeBytes = checkpoint.StateSizeBytes,
            Label = checkpoint.Label,
            Metadata = new CheckpointMetadataDto
            {
                IsAutomated = checkpoint.Metadata.IsAutomated,
                Reason = checkpoint.Metadata.Reason,
                UserId = checkpoint.Metadata.UserId,
                Context = checkpoint.Metadata.Context,
                CompletedAgents = checkpoint.Metadata.CompletedAgents
            }
        };
    }
}
