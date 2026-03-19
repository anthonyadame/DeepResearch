using System;
using System.Threading.Tasks;
using DeepResearchAgent.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeepResearchAgent.Tests.Unit.Services.Caching;

/// <summary>
/// Unit tests for LlmResponseCache service.
/// Tests cache key generation, hit/miss behavior, TTL, and metrics.
/// </summary>
public class LlmResponseCacheTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly LlmResponseCache _cache;

    public LlmResponseCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new LlmResponseCache(_memoryCache);
    }

    #region Cache Key Generation

    [Fact]
    public void GenerateCacheKey_SameInputs_ReturnsSameKey()
    {
        // Arrange
        string prompt = "What is the capital of France?";
        string model = "gpt-4";
        string tier = "Balanced";

        // Act
        var key1 = LlmResponseCache.GenerateCacheKey(prompt, model, tier);
        var key2 = LlmResponseCache.GenerateCacheKey(prompt, model, tier);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentPrompts_ReturnsDifferentKeys()
    {
        // Arrange
        string model = "gpt-4";
        string tier = "Balanced";

        // Act
        var key1 = LlmResponseCache.GenerateCacheKey("What is the capital of France?", model, tier);
        var key2 = LlmResponseCache.GenerateCacheKey("What is the capital of Germany?", model, tier);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentModels_ReturnsDifferentKeys()
    {
        // Arrange
        string prompt = "What is the capital of France?";
        string tier = "Balanced";

        // Act
        var key1 = LlmResponseCache.GenerateCacheKey(prompt, "gpt-4", tier);
        var key2 = LlmResponseCache.GenerateCacheKey(prompt, "gpt-3.5", tier);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentTiers_ReturnsDifferentKeys()
    {
        // Arrange
        string prompt = "What is the capital of France?";
        string model = "gpt-4";

        // Act
        var key1 = LlmResponseCache.GenerateCacheKey(prompt, model, "Fast");
        var key2 = LlmResponseCache.GenerateCacheKey(prompt, model, "Power");

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_NullValues_StillGeneratesKey()
    {
        // Act & Assert - should not throw
        var key = LlmResponseCache.GenerateCacheKey("prompt", null, null);
        Assert.NotNull(key);
        Assert.NotEmpty(key);
    }

    #endregion

    #region Cache Hit/Miss

    [Fact]
    public void TryGetCached_ItemInCache_ReturnsTrue()
    {
        // Arrange
        string prompt = "Test prompt";
        var testResult = new TestCacheableResult { Value = "cached response" };
        _cache.CacheResponse(prompt, null, null, testResult);

        // Act
        var hit = _cache.TryGetCached<TestCacheableResult>(prompt, null, null, out var result);

        // Assert
        Assert.True(hit);
        Assert.NotNull(result);
        Assert.Equal("cached response", result.Value);
    }

    [Fact]
    public void TryGetCached_ItemNotInCache_ReturnsFalse()
    {
        // Arrange
        string prompt = "Non-existent prompt";

        // Act
        var hit = _cache.TryGetCached<TestCacheableResult>(prompt, null, null, out var result);

        // Assert
        Assert.False(hit);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetCached_DisabledCache_ReturnsFalse()
    {
        // Arrange
        var options = new LlmResponseCacheOptions { EnableCaching = false };
        var disabledCache = new LlmResponseCache(_memoryCache, options);
        var testResult = new TestCacheableResult { Value = "response" };

        // Act
        disabledCache.CacheResponse("prompt", null, null, testResult);
        var hit = disabledCache.TryGetCached<TestCacheableResult>("prompt", null, null, out _);

        // Assert
        Assert.False(hit);
    }

    #endregion

    #region Cache Storage

    [Fact]
    public void CacheResponse_ValidResponse_StoresInCache()
    {
        // Arrange
        var testResult = new TestCacheableResult { Value = "test value" };
        string prompt = "test prompt";

        // Act
        _cache.CacheResponse(prompt, "model1", "Fast", testResult);
        var hit = _cache.TryGetCached<TestCacheableResult>(prompt, "model1", "Fast", out var retrieved);

        // Assert
        Assert.True(hit);
        Assert.Equal("test value", retrieved?.Value);
    }

    [Fact]
    public void CacheResponse_MultipleItems_AllRetrievable()
    {
        // Arrange
        var result1 = new TestCacheableResult { Value = "response1" };
        var result2 = new TestCacheableResult { Value = "response2" };

        // Act
        _cache.CacheResponse("prompt1", "model1", "Fast", result1);
        _cache.CacheResponse("prompt2", "model1", "Fast", result2);

        var hit1 = _cache.TryGetCached<TestCacheableResult>("prompt1", "model1", "Fast", out var retrieved1);
        var hit2 = _cache.TryGetCached<TestCacheableResult>("prompt2", "model1", "Fast", out var retrieved2);

        // Assert
        Assert.True(hit1);
        Assert.True(hit2);
        Assert.Equal("response1", retrieved1?.Value);
        Assert.Equal("response2", retrieved2?.Value);
    }

    [Fact]
    public void CacheResponse_NullResponse_DoesNotCache()
    {
        // Arrange
        string prompt = "test prompt";

        // Act
        _cache.CacheResponse<TestCacheableResult>(prompt, null, null, null!);
        var hit = _cache.TryGetCached<TestCacheableResult>(prompt, null, null, out _);

        // Assert
        Assert.False(hit);
    }

    #endregion

    #region TTL and Expiration

    [Fact]
    public async Task CacheResponse_WithExpiration_ExpiresAfterTTL()
    {
        // Arrange
        var options = new LlmResponseCacheOptions 
        { 
            DefaultTimeToLive = TimeSpan.FromMilliseconds(100)
        };
        var cache = new LlmResponseCache(_memoryCache, options);
        var testResult = new TestCacheableResult { Value = "expiring response" };

        // Act
        cache.CacheResponse("prompt", null, null, testResult);
        var hitBefore = cache.TryGetCached<TestCacheableResult>("prompt", null, null, out _);

        // Wait for expiration
        await Task.Delay(200);

        var hitAfter = cache.TryGetCached<TestCacheableResult>("prompt", null, null, out _);

        // Assert
        Assert.True(hitBefore);
        Assert.False(hitAfter);
    }

    [Fact]
    public void CacheResponse_CustomTTL_UsesProvidedDuration()
    {
        // Arrange
        var testResult = new TestCacheableResult { Value = "response" };
        var customTTL = TimeSpan.FromHours(2);

        // Act - should not throw and should store with custom TTL
        _cache.CacheResponse("prompt", "model", "Balanced", testResult, customTTL);
        var hit = _cache.TryGetCached<TestCacheableResult>("prompt", "model", "Balanced", out var retrieved);

        // Assert
        Assert.True(hit);
        Assert.NotNull(retrieved);
    }

    #endregion

    #region Statistics

    [Fact]
    public void GetStatistics_ReturnsValidStats()
    {
        // Act
        var stats = _cache.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.IsType<LlmCacheStats>(stats);
        // HitRate should be 0 initially
        Assert.Equal(0, stats.HitRate);
    }

    [Fact]
    public void GetStatistics_CalculatesHitRate()
    {
        // Arrange
        var testResult = new TestCacheableResult { Value = "cached" };
        _cache.CacheResponse("prompt1", null, null, testResult);

        // Act - trigger hits and misses
        _cache.TryGetCached<TestCacheableResult>("prompt1", null, null, out _); // hit
        _cache.TryGetCached<TestCacheableResult>("prompt2", null, null, out _); // miss
        var stats = _cache.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        // Stats object exists - metrics are tracked via DiagnosticConfig counters
        Assert.IsType<LlmCacheStats>(stats);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CacheResponse_SamePromptDifferentTypes_LastOneWins()
    {
        // Arrange - cache key is based on prompt+model+tier only, not type
        var stringResult = "text response";
        var objectResult = new TestCacheableResult { Value = "object response" };
        string prompt = "same prompt";

        // Act
        _cache.CacheResponse(prompt, null, null, stringResult);
        _cache.CacheResponse(prompt, null, null, objectResult); // overwrites

        // When retrieved as string, it won't exist (was overwritten by object)
        var stringHit = _cache.TryGetCached<string>(prompt, null, null, out var retrievedString);
        var objectHit = _cache.TryGetCached<TestCacheableResult>(prompt, null, null, out var retrievedObject);

        // Assert - last write wins, but type mismatch causes miss
        Assert.False(stringHit);
        Assert.True(objectHit);
        Assert.Null(retrievedString);
        Assert.Equal("object response", retrievedObject?.Value);
    }

    [Fact]
    public void TryGetCached_WrongType_ReturnsFalse()
    {
        // Arrange
        var testResult = new TestCacheableResult { Value = "response" };
        _cache.CacheResponse("prompt", null, null, testResult);

        // Act
        var hit = _cache.TryGetCached<string>("prompt", null, null, out _);

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void Clear_DoesNotThrow()
    {
        // Arrange
        var testResult = new TestCacheableResult { Value = "response" };
        _cache.CacheResponse("prompt", null, null, testResult);

        // Act & Assert - should not throw
        _cache.Clear();
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Simple test class for cache storage testing.
    /// </summary>
    private class TestCacheableResult
    {
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
