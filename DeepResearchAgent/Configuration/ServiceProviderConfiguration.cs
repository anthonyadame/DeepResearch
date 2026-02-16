using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.VectorDatabase;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Workflows;
using DeepResearchAgent.Workflows.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Configuration;

/// <summary>
/// Configures and builds the service provider with all required services and dependencies.
/// </summary>
public static class ServiceProviderConfiguration
{
    /// <summary>
    /// Builds and returns a configured ServiceProvider instance.
    /// </summary>
    public static ServiceProvider BuildServiceProvider()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        // Register configuration values
        RegisterConfigurationValues(services, configuration);

        // Register core services
        RegisterCoreServices(services);

        // Register persistence services
        RegisterPersistenceServices(services, configuration);

        // Register web search and scraping services
        RegisterWebSearchServices(services, configuration);

        // Register embedding and vector database services
        RegisterVectorDatabaseServices(services, configuration);

        // Register Agent-Lightning services
        RegisterAgentLightningServices(services, configuration);

        // Register workflow and agent services
        RegisterWorkflowAndAgentServices(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Returns the configuration and endpoint values as a tuple.
    /// </summary>
    public static (
        IConfiguration Configuration,
        string OllamaBaseUrl,
        string OllamaDefaultModel,
        string SearXngBaseUrl,
        string Crawl4aiBaseUrl,
        string LightningServerUrl,
        LightningAPOConfig ApoConfig,
        bool VectorDbEnabled,
        string QdrantBaseUrl,
        string QdrantCollectionName,
        int QdrantVectorDimension,
        string EmbeddingModel,
        string EmbeddingApiUrl
    ) GetConfigurationValues()
    {
        var configuration = BuildConfiguration();

        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var ollamaDefaultModel = configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b";
        var searxngBaseUrl = configuration["SearXNG:BaseUrl"] ?? "http://localhost:8080";
        var crawl4aiBaseUrl = configuration["Crawl4AI:BaseUrl"] ?? "http://localhost:11235";
        var lightningServerUrl = configuration["Lightning:ServerUrl"]
            ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
            ?? "http://localhost:8090";

        var apoConfig = new LightningAPOConfig();
        configuration.GetSection("Lightning:APO").Bind(apoConfig);

        var vectorDbEnabled = configuration.GetValue("VectorDatabase:Enabled", false);
        var qdrantBaseUrl = configuration["VectorDatabase:Qdrant:BaseUrl"] ?? "http://localhost:6333";
        var qdrantCollectionName = configuration["VectorDatabase:Qdrant:CollectionName"] ?? "research";
        var qdrantVectorDimension = configuration.GetValue("VectorDatabase:Qdrant:VectorDimension", 384);
        var embeddingModel = configuration["VectorDatabase:EmbeddingModel"] ?? "nomic-embed-text";
        var embeddingApiUrl = configuration["VectorDatabase:EmbeddingApiUrl"] ?? ollamaBaseUrl;

        return (
            configuration,
            ollamaBaseUrl,
            ollamaDefaultModel,
            searxngBaseUrl,
            crawl4aiBaseUrl,
            lightningServerUrl,
            apoConfig,
            vectorDbEnabled,
            qdrantBaseUrl,
            qdrantCollectionName,
            qdrantVectorDimension,
            embeddingModel,
            embeddingApiUrl
        );
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.websearch.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.apo.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static void RegisterConfigurationValues(IServiceCollection services, IConfiguration configuration)
    {
        var apoConfig = new LightningAPOConfig();
        configuration.GetSection("Lightning:APO").Bind(apoConfig);
        services.AddSingleton(apoConfig);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Error);
        });

