using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Agents Controller - Direct access to individual agents.
/// Provides endpoints for executing individual agent operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly ResearcherAgent _researcherAgent;
    private readonly AnalystAgent _analystAgent;
    private readonly ReportAgent _reportAgent;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        ILogger<AgentsController> logger)
    {
        _researcherAgent = researcherAgent;
        _analystAgent = analystAgent;
        _reportAgent = reportAgent;
        _logger = logger;
    }

    /// <summary>
    /// Execute research phase only.
    /// </summary>
    [HttpPost("research")]
    [ProducesResponseType(typeof(ResearchOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResearchOutput>> ExecuteResearch(
        [FromBody] AgentResearchRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing research agent for topic: {Topic}", request.Topic);

        try
        {
            var input = new ResearchInput
            {
                Topic = request.Topic,
                ResearchBrief = request.ResearchBrief,
                MaxIterations = request.MaxIterations ?? 3,
                MinQualityThreshold = request.MinQualityThreshold ?? 7.0f
            };

            var result = await _researcherAgent.ExecuteAsync(input, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Research agent execution failed");
            return BadRequest(new ErrorResponse
            {
                Error = "Research failed",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Execute analysis phase only.
    /// </summary>
    [HttpPost("analysis")]
    [ProducesResponseType(typeof(AnalysisOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalysisOutput>> ExecuteAnalysis(
        [FromBody] AgentAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing analyst agent for topic: {Topic}", request.Topic);

        try
        {
            var input = new AnalysisInput
            {
                Topic = request.Topic,
                Findings = request.Findings,
                ResearchBrief = request.ResearchBrief
            };

            var result = await _analystAgent.ExecuteAsync(input, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis agent execution failed");
            return BadRequest(new ErrorResponse
            {
                Error = "Analysis failed",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Execute report generation phase only.
    /// </summary>
    [HttpPost("report")]
    [ProducesResponseType(typeof(ReportOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportOutput>> ExecuteReport(
        [FromBody] AgentReportRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing report agent for topic: {Topic}", request.Topic);

        try
        {
            var input = new ReportInput
            {
                Topic = request.Topic,
                Research = request.Research,
                Analysis = request.Analysis
            };

            var result = await _reportAgent.ExecuteAsync(input, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report agent execution failed");
            return BadRequest(new ErrorResponse
            {
                Error = "Report generation failed",
                Details = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get agent capabilities and status.
    /// </summary>
    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(AgentCapabilities), StatusCodes.Status200OK)]
    public ActionResult<AgentCapabilities> GetCapabilities()
    {
        var capabilities = new AgentCapabilities
        {
            Agents = new List<AgentInfo>
            {
                new()
                {
                    Name = "ResearcherAgent",
                    Description = "Conducts web research and extracts facts",
                    Capabilities = new List<string>
                    {
                        "Web search",
                        "Content scraping",
                        "Fact extraction",
                        "Vector database search"
                    }
                },
                new()
                {
                    Name = "AnalystAgent",
                    Description = "Analyzes research findings and generates insights",
                    Capabilities = new List<string>
                    {
                        "Synthesis generation",
                        "Insight extraction",
                        "Theme identification",
                        "Contradiction detection"
                    }
                },
                new()
                {
                    Name = "ReportAgent",
                    Description = "Generates formatted research reports",
                    Capabilities = new List<string>
                    {
                        "Report formatting",
                        "Section generation",
                        "Citation management",
                        "Quality scoring"
                    }
                }
            }
        };

        return Ok(capabilities);
    }
}

/// <summary>
/// Request model for research agent.
/// </summary>
public class AgentResearchRequest
{
    public string Topic { get; set; } = string.Empty;
    public string ResearchBrief { get; set; } = string.Empty;
    public int? MaxIterations { get; set; }
    public float? MinQualityThreshold { get; set; }
}

/// <summary>
/// Request model for analyst agent.
/// </summary>
public class AgentAnalysisRequest
{
    public string Topic { get; set; } = string.Empty;
    public string ResearchBrief { get; set; } = string.Empty;
    public List<FactExtractionResult> Findings { get; set; } = new();
}

/// <summary>
/// Request model for report agent.
/// </summary>
public class AgentReportRequest
{
    public string Topic { get; set; } = string.Empty;
    public ResearchOutput Research { get; set; } = new();
    public AnalysisOutput Analysis { get; set; } = new();
}

/// <summary>
/// Agent capabilities response.
/// </summary>
public class AgentCapabilities
{
    public List<AgentInfo> Agents { get; set; } = new();
}

/// <summary>
/// Agent information.
/// </summary>
public class AgentInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
}
