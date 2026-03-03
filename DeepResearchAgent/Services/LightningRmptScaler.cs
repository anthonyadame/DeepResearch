using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Background service for Agent-Lightning RMPT auto-scaling.
/// Monitors server load and triggers scaling decisions based on RMPT configuration.
/// </summary>
public class LightningRmptScaler : BackgroundService
{
    private readonly LightningRMPTConfig _rmpt;
    private readonly IAgentLightningService _lightning;
    private readonly ILogger<LightningRmptScaler> _logger;

    public LightningRmptScaler(
        LightningRMPTConfig rmpt,
        IAgentLightningService lightning,
        ILogger<LightningRmptScaler> logger)
    {
        _rmpt = rmpt ?? throw new ArgumentNullException(nameof(rmpt));
        _lightning = lightning ?? throw new ArgumentNullException(nameof(lightning));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_rmpt.Enabled || !_rmpt.AutoScaling.Enabled)
        {
            _logger.LogInformation("RMPT auto-scaling disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "RMPT auto-scaling enabled: {Strategy} strategy, {Min}-{Max} instances, scale-up @ {Up}%, scale-down @ {Down}%",
            _rmpt.Strategy,
            _rmpt.AutoScaling.MinInstances,
            _rmpt.AutoScaling.MaxInstances,
            _rmpt.AutoScaling.ScaleUpThresholdPercent,
            _rmpt.AutoScaling.ScaleDownThresholdPercent);

        var currentInstances = _rmpt.AutoScaling.MinInstances;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check Lightning server health
                var isHealthy = await _lightning.IsHealthyAsync();
                if (!isHealthy)
                {
                    _logger.LogWarning("Lightning server unhealthy, skipping auto-scaling check");
                    await Task.Delay(TimeSpan.FromSeconds(_rmpt.AutoScaling.CooldownSeconds), stoppingToken);
                    continue;
                }

                // Get server info to calculate load
                var serverInfo = await _lightning.GetServerInfoAsync();
                var load = CalculateLoad(serverInfo);

                _logger.LogDebug(
                    "RMPT auto-scaler: Load={Load:F1}%, Agents={Agents}, Connections={Connections}, Instances={Instances}",
                    load,
                    serverInfo.RegisteredAgents,
                    serverInfo.ActiveConnections,
                    currentInstances);

                // Scale-up decision
                if (load >= _rmpt.AutoScaling.ScaleUpThresholdPercent && 
                    currentInstances < _rmpt.AutoScaling.MaxInstances)
                {
                    _logger.LogWarning(
                        "RMPT auto-scaler: Load {Load:F1}% exceeds scale-up threshold {Threshold}% - scaling up from {Current} to {Target} instances",
                        load,
                        _rmpt.AutoScaling.ScaleUpThresholdPercent,
                        currentInstances,
                        currentInstances + 1);

                    // Trigger scale-up (placeholder - would integrate with orchestrator)
                    await TriggerScaleUpAsync(currentInstances, currentInstances + 1);
                    currentInstances++;
                }
                // Scale-down decision
                else if (load <= _rmpt.AutoScaling.ScaleDownThresholdPercent && 
                         currentInstances > _rmpt.AutoScaling.MinInstances)
                {
                    _logger.LogInformation(
                        "RMPT auto-scaler: Load {Load:F1}% below scale-down threshold {Threshold}% - scaling down from {Current} to {Target} instances",
                        load,
                        _rmpt.AutoScaling.ScaleDownThresholdPercent,
                        currentInstances,
                        currentInstances - 1);

                    // Trigger scale-down (placeholder - would integrate with orchestrator)
                    await TriggerScaleDownAsync(currentInstances, currentInstances - 1);
                    currentInstances--;
                }

                // Wait for cooldown period before next check
                await Task.Delay(TimeSpan.FromSeconds(_rmpt.AutoScaling.CooldownSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("RMPT auto-scaler shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RMPT auto-scaler loop");
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
            "RMPT auto-scaler: Triggering scale-up from {Current} to {Target} instances",
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
            "RMPT auto-scaler: Triggering scale-down from {Current} to {Target} instances",
            currentInstances,
            targetInstances);

        await Task.CompletedTask;
    }
}
