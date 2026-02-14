using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Tool Result Cache Service: Caches tool execution results with TTL support.
/// Avoids duplicate searches and improves performance.
/// </summary>
public class ToolResultCacheService
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl;
    private readonly ILogger<ToolResultCacheService>? _logger;

    /// <summary>
    /// Cache entry with result and expiry time.
    /// </summary>
    private class CacheEntry
    {
        public object Result { get; set; }
        public DateTime Expiry { get; set; }
    }

    public ToolResultCacheService(TimeSpan? defaultTtl = null, ILogger<ToolResultCacheService>? logger = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(1);
        _logger = logger;
    }

    /// <summary>
    /// Get cached result or execute the provided function and cache the result.
    /// </summary>
    public async Task<T> GetOrExecuteAsync<T>(
        string cacheKey,
        Func<Task<T>> executor,
        TimeSpan? ttl = null)
    {
        // Validate cache key
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _logger?.LogWarning("Empty cache key provided, executing without caching");
            return await executor();
        }

        // Check if entry exists and is not expired
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (DateTime.UtcNow < entry.Expiry)
            {
                _logger?.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                return (T)entry.Result;
            }

            // Remove expired entry
            _cache.Remove(cacheKey);
            _logger?.LogDebug("Removed expired cache entry: {CacheKey}", cacheKey);
        }

        // Cache miss - execute function
        _logger?.LogDebug("Cache miss for key: {CacheKey}, executing function", cacheKey);
        var result = await executor();

        // Store in cache
        var expiry = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
        _cache[cacheKey] = new CacheEntry { Result = result, Expiry = expiry };
        _logger?.LogDebug("Cached result for key: {CacheKey} with TTL: {TTL}", 
            cacheKey, ttl ?? _defaultTtl);

        return result;
    }

    /// <summary>
    /// Clear all cache entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _logger?.LogInformation("Cache cleared");
    }

    /// <summary>
    /// Get number of items in cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Remove specific cache entry.
    /// </summary>
    public bool Remove(string cacheKey)
    {
        var removed = _cache.Remove(cacheKey);
        if (removed)
        {
            _logger?.LogDebug("Removed cache entry: {CacheKey}", cacheKey);
        }
        return removed;
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var validCount = _cache.Count(e => now < e.Value.Expiry);
        var expiredCount = _cache.Count - validCount;

        return new CacheStatistics
        {
            TotalEntries = _cache.Count,
            ValidEntries = validCount,
            ExpiredEntries = expiredCount
        };
    }
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int ValidEntries { get; set; }
    public int ExpiredEntries { get; set; }
}