        services.AddMemoryCache();
        services.AddSingleton<HttpClient>();
    }

    private static void RegisterPersistenceServices(IServiceCollection services, IConfiguration configuration)
    {
        var lightningServerUrl = configuration["Lightning:ServerUrl"]
            ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
            ?? "http://localhost:8090";

        services.AddSingleton<OllamaService>(_ => new OllamaService(
            baseUrl: configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
            defaultModel: configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b"
        ));

        services.AddSingleton<OllamaSharpService>(_ => new OllamaSharpService(
            baseUrl: configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
            defaultModel: configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b"
        ));

        services.AddSingleton<LightningStoreOptions>(sp => new LightningStoreOptions
        {
            DataDirectory = configuration["LightningStore:DataDirectory"] ?? "data",
            FileName = configuration["LightningStore:FileName"] ?? "lightningstore.json",
            LightningServerUrl = lightningServerUrl,
            UseLightningServer = configuration.GetValue("LightningStore:UseLightningServer", true),
            ResourceNamespace = configuration["LightningStore:ResourceNamespace"] ?? "facts"
        });

        services.AddSingleton<ILightningStore>(sp => new LightningStore(
            sp.GetRequiredService<LightningStoreOptions>(),
            sp.GetRequiredService<HttpClient>()
        ));

        services.AddSingleton<LightningStore>(sp => (LightningStore)sp.GetRequiredService<ILightningStore>());
    }

    private static void RegisterWebSearchServices(IServiceCollection services, IConfiguration configuration)
    {
        var searxngBaseUrl = configuration["SearXNG:BaseUrl"] ?? "http://localhost:8080";
        var crawl4aiBaseUrl = configuration["Crawl4AI:BaseUrl"] ?? "http://localhost:11235";

        services.AddSingleton<SearCrawl4AIService>(sp => new SearCrawl4AIService(
            sp.GetRequiredService<HttpClient>(),
            searxngBaseUrl,
            crawl4aiBaseUrl
        ));

        services.AddHttpClient("SearXNG");
        services.AddWebSearchProviders(configuration);
    }

    private static void RegisterVectorDatabaseServices(IServiceCollection services, IConfiguration configuration)
    {
        var vectorDbEnabled = configuration.GetValue("VectorDatabase:Enabled", false);
        var qdrantBaseUrl = configuration["VectorDatabase:Qdrant:BaseUrl"] ?? "http://localhost:6333";
        var qdrantCollectionName = configuration["VectorDatabase:Qdrant:CollectionName"] ?? "research";
        var qdrantVectorDimension = configuration.GetValue("VectorDatabase:Qdrant:VectorDimension", 384);
        var embeddingModel = configuration["VectorDatabase:EmbeddingModel"] ?? "nomic-embed-text";
        var embeddingApiUrl = configuration["VectorDatabase:EmbeddingApiUrl"]
            ?? configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

        services.AddSingleton<IEmbeddingService>(sp => new OllamaEmbeddingService(
            sp.GetRequiredService<HttpClient>(),
            baseUrl: embeddingApiUrl,
            model: embeddingModel,
            dimension: qdrantVectorDimension,
            logger: sp.GetService<Microsoft.Extensions.Logging.ILogger>()
        ));

        if (vectorDbEnabled)
        {
            services.AddSingleton<IVectorDatabaseService>(sp => new QdrantVectorDatabaseService(
                sp.GetRequiredService<HttpClient>(),
                new QdrantConfig
                {
                    BaseUrl = qdrantBaseUrl,
                    CollectionName = qdrantCollectionName,
                    VectorDimension = qdrantVectorDimension
                },
                sp.GetRequiredService<IEmbeddingService>(),
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger>()
            ));
        }

        services.AddSingleton<IVectorDatabaseFactory>(sp =>
        {
            var factory = new VectorDatabaseFactory(sp.GetService<Microsoft.Extensions.Logging.ILogger>());

            if (vectorDbEnabled && sp.GetService<IVectorDatabaseService>() != null)
            {
                factory.RegisterVectorDatabase("qdrant", sp.GetRequiredService<IVectorDatabaseService>());
            }

            return factory;
        });
    }

    private static void RegisterAgentLightningServices(IServiceCollection services, IConfiguration configuration)
    {
        var lightningServerUrl = configuration["Lightning:ServerUrl"]
            ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
            ?? "http://localhost:8090";

        services.AddHttpClient<IAgentLightningService, AgentLightningService>();
        services.AddSingleton<IAgentLightningService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AgentLightningService));
            var apo = sp.GetRequiredService<LightningAPOConfig>();

            httpClient.Timeout = TimeSpan.FromSeconds(apo.ResourceLimits.TaskTimeoutSeconds);

            return new AgentLightningService(
                httpClient,
                lightningServerUrl,
                clientId: null,
                apo: apo);
        });

        services.AddSingleton<ILightningVERLService>(sp => new LightningVERLService(
            sp.GetRequiredService<HttpClient>(),
            lightningServerUrl
        ));

        services.AddSingleton<ILightningStateService, LightningStateService>();
        services.AddHostedService<LightningApoScaler>();
    }

    private static void RegisterWorkflowAndAgentServices(IServiceCollection services)
    {
        services.AddSingleton<StateManager>();
        services.AddSingleton<WorkflowModelConfiguration>();

        // Register agents
        services.AddSingleton<ResearcherAgent>(sp => new ResearcherAgent(
            sp.GetRequiredService<OllamaService>(),
            new ToolInvocationService(
                sp.GetRequiredService<IWebSearchProvider>(),
                sp.GetRequiredService<OllamaService>()
            ),
            sp.GetService<ILogger<ResearcherAgent>>()
        ));

        services.AddSingleton<AnalystAgent>(sp => new AnalystAgent(
            sp.GetRequiredService<OllamaService>(),
            new ToolInvocationService(
                sp.GetRequiredService<IWebSearchProvider>(),
                sp.GetRequiredService<OllamaService>()
            ),
            sp.GetService<ILogger<AnalystAgent>>()
        ));

        services.AddSingleton<ReportAgent>(sp => new ReportAgent(
            sp.GetRequiredService<OllamaService>(),
            new ToolInvocationService(
                sp.GetRequiredService<IWebSearchProvider>(),
                sp.GetRequiredService<OllamaService>()
            ),
            sp.GetService<ILogger<ReportAgent>>()
        ));

        // Register workflows
        services.AddSingleton<ResearcherWorkflow>(sp => new ResearcherWorkflow(
            sp.GetRequiredService<ILightningStateService>(),
            sp.GetRequiredService<SearCrawl4AIService>(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<LightningStore>(),
            sp.GetService<IVectorDatabaseService>(),
            sp.GetService<IEmbeddingService>(),
            sp.GetService<ILogger<ResearcherWorkflow>>(),
            sp.GetService<IAgentLightningService>(),
            sp.GetService<LightningAPOConfig>()
        ));

        services.AddSingleton<SupervisorWorkflow>(sp => new SupervisorWorkflow(
            sp.GetRequiredService<ILightningStateService>(),
            sp.GetRequiredService<ResearcherWorkflow>(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<IWebSearchProvider>(),
            sp.GetRequiredService<LightningStore>(),
            sp.GetService<ILogger<SupervisorWorkflow>>(),
            sp.GetRequiredService<StateManager>(),
            sp.GetRequiredService<WorkflowModelConfiguration>()
        ));

        services.AddSingleton<MasterWorkflow>(sp => new MasterWorkflow(
            sp.GetRequiredService<ILightningStateService>(),
            sp.GetRequiredService<SupervisorWorkflow>(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<IWebSearchProvider>(),
            sp.GetService<ILogger<MasterWorkflow>>(),
            sp.GetRequiredService<StateManager>(),
            sp.GetService<ResearcherAgent>(),
            sp.GetService<AnalystAgent>(),
            sp.GetService<ReportAgent>()
        ));
    }
}
