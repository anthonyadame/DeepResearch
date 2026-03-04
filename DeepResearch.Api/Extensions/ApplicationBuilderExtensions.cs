namespace DeepResearch.Api.Extensions;

using Middleware;
using Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Use all API middleware in correct order.
    /// </summary>
    public static IApplicationBuilder UseApiMiddleware(this IApplicationBuilder app)
    {
        // Correlation ID middleware for request tracking
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Request logging middleware
        app.UseMiddleware<RequestLoggingMiddleware>();

        // Error handling middleware (must be first in the pipeline)
        app.UseMiddleware<ErrorHandlingMiddleware>();

        return app;
    }

    /// <summary>
    /// Configure Swagger UI with enhanced settings.
    /// </summary>
    public static IApplicationBuilder UseApiSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Deep Research Agent API v1");
            options.RoutePrefix = "swagger";
            options.DisplayOperationId();
            options.EnableTryItOutByDefault();
        });

        return app;
    }

    /// <summary>
    /// Enable response compression.
    /// </summary>
    public static IApplicationBuilder UseApiCompression(this IApplicationBuilder app)
    {
        app.UseResponseCompression();
        return app;
    }

    /// <summary>
    /// Map health check endpoints.
    /// Note: Must be called on IEndpointRouteBuilder (in MapGroup or extension methods on app.MapGet, etc).
    /// </summary>
    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }

    /// <summary>
    /// Configure HTTPS redirection.
    /// </summary>
    public static IApplicationBuilder UseApiHttpsRedirection(this IApplicationBuilder app)
    {
        app.UseHttpsRedirection();
        return app;
    }
}
