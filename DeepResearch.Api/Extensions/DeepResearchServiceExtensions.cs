using DeepResearchAgent.Agents;
using DeepResearchAgent.Configuration;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Workflows;
using DeepResearchAgent.Workflows.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeepResearch.Api.Extensions;

/// <summary>
/// Extension methods for registering DeepResearchAgent services
/// </summary>
public static class DeepResearchServiceExtensions
{
    /// <summary>
    /// Add all DeepResearchAgent services (core services, agents, workflows)
    /// </summary>
    public static IServiceCollection AddDeepResearchAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read configuration
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var ollamaDefaultModel = configuration["Ollama:DefaultModel"] ?? "llama3.1:8b";
        var searxngBaseUrl = configuration["Search:SearXNG:BaseUrl"] ?? "http://localhost:8080";
        var crawl4aiBaseUrl = configuration["Search:Crawl4AI:BaseUrl"] ?? "http://localhost:8000";
        var lightningServerUrl = configuration["Lightning:ServerUrl"] ?? "http://localhost:8090";

        // Core Services
        AddCoreServices(services, configuration, ollamaBaseUrl, ollamaDefaultModel, 
            searxngBaseUrl, crawl4aiBaseUrl);

        // Lightning Services
        AddLightningServices(services, lightningServerUrl);

        // Supporting Services
        AddSupportingServices(services);

        // Agents
        AddAgents(services);

        // Workflows
        AddWorkflows(services);

        return services;
    }

    private static void AddCoreServices(
        IServiceCollection services,
        IConfiguration configuration,
        string ollamaBaseUrl,
        string ollamaDefaultModel,
        string searxngBaseUrl,
        string crawl4aiBaseUrl)
    {
        // LLM Providers (Ollama and LiteLLM)
        services.AddLlmProviders(configuration);

        // Search Service
        services.AddSingleton<SearCrawl4AIService>(sp => new SearCrawl4AIService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            searxngBaseUrl,
            crawl4aiBaseUrl
        ));

        // Web Search Provider - use adapter to implement IWebSearchProvider
        services.AddSingleton<IWebSearchProvider>(sp => 
            new SearCrawl4AIAdapter(
                sp.GetRequiredService<SearCrawl4AIService>(),
                sp.GetService<ILogger<SearCrawl4AIAdapter>>()));
    }

    private static void AddLightningServices(
        IServiceCollection services,
        string lightningServerUrl)
    {
        // Agent Lightning Service (required by LightningStateService)
        services.AddSingleton<IAgentLightningService>(sp => new AgentLightningService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            lightningServerUrl,
            logger: sp.GetService<ILogger<AgentLightningService>>()
        ));

        // Lightning RLCS Service (Reasoning Layer Confidence Scoring - required by LightningStateService)
        services.AddSingleton<ILightningRLCSService>(sp => new LightningRLCSService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            lightningServerUrl
        ));

        // Lightning State Service
        services.AddSingleton<ILightningStateService, LightningStateService>();
    }

    private static void AddSupportingServices(IServiceCollection services)
    {
        // Tool Invocation Service (required by agents)
        services.AddSingleton<ToolInvocationService>(sp => new ToolInvocationService(
            sp.GetRequiredService<IWebSearchProvider>(),
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetService<ILogger<ToolInvocationService>>()
        ));

        // State Transitioner (Phase 5 support)
        services.AddSingleton<StateTransitioner>(sp => new StateTransitioner(
            sp.GetService<ILogger<StateTransitioner>>()
        ));

        // Agent Error Recovery
        services.AddSingleton<AgentErrorRecovery>(sp => new AgentErrorRecovery(
            sp.GetService<ILogger<AgentErrorRecovery>>(),
            maxRetries: 2,
            retryDelay: TimeSpan.FromSeconds(1)
        ));
    }

    private static void AddAgents(IServiceCollection services)
    {
        // Researcher Agent
        services.AddSingleton<ResearcherAgent>(sp => new ResearcherAgent(
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetRequiredService<ToolInvocationService>(),
            sp.GetService<ILogger<ResearcherAgent>>()
        ));

        // Analyst Agent
        services.AddSingleton<AnalystAgent>(sp => new AnalystAgent(
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetRequiredService<ToolInvocationService>(),
            sp.GetService<ILogger<AnalystAgent>>()
        ));

        // Report Agent
        services.AddSingleton<ReportAgent>(sp => new ReportAgent(
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetRequiredService<ToolInvocationService>(),
            sp.GetService<ILogger<ReportAgent>>()
        ));
    }

    private static void AddWorkflows(IServiceCollection services)
    {
        // Researcher Workflow (9 optional parameters)
        services.AddSingleton<ResearcherWorkflow>(sp => new ResearcherWorkflow(
            stateService: sp.GetRequiredService<ILightningStateService>(),
            searchService: sp.GetRequiredService<SearCrawl4AIService>(),
            llmService: sp.GetRequiredService<ILlmProvider>(),
            store: sp.GetRequiredService<LightningStore>(),
            vectorDb: null,  // Optional - vector database not configured by default
            embeddingService: null,  // Optional - embedding service not configured by default
            logger: sp.GetService<ILogger<ResearcherWorkflow>>(),
            lightningService: null,  // Optional - Lightning service integration
            rmptConfig: null  // Optional - RMPT configuration
        ));

        // Supervisor Workflow
        services.AddSingleton<SupervisorWorkflow>(sp => new SupervisorWorkflow(
            sp.GetRequiredService<ILightningStateService>(),
            sp.GetRequiredService<ResearcherWorkflow>(),
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetRequiredService<IWebSearchProvider>(),
            sp.GetRequiredService<LightningStore>(),
            sp.GetService<ILogger<SupervisorWorkflow>>()
        ));

        // Master Workflow
        services.AddSingleton<MasterWorkflow>(sp => new MasterWorkflow(
            sp.GetRequiredService<ILightningStateService>(),
            sp.GetRequiredService<SupervisorWorkflow>(),
            sp.GetRequiredService<ILlmProvider>(),
            sp.GetRequiredService<IWebSearchProvider>(),
            sp.GetService<ILogger<MasterWorkflow>>(),
            stateManager: null,
            researcherAgent: sp.GetService<ResearcherAgent>(),
            analystAgent: sp.GetService<AnalystAgent>(),
            reportAgent: sp.GetService<ReportAgent>()
        ));
    }
}
