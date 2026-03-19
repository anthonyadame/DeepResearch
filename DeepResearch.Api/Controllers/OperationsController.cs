using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace DeepResearch.Api.Controllers;

[ApiController]
[Route("api")]
public class OperationsController : ControllerBase
{
    private readonly ILlmProvider _ollamaService;
    private readonly SearCrawl4AIService _searchAndScrapeService;
    private readonly HttpClient _httpClient;
    private readonly IAgentLightningService _lightningService;
    private readonly MasterWorkflow _masterWorkflow;
    private readonly string _ollamaBaseUrl;
    private readonly string _searxngBaseUrl;
    private readonly string _crawl4aiBaseUrl;
    private readonly string _lightningServerUrl;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(
        ILlmProvider ollamaService,
        SearCrawl4AIService searchAndScrapeService,
        HttpClient httpClient,
        IAgentLightningService lightningService,
        MasterWorkflow masterWorkflow,
        IConfiguration configuration,
        ILogger<OperationsController> logger)
    {
        _ollamaService = ollamaService;
        _searchAndScrapeService = searchAndScrapeService;
        _httpClient = httpClient;
        _lightningService = lightningService;
        _masterWorkflow = masterWorkflow;
        _logger = logger;

        _ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _searxngBaseUrl = configuration["SearXNG:BaseUrl"] ?? "http://localhost:8080";
        _crawl4aiBaseUrl = configuration["Crawl4AI:BaseUrl"] ?? "http://localhost:11235";
        _lightningServerUrl = configuration["Lightning:ServerUrl"]
            ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
            ?? "http://localhost:8090";
    }

