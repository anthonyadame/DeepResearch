using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DeepResearchAgent.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Caching;

/// <summary>
/// LLM Response Cache: In-memory caching layer for LLM structured output responses.
/// Caches structured output from LLM calls to reduce redundant processing.
/// 
/// Cache Key: SHA256(prompt + model + tier)
/// TTL: Configurable per cache entry
/// Performance Target: 80% cache hit rate for typical workflows
/// </summary>
public class LlmResponseCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LlmResponseCache>? _logger;
    private readonly LlmResponseCacheOptions _options;

    public LlmResponseCache(
        IMemoryCache cache,
        LlmResponseCacheOptions? options = null,
        ILogger<LlmResponseCache>? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
        _options = options ?? new LlmResponseCacheOptions();
    }

    /// <summary>
    /// Generate cache key from prompt, model, and tier.
    /// Uses SHA256 to create a deterministic key from the combined inputs.
    /// </summary>
    public static string GenerateCacheKey(string prompt, string? model, string? tier)
    {
        var combined = $"{prompt}:{model}:{tier}";
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Try to get cached response for structured output.
    /// </summary>
    public bool TryGetCached<T>(string prompt, string? model, string? tier, out T? cachedResponse) where T : class
    {
        if (!_options.EnableCaching)
        {
            cachedResponse = null;
            return false;
        }

        var cacheKey = GenerateCacheKey(prompt, model, tier);

        if (_cache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is T typedValue)
        {
            DiagnosticConfig.LlmCacheHits.Add(1);
            _logger?.LogDebug("LlmResponseCache: Cache hit for key {CacheKey}", cacheKey);
            cachedResponse = typedValue;
            return true;
        }

        DiagnosticConfig.LlmCacheMisses.Add(1);
        _logger?.LogDebug("LlmResponseCache: Cache miss for key {CacheKey}", cacheKey);
        cachedResponse = null;
        return false;
    }

    /// <summary>
    /// Cache the response from LLM structured output call.
    /// </summary>
    public void CacheResponse<T>(string prompt, string? model, string? tier, T response, TimeSpan? ttl = null) where T : class
    {
        if (!_options.EnableCaching || response == null)
            return;

        var cacheKey = GenerateCacheKey(prompt, model, tier);
        var expirationTime = ttl ?? _options.DefaultTimeToLive;

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationTime,
            SlidingExpiration = _options.SlidingExpiration
        };

        _cache.Set(cacheKey, response, cacheOptions);
        DiagnosticConfig.LlmCacheEntries.Add(1);
        _logger?.LogDebug("LlmResponseCache: Cached response for key {CacheKey} with TTL {TTL}ms", 
            cacheKey, (long)expirationTime.TotalMilliseconds);
    }

    /// <summary>
    /// Clear all cached responses.
    /// </summary>
    public void Clear()
    {
        // Note: IMemoryCache doesn't provide a Clear() method directly
        // This is noted as a limitation. Users should create a new IMemoryCache instance
        // or use a distributed cache for production scenarios.
        _logger?.LogInformation("LlmResponseCache: Clear requested (full cache clear not supported in IMemoryCache)");
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public LlmCacheStats GetStatistics()
    {
        var stats = new LlmCacheStats();

        // Note: Exact item count not available from IMemoryCache
        // Metrics are tracked via DiagnosticConfig counters

        return stats;
    }
}

/// <summary>
/// Configuration options for LLM Response Cache.
/// </summary>
public class LlmResponseCacheOptions
{
    /// <summary>
    /// Enable or disable caching (default: enabled).
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Default time-to-live for cached entries (default: 1 hour).
    /// </summary>
    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Sliding expiration window (optional, null = absolute only).
    /// If set, extending access time extends the TTL.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of entries to cache (soft limit for memory management).
    /// </summary>
    public int MaxEntries { get; set; } = 1000;
}

/// <summary>
/// Cache statistics snapshot.
/// </summary>
public class LlmCacheStats
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalEntries { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 ? (double)TotalHits / (TotalHits + TotalMisses) : 0;
}
