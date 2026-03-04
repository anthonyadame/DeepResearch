namespace DeepResearch.Api.DTOs.Requests.Experiments;

/// <summary>
/// Request payload for logging a single experiment metric.
/// </summary>
public class ExperimentMetricEntryRequest
{
    public required string RunId { get; set; }
    public required string Task { get; set; }
    public string? Phase { get; set; }
    public required string Metric { get; set; }
    public required double Value { get; set; }
    public long? Step { get; set; }
    public string? Unit { get; set; }
    public DateTime? TimestampUtc { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? Notes { get; set; }
}
