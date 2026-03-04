namespace DeepResearch.Api.Middleware;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Middleware for handling errors consistently across the API.
/// Returns detailed error responses following the "Detailed" error preference.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new ApiError
        {
            Code = "INTERNAL_ERROR",
            Message = "An unexpected error occurred",
            Details = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.Message,
            StatusCode = StatusCodes.Status500InternalServerError,
            CorrelationId = context.Request.HttpContext.Items.TryGetValue("CorrelationId", out var id)
                ? id?.ToString()
                : Guid.NewGuid().ToString()
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Middleware for adding correlation IDs to requests for tracking.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var id)
            ? id.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        await _next(context);
    }
}

/// <summary>
/// Middleware for logging HTTP requests and responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
            ? id?.ToString()
            : "unknown";

        var startTime = DateTime.UtcNow;
        _logger.LogInformation(
            "Request started: {Method} {Path} [{CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        // Copy the original response stream
        var originalBodyStream = context.Response.Body;
        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Request completed: {Method} {Path} {StatusCode} ({Duration}ms) [{CorrelationId}]",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    duration.TotalMilliseconds,
                    correlationId);

                // FIX: Copy response body back BEFORE finally block
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            finally
            {
                // Restore original stream
                context.Response.Body = originalBodyStream;
            }
        }
    }
}

/// <summary>
/// Middleware for validation error handling.
/// </summary>
public class ValidationErrorMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationErrorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
