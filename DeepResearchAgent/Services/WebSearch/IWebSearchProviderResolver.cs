using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeepResearchAgent.Services.WebSearch;

/// <summary>
/// Resolver for selecting the appropriate web search provider.
/// Enables dynamic switching between providers based on configuration.
/// Similar to selecting different LLMs for different tasks.
/// </summary>
public interface IWebSearchProviderResolver
{
    /// <summary>
    /// Resolve the web search provider based on current configuration.
    /// </summary>
    /// <param name="providerName">Optional provider name override. Uses default if not specified.</param>
    /// <returns>Resolved web search provider</returns>
    IWebSearchProvider Resolve(string? providerName = null);

    /// <summary>
    /// Get list of available provider names.
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
}

/// <summary>
/// Default implementation of web search provider resolver.
/// </summary>
public class WebSearchProviderResolver : IWebSearchProviderResolver
{
    private readonly Dictionary<string, IWebSearchProvider> _providers;
    private readonly IOptionsMonitor<WebSearchOptions> _options;
    private readonly ILogger<WebSearchProviderResolver>? _logger;

    public WebSearchProviderResolver(
        IEnumerable<IWebSearchProvider> providers,
        IOptionsMonitor<WebSearchOptions> options,
        ILogger<WebSearchProviderResolver>? logger = null)
    {
        _providers = providers.ToDictionary(p => p.ProviderName.ToLowerInvariant());
        _options = options;
        _logger = logger;

        _logger?.LogInformation(
            "WebSearchProviderResolver initialized with {ProviderCount} providers: {Providers}",
            _providers.Count,
            string.Join(", ", _providers.Keys));
    }

    public IWebSearchProvider Resolve(string? providerName = null)
    {
        var selectedProvider = (providerName ?? _options.CurrentValue.Provider).ToLowerInvariant();

        if (!_providers.TryGetValue(selectedProvider, out var provider))
        {
            var availableProviders = string.Join(", ", _providers.Keys);
            _logger?.LogWarning(
                "Web search provider '{Provider}' not found. Available: {Available}. Using default.",
                selectedProvider,
                availableProviders);

            // Fall back to default provider
            if (!_providers.TryGetValue(_options.CurrentValue.Provider.ToLowerInvariant(), out provider))
            {
                throw new InvalidOperationException(
                    $"No web search providers available. Requested: {selectedProvider}, " +
                    $"Default: {_options.CurrentValue.Provider}");
            }
        }

        _logger?.LogInformation("Resolved web search provider: {Provider}", provider.ProviderName);
        return provider;
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _providers.Keys.ToList();
    }
}
