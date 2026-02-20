using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DeepResearch.Api;

/// <summary>
/// Extension methods for registering API services and middleware.
/// </summary>
public static class ApiServiceExtensions
{
    /// <summary>
    /// Add DeepResearch API services to the dependency injection container.
    /// Includes workflow controllers, Swagger documentation, and CORS.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDeepResearchApi(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    /// <summary>
    /// Use DeepResearch API middleware and configure HTTP pipeline.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="isDevelopment">Whether running in development environment.</param>
    /// <returns>Application builder for chaining.</returns>
    public static IApplicationBuilder UseDeepResearchApi(this IApplicationBuilder app, bool isDevelopment)
    {
        if (isDevelopment)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
