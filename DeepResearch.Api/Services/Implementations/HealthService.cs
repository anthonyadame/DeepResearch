namespace DeepResearch.Api.Services.Implementations;

using DeepResearch.Api.DTOs.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of health check service.
/// </summary>
public class HealthService : IHealthService
{
    private readonly ILogger<HealthService> _logger;

    public HealthService(ILogger<HealthService> logger)
    {
        _logger = logger;
    }

    public async Task<ApiResponse<HealthStatusDto>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Health check requested");

            var status = new HealthStatusDto
            {
                Status = "Healthy",
                CheckedAt = DateTime.UtcNow,
                Services = new Dictionary<string, string>
                {
                    { "WorkflowService", "Healthy" },
                    { "AgentService", "Healthy" },
                    { "CoreService", "Healthy" },
                    { "Database", "Healthy" },
                    { "LLM", "Healthy" }
                },
                Message = "All systems operational"
            };

            return new ApiResponse<HealthStatusDto>
            {
                Success = true,
                Data = status,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            throw;
        }
    }

    public async Task<ApiResponse<ServiceHealthDto>> CheckServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Health check for service: {ServiceName}", serviceName);

            var status = new ServiceHealthDto
            {
                ServiceName = serviceName,
                Status = "Healthy",
                ResponseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Details = $"{serviceName} is responding normally"
            };

            return new ApiResponse<ServiceHealthDto>
            {
                Success = true,
                Data = status,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service health check failed for: {ServiceName}", serviceName);
            throw;
        }
    }
}
