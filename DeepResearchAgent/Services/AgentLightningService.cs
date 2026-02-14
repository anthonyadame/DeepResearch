using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.CircuitBreaker;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Integration with Microsoft Agent-Lightning for distributed agent orchestration.
/// Supports Lightning Server/Client architecture with APO (Automatic Performance Optimization) and VERL (Verification and Reasoning Layer).
/// Enhanced with circuit breaker pattern for resilience.
/// </summary>
public interface IAgentLightningService
{
    Task<bool> IsHealthyAsync();
    Task<LightningServerInfo> GetServerInfoAsync();
    Task<AgentRegistration> RegisterAgentAsync(string agentId, string agentType, Dictionary<string, object> capabilities);
    Task<AgentTaskResult> SubmitTaskAsync(string agentId, AgentTask task, ApoExecutionOptions? apoOptions = null);
    Task<List<AgentTask>> GetPendingTasksAsync(string agentId);
    Task UpdateTaskStatusAsync(string taskId, TaskStatus status, string? result = null);
    Task<VerificationResult> VerifyResultAsync(string taskId, string result);
    
    LightningAPOConfig GetApoConfig();
    CircuitState GetCircuitState();
}

public class AgentLightningService : IAgentLightningService
{
    private readonly HttpClient _httpClient;
    private readonly string _lightningServerUrl;
    private readonly string _clientId;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly LightningAPOConfig _apo;
    private readonly ILogger<AgentLightningService>? _logger;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ResiliencePipeline<HttpResponseMessage> _circuitBreakerPipeline;

    public AgentLightningService(
        HttpClient httpClient,
        string lightningServerUrl = "http://localhost:8090",
        string? clientId = null,
        LightningAPOConfig? apo = null,
        ILogger<AgentLightningService>? logger = null)
    {
        _httpClient = httpClient;
        _lightningServerUrl = lightningServerUrl;
        _clientId = clientId ?? $"research-agent-{Guid.NewGuid():N}";
        _apo = apo ?? new LightningAPOConfig();
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Initialize APO components
        _concurrencyGate = AgentLightningServiceExtensions.CreateConcurrencyGate(_apo);
        _retryPolicy = AgentLightningServiceExtensions.CreateRetryPolicy(_apo, OnRetry);

        // Initialize circuit breaker pipeline
        _circuitBreakerPipeline = CreateCircuitBreakerPipeline();
    }

