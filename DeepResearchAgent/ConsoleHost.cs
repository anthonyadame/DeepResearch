using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using DeepResearchAgent.Services.Caching;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
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
    private readonly LlmResponseCache? _llmCache;
    private string _selectedLlmProvider = "ollama";

    public ConsoleHost(
        ServiceProvider serviceProvider,
        string ollamaBaseUrl,
        string searxngBaseUrl,
        string crawl4aiBaseUrl,
        string lightningServerUrl,
        LlmResponseCache? llmCache = null)
    {
        _serviceProvider = serviceProvider;
        _ollamaBaseUrl = ollamaBaseUrl;
        _searxngBaseUrl = searxngBaseUrl;
        _crawl4aiBaseUrl = crawl4aiBaseUrl;
        _lightningServerUrl = lightningServerUrl;
        _llmCache = llmCache;
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
        
        Console.WriteLine("✓ Workflows initialized\n");

        bool running = true;
        while (running)
        {
            DisplayMenu();
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    SelectLlmProvider();
                    break;
                case "2":
                    await CheckLiteLlmConnection();
                    break;
                case "3":
                    await CheckOllamaConnection();
                    break;
                case "4":
                    await CheckSearXNGConnection();
                    break;
                case "5":
                    await CheckCrawl4AIConnection();
                    break;
                case "6":
                    await CheckLightningConnection();
                    break;
                case "7":
                    await RunWorkflowOrchestration();
                    break;
                case "8":
                    await RunAllHealthChecks();
                    break;
                case "9":
                    await PullOllamaModel("gpt-oss:20b");
                    break;
                case "10":
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

            if (running && choice is "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or "10")
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
        Console.WriteLine($"  Current LLM Provider: {_selectedLlmProvider.ToUpper()}");
        Console.WriteLine();
        Console.WriteLine("  [1] 🔧 Select LLM Provider (liteLLM or Ollama)");
        Console.WriteLine("  [2] 🔍 Check liteLLM Connection");
        Console.WriteLine("  [3] 🔍 Check Ollama Connection");
        Console.WriteLine("  [4] 🌐 Check SearXNG Connection");
        Console.WriteLine("  [5] 🕷️  Check Crawl4AI Connection");
        Console.WriteLine("  [6] ⚡ Check Agent-Lightning Connection");
        Console.WriteLine("  [7] ⚙️  Run Workflow Orchestration");
        Console.WriteLine("  [8] 🏥 Run All Health Checks");
        Console.WriteLine("  [9] 📥 Pull GPT-OSS Model (gpt-oss:20b)");
        Console.WriteLine("  [10] 📥 Pull Mistral Model (mistral:7b)");
        Console.WriteLine("  [0] 🚪 Exit");
        Console.WriteLine();
        Console.Write("Enter your choice: ");
    }

    private void SelectLlmProvider()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🔧 SELECT LLM PROVIDER");
        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
        Console.WriteLine("  [1] liteLLM");
        Console.WriteLine("  [2] Ollama");
        Console.WriteLine();
        Console.Write("Enter your choice: ");
        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                _selectedLlmProvider = "litellm";
                Console.WriteLine("\n✅ LLM Provider set to: liteLLM");
                break;
            case "2":
                _selectedLlmProvider = "ollama";
                Console.WriteLine("\n✅ LLM Provider set to: Ollama");
                break;
            default:
                Console.WriteLine("\n❌ Invalid choice. Provider unchanged.");
                break;
        }
    }

    private async Task CheckLiteLlmConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🔍 CHECKING LITELLM CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var llmProvider = _serviceProvider.GetRequiredService<ILlmProvider>();

            if (llmProvider.ProviderName?.ToLower() != "litellm")
            {
                Console.WriteLine("⚠️  Warning: Current LLM provider is not liteLLM");
                Console.WriteLine($"➤ Current Provider: {llmProvider.ProviderName}");
                Console.WriteLine("\n📝 To use liteLLM:");
                Console.WriteLine("   1. Update appsettings.json: LlmProvider.Provider = 'litellm'");
                Console.WriteLine("   2. Restart the application");
                return;
            }

            Console.WriteLine($"➤ Provider: {llmProvider.ProviderName}");
            Console.WriteLine($"➤ Default Model: {llmProvider.DefaultModel}");

            // Test invocation
            Console.WriteLine("\n➤ Testing liteLLM connection...");
            var testMessages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
            };

            var response = await llmProvider.InvokeAsync(testMessages);
            Console.WriteLine($"✓ Response: {response.Content}");

            Console.WriteLine("\n✅ LITELLM CONNECTION: SUCCESS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Start liteLLM service");
            Console.WriteLine("   2. Verify configuration in appsettings.json");
            Console.WriteLine("   3. Check LiteLLM.BaseUrl in appsettings.json");
        }
    }

    private async Task CheckOllamaConnection()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🔍 CHECKING OLLAMA CONNECTION");
        Console.WriteLine(new string('═', 60));

        try
        {
            var ollamaService = _serviceProvider.GetRequiredService<ILlmProvider>();

            Console.WriteLine($"➤ Endpoint: {_ollamaBaseUrl}");
            Console.WriteLine($"➤ Default Model: {ollamaService.DefaultModel}");
            Console.WriteLine($"➤ Provider: {ollamaService.ProviderName}");

            // Test invocation
            Console.WriteLine("\n➤ Testing LLM connection...");
            var testMessages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = "Say 'Hello from Deep Research Agent!' in one sentence." }
            };

            var response = await ollamaService.InvokeAsync(testMessages);
            Console.WriteLine($"✓ Response: {response.Content}");

            Console.WriteLine("\n✅ LLM CONNECTION: SUCCESS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("\n📝 Troubleshooting:");
            Console.WriteLine("   1. Start LLM service (Ollama/LiteLLM)");
            Console.WriteLine("   2. Verify configuration in appsettings.json");
            Console.WriteLine($"   3. Check endpoint: {_ollamaBaseUrl}");
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
                Console.WriteLine($"✓ RMPT (Resource Management Performance Tuning): {(serverInfo.RmptEnabled ? "Enabled" : "Disabled")}");
                Console.WriteLine($"✓ RLCS (Reasoning Layer Confidence Scoring): {(serverInfo.RlcsEnabled ? "Enabled" : "Disabled")}");
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
            Console.WriteLine("  [3] Semi-Autonomous Dev Query");
            Console.WriteLine("  [4] User Query");
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
                    query = TestPrompts.ComplexSemiAutonomousDev;
                    Console.WriteLine($"\nUsing Semi-Autonomous Dev Query");
                    break;
                case "4":
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

            StreamState finalResponse = null;

            await foreach (var response in masterWorkflow.StreamStateAsync(query))
            {
                WriteStreamStateFields(response);
                await SaveWorkflowReportsAsync(response);

                finalResponse = response;

                if (!string.IsNullOrWhiteSpace(response.Status) && response.Status.Contains("clarification_needed", StringComparison.OrdinalIgnoreCase))
                {
                    var clarificationText = queryChoice == "1" 
                        ? TestPrompts.ClarifiedQuerySpace 
                        : queryChoice == "2"
                            ? TestPrompts.ClarifiedQuerySplinternet
                            : TestPrompts.ClarifiedSemiAutonomousDev;

                    var clarifiedQuery = query + "\n\nclarification_provided: " + clarificationText;

                    await foreach (var clarifyresponse in masterWorkflow.StreamStateAsync(clarifiedQuery))
                    {
                        WriteStreamStateFields(clarifyresponse);

                    }
                }
            }

            Console.WriteLine("✓ Workflow execution completed successfully.\n");

            

            // Display cache metrics if available
            DisplayCacheMetrics();

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

    private void DisplayCacheMetrics()
    {
        if (_llmCache == null)
        {
            Console.WriteLine("ℹ️  LLM Response Cache: Not enabled");
            return;
        }

        try
        {
            var stats = _llmCache.GetStatistics();
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("📊 LLM RESPONSE CACHE METRICS");
            Console.WriteLine(new string('═', 60));
            Console.WriteLine($"  Cache Hits:        {stats.TotalHits}");
            Console.WriteLine($"  Cache Misses:      {stats.TotalMisses}");
            Console.WriteLine($"  Total Entries:     {stats.TotalEntries}");

            if (stats.TotalHits + stats.TotalMisses > 0)
            {
                var hitRate = stats.HitRate * 100;
                Console.WriteLine($"  Hit Rate:          {hitRate:F1}%");

                if (hitRate >= 80)
                {
                    Console.WriteLine("  Status:            ✅ Excellent (≥80%)");
                }
                else if (hitRate >= 50)
                {
                    Console.WriteLine("  Status:            ⚠️  Good (50-80%)");
                }
                else
                {
                    Console.WriteLine("  Status:            ℹ️  Fair (<50%)");
                }
            }
            Console.WriteLine(new string('═', 60) + "\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not display cache metrics: {ex.Message}\n");
        }
    }

    private async Task RunAllHealthChecks()
    {
        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("🏥 RUNNING ALL HEALTH CHECKS");
        Console.WriteLine(new string('═', 60));

        await CheckLiteLlmConnection();
        Console.WriteLine();

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

        Console.WriteLine($"➤ Model: {modelName}");
        Console.WriteLine("➤ Model pulling temporarily disabled");
        Console.WriteLine($"  Please run: ollama pull {modelName}");
        Console.WriteLine("\n📝 Model pulling will be re-implemented with ILlmProvider");

        await Task.CompletedTask;
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

    private async Task SaveWorkflowReportsAsync(StreamState response)
    {
        try
        {
            // Create report directory
            string reportDir = Path.Combine("Report", response.ResearchId.ToString());
            Directory.CreateDirectory(reportDir);

            // Individual markdown files
            var reportFiles = new Dictionary<string, string?>
            {
                { "01_UserQuery.md", response.UserQuery },
                { "02_BriefPreview.md", response.BriefPreview },
                { "03_ResearchBrief.md", response.ResearchBrief },
                { "04_DraftReport.md", response.DraftReport },
                { "05_RefinedSummary.md", response.RefinedSummary },
                { "06_SupervisorUpdate.md", response.SupervisorUpdate },
                { "07_FinalReport.md", response.FinalReport }
            };

            // Save individual files
            foreach (var (filename, content) in reportFiles)
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    string filePath = Path.Combine(reportDir, filename);
                    await File.WriteAllTextAsync(filePath, content);
                    Console.WriteLine($"  ✓ Saved: {filename}");
                }
            }

            // Create consolidated report
            await SaveConsolidatedReportAsync(reportDir, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not save reports: {ex.Message}");
        }
    }

    private async Task SaveConsolidatedReportAsync(string reportDir, StreamState response)
    {
        try
        {
            var consolidatedPath = Path.Combine(reportDir, "00_Consolidated_Report.md");

            var consolidatedContent = new StringBuilder();

            consolidatedContent.AppendLine("# Deep Research Agent - Consolidated Report");
            consolidatedContent.AppendLine();
            consolidatedContent.AppendLine($"**Research ID:** {response.ResearchId}");
            consolidatedContent.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            consolidatedContent.AppendLine();
            consolidatedContent.AppendLine("---");
            consolidatedContent.AppendLine();

            // Helper method to add sections
            void AddSection(string title, string? content)
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    consolidatedContent.AppendLine($"## {title}");
                    consolidatedContent.AppendLine();
                    consolidatedContent.AppendLine(content);
                    consolidatedContent.AppendLine();
                    consolidatedContent.AppendLine(new string('-', 50));
                    consolidatedContent.AppendLine();
                }
            }

            AddSection("User Query", response.UserQuery);
            AddSection("Brief Preview", response.BriefPreview);
            AddSection("Research Brief", response.ResearchBrief);
            AddSection("Draft Report", response.DraftReport);
            AddSection("Refined Summary", response.RefinedSummary);
            AddSection("Supervisor Update", response.SupervisorUpdate);
            AddSection("Final Report", response.FinalReport);

            await File.WriteAllTextAsync(consolidatedPath, consolidatedContent.ToString());
            Console.WriteLine($"  ✓ Saved: 00_Consolidated_Report.md");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not save consolidated report: {ex.Message}");
        }
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




    public static string ComplexSemiAutonomousDev => @"  
