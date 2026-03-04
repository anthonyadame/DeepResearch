using System.Text.Json;

namespace DeepResearch.Api.Services.Experiments;

/// <summary>
/// File-backed experiment metrics logger.
/// </summary>
public interface IExperimentMetricsLogger
{
    Task LogAsync(ExperimentMetricEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metric entry stored in JSONL.
/// </summary>
public class ExperimentMetricEntry
{
    public string RunId { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public long? Step { get; set; }
    public string? Unit { get; set; }
    public DateTime TimestampUtc { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// JSONL implementation of experiment metrics logging.
/// </summary>
public class FileExperimentMetricsLogger : IExperimentMetricsLogger
{
    private readonly string _filePath;
    private readonly ILogger<FileExperimentMetricsLogger> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public FileExperimentMetricsLogger(IConfiguration configuration, ILogger<FileExperimentMetricsLogger> logger)
    {
        _logger = logger;

        var dataDirectory = configuration["ExperimentMetrics:DataDirectory"] ?? "data/experiments";
        var fileName = configuration["ExperimentMetrics:FileName"] ?? "metrics.jsonl";
        _filePath = Path.Combine(dataDirectory, fileName);

        Directory.CreateDirectory(dataDirectory);
    }

    public async Task LogAsync(ExperimentMetricEntry entry, CancellationToken cancellationToken = default)
    {
        entry.TimestampUtc = entry.TimestampUtc == default ? DateTime.UtcNow : entry.TimestampUtc;

        var payload = JsonSerializer.Serialize(entry, _serializerOptions);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, payload + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append experiment metric entry");
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }
}