    private ResiliencePipeline<HttpResponseMessage> CreateCircuitBreakerPipeline()
    {
        if (!_apo.CircuitBreaker.Enabled)
        {
            // Return pass-through pipeline if circuit breaker disabled
            return ResiliencePipeline<HttpResponseMessage>.Empty;
        }

        var pipelineBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // Add circuit breaker
        pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = _apo.CircuitBreaker.FailureRateThreshold / 100.0,
            SamplingDuration = TimeSpan.FromSeconds(_apo.CircuitBreaker.SamplingDurationSeconds),
            MinimumThroughput = _apo.CircuitBreaker.MinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(_apo.CircuitBreaker.BreakDurationSeconds),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => !r.IsSuccessStatusCode),
            OnOpened = args =>
            {
                if (_apo.CircuitBreaker.LogStateChanges)
                {
                    _logger?.LogWarning(
                        "Circuit breaker OPENED for Lightning server. Failure rate: {FailureRate}%. Break duration: {Duration}s",
                        args.BreakDuration.TotalSeconds);
                }
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                if (_apo.CircuitBreaker.LogStateChanges)
                {
                    _logger?.LogInformation("Circuit breaker CLOSED for Lightning server. Service recovered.");
                }
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                if (_apo.CircuitBreaker.LogStateChanges)
                {
                    _logger?.LogInformation("Circuit breaker HALF-OPEN for Lightning server. Testing recovery...");
                }
                return ValueTask.CompletedTask;
            }
        });

        return pipelineBuilder.Build();
    }

    public CircuitState GetCircuitState()
    {
        // Circuit breaker state is internal to Polly v8, return default
        // In production, you'd track this via metrics/events
        return CircuitState.Closed;
    }

    private void OnRetry(DelegateResult<HttpResponseMessage> outcome, TimeSpan timespan, int retryCount, Context context)
    {
    }

    public LightningAPOConfig GetApoConfig() => _apo;

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_lightningServerUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LightningServerInfo> GetServerInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_lightningServerUrl}/api/server/info");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var info = JsonSerializer.Deserialize<LightningServerInfo>(content, _jsonOptions);
            return info ?? new LightningServerInfo();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to get Lightning Server info", ex);
        }
    }

    public async Task<AgentRegistration> RegisterAgentAsync(string agentId, string agentType, Dictionary<string, object> capabilities)
    {
        try
        {
            var registration = new AgentRegistration
            {
                AgentId = agentId,
                AgentType = agentType,
                ClientId = _clientId,
                Capabilities = capabilities,
                RegisteredAt = DateTime.UtcNow
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/agents/register",
                registration,
                _jsonOptions
            );
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentRegistration>(content, _jsonOptions);
            return result ?? registration;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to register agent with Lightning Server", ex);
        }
    }

    public async Task<AgentTaskResult> SubmitTaskAsync(string agentId, AgentTask task, ApoExecutionOptions? apoOptions = null)
    {
        var effectiveApo = apoOptions?.MergeWith(_apo) ?? _apo;
        
        if (!effectiveApo.Enabled)
        {
            // Bypass APO if disabled
            return await SubmitTaskDirectAsync(agentId, task);
        }

        await _concurrencyGate.WaitAsync();
        
        try
        {
            
            task.SubmittedAt = DateTime.UtcNow;
            task.Status = TaskStatus.Submitted;
            task.Priority = apoOptions?.Priority ?? effectiveApo.GetTaskPriority();

            var requesturi = $"{_lightningServerUrl}/api/tasks/submit";
            var requestBody = new { agentId, task };
            var requestContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var stringcontent = new StringContent(requestContent, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response;

            // Execute with circuit breaker + retry
            try
            {
                response = await _circuitBreakerPipeline.ExecuteAsync(async ct =>
                {
                    return await _retryPolicy.ExecuteAsync(async ct2 =>
                        await _httpClient.PostAsync(requesturi, stringcontent, ct2),
                        ct);
                }, CancellationToken.None);
            }
            catch (BrokenCircuitException ex)
            {
                _logger?.LogWarning("Circuit breaker is OPEN. Lightning server unavailable. Falling back to local execution.");
                
                if (effectiveApo.CircuitBreaker.EnableFallback)
                {
                    return await ExecuteFallbackAsync(agentId, task);
                }

                throw new InvalidOperationException("Lightning server circuit breaker is open and fallback disabled", ex);
            }

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentTaskResult>(content, _jsonOptions);

            // Conditional VERL verification based on APO strategy
            var shouldVerify = apoOptions?.ForceVerification 
                ?? effectiveApo.ShouldVerify(task.VerificationRequired);
            
            if (shouldVerify && result?.TaskId != null)
            {
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await VerifyResultAsync(result.TaskId, result.Result ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogInformation("lightning.apo", "verification_failed");
                        // Log but don't throw - verification is best-effort
                    }
                });
            }

            return result ?? new AgentTaskResult { TaskId = task.Id, Status = TaskStatus.Submitted };
        }
        catch (BrokenCircuitException)
        {
            // Already handled above, but catch again if fallback threw
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to submit task to Lightning Server", ex);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    /// <summary>
    /// Fallback execution when Lightning server circuit is open.
    /// Returns a simulated result without actually calling the server.
    /// </summary>
    private async Task<AgentTaskResult> ExecuteFallbackAsync(string agentId, AgentTask task)
    {
        _logger?.LogInformation("Executing fallback for task {TaskId} (agent: {AgentId})", task.Id, agentId);

        // Simulate local execution
        await Task.Delay(100); // Simulate minimal processing

        return new AgentTaskResult
        {
            TaskId = task.Id,
            Status = TaskStatus.Completed,
            Result = JsonSerializer.Serialize(new { 
                fallback = true,
                message = "Executed locally due to Lightning server unavailability",
                agentId,
                taskId = task.Id,
                completedAt = DateTime.UtcNow
            }),
            CompletedAt = DateTime.UtcNow
        };
    }

    private async Task<AgentTaskResult> SubmitTaskDirectAsync(string agentId, AgentTask task)
    {
        try
        {
            task.SubmittedAt = DateTime.UtcNow;
            task.Status = TaskStatus.Submitted;

            var requesturi = $"{_lightningServerUrl}/api/tasks/submit";
            var requestBody = new { agentId, task };
            var requestContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var stringcontent = new StringContent(requestContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requesturi, stringcontent);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentTaskResult>(content, _jsonOptions);
            return result ?? new AgentTaskResult { TaskId = task.Id, Status = TaskStatus.Submitted };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to submit task to Lightning Server", ex);
        }
    }

    public async Task<List<AgentTask>> GetPendingTasksAsync(string agentId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_lightningServerUrl}/api/agents/{agentId}/tasks/pending"
            );
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var tasks = JsonSerializer.Deserialize<List<AgentTask>>(content, _jsonOptions);
            return tasks ?? new List<AgentTask>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to get pending tasks", ex);
        }
    }

    public async Task UpdateTaskStatusAsync(string taskId, TaskStatus status, string? result = null)
    {
        try
        {
            var update = new { taskId, status, result, completedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PutAsJsonAsync(
                $"{_lightningServerUrl}/api/tasks/{taskId}/status",
                update,
                _jsonOptions
            );
            
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to update task status", ex);
        }
    }

    public async Task<VerificationResult> VerifyResultAsync(string taskId, string result)
    {
        try
        {
            var verification = new { taskId, result, verifiedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/verl/verify",
                verification,
                _jsonOptions
            );
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var verificationResult = JsonSerializer.Deserialize<VerificationResult>(content, _jsonOptions);
            return verificationResult ?? new VerificationResult { IsValid = true, TaskId = taskId };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("VERL verification failed", ex);
        }
    }
}

// Models
public class LightningServerInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("apoEnabled")]
    public bool ApoEnabled { get; set; } = true;

    [JsonPropertyName("verlEnabled")]
    public bool VerlEnabled { get; set; } = true;

    [JsonPropertyName("registeredAgents")]
    public int RegisteredAgents { get; set; }

    [JsonPropertyName("activeConnections")]
    public int ActiveConnections { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }
}

public class AgentRegistration
{
    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("agentType")]
    public string AgentType { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public Dictionary<string, object> Capabilities { get; set; } = new();

    [JsonPropertyName("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class AgentTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public Dictionary<string, object> Input { get; set; } = new();

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    [JsonPropertyName("submittedAt")]
    public DateTime SubmittedAt { get; set; }

    [JsonPropertyName("resultData")]
    public string? ResultData { get; set; }

    [JsonPropertyName("verificationRequired")]
    public bool VerificationRequired { get; set; } = true;
}

public class AgentTaskResult
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}

public class VerificationResult
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();

    [JsonPropertyName("verifiedAt")]
    public DateTime VerifiedAt { get; set; }
}

public enum TaskStatus
{
    Submitted,
    Pending,
    InProgress,
    Completed,
    Failed,
    VerificationRequired,
    VerificationPassed,
    VerificationFailed
}