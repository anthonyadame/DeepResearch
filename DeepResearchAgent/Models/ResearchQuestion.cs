using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepResearchAgent.Models;

/// <summary>
/// A structured research brief generated from user intent.
/// This becomes the "guidance signal" for all subsequent research agents.
/// Every research step will be measured against this brief.
/// Maps to Python's ResearchQuestion/research brief generation.
/// </summary>
public record ResearchQuestion
{
    /// <summary>
    /// The formal, comprehensive research brief derived from the user's request.
    /// This is the distilled guidance signal that steers the entire research process.
    /// </summary>
    [JsonPropertyName("research_brief")]
    public required string ResearchBrief { get; init; }

    /// <summary>
    /// Key research objectives extracted from the user's request.
    /// These guide the researcher agents in focusing their work.
    /// </summary>
    [JsonPropertyName("objectives")]
    public List<string> Objectives { get; init; } = new();

    /// <summary>
    /// Scope constraints and boundaries for the research.
    /// Helps prevent scope creep and keeps research focused.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>
    /// Timestamp when this research brief was generated.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
