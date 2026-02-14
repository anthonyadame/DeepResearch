using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents.Middleware;

/// <summary>
/// Middleware for logging agent execution details.
/// Can be added to any AIAgent using AIAgentBuilder.
/// </summary>
public class LoggingAgentMiddleware : DelegatingAIAgent
{
    private readonly ILogger _logger;

    public LoggingAgentMiddleware(AIAgent innerAgent, ILogger logger)
        : base(innerAgent)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<AgentRunResponse> RunCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageCount = messages?.Count() ?? 0;
        _logger.LogInformation("Agent execution starting with {MessageCount} messages", messageCount);

        try
        {
            var response = await base.RunCoreAsync(messages!, thread, options, cancellationToken);
            _logger.LogInformation("Agent execution completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent execution failed");
            throw;
        }
    }
}

/// <summary>
/// Middleware for timing agent execution.
/// Records execution duration and can trigger alerts for slow operations.
/// </summary>
public class TimingAgentMiddleware : DelegatingAIAgent
{
    private readonly ILogger? _logger;
    private readonly TimeSpan _slowThreshold;

    public TimingAgentMiddleware(
        AIAgent innerAgent, 
        ILogger? logger = null, 
        TimeSpan? slowThreshold = null)
        : base(innerAgent)
    {
        _logger = logger;
        _slowThreshold = slowThreshold ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task<AgentRunResponse> RunCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            return await base.RunCoreAsync(messages, thread, options, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            
            if (duration > _slowThreshold)
            {
                _logger?.LogWarning(
                    "Agent execution took {Duration:F2}s (threshold: {Threshold:F2}s)",
                    duration.TotalSeconds,
                    _slowThreshold.TotalSeconds);
            }
            else
            {
                _logger?.LogDebug("Agent execution took {Duration:F2}s", duration.TotalSeconds);
            }
        }
    }
}

/// <summary>
/// Middleware for retry logic with exponential backoff.
/// Automatically retries failed agent operations.
/// </summary>
public class RetryAgentMiddleware : DelegatingAIAgent
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly ILogger? _logger;

    public RetryAgentMiddleware(
        AIAgent innerAgent,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null)
        : base(innerAgent)
    {
        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);
        _logger = logger;
    }

    protected override async Task<AgentRunResponse> RunCoreAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                return await base.RunCoreAsync(messages, thread, options, cancellationToken);
            }
            catch (Exception ex) when (attempt < _maxAttempts)
            {
                lastException = ex;
                var delay = TimeSpan.FromMilliseconds(
                    _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                
                _logger?.LogWarning(
                    ex,
                    "Agent execution failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay:F2}s...",
                    attempt,
                    _maxAttempts,
                    delay.TotalSeconds);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        // All attempts failed
        _logger?.LogError(
            lastException,
            "Agent execution failed after {MaxAttempts} attempts",
            _maxAttempts);
        throw lastException!;
    }
}