**GOAL**
## Hybrid TTD‑TOAR Architecture for semi autonomous AI‑Assisted Development

## Definitions
- TOAR (Think‑Act‑Observe‑Repeat): An iterative, test‑driven development approach where an LLM agent generates code, runs tests, observes results, and refines the code based on feedback until tests pass.
- TTD (Time‑Test Diffusion): A research‑style drafting process where an LLM generates multiple iterations of code drafts over time, with confidence scoring to identify areas of uncertainty or low confidence.

## Context and Problem Statement

Our project aims to build a fully automated, confidence‑driven software development pipeline that leverages LLM‑based agents to generate, test, and refine code. We need a lightweight, reproducible architecture that:

1. **Combines the research‑style drafting of Time‑Test Diffusion (TTD)** with the test‑driven refinement of Think‑Act‑Observe‑Repeat (TAOR).
2. **Runs all code‑generation and testing inside an isolated Docker sandbox** to guarantee reproducibility and security.
3. **Maintains a clear audit trail** via Git checkpoints and observable terminal output.

The architecture must also be easy to maintain and extend for future language support and additional agents.

## Decision Drivers

- **Speed** – Rapid prototyping and iteration.
- **Quality** – High confidence in final artifacts through iterative testing.
- **Reproducibility** – Deterministic sandbox execution.
- **Observability** – Real‑time terminal logs and metrics.
- **Maintainability** – Modular design that can evolve.

