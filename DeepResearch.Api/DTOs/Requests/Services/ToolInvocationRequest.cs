namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for tool invocation - Execute research tools.
/// </summary>
public class ToolInvocationRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Tool name to invoke.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// Tool parameters as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Whether to run asynchronously.
    /// </summary>
    public bool RunAsync { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
