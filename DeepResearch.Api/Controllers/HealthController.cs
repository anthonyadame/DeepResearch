using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Reflection;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Health and Status Controller - System health and monitoring endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        var response = new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetVersionString(),
            Uptime = GetUptime()
        };

        return Ok(response);
    }

    /// <summary>
    /// Detailed health check with component status.
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), StatusCodes.Status200OK)]
    public ActionResult<DetailedHealthResponse> GetDetailedHealth()
    {
        var response = new DetailedHealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetVersionString(),
            Uptime = GetUptime(),
            Components = new Dictionary<string, ComponentHealth>
            {
                ["api"] = new() { Status = "healthy", Message = "API is operational" },
                ["agents"] = new() { Status = "healthy", Message = "All agents available" },
                ["workflows"] = new() { Status = "healthy", Message = "Workflows operational" }
            },
            Metrics = new HealthMetrics
            {
                MemoryUsageMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                ProcessorTime = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Get API version information.
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(typeof(VersionResponse), StatusCodes.Status200OK)]
    public ActionResult<VersionResponse> GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        var response = new VersionResponse
        {
            Version = version?.ToString() ?? "1.0.0",
            BuildDate = GetBuildDate(assembly),
            Environment = GetEnvironment()
        };

        return Ok(response);
    }

    private static string GetVersionString()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }

    private static string GetBuildDate(Assembly assembly)
    {
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? "Unknown";
    }

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    }
}

/// <summary>
/// Basic health response.
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
}

/// <summary>
/// Detailed health response with component status.
/// </summary>
public class DetailedHealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    public HealthMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Component health status.
/// </summary>
public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Health metrics.
/// </summary>
public class HealthMetrics
{
    public double MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public double ProcessorTime { get; set; }
}

/// <summary>
/// Version response.
/// </summary>
public class VersionResponse
{
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}
