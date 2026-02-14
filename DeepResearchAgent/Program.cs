using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.VectorDatabase;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Workflows;
using DeepResearchAgent.Workflows.Extensions;
using DeepResearchAgent.Agents;
using DeepResearchAgent.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

Console.WriteLine("=== Deep Research Agent - C# Implementation ===");
Console.WriteLine("Multi-agent research system with modern workflow architecture");
Console.WriteLine("Powered by Microsoft Agent-Lightning\n");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.websearch.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.apo.json"), optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaDefaultModel = configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b";
var searxngBaseUrl = configuration["SearXNG:BaseUrl"] ?? "http://localhost:8080";
var crawl4aiBaseUrl = configuration["Crawl4AI:BaseUrl"] ?? "http://localhost:11235";
var lightningServerUrl = configuration["Lightning:ServerUrl"]
    ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
    ?? "http://localhost:8090";

// APO configuration
var apoConfig = new LightningAPOConfig();
configuration.GetSection("Lightning:APO").Bind(apoConfig);

// Vector database configuration
var vectorDbEnabled = configuration.GetValue("VectorDatabase:Enabled", false);
var qdrantBaseUrl = configuration["VectorDatabase:Qdrant:BaseUrl"] ?? "http://localhost:6333";
var qdrantCollectionName = configuration["VectorDatabase:Qdrant:CollectionName"] ?? "research";
var qdrantVectorDimension = configuration.GetValue("VectorDatabase:Qdrant:VectorDimension", 384);
var embeddingModel = configuration["VectorDatabase:EmbeddingModel"] ?? "nomic-embed-text";
var embeddingApiUrl = configuration["VectorDatabase:EmbeddingApiUrl"] ?? ollamaBaseUrl;

// Initialize services
var services = new ServiceCollection();

services.AddLogging(logging => 
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Error);
});

// Register core services
services.AddMemoryCache();
services.AddSingleton(apoConfig); // Register APO config
services.AddSingleton<OllamaService>(_ => new OllamaService(
    baseUrl: ollamaBaseUrl,
    defaultModel: ollamaDefaultModel
));
services.AddSingleton<HttpClient>();

// Register LightningStore (required by ResearcherWorkflow and SupervisorWorkflow)
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

services.AddSingleton<SearCrawl4AIService>(sp => new SearCrawl4AIService(
    sp.GetRequiredService<HttpClient>(),
    searxngBaseUrl,
    crawl4aiBaseUrl
));

// Register web search providers (SearXNG + Tavily) and resolver
services.AddHttpClient("SearXNG");          // used by TavilySearchService
services.AddWebSearchProviders(configuration);   // wires IWebSearchProvider/IWebSearchProviderResolver

// Register embedding service
services.AddSingleton<IEmbeddingService>(sp => new OllamaEmbeddingService(
    sp.GetRequiredService<HttpClient>(),
    baseUrl: embeddingApiUrl,
    model: embeddingModel,
    dimension: qdrantVectorDimension,
    logger: sp.GetService<Microsoft.Extensions.Logging.ILogger>()
));

// Register vector database service (Qdrant)
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

// Register vector database factory (for multiple DB support)
services.AddSingleton<IVectorDatabaseFactory>(sp =>
{
    var factory = new VectorDatabaseFactory(sp.GetService<Microsoft.Extensions.Logging.ILogger>());
    
    if (vectorDbEnabled && sp.GetService<IVectorDatabaseService>() != null)
    {
        factory.RegisterVectorDatabase("qdrant", sp.GetRequiredService<IVectorDatabaseService>());
    }
    
    return factory;
});

// Register Agent-Lightning services
services.AddHttpClient<IAgentLightningService, AgentLightningService>();
services.AddSingleton<IAgentLightningService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AgentLightningService));
    var apo = sp.GetRequiredService<LightningAPOConfig>();
    
    // Configure HTTP client timeout based on APO config
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

// Register APO auto-scaler as hosted service (if enabled)
services.AddHostedService<LightningApoScaler>();

// Register supporting services for workflows
services.AddSingleton<StateManager>();
services.AddSingleton<WorkflowModelConfiguration>();

// Register Phase 4 agents (ResearcherAgent, AnalystAgent, ReportAgent)
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

