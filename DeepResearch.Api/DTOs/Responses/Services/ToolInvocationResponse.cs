namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from tool invocation.
/// </summary>
public class ToolInvocationResponse
{
    /// <summary>
    /// Tool name invoked.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Tool execution result.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Whether execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Execution status message.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// If async, the job ID for polling.
    /// </summary>
    public string? JobId { get; set; }

    /// <summary>
    /// If async, the status URL for polling.
    /// </summary>
    public string? StatusUrl { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
