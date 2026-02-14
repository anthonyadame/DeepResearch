using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Background service for Agent-Lightning APO auto-scaling.
/// Monitors server load and triggers scaling decisions based on APO configuration.
/// </summary>
public class LightningApoScaler : BackgroundService
{
    private readonly LightningAPOConfig _apo;
    private readonly IAgentLightningService _lightning;
    private readonly ILogger<LightningApoScaler> _logger;

    public LightningApoScaler(
        LightningAPOConfig apo,
        IAgentLightningService lightning,
        ILogger<LightningApoScaler> logger)
    {
        _apo = apo ?? throw new ArgumentNullException(nameof(apo));
        _lightning = lightning ?? throw new ArgumentNullException(nameof(lightning));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_apo.Enabled || !_apo.AutoScaling.Enabled)
        {
            _logger.LogInformation("APO auto-scaling disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "APO auto-scaling enabled: {Strategy} strategy, {Min}-{Max} instances, scale-up @ {Up}%, scale-down @ {Down}%",
            _apo.Strategy,
            _apo.AutoScaling.MinInstances,
            _apo.AutoScaling.MaxInstances,
            _apo.AutoScaling.ScaleUpThresholdPercent,
            _apo.AutoScaling.ScaleDownThresholdPercent);

        var currentInstances = _apo.AutoScaling.MinInstances;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check Lightning server health
                var isHealthy = await _lightning.IsHealthyAsync();
                if (!isHealthy)
                {
                    _logger.LogWarning("Lightning server unhealthy, skipping auto-scaling check");
                    await Task.Delay(TimeSpan.FromSeconds(_apo.AutoScaling.CooldownSeconds), stoppingToken);
                    continue;
                }

                // Get server info to calculate load
                var serverInfo = await _lightning.GetServerInfoAsync();
                var load = CalculateLoad(serverInfo);

                _logger.LogDebug(
                    "APO auto-scaler: Load={Load:F1}%, Agents={Agents}, Connections={Connections}, Instances={Instances}",
                    load,
                    serverInfo.RegisteredAgents,
                    serverInfo.ActiveConnections,
                    currentInstances);

                // Scale-up decision
                if (load >= _apo.AutoScaling.ScaleUpThresholdPercent && 
                    currentInstances < _apo.AutoScaling.MaxInstances)
                {
                    _logger.LogWarning(
                        "APO auto-scaler: Load {Load:F1}% exceeds scale-up threshold {Threshold}% - scaling up from {Current} to {Target} instances",
                        load,
                        _apo.AutoScaling.ScaleUpThresholdPercent,
                        currentInstances,
                        currentInstances + 1);

                    // Trigger scale-up (placeholder - would integrate with orchestrator)
                    await TriggerScaleUpAsync(currentInstances, currentInstances + 1);
                    currentInstances++;
                }
                // Scale-down decision
                else if (load <= _apo.AutoScaling.ScaleDownThresholdPercent && 
                         currentInstances > _apo.AutoScaling.MinInstances)
                {
                    _logger.LogInformation(
                        "APO auto-scaler: Load {Load:F1}% below scale-down threshold {Threshold}% - scaling down from {Current} to {Target} instances",
                        load,
                        _apo.AutoScaling.ScaleDownThresholdPercent,
                        currentInstances,
                        currentInstances - 1);

                    // Trigger scale-down (placeholder - would integrate with orchestrator)
                    await TriggerScaleDownAsync(currentInstances, currentInstances - 1);
                    currentInstances--;
                }

                // Wait for cooldown period before next check
                await Task.Delay(TimeSpan.FromSeconds(_apo.AutoScaling.CooldownSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("APO auto-scaler shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in APO auto-scaler loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private double CalculateLoad(LightningServerInfo serverInfo)
    {
        if (serverInfo.RegisteredAgents == 0)
        {
            return 0;
        }

        // Calculate load as percentage of active connections to registered agents
        // This is a simplified metric - real implementation would consider CPU/memory
        var connectionRatio = (double)serverInfo.ActiveConnections / serverInfo.RegisteredAgents * 100;
        
        return Math.Min(connectionRatio, 100);
    }

    private async Task TriggerScaleUpAsync(int currentInstances, int targetInstances)
    {
        // Placeholder for actual scaling logic
        // In production, this would:
        // 1. Call orchestrator API (Kubernetes, Azure Container Apps, etc.)
        // 2. Register new agent instances with Lightning server
        // 3. Update service discovery
        
        _logger.LogInformation(
            "APO auto-scaler: Triggering scale-up from {Current} to {Target} instances",
            currentInstances,
            targetInstances);

        await Task.CompletedTask;
    }

    private async Task TriggerScaleDownAsync(int currentInstances, int targetInstances)
    {
        // Placeholder for actual scaling logic
        // In production, this would:
        // 1. Drain connections from instance to be removed
        // 2. Deregister agent from Lightning server
        // 3. Call orchestrator API to remove instance
        
        _logger.LogInformation(
            "APO auto-scaler: Triggering scale-down from {Current} to {Target} instances",
            currentInstances,
            targetInstances);

        await Task.CompletedTask;
    }
}
