using DeepResearch.Api.Models.Chat;
using Microsoft.AspNetCore.Mvc;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Configuration Controller - Provides configuration data for the WebUI
/// </summary>
[ApiController]
[Route("api/config")]
[Produces("application/json")]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(ILogger<ConfigurationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get available LLM models
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(List<ModelInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<ModelInfo>> GetModels()
    {
        var models = new List<ModelInfo>
        {
            new() { Id = "gpt-4", Name = "GPT-4", ContextWindow = 8192 },
            new() { Id = "gpt-3.5-turbo", Name = "GPT-3.5 Turbo", ContextWindow = 4096 },
            new() { Id = "claude-3-opus", Name = "Claude 3 Opus", ContextWindow = 200000 },
            new() { Id = "claude-3-sonnet", Name = "Claude 3 Sonnet", ContextWindow = 200000 },
            new() { Id = "llama-3-70b", Name = "Llama 3 70B", ContextWindow = 8192 }
        };

        return Ok(models);
    }

    /// <summary>
    /// Get available search tools
    /// </summary>
    [HttpGet("search-tools")]
    [ProducesResponseType(typeof(List<SearchToolInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<SearchToolInfo>> GetSearchTools()
    {
        var searchTools = new List<SearchToolInfo>
        {
            new() { Id = "searxng", Name = "SearXNG" },
            new() { Id = "google", Name = "Google" },
            new() { Id = "bing", Name = "Bing" },
            new() { Id = "duckduckgo", Name = "DuckDuckGo" },
            new() { Id = "perplexity", Name = "Perplexity" }
        };

        return Ok(searchTools);
    }

    /// <summary>
    /// Save configuration (placeholder)
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult SaveConfig([FromBody] ResearchConfig config)
    {
        _logger.LogInformation("Configuration save requested (not persisted in this implementation)");
        return Ok(new { message = "Configuration saved (in-memory only)" });
    }
}
