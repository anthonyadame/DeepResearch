using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent;

/// <summary>
/// Demo program to evaluate and compare ClarifyAgent vs ClarifyIterativeAgent.
/// Implements PromptWizard-inspired A/B testing for prompt optimization.
/// </summary>
public class ClarificationAgentDemo
{
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("🧙 PromptWizard-Inspired Clarification Agent Comparison Demo");
        Console.WriteLine("=" + new string('=', 70));
        Console.WriteLine();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<ClarificationAgentDemo>();

        // Initialize services
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var defaultModel = configuration["Ollama:DefaultModel"] ?? "gpt-oss:20b";
        
        var ollamaService = new OllamaService(ollamaBaseUrl, defaultModel);

        // Load iterative config
        var iterativeConfig = new IterativeClarificationConfig();
        configuration.GetSection("IterativeClarification").Bind(iterativeConfig);

        // Create agents
        var standardAgent = new ClarifyAgent(
            ollamaService,
            loggerFactory.CreateLogger<ClarifyAgent>());

        var iterativeAgent = new ClarifyIterativeAgent(
            ollamaService,
            iterativeConfig,
            loggerFactory.CreateLogger<ClarifyIterativeAgent>());

        var comparisonService = new ClarificationComparisonService(
            standardAgent,
            iterativeAgent,
            loggerFactory.CreateLogger<ClarificationComparisonService>());

        // Test scenarios
        var scenarios = GetTestScenarios();

        Console.WriteLine($"Running {scenarios.Count} test scenarios...\n");

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"📋 Scenario: {scenario.Name}");
            Console.WriteLine($"   Description: {scenario.Description}");
            Console.WriteLine();

            try
            {
                var comparison = await comparisonService.CompareAgentsAsync(
                    scenario.Messages,
                    CancellationToken.None);

                PrintComparisonResults(scenario.Name, comparison);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in scenario '{scenario.Name}': {ex.Message}");
            }

            Console.WriteLine(new string('-', 70));
            Console.WriteLine();
        }

        Console.WriteLine("✅ Demo complete!");
    }

    private static void PrintComparisonResults(string scenarioName, ComparisonResult comparison)
    {
        Console.WriteLine("📊 COMPARISON RESULTS");
        Console.WriteLine();

        // Standard Agent Results
        Console.WriteLine("  🔵 Standard ClarifyAgent:");
        if (comparison.StandardSucceeded)
        {
            Console.WriteLine($"     Duration: {comparison.StandardDurationMs:F0}ms");
            Console.WriteLine($"     Need Clarification: {comparison.StandardResult?.NeedClarification}");
            Console.WriteLine($"     Question: {TruncateString(comparison.StandardResult?.Question ?? "", 80)}");
        }
        else
        {
            Console.WriteLine($"     ❌ Failed: {comparison.StandardError}");
        }
        Console.WriteLine();

        // Iterative Agent Results
        Console.WriteLine("  🟣 Iterative ClarifyAgent (PromptWizard-inspired):");
        if (comparison.IterativeSucceeded)
        {
            Console.WriteLine($"     Duration: {comparison.IterativeDurationMs:F0}ms");
            Console.WriteLine($"     Iterations: {comparison.IterativeResult?.IterationCount}");
            Console.WriteLine($"     Need Clarification: {comparison.IterativeResult?.NeedClarification}");
            
            if (comparison.IterativeResult?.QualityMetrics != null)
            {
                var metrics = comparison.IterativeResult.QualityMetrics;
                Console.WriteLine($"     Quality Metrics:");
                Console.WriteLine($"       - Clarity:       {metrics.ClarityScore:F1}/100");
                Console.WriteLine($"       - Completeness:  {metrics.CompletenessScore:F1}/100");
                Console.WriteLine($"       - Actionability: {metrics.ActionabilityScore:F1}/100");
                Console.WriteLine($"       - Overall:       {metrics.OverallScore:F1}/100 {(metrics.MeetsThreshold() ? "✅" : "⚠️")}");
                
                if (metrics.IdentifiedGaps.Length > 0)
                {
                    Console.WriteLine($"       - Gaps: {string.Join(", ", metrics.IdentifiedGaps)}");
                }
            }

            Console.WriteLine($"     Question: {TruncateString(comparison.IterativeResult?.Question ?? "", 80)}");

            if (comparison.IterativeResult?.IterationHistory.Count > 0)
            {
                Console.WriteLine($"     Iteration History:");
                foreach (var iter in comparison.IterativeResult.IterationHistory)
                {
                    Console.WriteLine($"       [{iter.Iteration}] Score: {iter.QualityScore:F1} | {iter.DurationMs:F0}ms");
                }
            }
        }
        else
        {
            Console.WriteLine($"     ❌ Failed: {comparison.IterativeError}");
        }
        Console.WriteLine();

        // Performance Comparison
        Console.WriteLine("  ⚡ Performance Metrics:");
        Console.WriteLine($"     Latency Overhead: {comparison.LatencyOverheadMultiplier:F2}x");
        Console.WriteLine($"     Quality Score: {comparison.QualityImprovement:F1}/100");
        Console.WriteLine($"     Summary: {comparison.Summary}");
        Console.WriteLine();
    }

    private static string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }

    private static List<TestScenario> GetTestScenarios()
    {
        return new List<TestScenario>
        {
            new TestScenario
            {
                Name = "Vague Research Request",
                Description = "User asks about AI but provides minimal detail",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "I want to learn about AI" }
                }
            },
            new TestScenario
            {
                Name = "Moderately Clear Request",
                Description = "User asks about specific AI topic with some context",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "Can you research recent developments in large language models for code generation?" }
                }
            },
            new TestScenario
            {
                Name = "Detailed Research Request",
                Description = "User provides comprehensive research parameters",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage 
                    { 
                        Role = "user", 
                        Content = "I need a comprehensive analysis of PromptWizard's feedback-driven prompt optimization approach. " +
                                  "Focus on the iterative refinement mechanism, compare it to baseline methods like PromptBreeder and APE, " +
                                  "and include performance metrics on benchmark datasets like GSM8k and BigBench. " +
                                  "Please structure the report with sections on methodology, results, and practical applications." 
                    }
                }
            },
            new TestScenario
            {
                Name = "Ambiguous Multi-Part Request",
                Description = "User asks multiple related questions without clear priority",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage 
                    { 
                        Role = "user", 
                        Content = "What are the best practices for .NET development? Also curious about microservices and cloud deployment strategies." 
                    }
                }
            },
            new TestScenario
            {
                Name = "Follow-up Conversation",
                Description = "Multi-turn conversation with context building",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "I'm building a research agent" },
                    new ChatMessage { Role = "assistant", Content = "That sounds interesting! What kind of research will your agent perform?" },
                    new ChatMessage { Role = "user", Content = "Deep research on technical topics" }
                }
            }
        };
    }
}

public class TestScenario
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required List<ChatMessage> Messages { get; init; }
}