// Build service provider
var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("✓ Services initialized");
Console.WriteLine($"✓ Ollama connection configured ({ollamaBaseUrl})");
Console.WriteLine($"✓ Web search + scraping configured (SearXNG: {searxngBaseUrl}, Crawl4AI: {crawl4aiBaseUrl})");
Console.WriteLine("✓ Knowledge persistence configured (LightningStore)");
if (vectorDbEnabled)
{
    Console.WriteLine($"✓ Vector database configured (Qdrant: {qdrantBaseUrl})");
    Console.WriteLine($"✓ Embedding service configured ({embeddingModel})");
}
else
{
    Console.WriteLine("ℹ Vector database disabled (configure VectorDatabase:Enabled to enable)");
}
Console.WriteLine($"✓ Agent-Lightning integration configured ({lightningServerUrl})");
Console.WriteLine($"✓ APO (Automatic Performance Optimization) enabled - Strategy: {apoConfig.Strategy}");
Console.WriteLine($"✓ APO Resource Limits - MaxConcurrent: {apoConfig.ResourceLimits.MaxConcurrentTasks}, Timeout: {apoConfig.ResourceLimits.TaskTimeoutSeconds}s");
if (apoConfig.AutoScaling.Enabled)
{
    Console.WriteLine($"✓ APO Auto-Scaling enabled - {apoConfig.AutoScaling.MinInstances}-{apoConfig.AutoScaling.MaxInstances} instances");
}
Console.WriteLine("✓ VERL (Verification and Reasoning Layer) enabled");
Console.WriteLine("✓ Workflows initialized\n");

// Interactive menu loop
bool running = true;
while (running)
{
    DisplayMenu();
    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            await CheckOllamaConnection(serviceProvider, ollamaBaseUrl);
            break;
        case "2":
            await CheckSearXNGConnection(serviceProvider, searxngBaseUrl);
            break;
        case "3":
            await CheckCrawl4AIConnection(serviceProvider, crawl4aiBaseUrl);
            break;
        case "4":
            await CheckLightningConnection(serviceProvider, lightningServerUrl);
            break;
        case "5":
            await RunWorkflowOrchestration(serviceProvider);
            break;
        case "6":
            await RunAllHealthChecks(serviceProvider, ollamaBaseUrl, searxngBaseUrl, crawl4aiBaseUrl, lightningServerUrl);
            break;
        case "0":
            running = false;
            Console.WriteLine("\n👋 Goodbye!");
            break;
        default:
            Console.WriteLine("\n❌ Invalid choice. Please try again.\n");
            break;
    }

    if (running && choice is "1" or "2" or "3" or "4" or "5" or "6")
    {
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();
    }
}

static void DisplayMenu()
{
    Console.WriteLine("\n");
    Console.WriteLine("╔════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Deep Research Agent - Main Menu                    ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("  [1] 🔍 Check Ollama Connection");
    Console.WriteLine("  [2] 🌐 Check SearXNG Connection");
    Console.WriteLine("  [3] 🕷️  Check Crawl4AI Connection");
    Console.WriteLine("  [4] ⚡ Check Agent-Lightning Connection");
    Console.WriteLine("  [5] ⚙️  Run Workflow Orchestration");
    Console.WriteLine("  [6] 🏥 Run All Health Checks");
    Console.WriteLine("  [0] 🚪 Exit");
    Console.WriteLine();
    Console.Write("Enter your choice: ");
}

static async Task CheckOllamaConnection(ServiceProvider serviceProvider, string ollamaBaseUrl)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("🔍 CHECKING OLLAMA CONNECTION");
    Console.WriteLine(new string('═', 60));

    try
    {
        var ollamaService = serviceProvider.GetRequiredService<OllamaService>();
        
        Console.WriteLine($"➤ Endpoint: {ollamaBaseUrl}");
        Console.WriteLine("➤ Checking health...");
        
        var isHealthy = await ollamaService.IsHealthyAsync();
        
        if (isHealthy)
        {
            Console.WriteLine("✓ Ollama is running and healthy\n");
            
            Console.WriteLine("➤ Fetching available models...");
            var models = await ollamaService.GetAvailableModelsAsync();
            
            if (models.Any())
            {
                Console.WriteLine($"✓ Found {models.Count()} model(s):");
                foreach (var model in models.Take(5))
                {
                    Console.WriteLine($"  • {model}");
                }
                if (models.Count() > 5)
                {
                    Console.WriteLine($"  ... and {models.Count() - 5} more");
                }
            }
            else
            {
                Console.WriteLine("⚠ No models found. Run: ollama pull mistral");
            }

            foreach (var model in models.Where(x => x.Contains("mistral") || x.Contains("gpt-oss")).Take(2))
            {
                var message = new List<OllamaChatMessage>
                {
                    new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
                };

                var modelInfo = await ollamaService.InvokeAsync(message,model);
                Console.WriteLine($"\n➤ Model Info for '{model}':");
                Console.WriteLine(JsonSerializer.Serialize(modelInfo.Content, new JsonSerializerOptions { WriteIndented = true }));
            }

            // Test invocation
            Console.WriteLine("\n➤ Testing LLM invocation...");
            var testMessages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
            };

            var response = await ollamaService.InvokeAsync(testMessages);
            Console.WriteLine($"✓ Response: {response.Content}");

            Console.WriteLine("\n✅ OLLAMA CONNECTION: SUCCESS");
        }
        else
        {
            Console.WriteLine($"❌ Ollama is not accessible at {ollamaBaseUrl}");
            Console.WriteLine("\n📝 To start Ollama:");
            Console.WriteLine("   ollama serve");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR: {ex.Message}");
        Console.WriteLine("\n📝 Troubleshooting:");
        Console.WriteLine("   1. Start Ollama: ollama serve");
        Console.WriteLine("   2. Pull a model: ollama pull mistral");
        Console.WriteLine($"   3. Verify endpoint: {ollamaBaseUrl}");
    }
}