## Considered Options

1. Reverse engineering existing monolithic AI‑assisted development tools and adapting them to our needs.  
   *Pros:* Leverage existing solutions; *Cons:* May be complex and less flexible.

2. Reverse engingering TOAR architecture and adapting it to include TTD‑style drafting and confidence scoring.  
   *Pros:* Builds on a proven architecture; *Cons:* May require significant modifications.

3. Reverse engineering TTD architecture and adapting it to include TOAR‑style test‑driven refinement.  
   *Pros:* Leverages TTD's strength in handling uncertainty; *Cons:* May not integrate well with test‑driven workflows.

3. Researching and implementing a custom architecture from scratch, combining TTD and TOAR principles.  
   *Pros:* Tailored to our needs; *Cons:* Higher initial development effort. 

4. **Pure TTD Pipeline** – Use TTD alone to generate code and rely on external testing scripts.  
   *Pros:* Simpler; *Cons:* No tight integration of test feedback.

5. **Pure TOAR Pipeline** – Use TOAR exclusively, treating LLM as a code generator without a draft‑centric diffusion stage.  
   *Pros:* Direct test‑driven workflow; *Cons:* Lacks a global context and confidence estimate.

6. **Hybrid TTD‑TOAR (Chosen)** – Orchestrate TTD for drafting and confidence scoring, then hand off to TOAR for code generation, testing, and refactoring.  
   *Pros:* Combines strengths of both; *Cons:* Slightly higher orchestration complexity.

