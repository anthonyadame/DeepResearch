using DeepResearch.Api.DTOs.Requests.Experiments;
using DeepResearch.Api.Services.Experiments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Controller for experiment metrics logging and tracking.
/// </summary>
[ApiController]
[Route("api/experiments")]
public class ExperimentsController : ControllerBase
{
    private readonly IExperimentMetricsLogger _metricsLogger;
    private readonly ILogger<ExperimentsController> _logger;

    public ExperimentsController(
        IExperimentMetricsLogger metricsLogger,
        ILogger<ExperimentsController> logger)
    {
        _metricsLogger = metricsLogger;
        _logger = logger;
    }

    /// <summary>
    /// Log a single experiment metric entry.
    /// </summary>
    /// <param name="request">Metric entry data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>204 No Content on success</returns>
    /// <response code="204">Metric logged successfully</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost("metrics")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostMetric(
        [FromBody] ExperimentMetricEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new ExperimentMetricEntry
            {
                RunId = request.RunId,
                Task = request.Task,
                Phase = request.Phase,
                Metric = request.Metric,
                Value = request.Value,
                Step = request.Step,
                Unit = request.Unit,
                TimestampUtc = request.TimestampUtc ?? DateTime.UtcNow,
                Tags = request.Tags,
                Notes = request.Notes
            };

            await _metricsLogger.LogAsync(entry, cancellationToken);

            _logger.LogInformation(
                "Logged metric: RunId={RunId}, Task={Task}, Metric={Metric}, Value={Value}",
                entry.RunId, entry.Task, entry.Metric, entry.Value);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log experiment metric");
            return BadRequest(new { error = "Failed to log metric", details = ex.Message });
        }
    }
}
