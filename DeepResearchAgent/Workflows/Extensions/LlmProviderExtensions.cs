using DeepResearchAgent.Services.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeepResearchAgent.Workflows.Extensions;

/// <summary>
/// Extension methods for registering LLM provider services in dependency injection.
/// </summary>
public static class LlmProviderExtensions
{
    /// <summary>
    /// Register LLM providers and resolver.
    /// Configures available LLM providers (Ollama, LiteLLM) and sets up provider selection.
    /// </summary>
    /// <example>
    /// services.AddLlmProviders(configuration);
    /// </example>
    public static IServiceCollection AddLlmProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind LlmProviderOptions from configuration (section: "LlmProvider")
        services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));

        // Register Ollama provider
        services.AddSingleton<OllamaLlmProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmProviderOptions>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<OllamaLlmProvider>>();

            var httpClient = httpClientFactory.CreateClient("OllamaClient");
            var timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
            httpClient.Timeout = timeout;

            return new OllamaLlmProvider(
                options.Value.Ollama.BaseUrl,
                options.Value.Ollama.DefaultModel,
                httpClient,
                logger);
        });

        // Register LiteLLM provider
        services.AddSingleton<LiteLlmProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmProviderOptions>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<LiteLlmProvider>>();

            var httpClient = httpClientFactory.CreateClient("LiteLlmClient");
            var timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
            httpClient.Timeout = timeout;

            return new LiteLlmProvider(
                options.Value.LiteLLM.BaseUrl,
                options.Value.LiteLLM.DefaultModel,
                httpClient,
                options.Value.LiteLLM.ApiKey,
                logger);
        });

        // Register provider resolver
        services.AddSingleton<ILlmProviderResolver>(sp =>
        {
            var ollamaProvider = sp.GetRequiredService<OllamaLlmProvider>();
            var liteLlmProvider = sp.GetRequiredService<LiteLlmProvider>();
            var options = sp.GetRequiredService<IOptionsMonitor<LlmProviderOptions>>();
            var logger = sp.GetService<ILogger<LlmProviderResolver>>();

            var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

            return new LlmProviderResolver(providers, options, logger);
        });

        // Register default ILlmProvider as a factory that resolves from resolver
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var resolver = sp.GetRequiredService<ILlmProviderResolver>();
            return resolver.Resolve();
        });

        // Register HttpClients for each provider
        services.AddHttpClient("OllamaClient");
        services.AddHttpClient("LiteLlmClient");

        return services;
    }
}
