using System.Text.Json;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// ReportAgent: Formats research and analysis into a polished final report.
/// 
/// Responsibilities:
/// 1. Structures findings into report sections
/// 2. Polishes language for publication quality
/// 3. Adds proper citations and references
/// 4. Validates completeness and quality
/// 5. Generates executive summary
/// 6. Produces final formatted report
/// 
/// Returns: ReportOutput with complete, publication-ready report
/// </summary>
public class ReportAgent
{
    private readonly OllamaService _llmService;
    private readonly ToolInvocationService _toolService;
    private readonly ILogger<ReportAgent>? _logger;

    protected virtual string AgentName => "ReportAgent";

    public ReportAgent(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<ReportAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
        _logger = logger;
    }

    /// <summary>
    /// Execute report generation: Structure → Polish → Citations → Validate → Output
    /// </summary>
    public async Task<ReportOutput> ExecuteAsync(
        ReportInput input,
        CancellationToken cancellationToken = default)
    {
        var output = new ReportOutput
        {
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _logger?.LogInformation("ReportAgent: Starting report generation for: {Topic}", input.Topic);

            // Step 1: Generate title
            output.Title = await GenerateTitleAsync(input.Topic, cancellationToken);
            _logger?.LogInformation("ReportAgent: Generated title: {Title}", output.Title);

            // Step 2: Generate executive summary
            output.ExecutiveSummary = await GenerateExecutiveSummaryAsync(input, cancellationToken);
            _logger?.LogInformation("ReportAgent: Generated executive summary ({Length} chars)", 
                output.ExecutiveSummary.Length);

            // Step 3: Structure report sections
            output.Sections = await StructureReportAsync(input, cancellationToken);
            _logger?.LogInformation("ReportAgent: Created {SectionCount} report sections", output.Sections.Count);

            // Step 4: Polish content
            output.Sections = await PolishContentAsync(output.Sections, cancellationToken);
            _logger?.LogInformation("ReportAgent: Polished report content");

            // Step 5: Add citations
            output.Citations = await GenerateCitationsAsync(input, cancellationToken);
            _logger?.LogInformation("ReportAgent: Added {CitationCount} citations", output.Citations.Count);

            // Step 6: Validate completeness
            var isComplete = await ValidateCompletenessAsync(output, input, cancellationToken);
            output.CompletionStatus = isComplete ? "complete" : "incomplete";
            _logger?.LogInformation("ReportAgent: Validation status: {Status}", output.CompletionStatus);

            // Step 7: Calculate quality score
            output.QualityScore = CalculateQualityScore(input, output);
            _logger?.LogInformation("ReportAgent: Report quality score: {Quality:F2}", output.QualityScore);

            _logger?.LogInformation("ReportAgent: Report generation complete ({Length} chars total)", 
                output.ExecutiveSummary.Length + output.Sections.Sum(s => s.Content.Length));

            return output;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ReportAgent: Report generation failed");
            output.CompletionStatus = "failed";
            throw;
        }
    }

