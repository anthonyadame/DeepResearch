using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// An atomic unit of knowledge, extracted from raw research notes and stored in our structured knowledge base.
/// </summary>
public record FactState
{
    /// <summary>
    /// Unique identifier for this fact, used for deduplication and storage.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The core factual statement itself, extracted from a source.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// We must track the provenance of every fact for traceability and citations in the final report.
    /// </summary>
    [JsonPropertyName("sourceUrl")]
    public required string SourceUrl { get; init; }

    /// <summary>
    /// A confidence score allows the system to weigh more credible sources higher during synthesis.
    /// Range: 0.0-1.0 (normalized) or 1-100 (legacy format).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    /// <summary>
    /// Legacy confidence score (1-100 range).
    /// </summary>
    [JsonPropertyName("confidenceScore")]
    public int ConfidenceScore
    {
        get => (int)(Confidence * 100);
        init => Confidence = value / 100.0;
    }

    /// <summary>
    /// Timestamp when this fact was extracted.
    /// </summary>
    [JsonPropertyName("extractedAt")]
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// This flag allows our Red Team to mark facts that are contradicted by other evidence,
    /// a key part of our self-correction loop.
    /// </summary>
    [JsonPropertyName("isDisputed")]
    public bool IsDisputed { get; init; } = false;

    /// <summary>
    /// Optional tags for categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Optional metadata for additional context.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}
