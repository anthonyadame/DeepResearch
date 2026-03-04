namespace DeepResearch.Api.DTOs.Common;

/// <summary>
/// A research finding/fact for analysis.
/// </summary>
public class FindingDto
{
    /// <summary>
    /// Unique ID for the finding.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The finding statement.
    /// </summary>
    public required string Statement { get; set; }

    /// <summary>
    /// Source URL where finding came from.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Confidence score (0.0-1.0).
    /// </summary>
    public double ConfidenceScore { get; set; } = 0.5;

    /// <summary>
    /// Topic/category this finding belongs to.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Optional supporting evidence.
    /// </summary>
    public string? SupportingEvidence { get; set; }

    /// <summary>
    /// When the fact was extracted.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A fact from research findings (alias for FindingDto for workflows).
/// </summary>
public class FactDto : FindingDto
{
    /// <summary>
    /// Category for the fact.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Identified theme from analysis.
/// </summary>
public class ThemeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> RelatedFacts { get; set; } = new();
    public double Relevance { get; set; }
}

/// <summary>
/// Detected contradiction in findings.
/// </summary>
public class ContradictionDto
{
    public string Statement1 { get; set; } = string.Empty;
    public string Statement2 { get; set; } = string.Empty;
    public string? Source1 { get; set; }
    public string? Source2 { get; set; }
    public string? Resolution { get; set; }
    public double Severity { get; set; }
}

/// <summary>
/// Synthesized insight from analysis.
/// </summary>
public class InsightDto
{
    public string InsightStatement { get; set; } = string.Empty;
    public List<string> SupportingFacts { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public string? ImplicationsSummary { get; set; }
}

/// <summary>
/// Identified pattern in findings.
/// </summary>
public class PatternDto
{
    public string PatternDescription { get; set; } = string.Empty;
    public List<string> ExamplesInFindings { get; set; } = new();
    public double Consistency { get; set; }
    public string? SignificanceLevel { get; set; }
}