    /// <summary>
    /// Format ReportOutput as readable text.
    /// </summary>
    private string FormatReportAsText(ReportOutput report)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"# {report.Title}");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine(report.ExecutiveSummary);
        sb.AppendLine();

        foreach (var section in report.Sections)
        {
            sb.AppendLine($"## {section.Heading}");
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        if (report.Citations.Any())
        {
            sb.AppendLine("## References");
            foreach (var citation in report.Citations)
            {
                sb.AppendLine($"[{citation.Index}] {citation.Source}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> GenerateTitleAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        try
        {
            var titlePrompt = $@"Generate a compelling, professional report title for research on:
Topic: {topic}

The title should be:
- Concise (under 10 words)
- Professional
- Descriptive

Respond with ONLY the title, nothing else.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = titlePrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            return response.Content?.Trim() ?? $"Research Report: {topic}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate title");
            return $"Research Report: {topic}";
        }
    }

    private async Task<string> GenerateExecutiveSummaryAsync(
        ReportInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var researchSummary = string.Join("\n", 
                input.Research.Findings.SelectMany(f => f.Facts.Take(5)).Select(f => f.Statement));
            var analysisSummary = input.Analysis.SynthesisNarrative;

            var summaryPrompt = $@"Create a professional executive summary (150-200 words) for a research report on: {input.Topic}

Research Findings:
{researchSummary}

Analysis:
{analysisSummary}

The summary should:
1. Provide a high-level overview
2. State key findings
3. Highlight main insights
4. Indicate overall conclusion";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = summaryPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            return response.Content ?? "Executive summary unavailable";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate executive summary");
            return "Executive summary could not be generated";
        }
    }

    private async Task<List<ReportSection>> StructureReportAsync(
        ReportInput input,
        CancellationToken cancellationToken)
    {
        var sections = new List<ReportSection>();

        try
        {
            // Introduction section
            sections.Add(new ReportSection
            {
                Heading = "Introduction",
                Content = $"This report analyzes research on {input.Topic}. The research encompasses findings from multiple sources and expert analysis.",
                CitationIndices = new List<int>()
            });

            // Key Findings section
            var findingsSummary = string.Join("\n", 
                input.Research.Findings.SelectMany(f => f.Facts.Take(5)).Select(f => $"• {f.Statement}"));
            sections.Add(new ReportSection
            {
                Heading = "Key Findings",
                Content = findingsSummary,
                CitationIndices = new List<int>()
            });

            // Analysis section
            sections.Add(new ReportSection
            {
                Heading = "Analysis",
                Content = input.Analysis.SynthesisNarrative,
                CitationIndices = new List<int>()
            });

            // Insights section
            var insightsSummary = string.Join("\n", 
                input.Analysis.KeyInsights.Take(5).Select(i => $"• {i.Statement}"));
            sections.Add(new ReportSection
            {
                Heading = "Key Insights",
                Content = insightsSummary,
                CitationIndices = new List<int>()
            });

            // Conclusion section
            var conclusionPrompt = $@"Write a conclusion paragraph for a research report on {input.Topic} based on these insights:

{insightsSummary}

The conclusion should summarize the main takeaways.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = conclusionPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            sections.Add(new ReportSection
            {
                Heading = "Conclusion",
                Content = response.Content ?? "Further research is needed.",
                CitationIndices = new List<int>()
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to structure report sections");
        }

        return sections;
    }

    private async Task<List<ReportSection>> PolishContentAsync(
        List<ReportSection> sections,
        CancellationToken cancellationToken)
    {
        try
        {
            var polishedSections = new List<ReportSection>();

            foreach (var section in sections)
            {
                var polishPrompt = $@"Polish this report section for professional publication quality:

Heading: {section.Heading}
Content: {section.Content}

Improve clarity, grammar, and flow while maintaining accuracy.
Respond with ONLY the polished content.";

                var messages = new List<OllamaChatMessage>
                {
                    new() { Role = "user", Content = polishPrompt }
                };

                var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
                
                polishedSections.Add(new ReportSection
                {
                    Heading = section.Heading,
                    Content = response.Content ?? section.Content,
                    CitationIndices = section.CitationIndices
                });
            }

            return polishedSections;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to polish content");
            return sections;
        }
    }

    private async Task<List<Citation>> GenerateCitationsAsync(
        ReportInput input,
        CancellationToken cancellationToken)
    {
        var citations = new List<Citation>();

        try
        {
            int index = 1;
            var urls = new HashSet<string>();

            // Extract unique URLs from findings
            foreach (var finding in input.Research.Findings)
            {
                foreach (var fact in finding.Facts.Take(3))
                {
                    if (!urls.Contains(fact.Source) && urls.Count < 10)
                    {
                        urls.Add(fact.Source);
                        citations.Add(new Citation
                        {
                            Index = index++,
                            Source = fact.Source,
                            Url = fact.Source,
                            AccessedAt = DateTime.UtcNow.AddDays(-1)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate citations");
        }

        return citations;
    }

    private async Task<bool> ValidateCompletenessAsync(
        ReportOutput report,
        ReportInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            bool hasTitle = !string.IsNullOrEmpty(report.Title);
            bool hasSummary = !string.IsNullOrEmpty(report.ExecutiveSummary) && 
                             report.ExecutiveSummary.Length > 50;
            bool hasSections = report.Sections.Count >= 3;
            bool hasContent = report.Sections.All(s => s.Content.Length > 20);

            return hasTitle && hasSummary && hasSections && hasContent;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate completeness");
            return false;
        }
    }

    private float CalculateQualityScore(ReportInput input, ReportOutput output)
    {
        var factors = new List<float>();

        // Title quality (0-1)
        factors.Add(string.IsNullOrEmpty(output.Title) ? 0 : 
                   (output.Title.Length < 30 && output.Title.Length > 10 ? 1f : 0.7f));

        // Summary quality (0-1)
        factors.Add(output.ExecutiveSummary.Length > 100 ? 1f : 
                   output.ExecutiveSummary.Length > 50 ? 0.7f : 0.3f);

        // Section completeness (0-1)
        factors.Add(Math.Min(1f, output.Sections.Count / 5f));

        // Citation coverage (0-1)
        factors.Add(Math.Min(1f, output.Citations.Count / 5f));

        // Input quality factor (0-1)
        var inputQuality = (input.Research.AverageQuality + input.Analysis.ConfidenceScore) / 2f;
        factors.Add(inputQuality);

        // Average all factors
        return factors.Average();
    }
}
