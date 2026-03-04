using DeepResearchAgent.Services;
using DeepResearch.Api.Services.ChatHistory;
using Microsoft.Extensions.Logging;

namespace DeepResearch.Api.Tests.Helpers;

/// <summary>
/// Mock implementation of OllamaService for testing
/// Since OllamaService methods are not virtual, we provide a working instance
/// that doesn't require an actual Ollama server
/// </summary>
public class MockOllamaService : OllamaService
{
    public MockOllamaService() : base(
        "http://localhost:11434",
        "llama3.1:8b",
        new HttpClient(new MockHttpMessageHandler()),
        null)
    {
    }
}

/// <summary>
/// Mock HTTP handler that returns valid Ollama API responses
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Return a mock Ollama chat response
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""model"": ""llama3.1:8b"",
                ""message"": {
                    ""role"": ""assistant"",
                    ""content"": ""Mock LLM response for testing""
                }
            }", System.Text.Encoding.UTF8, "application/json")
        };

        return Task.FromResult(mockResponse);
    }
}

/// <summary>
/// Mock implementation of ICategorizationService for testing
/// </summary>
public class MockCategorizationService : ICategorizationService
{
    public Task<string[]> CategorizeAsync(
        string title,
        string[] messageSnippets,
        CancellationToken cancellationToken = default)
    {
        // Simple categorization logic for testing
        if (title.Contains("Research", StringComparison.OrdinalIgnoreCase) ||
            messageSnippets.Any(m => m.Contains("research", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new[] { "research" });
        }

        if (title.Contains("Technical", StringComparison.OrdinalIgnoreCase) ||
            messageSnippets.Any(m => m.Contains("code", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new[] { "technical" });
        }

        return Task.FromResult(new[] { "general" });
    }
}

/// <summary>
/// Mock implementation of IAgentLightningService for testing
/// </summary>
public class MockAgentLightningService : IAgentLightningService
{
    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<LightningServerInfo> GetServerInfoAsync()
    {
        return Task.FromResult(new LightningServerInfo
        {
            Version = "1.0.0-mock",
            RmptEnabled = true,
            RlcsEnabled = true,
            RegisteredAgents = 0,
            ActiveConnections = 0,
            StartedAt = DateTime.UtcNow
        });
    }

    public Task<AgentRegistration> RegisterAgentAsync(string agentId, string agentType, Dictionary<string, object> capabilities)
    {
        return Task.FromResult(new AgentRegistration
        {
            AgentId = agentId,
            AgentType = agentType,
            ClientId = "mock-client",
            Capabilities = capabilities,
            RegisteredAt = DateTime.UtcNow,
            IsActive = true
        });
    }

    public Task<AgentTaskResult> SubmitTaskAsync(string agentId, AgentTask task, RmptExecutionOptions? rmptOptions = null)
    {
        return Task.FromResult(new AgentTaskResult
        {
            TaskId = task.Id,
            Status = DeepResearchAgent.Services.TaskStatus.Completed,
            Result = "Mock task result",
            CompletedAt = DateTime.UtcNow
        });
    }

    public Task<List<AgentTask>> GetPendingTasksAsync(string agentId)
    {
        return Task.FromResult(new List<AgentTask>());
    }

    public Task UpdateTaskStatusAsync(string taskId, DeepResearchAgent.Services.TaskStatus status, string? result = null)
    {
        return Task.CompletedTask;
    }

    public Task<VerificationResult> VerifyResultAsync(string taskId, string result)
    {
        return Task.FromResult(new VerificationResult
        {
            TaskId = taskId,
            IsValid = true,
            Confidence = 1.0,
            Issues = new List<string>(),
            VerifiedAt = DateTime.UtcNow
        });
    }

    public LightningRMPTConfig GetRmptConfig()
    {
        return new LightningRMPTConfig
        {
            Enabled = true,
            Strategy = OptimizationStrategy.Balanced
        };
    }

    public Polly.CircuitBreaker.CircuitState GetCircuitState()
    {
        return Polly.CircuitBreaker.CircuitState.Closed;
    }
}

/// <summary>
/// Mock implementation of ILightningRLCSService for testing
/// Returns simple mock responses without requiring Lightning server
/// </summary>
public class MockLightningRLCSService : ILightningRLCSService
{
    public Task<ReasoningChainValidation> ValidateReasoningChainAsync(List<ReasoningStep> steps)
    {
        return Task.FromResult(new ReasoningChainValidation
        {
            IsValid = true,
            Score = 0.95,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            ValidatedAt = DateTime.UtcNow
        });
    }

    public Task<ConfidenceScore> EvaluateConfidenceAsync(string content, string context)
    {
        return Task.FromResult(new ConfidenceScore
        {
            Score = 0.85,
            Factors = new Dictionary<string, double>(),
            Reasoning = "Mock confidence evaluation"
        });
    }

    public Task<FactCheckResult> VerifyFactsAsync(List<string> facts, string source)
    {
        return Task.FromResult(new FactCheckResult
        {
            VerifiedCount = facts.Count,
            TotalCount = facts.Count,
            UnreliableFacts = new List<string>(),
            VerificationScore = 0.9
        });
    }

    public Task<ConsistencyCheckResult> CheckConsistencyAsync(List<string> statements)
    {
        return Task.FromResult(new ConsistencyCheckResult
        {
            IsConsistent = true,
            Score = 0.9,
            Contradictions = new List<string>()
        });
    }
}
