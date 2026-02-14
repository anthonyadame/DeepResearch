using DeepResearchAgent.Services;
using DeepResearchAgent.Services.WebSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeepResearchAgent.Workflows.Extensions;

/// <summary>
/// Extension methods for registering web search provider services in dependency injection.
/// </summary>
public static class WebSearchProviderExtensions
{
    /// <summary>
    /// Register web search providers and resolver.
    /// Configures available search providers (SearXNG, Tavily) and sets up provider selection.
    /// </summary>
    /// <example>
    /// services.AddWebSearchProviders(configuration);
    /// </example>
    public static IServiceCollection AddWebSearchProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind WebSearchOptions from configuration (section: "WebSearch")
        services.Configure<WebSearchOptions>(configuration.GetSection("WebSearch"));

        // Register SearCrawl4AI as a provider (existing service) - Changed to Singleton for API compatibility
        services.AddSingleton<SearCrawl4AIAdapter>(sp =>
        {
            var searCrawl4AIService = sp.GetRequiredService<SearCrawl4AIService>();
            var logger = sp.GetService<ILogger<SearCrawl4AIAdapter>>();
            return new SearCrawl4AIAdapter(searCrawl4AIService, logger);
        });

        // Register Tavily provider - Changed to Singleton for API compatibility
        services.AddSingleton<TavilySearchService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WebSearchOptions>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<TavilySearchService>>();

            var apiKey = options.Value.TavilyApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Tavily API key is not configured. " +
                    "Set 'WebSearch:TavilyApiKey' in appsettings.json or via environment variable.");
            }

            var timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
            var httpClient = httpClientFactory.CreateClient("TavilyClient");

            return new TavilySearchService(httpClient, apiKey, options.Value.TavilyBaseUrl, logger, timeout);
        });

        // Register provider resolver - Changed to Singleton for API compatibility
        services.AddSingleton<IWebSearchProviderResolver>(sp =>
        {
            var searCrawl4AIAdapter = sp.GetRequiredService<SearCrawl4AIAdapter>();
            var tavilyService = sp.GetService<TavilySearchService>();
            var options = sp.GetRequiredService<IOptionsMonitor<WebSearchOptions>>();
            var logger = sp.GetService<ILogger<WebSearchProviderResolver>>();

            var providers = new List<IWebSearchProvider> { searCrawl4AIAdapter };
            if (tavilyService != null)
            {
                providers.Add(tavilyService);
            }

            return new WebSearchProviderResolver(providers, options, logger);
        });

        // Register default IWebSearchProvider as a factory that resolves from resolver - Changed to Singleton
        services.AddSingleton<IWebSearchProvider>(sp =>
        {
            var resolver = sp.GetRequiredService<IWebSearchProviderResolver>();
            return resolver.Resolve();
        });

        return services;
    }
}
