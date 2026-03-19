using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeepResearchAgent.Services.LLM;

/// <summary>
/// Resolver for selecting the appropriate LLM provider.
/// Enables dynamic switching between providers based on configuration.
/// </summary>
public interface ILlmProviderResolver
{
    /// <summary>
    /// Resolve the LLM provider based on current configuration.
    /// </summary>
    /// <param name="providerName">Optional provider name override. Uses default if not specified.</param>
    /// <returns>Resolved LLM provider</returns>
    ILlmProvider Resolve(string? providerName = null);

    /// <summary>
    /// Get list of available provider names.
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
}

/// <summary>
/// Default implementation of LLM provider resolver.
/// </summary>
public class LlmProviderResolver : ILlmProviderResolver
{
    private readonly Dictionary<string, ILlmProvider> _providers;
    private readonly IOptionsMonitor<LlmProviderOptions> _options;
    private readonly ILogger<LlmProviderResolver>? _logger;

    public LlmProviderResolver(
        IEnumerable<ILlmProvider> providers,
        IOptionsMonitor<LlmProviderOptions> options,
        ILogger<LlmProviderResolver>? logger = null)
    {
        _providers = providers.ToDictionary(p => p.ProviderName.ToLowerInvariant());
        _options = options;
        _logger = logger;

        _logger?.LogInformation(
            "LlmProviderResolver initialized with {ProviderCount} providers: {Providers}",
            _providers.Count,
            string.Join(", ", _providers.Keys));
    }

    public ILlmProvider Resolve(string? providerName = null)
    {
        var selectedProvider = (providerName ?? _options.CurrentValue.Provider).ToLowerInvariant();

        if (!_providers.TryGetValue(selectedProvider, out var provider))
        {
            var availableProviders = string.Join(", ", _providers.Keys);
            _logger?.LogWarning(
                "LLM provider '{Provider}' not found. Available: {Available}. Using default.",
                selectedProvider,
                availableProviders);

            // Fall back to default provider
            if (!_providers.TryGetValue(_options.CurrentValue.Provider.ToLowerInvariant(), out provider))
            {
                throw new InvalidOperationException(
                    $"No LLM providers available. Requested: {selectedProvider}, " +
                    $"Default: {_options.CurrentValue.Provider}");
            }
        }

        _logger?.LogInformation("Resolved LLM provider: {Provider} (model: {Model})", 
            provider.ProviderName, provider.DefaultModel);
        return provider;
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _providers.Keys.ToList();
    }
}