static async Task CheckSearXNGConnection(ServiceProvider serviceProvider, string searxngBaseUrl)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("🌐 CHECKING SEARXNG CONNECTION");
    Console.WriteLine(new string('═', 60));

    try
    {
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        
        Console.WriteLine($"➤ Endpoint: {searxngBaseUrl}");
        Console.WriteLine("➤ Checking health...");
        
        var response = await httpClient.GetAsync($"{searxngBaseUrl}/healthz");
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✓ SearXNG is running and healthy");
            
            Console.WriteLine("\n➤ Testing search functionality...");
            var searchUrl = $"{searxngBaseUrl}/search?q=test&format=json";
            var searchResponse = await httpClient.GetAsync(searchUrl);
            
            if (searchResponse.IsSuccessStatusCode)
            {
                var content = await searchResponse.Content.ReadAsStringAsync();
                Console.WriteLine("✓ Search API is responding");
                Console.WriteLine($"  Sample response length: {content.Length} characters");
            }
            
            Console.WriteLine("\n✅ SEARXNG CONNECTION: SUCCESS");
        }
        else
        {
            Console.WriteLine($"❌ SearXNG responded with: {response.StatusCode}");
            Console.WriteLine("\n📝 To start SearXNG:");
            Console.WriteLine("   docker-compose up searxng -d");
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ Cannot connect to SearXNG: {ex.Message}");
        Console.WriteLine("\n📝 Troubleshooting:");
        Console.WriteLine("   1. Start SearXNG: docker-compose up searxng -d");
        Console.WriteLine("   2. Check Docker: docker ps");
        Console.WriteLine($"   3. Verify endpoint: {searxngBaseUrl}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR: {ex.Message}");
    }
}

static async Task CheckCrawl4AIConnection(ServiceProvider serviceProvider, string crawl4aiBaseUrl)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("🕷️  CHECKING CRAWL4AI CONNECTION");
    Console.WriteLine(new string('═', 60));

    try
    {
        var crawl4aiService = serviceProvider.GetRequiredService<SearCrawl4AIService>();
        
        Console.WriteLine($"➤ Endpoint: {crawl4aiBaseUrl}");
        Console.WriteLine("✓ Crawl4AI service initialized");
        
        Console.WriteLine("\n✅ CRAWL4AI CONNECTION: SUCCESS");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR: {ex.Message}");
        Console.WriteLine("\n📝 Troubleshooting:");
        Console.WriteLine("   1. Start Crawl4AI: docker-compose up crawl4ai -d");
        Console.WriteLine("   2. Check Docker: docker ps");
        Console.WriteLine($"   3. Verify endpoint: {crawl4aiBaseUrl}");
    }
}