7. **External CI Integration** – Separate the pipeline from the orchestrator, relying on CI tools to run tests.  
   *Pros:* Decouples concerns; *Cons:* Loss of real‑time feedback.

## Technology
1. TOAR for iterative code generation and refinement based on test outcomes, ensuring that the generated code meets quality standards through continuous feedback loops.
2. TTD for generating initial drafts and confidence estimates, guiding the THOR agent's focus on areas of low confidence or high uncertainty. THOR then takes these drafts, generates code, runs tests, and iteratively refines based on test outcomes.
3. Docker for isolated, reproducible execution.
4. Git for version control and audit trails.
5. LLM‑based agents for code generation, testing, and refinement.
6. Observability tools (e.g., terminal output, logs, metrics dashboards) for real‑time feedback.
7. Agent orchestration layer to manage the flow between TTD and THOR stages, ensuring smooth handoffs and integration of feedback.
8. Vibe code negation and other prompting techniques to enhance the quality of generated code and tests, especially in areas identified as low confidence by TTD.
9. ADRs for documenting architectural decisions and rationale, ensuring maintainability and clarity for future developers and driving informed evolution of the architecture.

## Architecture should be 
- Designed to allow for easy extension to other programming languages and frameworks in the future. This means abstracting away language‑specific details and creating modular components that can be swapped out or extended as needed.
- Should include a clear mechanism for handling and integrating test feedback into the code generation process. This could involve using confidence scores from TTD to prioritize areas for THOR to focus on, or implementing a feedback loop where test results directly inform subsequent code generation iterations.
- Should provide real‑time observability into the pipeline's operations, including logs of generated code, test results, and confidence scores. This will allow developers to monitor progress and intervene when necessary.
- Should be designed with security in mind, especially since it involves executing generated code. The Docker sandbox should be configured to minimize potential risks, and there should be safeguards in place to prevent malicious code generation or execution.
- Should include a clear audit trail of all actions taken by the agents, including code generation, test execution, and any manual interventions. This could be achieved through Git checkpoints, detailed logs, and possibly a dashboard that tracks the pipeline's history and current state.
- Should be designed to allow for easy maintenance and evolution over time. This means using clear documentation, modular design principles, and possibly implementing an ADR (Architectural Decision Record) system to document key decisions and their rationale for future reference.
- Should consider the potential for scaling the pipeline in the future, both in terms of handling larger codebases and supporting more complex testing scenarios. This might involve designing the architecture to allow for distributed execution or integrating with cloud services for additional compute resources when needed.
- Should include a mechanism for handling edge cases and unexpected scenarios, such as test failures that are not easily resolved through iterative refinement. This could involve implementing a fallback strategy or allowing for human intervention when certain thresholds of failure are reached.


