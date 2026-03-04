namespace DeepResearch.Api.DTOs.Responses.Experiments;

/// <summary>
/// Response returned after logging a metric entry.
/// </summary>
public class ExperimentMetricLogResponse
{
    public string RunId { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
