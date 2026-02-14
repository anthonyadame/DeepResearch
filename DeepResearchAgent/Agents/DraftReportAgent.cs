using System.Globalization;
using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// DraftReportAgent: Generates initial draft report.
/// 
/// Responsibility:
/// - Create initial draft based on research brief and conversation
/// - Structure draft into logical sections
/// - Identify gaps and areas needing research
/// - Mark starting quality baseline
/// 
/// Maps to Python's write_initial_draft_report node (~1000 in rd-code.py)
/// </summary>
public class DraftReportAgent
{
    private readonly OllamaService _llmService;
    private readonly ILogger<DraftReportAgent>? _logger;

    protected virtual string AgentName => "DraftReportAgent";

    public DraftReportAgent(
        OllamaService llmService, 
        ILogger<DraftReportAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger;
    }

    /// <summary>
    /// Generate initial draft report from research brief.
    /// This is the "noisy image" that will be iteratively refined through the diffusion loop.
    /// </summary>
    public async Task<DraftReport> GenerateDraftReportAsync(
        string researchBrief,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        // Removed metrics start code

        try
        {
            var currentDate = GetTodayString();
            
            var prompt = PromptTemplates.DraftReportGenerationPrompt
                .Replace("{research_brief}", researchBrief)
                .Replace("{date}", currentDate);
            
            _logger?.LogInformation("DraftReportAgent: Generating initial draft from research brief");
            
            // Convert ChatMessage to OllamaChatMessage for the service
            var ollamaMessages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = prompt }
            };
            
            // Get the raw response first
            var rawResponse = await _llmService.InvokeAsync(
                ollamaMessages,
                cancellationToken: cancellationToken);
            
            _logger?.LogInformation("DraftReportAgent: Draft generation completed with {ContentLength} characters", 
                rawResponse.Content.Length);
            
            // Parse the response into a DraftReport
            var draftReport = ParseDraftReport(rawResponse.Content);
            
            // Removed metrics success code
            return draftReport;
        }
        catch (Exception ex)
        {
            // Removed metrics error and failure code
            _logger?.LogError(ex, "DraftReportAgent: Error generating draft report");
            throw new InvalidOperationException("Failed to generate draft report from research brief", ex);
        }
        finally
        {
            // Removed metrics record request code
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Parse the LLM response into a structured DraftReport.
    /// Extracts sections and identifies gaps.
    /// </summary>
    private static DraftReport ParseDraftReport(string content)
    {
        var sections = ExtractSections(content);
        
        return new DraftReport
        {
            Content = content,
            Sections = sections,
            Metadata = new Dictionary<string, object>
            {
                { "generated_at", DateTime.UtcNow.ToString("O") },
                { "phase", "initial_draft" },
                { "section_count", sections.Count }
            }
        };
    }

    /// <summary>
    /// Extract sections from markdown content.
    /// Looks for ## and ### headers to identify sections.
    /// </summary>
    private static List<DraftReportSection> ExtractSections(string content)
    {
        var sections = new List<DraftReportSection>();
        
        // Split by ## headers (main sections)
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        var currentSection = (Title: string.Empty, Content: new System.Text.StringBuilder());
        var foundFirstSection = false;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Check for main section header (##)
            if (line.StartsWith("## "))
            {
                // Save previous section if exists
                if (foundFirstSection && !string.IsNullOrEmpty(currentSection.Title))
                {
                    sections.Add(new DraftReportSection
                    {
                        Title = currentSection.Title,
                        Content = currentSection.Content.ToString().Trim(),
                        IdentifiedGaps = new List<string>()
                    });
                }
                
                currentSection.Title = line.Substring(3).Trim();
                currentSection.Content = new System.Text.StringBuilder();
                foundFirstSection = true;
            }
            else if (foundFirstSection)
            {
                currentSection.Content.AppendLine(line);
            }
        }
        
        // Add final section
        if (foundFirstSection && !string.IsNullOrEmpty(currentSection.Title))
        {
            sections.Add(new DraftReportSection
            {
                Title = currentSection.Title,
                Content = currentSection.Content.ToString().Trim(),
                IdentifiedGaps = new List<string>()
            });
        }
        
        return sections;
    }

    /// <summary>
    /// Get today's date in human-readable format.
    /// Similar to Python's get_today_str() function.
    /// </summary>
    private static string GetTodayString()
    {
        var today = DateTime.Now;
        // Format: "Mon Dec 25, 2024"
        return today.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
