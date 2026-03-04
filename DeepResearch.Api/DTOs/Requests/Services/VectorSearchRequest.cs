namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for vector database search - Semantic similarity search.
/// </summary>
public class VectorSearchRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Query vector for search (as float array).
    /// </summary>
    public required List<float> QueryVector { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Similarity threshold (0.0-1.0).
    /// </summary>
    public double? SimilarityThreshold { get; set; }

    /// <summary>
    /// Optional filter conditions.
    /// </summary>
    public Dictionary<string, object>? FilterConditions { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>
/// Request to add vector to database.
/// </summary>
public class VectorAddRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Unique ID for the vector.
    /// </summary>
    public required string VectorId { get; set; }

    /// <summary>
    /// The vector data (as float array).
    /// </summary>
    public required List<float> Vector { get; set; }

    /// <summary>
    /// Associated payload/metadata.
    /// </summary>
    public required Dictionary<string, object> Payload { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