static async Task CheckLightningConnection(ServiceProvider serviceProvider, string lightningServerUrl)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("⚡ CHECKING AGENT-LIGHTNING CONNECTION");
    Console.WriteLine(new string('═', 60));

    try
    {
        var lightningService = serviceProvider.GetRequiredService<IAgentLightningService>();
        
        Console.WriteLine($"➤ Endpoint: {lightningServerUrl}");
        Console.WriteLine("➤ Checking health...");
        
        var isHealthy = await lightningService.IsHealthyAsync();
        
        if (isHealthy)
        {
            Console.WriteLine("✓ Agent-Lightning Server is running\n");
            
            Console.WriteLine("➤ Fetching server information...");
            var serverInfo = await lightningService.GetServerInfoAsync();
            
            Console.WriteLine($"✓ Server Version: {serverInfo.Version}");
            Console.WriteLine($"✓ APO (Auto Performance Optimization): {(serverInfo.ApoEnabled ? "Enabled" : "Disabled")}");
            Console.WriteLine($"✓ VERL (Verification and Reasoning Layer): {(serverInfo.VerlEnabled ? "Enabled" : "Disabled")}");
            Console.WriteLine($"✓ Registered Agents: {serverInfo.RegisteredAgents}");
            Console.WriteLine($"✓ Active Connections: {serverInfo.ActiveConnections}");
            
            Console.WriteLine("\n✅ AGENT-LIGHTNING CONNECTION: SUCCESS");
        }
        else
        {
            Console.WriteLine($"❌ Agent-Lightning Server is not accessible at {lightningServerUrl}");
            Console.WriteLine("\n📝 To start Lightning Server:");
            Console.WriteLine("   docker-compose up lightning-server -d");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR: {ex.Message}");
        Console.WriteLine("\n📝 Troubleshooting:");
        Console.WriteLine("   1. Start Lightning Server: docker-compose up lightning-server -d");
        Console.WriteLine("   2. Check Docker: docker ps");
        Console.WriteLine($"   3. Verify endpoint: {lightningServerUrl}");
    }
}

static async Task RunWorkflowOrchestration(ServiceProvider serviceProvider)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("⚙️  RUNNING WORKFLOW ORCHESTRATION");
    Console.WriteLine(new string('═', 60));

    try
    {
        Console.Write("\nEnter your research query (or press Enter for default): ");
        var query = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(query))
        {
            //query = TestPrompts.ComplexQuery;
            query = TestPrompts.ComplexQuerySpace;
            Console.WriteLine($"Using default query: {query}");
        }

        var masterWorkflow = serviceProvider.GetRequiredService<MasterWorkflow>();
        
        Console.WriteLine("\n➤ Starting workflow execution...\n");
        Console.WriteLine(new string('-', 60));


        await foreach (var response in masterWorkflow.StreamStateAsync(query))
        {
            WriteStreamStateFields(response);

            if (!string.IsNullOrWhiteSpace(response.Status) && response.Status.Contains("clarification_needed", StringComparison.OrdinalIgnoreCase))
            {
                var clarifiedQuery = query + "\n\nclarification_provided: " + TestPrompts.ClarifiedQuerySpace;

                await foreach (var clarifyresponse in masterWorkflow.StreamStateAsync(clarifiedQuery))
                {
                    WriteStreamStateFields(clarifyresponse);
                }
            }
        }


        Console.WriteLine("✓ Workflow execution completed successfully.\n");
        
        Console.WriteLine(new string('-', 60));
        Console.WriteLine("\n✅ WORKFLOW EXECUTION: COMPLETE");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ WORKFLOW ERROR: {ex.Message}");
        Console.WriteLine("\n📝 Verify:");
        Console.WriteLine("   1. All services are running (Ollama, SearXNG, Crawl4AI, Lightning)");
        Console.WriteLine("   2. Run health checks first (option 6)");
    }
}

static async Task RunAllHealthChecks(
    ServiceProvider serviceProvider,
    string ollamaBaseUrl,
    string searxngBaseUrl,
    string crawl4aiBaseUrl,
    string lightningServerUrl)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("🏥 RUNNING ALL HEALTH CHECKS");
    Console.WriteLine(new string('═', 60));

    await CheckOllamaConnection(serviceProvider, ollamaBaseUrl);
    Console.WriteLine();
    
    await CheckSearXNGConnection(serviceProvider, searxngBaseUrl);
    Console.WriteLine();
    
    await CheckCrawl4AIConnection(serviceProvider, crawl4aiBaseUrl);
    Console.WriteLine();
    
    await CheckLightningConnection(serviceProvider, lightningServerUrl);
    
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("🏥 HEALTH CHECK SUMMARY COMPLETE");
    Console.WriteLine(new string('═', 60));
}