    [HttpGet("health/ollama")]
    public async Task<ActionResult<OllamaHealthResult>> CheckOllama(CancellationToken cancellationToken)
    {
        var result = await CheckOllamaInternal(cancellationToken);
        if (!result.Healthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        return Ok(result);
    }

    [HttpGet("health/searxng")]
    public async Task<ActionResult<SearxHealthResult>> CheckSearxng(CancellationToken cancellationToken)
    {
        var result = await CheckSearxngInternal(cancellationToken);
        if (!result.Healthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        return Ok(result);
    }

    [HttpGet("health/crawl4ai")]
    public async Task<ActionResult<CrawlHealthResult>> CheckCrawl4Ai(CancellationToken cancellationToken)
    {
        var result = await CheckCrawl4AiInternal(cancellationToken);
        if (!result.Healthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        return Ok(result);
    }

    [HttpGet("health/lightning")]
    public async Task<ActionResult<LightningHealthResult>> CheckLightning(CancellationToken cancellationToken)
    {
        var result = await CheckLightningInternal(cancellationToken);
        if (!result.Healthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        return Ok(result);
    }

    [HttpGet("health/all")]
    public async Task<ActionResult<HealthSummaryResult>> RunAllHealthChecks(CancellationToken cancellationToken)
    {
        var ollama = await CheckOllamaInternal(cancellationToken);
        var searxng = await CheckSearxngInternal(cancellationToken);
        var crawl4ai = await CheckCrawl4AiInternal(cancellationToken);
        var lightning = await CheckLightningInternal(cancellationToken);

        var summary = new HealthSummaryResult(ollama, searxng, crawl4ai, lightning);
        if (!ollama.Healthy || !searxng.Healthy || !crawl4ai.Healthy || !lightning.Healthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, summary);
        }
        return Ok(summary);
    }

    [HttpPost("workflow/run")]
    public async Task<ActionResult<RunWorkflowResponse>> RunWorkflow([FromBody] RunWorkflowRequest? request, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(request?.Query)
            ? "Summarize the latest advancements in transformer architectures"
            : request!.Query!;

        var updates = new List<string>();
        await foreach (var update in _masterWorkflow.StreamAsync(query, cancellationToken))
        {
            updates.Add(update);
        }

        var response = new RunWorkflowResponse(query, updates);
        return Ok(response);
    }

    private async Task<OllamaHealthResult> CheckOllamaInternal(CancellationToken cancellationToken)
    {
        try
        {
            var healthy = await _ollamaService.IsHealthyAsync(cancellationToken);
            var result = new OllamaHealthResult
            {
                Endpoint = _ollamaBaseUrl,
                Healthy = healthy
            };

            if (healthy)
            {
                var models = await _ollamaService.GetAvailableModelsAsync(cancellationToken);
                result.Models = models;

                var testMessages = new List<OllamaChatMessage>
                {
                    new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
                };

                var response = await _ollamaService.InvokeAsync(testMessages, cancellationToken: cancellationToken);
                result.TestResponse = response.Content;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama health check failed");
            return new OllamaHealthResult
            {
                Endpoint = _ollamaBaseUrl,
                Healthy = false,
                Error = ex.Message
            };
        }
    }

    private async Task<SearxHealthResult> CheckSearxngInternal(CancellationToken cancellationToken)
    {
        try
        {
            var healthResponse = await _httpClient.GetAsync($"{_searxngBaseUrl}/healthz", cancellationToken);
            var healthy = healthResponse.IsSuccessStatusCode;

            var result = new SearxHealthResult
            {
                Endpoint = _searxngBaseUrl,
                Healthy = healthy
            };

            if (healthy)
            {
                var searchResponse = await _httpClient.GetAsync($"{_searxngBaseUrl}/search?q=test&format=json", cancellationToken);
                if (searchResponse.IsSuccessStatusCode)
                {
                    var content = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
                    result.SampleResponseLength = content.Length;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearXNG health check failed");
            return new SearxHealthResult
            {
                Endpoint = _searxngBaseUrl,
                Healthy = false,
                Error = ex.Message
            };
        }
    }

    private async Task<CrawlHealthResult> CheckCrawl4AiInternal(CancellationToken cancellationToken)
    {
        try
        {
            var testUrl = new List<string> { "https://example.com" };
            var scrapeResponse = await _searchAndScrapeService.ScrapeAsync(testUrl, cancellationToken: cancellationToken);

            var result = new CrawlHealthResult
            {
                Endpoint = _crawl4aiBaseUrl,
                Healthy = scrapeResponse.Success,
                Results = scrapeResponse.Results
                    .Select(r => new ScrapeSummary(r.Url, r.Title, r.CleanedHtml?.Length ?? 0, r.Success, r.ErrorMessage))
                    .ToList()
            };

            if (!scrapeResponse.Success)
            {
                result.Error = scrapeResponse.ErrorMessage;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crawl4AI health check failed");
            return new CrawlHealthResult
            {
                Endpoint = _crawl4aiBaseUrl,
                Healthy = false,
                Error = ex.Message
            };
        }
    }

    private async Task<LightningHealthResult> CheckLightningInternal(CancellationToken cancellationToken)
    {
        try
        {
            var healthy = await _lightningService.IsHealthyAsync();
            var result = new LightningHealthResult
            {
                Endpoint = _lightningServerUrl,
                Healthy = healthy
            };

            if (healthy)
            {
                var serverInfo = await _lightningService.GetServerInfoAsync();
                result.ServerInfo = serverInfo;

                var capabilities = new Dictionary<string, object>
                {
                    { "research", true },
                    { "synthesis", true },
                    { "verification", true },
                    { "web_search", true },
                    { "content_scraping", true }
                };

                var registration = await _lightningService.RegisterAgentAsync("deep-research-agent", "ResearchOrchestrator", capabilities);
                result.RegisteredAgentId = registration.AgentId;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lightning health check failed");
            return new LightningHealthResult
            {
                Endpoint = _lightningServerUrl,
                Healthy = false,
                Error = ex.Message
            };
        }
    }
}

public record RunWorkflowRequest(string? Query);
public record RunWorkflowResponse(string Query, List<string> Updates);

public record OllamaHealthResult
{
    public string Endpoint { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public IEnumerable<string> Models { get; set; } = Enumerable.Empty<string>();
    public string? TestResponse { get; set; }
    public string? Error { get; set; }
}

public record SearxHealthResult
{
    public string Endpoint { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public int? SampleResponseLength { get; set; }
    public string? Error { get; set; }
}

public record CrawlHealthResult
{
    public string Endpoint { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public List<ScrapeSummary> Results { get; set; } = new();
    public string? Error { get; set; }
}

public record ScrapeSummary(string Url, string Title, int ContentLength, bool Success, string? ErrorMessage);

public record LightningHealthResult
{
    public string Endpoint { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public LightningServerInfo? ServerInfo { get; set; }
    public string? RegisteredAgentId { get; set; }
    public string? Error { get; set; }
}

public record HealthSummaryResult(
    OllamaHealthResult Ollama,
    SearxHealthResult SearXng,
    CrawlHealthResult Crawl4Ai,
    LightningHealthResult Lightning);
