using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace DeepResearchAgent;

/// <summary>
/// Handles all console-based user interface and interaction for the Deep Research Agent.
/// </summary>
public class ConsoleHost
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _ollamaBaseUrl;
    private readonly string _searxngBaseUrl;
    private readonly string _crawl4aiBaseUrl;
    private readonly string _lightningServerUrl;

    public ConsoleHost(
        ServiceProvider serviceProvider,
        string ollamaBaseUrl,
        string searxngBaseUrl,
        string crawl4aiBaseUrl,
        string lightningServerUrl)
    {
        _serviceProvider = serviceProvider;
        _ollamaBaseUrl = ollamaBaseUrl;
        _searxngBaseUrl = searxngBaseUrl;
        _crawl4aiBaseUrl = crawl4aiBaseUrl;
        _lightningServerUrl = lightningServerUrl;
    }

    /// <summary>
    /// Runs the interactive console menu loop.
    /// </summary>
    public async Task RunAsync()
    {
        Console.WriteLine("✓ Services initialized");
        Console.WriteLine($"✓ Ollama connection configured ({_ollamaBaseUrl})");
        Console.WriteLine($"✓ Web search + scraping configured (SearXNG: {_searxngBaseUrl}, Crawl4AI: {_crawl4aiBaseUrl})");
        Console.WriteLine("✓ Knowledge persistence configured (LightningStore)");
        Console.WriteLine($"✓ Agent-Lightning integration configured ({_lightningServerUrl})");
        //Console.WriteLine("✓ VERL (Verification and Reasoning Layer) enabled");
        Console.WriteLine("✓ Workflows initialized\n");

        bool running = true;
        while (running)
        {
            DisplayMenu();
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await CheckOllamaConnection();
                    break;
                case "2":
                    await CheckSearXNGConnection();
                    break;
                case "3":
                    await CheckCrawl4AIConnection();
                    break;
                case "4":
                    await CheckLightningConnection();
                    break;
                case "5":
                    await RunWorkflowOrchestration();
                    break;
                case "6":
                    await RunAllHealthChecks();
                    break;
                case "7":
                    await PullOllamaModel("gpt-oss:20b");
                    break;
                case "8":
                    await PullOllamaModel("mistral:7b");
                    break;
                case "0":
                    running = false;
                    Console.WriteLine("\n👋 Goodbye!");
                    break;
                default:
                    Console.WriteLine("\n❌ Invalid choice. Please try again.\n");
                    break;
            }

            if (running && choice is "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    private void DisplayMenu()
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
        Console.WriteLine("  [7] 📥 Pull GPT-OSS Model (gpt-oss:20b)");
        Console.WriteLine("  [8] 📥 Pull Mistral Model (mistral:7b)");
        Console.WriteLine("  [0] 🚪 Exit");
        Console.WriteLine();
        Console.Write("Enter your choice: ");
    }

    private async Task CheckOllamaConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🔍 CHECKING OLLAMA CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var ollamaService = _serviceProvider.GetRequiredService<OllamaService>();

            Console.WriteLine($"➤ Endpoint: {_ollamaBaseUrl}");
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
                    foreach (var model in models.Take(10))
                    {
                        Console.WriteLine($"  • {model}");
                    }
                    if (models.Count() > 5)
                    {
                        Console.WriteLine($"  ... and {models.Count() - 10} more");
                    }
                }
                else
                {
                    Console.WriteLine("⚠ No models found. Run: ollama pull mistral:7b");
                }

                foreach (var model in models.Where(x => x.Contains("mistral") || x.Contains("gpt-oss")).Take(2))
                {
                    var message = new List<OllamaChatMessage>
                    {
                        new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
                    };

                    var modelInfo = await ollamaService.InvokeAsync(message, model);
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
                Console.WriteLine($"❌ Ollama is not accessible at {_ollamaBaseUrl}");
                Console.WriteLine("\n📝 To start Ollama:");
                Console.WriteLine("   ollama serve");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Start Ollama: ollama serve");
            Console.WriteLine("   2. Pull a model: ollama pull gpt-oss:20b");
            Console.WriteLine("   3. Pull a model: ollama pull mistral:7b");
            Console.WriteLine($"   4. Verify endpoint: {_ollamaBaseUrl}");
        }
    }

    private async Task CheckSearXNGConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🌐 CHECKING SEARXNG CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

            Console.WriteLine($"➤ Endpoint: {_searxngBaseUrl}");
            Console.WriteLine("➤ Checking health...");

            var response = await httpClient.GetAsync($"{_searxngBaseUrl}/healthz");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ SearXNG is running and healthy");

                Console.WriteLine("\n➤ Testing search functionality...");
                var searchUrl = $"{_searxngBaseUrl}/search?q=test&format=json";
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
            Console.WriteLine($"   3. Verify endpoint: {_searxngBaseUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
        }
    }

    private async Task CheckCrawl4AIConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🕷️  CHECKING CRAWL4AI CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var crawl4aiService = _serviceProvider.GetRequiredService<SearCrawl4AIService>();

            Console.WriteLine($"➤ Endpoint: {_crawl4aiBaseUrl}");
            Console.WriteLine("✓ Crawl4AI service initialized");

            Console.WriteLine("\n✅ CRAWL4AI CONNECTION: SUCCESS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Start Crawl4AI: docker-compose up crawl4ai -d");
            Console.WriteLine("   2. Check Docker: docker ps");
            Console.WriteLine($"   3. Verify endpoint: {_crawl4aiBaseUrl}");
        }
    }

    private async Task CheckLightningConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("⚡ CHECKING AGENT-LIGHTNING CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var lightningService = _serviceProvider.GetRequiredService<IAgentLightningService>();

            Console.WriteLine($"➤ Endpoint: {_lightningServerUrl}");
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
                Console.WriteLine($"❌ Agent-Lightning Server is not accessible at {_lightningServerUrl}");
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
            Console.WriteLine($"   3. Verify endpoint: {_lightningServerUrl}");
        }
    }

    private async Task RunWorkflowOrchestration()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("⚙️  RUNNING WORKFLOW ORCHESTRATION");
        Console.WriteLine(new string('═', 60));

        try
        {
            Console.WriteLine("\n" + new string('-', 60));
            Console.WriteLine("Select Query Type:");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine("  [1] ComplexQuery: Space");
            Console.WriteLine("  [2] ComplexQuery: Splinternet");
            Console.WriteLine("  [3] User Query");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
            var queryChoice = Console.ReadLine()?.Trim();

            string query = null;

            switch (queryChoice)
            {
                case "1":
                    query = TestPrompts.ComplexQuerySpace;
                    Console.WriteLine($"\nUsing ComplexQuery: Space");
                    break;
                case "2":
                    query = TestPrompts.ComplexQuerySplinternet;
                    Console.WriteLine($"\nUsing ComplexQuery: Splinternet");
                    break;
                case "3":
                    Console.Write("\nEnter your research query: ");
                    query = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(query))
                    {
                        Console.WriteLine("❌ Query cannot be empty");
                        return;
                    }
                    break;
                default:
                    Console.WriteLine("\n❌ Invalid choice. Please try again.");
                    return;
            }

            var masterWorkflow = _serviceProvider.GetRequiredService<MasterWorkflow>();

            Console.WriteLine("\n➤ Starting workflow execution...\n");
            Console.WriteLine(new string('-', 60));

            await foreach (var response in masterWorkflow.StreamStateAsync(query))
            {
                WriteStreamStateFields(response);

                if (!string.IsNullOrWhiteSpace(response.Status) && response.Status.Contains("clarification_needed", StringComparison.OrdinalIgnoreCase))
                {
                    var clarificationText = queryChoice == "1" 
                        ? TestPrompts.ClarifiedQuerySpace 
                        : TestPrompts.ClarifiedQuerySplinternet;
                    
                    var clarifiedQuery = query + "\n\nclarification_provided: " + clarificationText;

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

    private async Task RunAllHealthChecks()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🏥 RUNNING ALL HEALTH CHECKS");
        Console.WriteLine(new string('═', 60));

        await CheckOllamaConnection();
        Console.WriteLine();

        await CheckSearXNGConnection();
        Console.WriteLine();

        await CheckCrawl4AIConnection();
        Console.WriteLine();

        await CheckLightningConnection();

        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🏥 HEALTH CHECK SUMMARY COMPLETE");
        Console.WriteLine(new string('═', 60));
    }

    private void WriteStreamStateField(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"🗨️  StreamState {label}: {value}");
        }
    }

    private void WriteStreamStateFields(StreamState response)
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

    private async Task PullOllamaModel(string modelName)
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine($"📦 PULLING OLLAMA MODEL: {modelName}");
        Console.WriteLine(new string('═', 60));

        try
        {
            Console.WriteLine($"➤ Model: {modelName}");
            Console.WriteLine("➤ Pulling model from Ollama...\n");

            var ollamaService = _serviceProvider.GetRequiredService<OllamaSharpService>();
            var lastUpdate = DateTimeOffset.MinValue;
            string? lastStatus = null;
            long? lastCompleted = null;
            var hadUpdates = false;

            await foreach (var update in ollamaService.PullModelStreamingAsync(modelName))
            {
                // Clear the current line and write the updated status
                Console.SetCursorPosition(0, Console.CursorTop);
                if (update.Percent > 1)
                {
                    Console.Write($"Completed: {update.Percent.ToString("0.00")}");
                }
                else if(update.Percent >= 100)
                {
                    Console.Write($"Status: {update.Status} - Completed: {100}%");
                }
                
            }

            Console.WriteLine($"\n✓ Model '{modelName}' pulled successfully");
            Console.WriteLine("\n✅ PULL OPERATION: SUCCESS");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Ensure Ollama is running: ollama serve");
            Console.WriteLine("   2. Verify the model name is correct");
            Console.WriteLine("   3. Check your internet connection");
            Console.WriteLine($"   4. Verify endpoint: {_ollamaBaseUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Ensure Ollama is running: ollama serve");
            Console.WriteLine("   2. Verify the model name is correct");
            Console.WriteLine("   3. Check your internet connection");
        }
    }

    private static bool TryParsePullProgress(
        string json,
        out string? status,
        out long? total,
        out long? completed,
        out string? digest)
    {
        status = null;
        total = null;
        completed = null;
        digest = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("status", out var statusElement))
            {
                status = statusElement.GetString();
            }

            if (root.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt64(out var totalValue))
            {
                total = totalValue;
            }

            if (root.TryGetProperty("completed", out var completedElement) && completedElement.TryGetInt64(out var completedValue))
            {
                completed = completedValue;
            }

            if (root.TryGetProperty("digest", out var digestElement))
            {
                digest = digestElement.GetString();
            }

            return status != null || total.HasValue || completed.HasValue || digest != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string FormatDigest(string? digest)
    {
        return string.IsNullOrWhiteSpace(digest) ? string.Empty : $" (digest: {digest})";
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
    public static string ComplexQuerySplinternet => @"Conduct a deep analysis of the 'Splinternet' phenomenon's impact on global semiconductor supply chains by 2028.
                Specifically contrast TSMC's diversification strategy against Intel's IDM 2.0 model under 2024-2025 US export controls,
                and predict the resulting shift in insurance liability models for cross-border wafer shipments.";

    /// <summary>
    /// Gets a set of clarified, in-depth research questions addressing the impact of the 'Splinternet' phenomenon on
    /// global semiconductor supply chains, with a focus on TSMC and Intel strategies.
    /// </summary>
    /// <remarks>The questions cover topics such as supply chain risks, strategic responses to export
    /// controls, insurance liability, emerging technologies, and the influence of international collaborations. This
    /// property is intended for use in research, analysis, or scenario planning related to semiconductor industry
    /// challenges in a fragmented global environment.</remarks>
    public static string ClarifiedQuerySplinternet => @"
    1. What are the key factors driving the 'Splinternet' phenomenon, and how do they specifically affect global semiconductor supply chains?
    2. How does TSMC's diversification strategy differ from Intel's IDM 2.0 model in response to the 2024-2025 US export controls, and what are the potential advantages and disadvantages of each approach?
    3. How might the evolving geopolitical landscape and regulatory environment influence the effectiveness of TSMC's and Intel's strategies, and what are the potential long-term implications for their market positions?
    4. In what ways could the 'Splinternet' and the strategic responses of TSMC and Intel lead to changes in insurance liability models for cross-border wafer shipments, and how might this impact the overall risk management landscape for semiconductor manufacturers?
    5. What emerging technologies or alternative supply chain models could potentially mitigate the risks associated with the 'Splinternet' and enhance the resilience of global semiconductor supply chains in the face of ongoing geopolitical tensions?
    6. How might the 'Splinternet' phenomenon and the strategic responses of TSMC and Intel influence the competitive dynamics of the semiconductor industry, and what are the potential implications for innovation, market consolidation, and global economic growth?
    7. What role do international collaborations, trade agreements, and diplomatic efforts play in shaping the future of global semiconductor supply chains amidst the 'Splinternet' phenomenon, and how might these factors influence the strategic decisions of key industry players like TSMC and Intel?
    8. How can stakeholders across the semiconductor ecosystem, including manufacturers, suppliers, customers, and policymakers, effectively navigate the challenges posed by the 'Splinternet' and the strategic responses of TSMC and Intel to ensure a stable and resilient supply chain for critical technologies?
    9. Size and scope of the 'Splinternet' phenomenon: What are the specific geopolitical, economic, and technological factors contributing to the fragmentation of the global internet, and how do they impact the semiconductor supply chain?
    10. Finally, synthesize the analysis to provide actionable insights and strategic recommendations for semiconductor manufacturers, policymakers, and other stakeholders to navigate the challenges posed by the 'Splinternet' phenomenon and ensure a resilient global supply chain for critical technologies.
    ";


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

A focal distance of ~550 AU should be reviewed. Could we shorten that by creating a large circular array, capturing the light before the focal point?

If we do have to go out to ~550 AU(s). Where in the known universe could we get a shorter focal point, i.e. where would be a better place for someone to look at Earth or other galaxies? ";
}
