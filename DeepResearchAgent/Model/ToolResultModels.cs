using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// Result of quality evaluation on multiple dimensions.
/// </summary>
public class QualityEvaluationResult
{
    [JsonPropertyName("dimension_scores")]
    public Dictionary<string, DimensionScore> DimensionScores { get; set; } = new();

    [JsonPropertyName("overall_score")]
    public float OverallScore { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Score for a single quality dimension.
/// </summary>
public class DimensionScore
{
    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Result of webpage summarization.
/// </summary>
public class PageSummaryResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("key_points")]
    public List<string> KeyPoints { get; set; } = new();
}

/// <summary>
/// Result of fact extraction.
/// </summary>
public class FactExtractionResult
{
    [JsonPropertyName("facts")]
    public List<ExtractedFact> Facts { get; set; } = new();

    [JsonPropertyName("total_facts")]
    public int TotalFacts => Facts.Count;
}

/// <summary>
/// A single extracted fact.
/// </summary>
public class ExtractedFact
{
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Result of draft report refinement.
/// </summary>
public class RefinedDraftResult
{
    [JsonPropertyName("refined_report")]
    public string RefinedReport { get; set; } = string.Empty;

    [JsonPropertyName("changes_made")]
    public List<string> ChangesMade { get; set; } = new();

    [JsonPropertyName("improvement_score")]
    public float ImprovementScore { get; set; }
}

/// <summary>
/// Search result with confidence scoring.
/// Combines relevance and source credibility into overall confidence.
/// </summary>
public class ScoredSearchResult
{
    [JsonPropertyName("result")]
    public WebSearchResult Result { get; set; } = null!;

    [JsonPropertyName("relevance_score")]
    public float RelevanceScore { get; set; }

    [JsonPropertyName("source_credibility")]
    public float SourceCredibility { get; set; }

    [JsonPropertyName("overall_confidence")]
    public float OverallConfidence { get; set; }
}

/// <summary>
/// Utility for calculating confidence scores for search results.
/// </summary>
public static class ConfidenceScorer
{
    /// <summary>
    /// Calculate relevance score based on keyword matches (0-1).
    /// </summary>
    public static float CalculateRelevanceScore(string query, string content)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(content))
            return 0.5f;

        var queryWords = query.ToLower().Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Length == 0)
            return 0.5f;

        var contentLower = content.ToLower();
        var matches = queryWords.Count(w => w.Length > 2 && contentLower.Contains(w));
        var score = (float)matches / queryWords.Length;

        return Math.Clamp(score, 0f, 1f);
    }

    /// <summary>
    /// Get source credibility score based on domain (0-1).
    /// </summary>
    public static float GetSourceCredibility(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return 0.7f;

        var domain = new Uri(url).Host.ToLower();

        // Government and academic sites are most credible
        if (domain.Contains("gov") || domain.Contains("edu") || domain.Contains("ac."))
            return 0.95f;

        // Organization sites
        if (domain.Contains("org") || domain.Contains("ieee") || domain.Contains("acm"))
            return 0.85f;

        // News sites
        if (domain.Contains("news") || domain.Contains("times") || domain.Contains("bbc"))
            return 0.80f;

        // Research institutions
        if (domain.Contains("research") || domain.Contains("science") || domain.Contains("nature"))
            return 0.85f;

        // Default credibility
        return 0.70f;
    }

    /// <summary>
    /// Calculate overall confidence combining relevance and credibility.
    /// Weighted: 60% relevance, 40% credibility.
    /// </summary>
    public static float CalculateOverallConfidence(float relevanceScore, float credibilityScore)
    {
        var relevanceScore_clamped = Math.Clamp(relevanceScore, 0f, 1f);
        var credibilityScore_clamped = Math.Clamp(credibilityScore, 0f, 1f);

        return (relevanceScore_clamped * 0.6f) + (credibilityScore_clamped * 0.4f);
    }
}
