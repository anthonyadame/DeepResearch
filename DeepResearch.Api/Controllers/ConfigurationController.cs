using Microsoft.AspNetCore.Mvc;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Configuration Controller - Provides configuration options for the UI
/// </summary>
[ApiController]
[Route("api/config")]
[Produces("application/json")]
public class ConfigurationController : ControllerBase
{
    private readonly ILlmProvider _ollamaService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        ILlmProvider ollamaService,
        ILogger<ConfigurationController> logger)
    {
        _ollamaService = ollamaService;
        _logger = logger;
    }

    /// <summary>
    /// Get available LLM models
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetAvailableModels()
    {
        _logger.LogInformation("Retrieving available models");

        try
        {
            // Get models from Ollama
            // For now, return common models
            var models = new List<string>
            {
                "gpt-oss:20b",
                "llama3.1:8b",
                "llama2:13b",
                "mistral:7b",
                "mixtral:8x7b"
            };

            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models");
            return StatusCode(500, new { error = "Failed to retrieve models", details = ex.Message });
        }
    }

    /// <summary>
    /// Get available web search tools/providers
    /// </summary>
    [HttpGet("search-tools")]
    [ProducesResponseType(typeof(List<SearchTool>), StatusCodes.Status200OK)]
    public ActionResult<List<SearchTool>> GetSearchTools()
    {
        _logger.LogInformation("Retrieving search tools");

        var tools = new List<SearchTool>
        {
            new SearchTool { Id = "searxng", Name = "SearXNG", Description = "Privacy-focused metasearch engine", Enabled = true },
            new SearchTool { Id = "google", Name = "Google Search", Description = "Google Search API", Enabled = false },
            new SearchTool { Id = "bing", Name = "Bing Search", Description = "Microsoft Bing Search API", Enabled = false },
            new SearchTool { Id = "duckduckgo", Name = "DuckDuckGo", Description = "Privacy-focused search", Enabled = false }
        };

        return Ok(tools);
    }

    /// <summary>
    /// Get current configuration
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentConfiguration), StatusCodes.Status200OK)]
    public ActionResult<CurrentConfiguration> GetCurrentConfiguration()
    {
        _logger.LogInformation("Retrieving current configuration");

        var config = new CurrentConfiguration
        {
            DefaultModel = "gpt-oss:20b",
            DefaultSearchTool = "searxng",
            MaxDepth = 3,
            TimeoutSeconds = 300,
            Language = "English"
        };

        return Ok(config);
    }

    /// <summary>
    /// Save configuration (placeholder)
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult SaveConfiguration([FromBody] CurrentConfiguration config)
    {
        _logger.LogInformation("Saving configuration");

        try
        {
            // TODO: Implement configuration persistence
            // For now, just log and return success
            _logger.LogInformation("Configuration saved: {@Config}", config);
            
            return Ok(new { message = "Configuration saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return StatusCode(500, new { error = "Failed to save configuration", details = ex.Message });
        }
    }
}

/// <summary>
/// Search tool information
/// </summary>
public record SearchTool
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool Enabled { get; init; }
}

/// <summary>
/// Current configuration settings
/// </summary>
public record CurrentConfiguration
{
    public required string DefaultModel { get; init; }
    public required string DefaultSearchTool { get; init; }
    public int MaxDepth { get; init; }
    public int TimeoutSeconds { get; init; }
    public required string Language { get; init; }
}
