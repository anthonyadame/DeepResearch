using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// The initial draft report generated at the start of the diffusion process.
/// This is the "noisy image" that will be iteratively refined through the denoising loop.
/// Maps to Python's initial draft report generation.
/// </summary>
public record DraftReport
{
    /// <summary>
    /// The content of the draft report.
    /// This is the starting point for the iterative refinement process.
    /// </summary>
    [JsonPropertyName("draft_report")]
    public required string Content { get; init; }

    /// <summary>
    /// Sections of the draft report for structured refinement.
    /// Allows targeted improvement of specific sections.
    /// </summary>
    [JsonPropertyName("sections")]
    public List<DraftReportSection> Sections { get; init; } = new();

    /// <summary>
    /// Timestamp when this draft was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Metadata about the draft (iteration, research phase, etc.).
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Individual section of a draft report for granular refinement.
/// </summary>
public record DraftReportSection
{
    /// <summary>
    /// Section title/identifier.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Section content.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Quality score for this section (0-10).
    /// Helps target refinement efforts.
    /// </summary>
    [JsonPropertyName("quality_score")]
    public int? QualityScore { get; init; }

    /// <summary>
    /// Identified gaps in this section requiring research.
    /// </summary>
    [JsonPropertyName("identified_gaps")]
    public List<string> IdentifiedGaps { get; init; } = new();
}
