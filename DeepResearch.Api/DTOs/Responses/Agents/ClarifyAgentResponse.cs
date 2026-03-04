namespace DeepResearch.Api.DTOs.Responses.Agents;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from ClarifyAgent - Indicates if query needs clarification.
/// </summary>
public class ClarifyAgentResponse
{
    /// <summary>
    /// Whether clarification is needed from user.
    /// </summary>
    public bool NeedsClarification { get; set; }

    /// <summary>
    /// Clarification question if clarification is needed.
    /// </summary>
    public string? ClarificationQuestion { get; set; }

    /// <summary>
    /// Confirmation message if query is clear enough to proceed.
    /// </summary>
    public string? ProceedingConfirmation { get; set; }

    /// <summary>
    /// Confidence score that query is sufficiently detailed (0.0-1.0).
    /// </summary>
    public double DetailednessScore { get; set; }

    /// <summary>
    /// Identified aspects of the query.
    /// </summary>
    public List<string> IdentifiedAspects { get; set; } = new();

    /// <summary>
    /// Recommended next steps.
    /// </summary>
    public string? RecommendedNextSteps { get; set; }

    /// <summary>
    /// Status: Success, NeedsClarification, Error.
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
