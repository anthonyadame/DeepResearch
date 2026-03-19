using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Hosted service that starts Prometheus HttpListener to expose metrics endpoint.
/// This allows the console application to expose /metrics for Prometheus scraping without ASP.NET Core.
/// </summary>
public class MetricsHostedService : IHostedService
{
    private readonly ILogger<MetricsHostedService> _logger;
    private MeterProvider? _meterProvider;

    public MetricsHostedService(ILogger<MetricsHostedService>? logger = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Prometheus metrics endpoint service is disabled (Prometheus exporter removed)");
        _logger.LogInformation("Metrics are exported via OTLP to the configured endpoint");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Prometheus metrics endpoint...");

        _meterProvider?.Dispose();
        _meterProvider = null;

        _logger.LogInformation("Prometheus metrics endpoint stopped");
        return Task.CompletedTask;
    }
}
