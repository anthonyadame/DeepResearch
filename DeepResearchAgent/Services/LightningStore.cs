using DeepResearchAgent.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services;

/// <summary>
/// Integration with Microsoft Agent-Lightning's LightningStore for persisting facts and research data.
/// This wraps the Lightning Server's HTTP API to interact with Agent-Lightning's store.
/// </summary>
public interface ILightningStore
{
    Task SaveFactAsync(FactState fact, CancellationToken cancellationToken = default);
    Task SaveFactsAsync(IEnumerable<FactState> facts, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FactState>> GetAllFactsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FactState>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    // Lightning Store-specific methods
    Task<StoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<ResourcesUpdate> AddResourcesAsync(Dictionary<string, string> resources, CancellationToken cancellationToken = default);
    Task<ResourcesUpdate?> GetLatestResourcesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for LightningStore integration.
/// </summary>
public class LightningStoreOptions
{
    /// <summary>Local file-based storage directory (fallback mode)</summary>
    public string DataDirectory { get; set; } = "data";
    
    /// <summary>Local file name (fallback mode)</summary>
    public string FileName { get; set; } = "lightningstore.json";
    
    /// <summary>Lightning Server URL for distributed store access</summary>
    public string LightningServerUrl { get; set; } = "http://localhost:8090";
    
    /// <summary>Use Lightning Server (true) or local file storage (false)</summary>
    public bool UseLightningServer { get; set; } = true;
    
    /// <summary>Resource namespace for facts storage</summary>
    public string ResourceNamespace { get; set; } = "facts";

    public string FilePath => Path.Combine(DataDirectory, FileName);
}

/// <summary>
/// Hybrid LightningStore implementation that integrates with Microsoft Agent-Lightning.
/// - Uses Lightning Server HTTP API when available (distributed mode)
/// - Falls back to local file storage when Lightning Server is unavailable
/// </summary>
public class LightningStore : ILightningStore
{
    private readonly LightningStoreOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    private bool _isLightningServerAvailable;

    public LightningStore(LightningStoreOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? new LightningStoreOptions();
        _httpClient = httpClient ?? new HttpClient();
        Directory.CreateDirectory(_options.DataDirectory);
        
        // Check Lightning Server availability asynchronously
        _isLightningServerAvailable = CheckLightningServerAsync().GetAwaiter().GetResult();
    }

    private async Task<bool> CheckLightningServerAsync()
    {
        if (!_options.UseLightningServer)
            return false;
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{_options.LightningServerUrl}/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task SaveFactAsync(FactState fact, CancellationToken cancellationToken = default)
    {
        await SaveFactsAsync(new[] { fact }, cancellationToken);
    }

    public async Task SaveFactsAsync(IEnumerable<FactState> facts, CancellationToken cancellationToken = default)
    {
        if (_isLightningServerAvailable)
        {
            await SaveFactsToLightningServerAsync(facts, cancellationToken);
        }
        else
        {
            await SaveFactsToFileAsync(facts, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<FactState>> GetAllFactsAsync(CancellationToken cancellationToken = default)
    {
        if (_isLightningServerAvailable)
        {
            return await GetFactsFromLightningServerAsync(cancellationToken);
        }
        else
        {
            return await GetFactsFromFileAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<FactState>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<FactState>();
        }

        var allFacts = await GetAllFactsAsync(cancellationToken);
        return allFacts
            .Where(f => f.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        f.SourceUrl.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_isLightningServerAvailable)
        {
            // Clear resources in Lightning Server by uploading empty resource set
            await AddResourcesAsync(new Dictionary<string, string>(), cancellationToken);
        }
        else
        {
            await _mutex.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(_options.FilePath))
                {
                    File.Delete(_options.FilePath);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

    // ========== Lightning Store-Specific Methods ==========
    
    public async Task<StoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        if (!_isLightningServerAvailable)
        {
            return new StoreStatistics
            {
                TotalRollouts = 0,
                TotalAttempts = 0,
                TotalSpans = 0,
                ActiveRollouts = 0
            };
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.LightningServerUrl}/api/server/info",
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            
            var serverInfo = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: cancellationToken);
            
            return new StoreStatistics
            {
                TotalRollouts = 0,
                TotalAttempts = 0,
                TotalSpans = 0,
                ActiveRollouts = 0,
                ServerInfo = serverInfo
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to get Lightning Store statistics", ex);
        }
    }

    public async Task<ResourcesUpdate> AddResourcesAsync(Dictionary<string, string> resources, CancellationToken cancellationToken = default)
    {
        if (!_isLightningServerAvailable)
        {
            throw new InvalidOperationException("Lightning Server not available for resource storage");
        }

        try
        {
            var payload = new
            {
                resources = resources,
                namespace_name = _options.ResourceNamespace,
                timestamp = DateTime.UtcNow
            };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.LightningServerUrl}/api/resources/add",
                payload,
                cancellationToken
            );
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Warning: Failed to add resources via Lightning Server: {response.StatusCode}");
            }
            
            return new ResourcesUpdate
            {
                ResourcesId = Guid.NewGuid().ToString(),
                Resources = resources,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to add resources to Lightning Store", ex);
        }
    }

    public async Task<ResourcesUpdate?> GetLatestResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (!_isLightningServerAvailable)
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.LightningServerUrl}/api/resources/latest",
                cancellationToken
            );
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            return await response.Content.ReadFromJsonAsync<ResourcesUpdate>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    // ========== Private Lightning Server Methods ==========
    
    private async Task SaveFactsToLightningServerAsync(IEnumerable<FactState> facts, CancellationToken cancellationToken)
    {
        try
        {
            // Convert facts to resources format for Lightning Store
            var resources = new Dictionary<string, string>();
            foreach (var fact in facts)
            {
                // Use Id property, fallback to generating one if needed
                var factId = string.IsNullOrEmpty(fact.Id) ? Guid.NewGuid().ToString() : fact.Id;
                var key = $"{_options.ResourceNamespace}:{factId}";
                resources[key] = JsonSerializer.Serialize(fact, _serializerOptions);
            }
            
            await AddResourcesAsync(resources, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fallback to file storage if Lightning Server fails
            Console.WriteLine($"Lightning Server storage failed, falling back to file: {ex.Message}");
            _isLightningServerAvailable = false;
            await SaveFactsToFileAsync(facts, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<FactState>> GetFactsFromLightningServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resourcesUpdate = await GetLatestResourcesAsync(cancellationToken);
            if (resourcesUpdate?.Resources == null)
            {
                return Array.Empty<FactState>();
            }
            
            var facts = new List<FactState>();
            var prefix = $"{_options.ResourceNamespace}:";
            
            foreach (var (key, value) in resourcesUpdate.Resources)
            {
                if (key.StartsWith(prefix))
                {
                    var fact = JsonSerializer.Deserialize<FactState>(value, _serializerOptions);
                    if (fact != null)
                    {
                        facts.Add(fact);
                    }
                }
            }
            
            return facts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lightning Server retrieval failed, falling back to file: {ex.Message}");
            _isLightningServerAvailable = false;
            return await GetFactsFromFileAsync(cancellationToken);
        }
    }

    // ========== Private File Storage Methods (Fallback) =========
    
    private async Task SaveFactsToFileAsync(IEnumerable<FactState> facts, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var allFacts = await LoadFactsFromFileLockedAsync(cancellationToken);
            allFacts.AddRange(facts);
            await PersistFactsToFileLockedAsync(allFacts, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<IReadOnlyList<FactState>> GetFactsFromFileAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var facts = await LoadFactsFromFileLockedAsync(cancellationToken);
            return facts.ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<FactState>> LoadFactsFromFileLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
        {
            return new List<FactState>();
        }

        await using var stream = File.Open(_options.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var facts = await JsonSerializer.DeserializeAsync<List<FactState>>(stream, _serializerOptions, cancellationToken);
        return facts ?? new List<FactState>();
    }

    private async Task PersistFactsToFileLockedAsync(List<FactState> facts, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(_options.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, facts, _serializerOptions, cancellationToken);
    }
}

// ========== Supporting Models ==========

public class StoreStatistics
{
    public int TotalRollouts { get; set; }
    public int TotalAttempts { get; set; }
    public int TotalSpans { get; set; }
    public int ActiveRollouts { get; set; }
    public Dictionary<string, object>? ServerInfo { get; set; }
}

public class ResourcesUpdate
{
    public string ResourcesId { get; set; } = string.Empty;
    public Dictionary<string, string> Resources { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}
