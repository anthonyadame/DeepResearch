namespace DeepResearchAgent.Services.VectorDatabase;

/// <summary>
/// Interface for vector database operations.
/// Abstracts different vector database implementations (Qdrant, Pinecone, Milvus, etc.)
/// </summary>
public interface IVectorDatabaseService
{
    /// <summary>
    /// Get the name/identifier of this vector database implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Insert or upsert a document with its embedding.
    /// </summary>
    Task<string> UpsertAsync(
        string id,
        string content,
        float[] embedding,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar documents by embedding.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] embedding,
        int topK = 5,
        double? scoreThreshold = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar documents by content (will be embedded internally).
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchByContentAsync(
        string content,
        int topK = 5,
        double? scoreThreshold = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a document by ID.
    /// </summary>
    Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all documents from the collection/namespace.
    /// </summary>
    Task ClearAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get database statistics and health.
    /// </summary>
    Task<VectorDatabaseStats> GetStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the vector database is available/healthy.
    /// </summary>
    Task<bool> HealthCheckAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a vector database search query.
/// </summary>
public class VectorSearchResult
{
    /// <summary>Document identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Document content/text.</summary>
    public required string Content { get; init; }

    /// <summary>Similarity score (typically 0-1 or cosine similarity).</summary>
    public required double Score { get; init; }

    /// <summary>Additional metadata associated with the document.</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Statistics about the vector database.
/// </summary>
public class VectorDatabaseStats
{
    /// <summary>Total number of vectors/documents in the database.</summary>
    public long DocumentCount { get; init; }

    /// <summary>Total size of the database in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Whether the database is healthy and accessible.</summary>
    public bool IsHealthy { get; init; }

    /// <summary>Additional status information.</summary>
    public string? Status { get; init; }

    /// <summary>Timestamp of the last operation.</summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}
