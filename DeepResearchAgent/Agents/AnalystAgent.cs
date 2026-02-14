using System.Text.Json;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// AnalystAgent: Analyzes research findings and synthesizes insights.
/// 
/// Responsibilities:
/// 1. Evaluates quality of research findings
/// 2. Identifies key themes and patterns
/// 3. Detects contradictions in findings
/// 4. Scores importance of facts
/// 5. Synthesizes findings into coherent insights
/// 6. Generates confidence scores
/// 
/// Returns: AnalysisOutput with synthesized insights and quality metrics
/// </summary>
public class AnalystAgent
{
    private readonly OllamaService _llmService;
    private readonly ToolInvocationService _toolService;
    private readonly ILogger<AnalystAgent>? _logger;

    protected virtual string AgentName => "AnalystAgent";

    public AnalystAgent(
        OllamaService llmService,
        ToolInvocationService toolService,
        ILogger<AnalystAgent>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
        _logger = logger;
    }

    /// <summary>
    /// Execute analysis workflow: Evaluate → Identify Themes → Detect Contradictions → Synthesize
    /// </summary>
    public async Task<AnalysisOutput> ExecuteAsync(
        AnalysisInput input,
        CancellationToken cancellationToken = default)
    {
        var output = new AnalysisOutput();

        try
        {
            _logger?.LogInformation("AnalystAgent: Starting analysis of {FindingCount} findings", 
                input.Findings.Sum(f => f.Facts.Count));

            // Step 1: Evaluate quality of findings
            var qualityScores = await EvaluateFindingsQualityAsync(input, cancellationToken);
            _logger?.LogInformation("AnalystAgent: Evaluated findings, average quality: {AvgQuality:F2}",
                qualityScores.Values.Any() ? qualityScores.Values.Average() : 0);

            // Step 2: Identify themes and patterns
            var themes = await IdentifyThemesAsync(input, cancellationToken);
            output.ThemesIdentified = themes;
            _logger?.LogInformation("AnalystAgent: Identified {ThemeCount} themes", themes.Count);

            // Step 3: Detect contradictions
            var contradictions = await DetectContradictionsAsync(input, cancellationToken);
            output.Contradictions = contradictions;
            _logger?.LogInformation("AnalystAgent: Detected {ContradictionCount} contradictions", 
                contradictions.Count);

            // Step 4: Score importance of findings
            var importanceScores = await ScoreImportanceAsync(input, cancellationToken);
            _logger?.LogInformation("AnalystAgent: Scored {FactCount} findings for importance", 
                importanceScores.Count);

            // Step 5: Create key insights from high-importance findings
            output.KeyInsights = await CreateKeyInsightsAsync(input, importanceScores, cancellationToken);
            _logger?.LogInformation("AnalystAgent: Created {InsightCount} key insights", 
                output.KeyInsights.Count);

            // Step 6: Synthesize findings into narrative
            output.SynthesisNarrative = await SynthesizeInsightsAsync(
                input, output.KeyInsights, output.Contradictions, cancellationToken);
            _logger?.LogInformation("AnalystAgent: Synthesized narrative ({Length} chars)", 
                output.SynthesisNarrative.Length);

            // Calculate confidence score
            output.ConfidenceScore = CalculateConfidenceScore(qualityScores, themes.Count, contradictions.Count);
            _logger?.LogInformation("AnalystAgent: Analysis complete with confidence score: {Confidence:F2}", 
                output.ConfidenceScore);

            return output;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AnalystAgent: Analysis failed");
            throw;
        }
    }

    /// <summary>
    /// Evaluate quality of each finding using LLM.
    /// </summary>
    private async Task<Dictionary<string, float>> EvaluateFindingsQualityAsync(
        AnalysisInput input,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<string, float>();

        try
        {
            var factsSummary = string.Join("\n", 
                input.Findings.SelectMany(f => f.Facts.Take(10)).Select(f => f.Statement));

            var evalPrompt = $@"Evaluate the quality of these research findings on a scale of 0-10:

{factsSummary}

For each major finding, assess:
1. Relevance to: {input.Topic}
2. Credibility (0-10)
3. Specificity (0-10)

Respond with a single average quality score (0-10), just the number.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = evalPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);

            if (float.TryParse(response.Content.Trim(), out var quality))
            {
                scores["overall"] = Math.Clamp(quality / 10f, 0f, 1f); // Normalize to 0-1
            }
            else
            {
                scores["overall"] = 0.5f;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to evaluate findings quality");
            scores["overall"] = 0.5f;
        }

        return scores;
    }

    /// <summary>
    /// Identify themes and patterns in the findings.
    /// </summary>
    private async Task<List<string>> IdentifyThemesAsync(
        AnalysisInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var factsSummary = string.Join("\n", 
                input.Findings.SelectMany(f => f.Facts.Take(8)).Select(f => f.Statement));

            var themePrompt = $@"Analyze these research findings and identify 3-5 major themes or patterns:

{factsSummary}

Respond with ONLY a JSON array of theme names (strings), nothing else.
Example: [""theme1"", ""theme2"", ""theme3""]";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = themePrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            var trimmedResponse = response.Content.Trim();

            if (trimmedResponse.StartsWith("[") && trimmedResponse.EndsWith("]"))
            {
                var themes = JsonSerializer.Deserialize<List<string>>(trimmedResponse) ?? new();
                return themes;
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to identify themes");
            return new List<string>();
        }
    }