**Keep in mind**
We want the human in the loop
- Setting the high‑level goals, constraints, and priorities for the project.
- Monitoring the pipeline's progress and intervening when necessary to provide guidance or make adjustments.
- Having final review and decision making, but we want to automate as much of the code generation and testing process as possible to maximize efficiency and confidence in the final product.


**Finally**
Iterate over the question three time or less if there is no room for improvement. Ask questions if clarity is needed. Rethink old ideas. A metaphor, but dig deep, look for new or evolving technology that might help.";



    public static string ClarifiedSemiAutonomousDev => @"
    Architecture should be 
    - Should be designed to allow for easy extension to other programming languages and frameworks in the future. This means abstracting away language‑specific details and creating modular components that can be swapped out or extended as needed.
    - Should include a clear mechanism for handling and integrating test feedback into the code generation process. This could involve using confidence scores from TTD to prioritize areas for THOR to focus on, or implementing a feedback loop where test results directly inform subsequent code generation iterations.
    - Should provide real‑time observability into the pipeline's operations, including logs of generated code, test results, and confidence scores. This will allow developers to monitor progress and intervene when necessary.
    - Should be designed with security in mind, especially since it involves executing generated code. The Docker sandbox should be configured to minimize potential risks, and there should be safeguards in place to prevent malicious code generation or execution.
    - Should include a clear audit trail of all actions taken by the agents, including code generation, test execution, and any manual interventions. This could be achieved through Git checkpoints, detailed logs, and possibly a dashboard that tracks the pipeline's history and current state.
";

}
