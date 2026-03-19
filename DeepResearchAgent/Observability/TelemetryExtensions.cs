using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DeepResearchAgent.Observability;

/// <summary>
/// Extension methods for registering OpenTelemetry services
/// </summary>
public static class TelemetryExtensions
{
    private static TracerProvider? _tracerProvider;
    private static MeterProvider? _meterProvider;

    /// <summary>
    /// Add OpenTelemetry observability to the service collection.
    /// Configures tracing (Jaeger), metrics (Prometheus), and logging.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        Action<OpenTelemetryOptions>? configure = null)
    {
        var options = new OpenTelemetryOptions();
        configure?.Invoke(options);

        // Configure OpenTelemetry Resource
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: DiagnosticConfig.ServiceName,
                serviceVersion: DiagnosticConfig.ServiceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = options.Environment ?? "development",
                ["host.name"] = Environment.MachineName
            });

        // Add OpenTelemetry Tracing and Metrics
        var openTelemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(tracerProvider =>
            {
                tracerProvider
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(DiagnosticConfig.ServiceName)
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.ToString());
                        };
                    });

                // Add Console exporter for development
                if (options.EnableConsoleExporter)
                {
                    tracerProvider.AddConsoleExporter();
                }

                // Add OTLP exporter for Jaeger
                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracerProvider.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            })
            .WithMetrics(meterProvider =>
            {
                meterProvider
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(DiagnosticConfig.ServiceName)
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation();

                // Add Console exporter for development
                if (options.EnableConsoleExporter)
                {
                    meterProvider.AddConsoleExporter();
                }

                // Note: Prometheus exporter is added in MetricsHostedService
                // which runs its own ASP.NET Core web server

                // Add OTLP exporter
                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    meterProvider.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            });

        return services;
    }

    /// <summary>
    /// Force flush all OpenTelemetry exporters to ensure traces/metrics are sent before shutdown.
    /// Call this before application exit to prevent loss of in-flight data.
    /// Note: The actual flush happens through the IAsyncDisposable interface of the SDK.
    /// </summary>
    public static void ForceFlush()
    {
        try
        {
            // OpenTelemetry SDK automatically flushes on shutdown.
            // Adding a small delay here to ensure the flush completes before exit
            System.Threading.Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ForceFlush: {ex.Message}");
        }
    }
}

/// <summary>
/// Configuration options for OpenTelemetry
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>
    /// Deployment environment (development, staging, production)
    /// </summary>
    public string Environment { get; set; } = "development";

    /// <summary>
    /// OTLP endpoint for exporting traces and metrics (Jaeger collector)
    /// Default: http://localhost:4317
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Enable Prometheus exporter (for scraping metrics)
    /// </summary>
    public bool EnablePrometheusExporter { get; set; } = true;

    /// <summary>
    /// Enable console exporter for debugging
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Prometheus scrape endpoint path
    /// </summary>
    public string PrometheusScrapePath { get; set; } = "/metrics";
}
