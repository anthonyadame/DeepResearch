using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.VectorDatabase;

/// <summary>
/// Factory for creating and managing vector database service instances.
/// Supports multiple vector database implementations with a pluggable architecture.
/// </summary>
public interface IVectorDatabaseFactory
{
    /// <summary>
    /// Get a vector database service by name.
    /// </summary>
    IVectorDatabaseService GetVectorDatabase(string name);

    /// <summary>
    /// Get the default vector database service.
    /// </summary>
    IVectorDatabaseService GetDefaultVectorDatabase();

    /// <summary>
    /// Register a new vector database implementation.
    /// </summary>
    void RegisterVectorDatabase(string name, IVectorDatabaseService service);

    /// <summary>
    /// Get all registered vector database names.
    /// </summary>
    IReadOnlyList<string> GetAvailableDatabases();
}

/// <summary>
/// Default implementation of vector database factory.
/// </summary>
public class VectorDatabaseFactory : IVectorDatabaseFactory
{
    private readonly Dictionary<string, IVectorDatabaseService> _databases = new();
    private string? _defaultName;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    public VectorDatabaseFactory(Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
    }

    public IVectorDatabaseService GetVectorDatabase(string name)
    {
        if (_databases.TryGetValue(name, out var db))
        {
            return db;
        }

        throw new KeyNotFoundException($"Vector database '{name}' is not registered.");
    }

    public IVectorDatabaseService GetDefaultVectorDatabase()
    {
        if (string.IsNullOrEmpty(_defaultName) || !_databases.TryGetValue(_defaultName, out var db))
        {
            throw new InvalidOperationException("No default vector database is set.");
        }

        return db;
    }

    public void RegisterVectorDatabase(string name, IVectorDatabaseService service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        _databases[name] = service;

        // Set as default if it's the first one registered
        if (string.IsNullOrEmpty(_defaultName))
        {
            _defaultName = name;
            _logger?.LogInformation("Set '{name}' as default vector database", name);
        }

        _logger?.LogInformation("Registered vector database: {name}", name);
    }

    public IReadOnlyList<string> GetAvailableDatabases()
    {
        return _databases.Keys.ToList().AsReadOnly();
    }
}

/// <summary>
/// Configuration for vector database support.
/// </summary>
public class VectorDatabaseOptions
{
    /// <summary>Enable vector database support.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default vector database to use.</summary>
    public string DefaultDatabase { get; set; } = "qdrant";

    /// <summary>Qdrant configuration.</summary>
    public QdrantConfig Qdrant { get; set; } = new();

    /// <summary>Embedding service configuration.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>Embedding model API URL.</summary>
    public string EmbeddingApiUrl { get; set; } = "http://localhost:11434";

    /// <summary>Embedding dimension.</summary>
    public int EmbeddingDimension { get; set; } = 384;
}