    /// <summary>
    /// Detect contradictions between findings.
    /// </summary>
    private async Task<List<Contradiction>> DetectContradictionsAsync(
        AnalysisInput input,
        CancellationToken cancellationToken)
    {
        var contradictions = new List<Contradiction>();

        try
        {
            var facts = input.Findings.SelectMany(f => f.Facts).Take(15).ToList();
            if (facts.Count < 2) return contradictions;

            var factsText = string.Join("\n", facts.Select(f => $"- {f.Statement}"));

            var contradictPrompt = $@"Identify any contradictions or conflicts between these findings:

{factsText}

For each contradiction found, provide:
1. Fact 1
2. Fact 2
3. How they contradict (brief explanation)
4. Severity (0.0-1.0)

Respond with a JSON array of objects, each with fields: fact_1, fact_2, explanation, severity";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = contradictPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            var trimmedResponse = response.Content.Trim();

            if (trimmedResponse.StartsWith("["))
            {
                var parsed = JsonSerializer.Deserialize<List<Contradiction>>(trimmedResponse);
                if (parsed != null) contradictions.AddRange(parsed);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect contradictions");
        }

        return contradictions;
    }

    /// <summary>
    /// Score the importance of each finding.
    /// </summary>
    private async Task<Dictionary<string, float>> ScoreImportanceAsync(
        AnalysisInput input,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<string, float>();

        try
        {
            var facts = input.Findings.SelectMany(f => f.Facts).Take(10).ToList();

            foreach (var fact in facts)
            {
                var importancePrompt = $@"Rate the importance of this finding on a scale of 0-10:

Finding: {fact.Statement}
Topic: {input.Topic}

Consider relevance, impact, and specificity.
Respond with ONLY a number (0-10).";

                var messages = new List<OllamaChatMessage>
                {
                    new() { Role = "user", Content = importancePrompt }
                };

                var response = await _llmService.InvokeAsync(messages, null, cancellationToken);

                if (float.TryParse(response.Content.Trim(), out var importance))
                {
                    scores[fact.Statement] = Math.Clamp(importance / 10f, 0f, 1f); // Normalize to 0-1
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to score importance");
        }

        return scores;
    }

    /// <summary>
    /// Create key insights from high-importance findings.
    /// </summary>
    private async Task<List<KeyInsight>> CreateKeyInsightsAsync(
        AnalysisInput input,
        Dictionary<string, float> importanceScores,
        CancellationToken cancellationToken)
    {
        var insights = new List<KeyInsight>();

        // Get top-importance findings
        var topFindings = importanceScores
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(x => x.Key)
            .ToList();

        foreach (var finding in topFindings)
        {
            var insight = new KeyInsight
            {
                Statement = finding,
                Importance = importanceScores.TryGetValue(finding, out var imp) ? imp : 0.5f,
                SourceFacts = new List<string> { finding },
                SupportingEvidence = $"Finding supports research topic: {input.Topic}"
            };
            insights.Add(insight);
        }

        return insights;
    }

    /// <summary>
    /// Synthesize findings into a coherent narrative.
    /// </summary>
    private async Task<string> SynthesizeInsightsAsync(
        AnalysisInput input,
        List<KeyInsight> insights,
        List<Contradiction> contradictions,
        CancellationToken cancellationToken)
    {
        try
        {
            var insightsSummary = string.Join("\n", 
                insights.Take(5).Select(i => $"- {i.Statement}"));
            var contradictionsSummary = contradictions.Any()
                ? string.Join("\n", contradictions.Take(3).Select(c => $"- {c.Fact1} vs {c.Fact2}"))
                : "No major contradictions found.";

            var synthesisPrompt = $@"Create a coherent synthesis narrative based on these key insights:

Key Insights:
{insightsSummary}

Potential Contradictions:
{contradictionsSummary}

Write a paragraph (5-8 sentences) that:
1. Summarizes the main findings about: {input.Topic}
2. Connects insights logically
3. Acknowledges contradictions if any
4. Provides overall perspective";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "user", Content = synthesisPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, null, cancellationToken);
            return response.Content ?? "No synthesis available";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to synthesize insights");
            return "Analysis synthesis could not be completed";
        }
    }

    /// <summary>
    /// Calculate overall confidence score based on quality, theme diversity, and contradiction presence.
    /// </summary>
    private float CalculateConfidenceScore(
        Dictionary<string, float> qualityScores,
        int themeCount,
        int contradictionCount)
    {
        var baseQuality = qualityScores.TryGetValue("overall", out var q) ? q : 0.5f;
        var themeFactor = Math.Min(1f, themeCount / 5f); // More themes = higher confidence
        var contradictionPenalty = Math.Min(0.3f, contradictionCount * 0.1f); // Contradictions lower confidence

        var confidence = (baseQuality * 0.6f) + (themeFactor * 0.4f) - contradictionPenalty;
        return Math.Clamp(confidence, 0f, 1f);
    }
}
