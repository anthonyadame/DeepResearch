using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Input for ResearcherAgent: Contains the research topic and parameters.
/// </summary>
public class ResearchInput
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("research_brief")]
    public string ResearchBrief { get; set; } = string.Empty;

    [JsonPropertyName("max_iterations")]
    public int MaxIterations { get; set; } = 3;

    [JsonPropertyName("min_quality_threshold")]
    public float MinQualityThreshold { get; set; } = 7.0f;
}

/// <summary>
/// Output from ResearcherAgent: Contains research findings and quality metrics.
/// </summary>
public class ResearchOutput
{
    [JsonPropertyName("findings")]
    public List<FactExtractionResult> Findings { get; set; } = new();

    [JsonPropertyName("average_quality")]
    public float AverageQuality { get; set; }

    [JsonPropertyName("iterations_used")]
    public int IterationsUsed { get; set; }

    [JsonPropertyName("total_facts_extracted")]
    public int TotalFactsExtracted => Findings.Sum(f => f.Facts.Count);

    [JsonPropertyName("research_topics_covered")]
    public List<string> ResearchTopicsCovered { get; set; } = new();

    [JsonPropertyName("completion_status")]
    public string CompletionStatus { get; set; } = "pending";
}

/// <summary>
/// Input for AnalystAgent: Contains research findings to analyze.
/// </summary>
public class AnalysisInput
{
    [JsonPropertyName("findings")]
    public List<FactExtractionResult> Findings { get; set; } = new();

    [JsonPropertyName("research_brief")]
    public string ResearchBrief { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;
}

/// <summary>
/// Key insight identified by AnalystAgent.
/// </summary>
public class KeyInsight
{
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("importance")]
    public float Importance { get; set; }

    [JsonPropertyName("source_facts")]
    public List<string> SourceFacts { get; set; } = new();

    [JsonPropertyName("supporting_evidence")]
    public string SupportingEvidence { get; set; } = string.Empty;
}

/// <summary>
/// Contradiction detected in research findings.
/// </summary>
public class Contradiction
{
    [JsonPropertyName("fact_1")]
    public string Fact1 { get; set; } = string.Empty;

    [JsonPropertyName("fact_2")]
    public string Fact2 { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public float Severity { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Output from AnalystAgent: Contains analysis and insights.
/// </summary>
public class AnalysisOutput
{
    [JsonPropertyName("synthesis_narrative")]
    public string SynthesisNarrative { get; set; } = string.Empty;

    [JsonPropertyName("key_insights")]
    public List<KeyInsight> KeyInsights { get; set; } = new();

    [JsonPropertyName("contradictions")]
    public List<Contradiction> Contradictions { get; set; } = new();

    [JsonPropertyName("confidence_score")]
    public float ConfidenceScore { get; set; }

    [JsonPropertyName("themes_identified")]
    public List<string> ThemesIdentified { get; set; } = new();
}

/// <summary>
/// Section of a report.
/// </summary>
public class ReportSection
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("citation_indices")]
    public List<int> CitationIndices { get; set; } = new();
}

/// <summary>
/// Citation for a fact in the report.
/// </summary>
public class Citation
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("accessed_at")]
    public DateTime AccessedAt { get; set; }
}

/// <summary>
/// Input for ReportAgent: Contains research and analysis to format.
/// </summary>
public class ReportInput
{
    [JsonPropertyName("research")]
    public ResearchOutput Research { get; set; } = new();

    [JsonPropertyName("analysis")]
    public AnalysisOutput Analysis { get; set; } = new();

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Deep Research Agent";
}

/// <summary>
/// Output from ReportAgent: The final formatted report.
/// </summary>
public class ReportOutput
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("executive_summary")]
    public string ExecutiveSummary { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<ReportSection> Sections { get; set; } = new();

    [JsonPropertyName("citations")]
    public List<Citation> Citations { get; set; } = new();

    [JsonPropertyName("quality_score")]
    public float QualityScore { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completion_status")]
    public string CompletionStatus { get; set; } = "complete";
}
