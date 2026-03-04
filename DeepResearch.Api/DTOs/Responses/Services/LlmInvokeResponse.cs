namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from LLM invocation.
/// </summary>
public class LlmInvokeResponse
{
    /// <summary>
    /// LLM response message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Response role (usually "assistant").
    /// </summary>
    public string Role { get; set; } = "assistant";

    /// <summary>
    /// Model used for generation.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Tokens used by prompt.
    /// </summary>
    public int? PromptTokens { get; set; }

    /// <summary>
    /// Tokens used in response.
    /// </summary>
    public int? CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens used.
    /// </summary>
    public int? TotalTokens { get; set; }

    /// <summary>
    /// Parsed structured output if requested.
    /// </summary>
    public object? StructuredOutput { get; set; }

    /// <summary>
    /// Generation finish reason.
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