static void WriteStreamStateField(string label, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        Console.WriteLine($"🗨️  StreamState {label}: {value}");
    }
}

static void WriteStreamStateFields(StreamState response)
{
    var streamFields = new (string Label, string? Value)[]
    {
        ("Status", response.Status),
        ("ResearchId", response.ResearchId),
        ("UserQuery", response.UserQuery),
        ("BriefPreview", response.BriefPreview),
        ("ResearchBrief", response.ResearchBrief),
        ("DraftReport", response.DraftReport),
        ("RefinedSummary", response.RefinedSummary),
        ("FinalReport", response.FinalReport),
        ("SupervisorUpdate", response.SupervisorUpdate),
        ("SupervisorUpdateCount", response.SupervisorUpdateCount > 0 ? response.SupervisorUpdateCount.ToString() : null)
    };

    foreach (var (label, value) in streamFields)
    {
        WriteStreamStateField(label, value);
    }
}

/// <summary>
/// All prompt templates used throughout the deep research agent system.
/// These are C# ports of the original Python prompts.
/// </summary>
public static class TestPrompts
{
    /// <summary>
    /// This prompt guides the first agent in our workflow, which decides if it has enough information from the user.
    /// clarify_with_user_instructions
    /// </summary>
    public static string ComplexQuery => @"Conduct a deep analysis of the 'Splinternet' phenomenon's impact on global semiconductor supply chains by 2028.
                Specifically contrast TSMC's diversification strategy against Intel's IDM 2.0 model under 2024-2025 US export controls,
                and predict the resulting shift in insurance liability models for cross-border wafer shipments.";

    /// <summary>
    /// Gets the complex query template used for evaluating the cost, feasibility, probability, and timeframe of a
    /// satellite mission to Jupiter utilizing advanced telescope arrays.
    /// Note : The grandouse/ambigous statement sare intentional to encourage clarification and deep research and exploration. 
    /// </summary>
    /// <remarks>
    /// This property provides a comprehensive framework for assessing the viability of deploying
    /// satellites as telescopes near Jupiter, including considerations for cost analysis, technological requirements,
    /// and project timelines. It encourages innovative approaches and the exploration of emerging technologies to
    /// address the mission's challenges.
    /// </remarks>
    public static string ComplexQuerySpace => @"  
**GOAL**
How much would it cost and estimate the viability, probability and time frame to succeed.

**Mission**
To send a series of satellites to approximately where Jupiter is current orbiting and use them as a series of telescopes. The telescopes would take advantage of the fact that light bends because of the gravity of the sun. An array or better yet something along the lines of a Dyson sphere would allow use to pear much deeper into space. We seek to go beyond the moon so to speak. We want to pear into the heavens and beyond.

**Scope - Using Starlink as a basic model and limit to a minimum viability test amount**

Cost:
1. Price per satellite?
2. Price to launch?
3. Total price to get one from concept to reality. Orbiting near Jupiter?
4. Minimum minimum viability test amount?
5. Potential cost over runs and potential cost saving. The first is usually the most expensive, once scaled the price drops?

Timeframe:
1. From dream to first orbiting?
2. Total time to minimum viability testing?
3. Time to build momentum and acceptance. How long would it take for other to get invested and contributing, money and human capital.

Technology
1. Can we leverage what we have or need to develop new?

**Keep in mind**
1. Extreme expense to achieve orbit
2. We know light bends but would the light captured suffice for the mission. Light can be polluted, scattered, blocked, etc.

**Finally**
Iterate over the question three time or less if there is no room for improvement. Ask questions if clarity is needed. Do we really need to use old rocket technology. If light can move mass, do we need rocket so to speak. Use every resource under the sun to answer the question. Rethink old ideas. A metaphor, but dig deep, look for new or evolving technology that might help.";



    public static string ClarifiedQuerySpace => @"Number of satellites needs to be estimated as part of the output. Not sure and need to know for the minimum viability test?

Minimum payload to accomplish the mission. Moving mass into space is costly, so we need to determine what would be most effective a couple big satellites or a large array of small satellites.

A focal distance of ~550 AU should be reviewed. Could we shorten that by creating a large circular array, capturing the light before the focal point?

If we do have to go out to ~550 AU(s). Where in the known universe could we get a shorter focal point, i.e. where would be a better place for someone to look at Earth or other galaxies? ";
}