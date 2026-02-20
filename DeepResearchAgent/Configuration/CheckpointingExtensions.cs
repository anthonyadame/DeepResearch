using System;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.DependencyInjection;

namespace DeepResearchAgent.Configuration;

/// <summary>
/// Extension methods for registering checkpoint service in the DI container.
/// </summary>
public static class CheckpointingExtensions
{
    /// <summary>
    /// Register checkpoint service with default options.
    /// </summary>
    public static IServiceCollection AddCheckpointService(this IServiceCollection services)
    {
        return AddCheckpointService(services, options => { });
    }

    /// <summary>
    /// Register checkpoint service with custom options.
    /// </summary>
    public static IServiceCollection AddCheckpointService(
        this IServiceCollection services,
        Action<CheckpointServiceOptions> configureOptions)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var options = new CheckpointServiceOptions();
        configureOptions(options);

        services.AddSingleton(options);
        services.AddSingleton<ICheckpointService, CheckpointService>();

        return services;
    }
}
