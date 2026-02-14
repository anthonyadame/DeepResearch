using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services;

/// <summary>
/// VERL (Verification and Reasoning Layer) Service for Agent-Lightning.
/// Handles verification of agent outputs, reasoning chain validation, and confidence scoring.
/// </summary>
public interface ILightningVERLService
{
    Task<ReasoningChainValidation> ValidateReasoningChainAsync(List<ReasoningStep> steps);
    Task<ConfidenceScore> EvaluateConfidenceAsync(string content, string context);
    Task<FactCheckResult> VerifyFactsAsync(List<string> facts, string source);
    Task<ConsistencyCheckResult> CheckConsistencyAsync(List<string> statements);
}

public class LightningVERLService : ILightningVERLService
{
    private readonly HttpClient _httpClient;
    private readonly string _lightningServerUrl;

    public LightningVERLService(HttpClient httpClient, string lightningServerUrl = "http://localhostr:8090")
    {
        _httpClient = httpClient;
        _lightningServerUrl = lightningServerUrl;
    }

    public async Task<ReasoningChainValidation> ValidateReasoningChainAsync(List<ReasoningStep> steps)
    {
        try
        {
            var request = new { steps, validatedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/verl/validate-reasoning",
                request
            );
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            var result = System.Text.Json.JsonSerializer.Deserialize<ReasoningChainValidation>(content);
            return result ?? new ReasoningChainValidation { IsValid = true, Score = 1.0 };
        }
        catch (Exception ex)
        {
            return new ReasoningChainValidation
            {
                IsValid = false,
                Score = 0,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ConfidenceScore> EvaluateConfidenceAsync(string content, string context)
    {
        try
        {
            var request = new { content, context, evaluatedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/verl/evaluate-confidence",
                request
            );
            
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            
            var result = System.Text.Json.JsonSerializer.Deserialize<ConfidenceScore>(responseContent);
            return result ?? new ConfidenceScore { Score = 0.5 };
        }
        catch
        {
            return new ConfidenceScore { Score = 0.5 };
        }
    }

    public async Task<FactCheckResult> VerifyFactsAsync(List<string> facts, string source)
    {
        try
        {
            var request = new { facts, source, checkedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/verl/verify-facts",
                request
            );
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            var result = System.Text.Json.JsonSerializer.Deserialize<FactCheckResult>(content);
            return result ?? new FactCheckResult { VerifiedCount = 0, TotalCount = facts.Count };
        }
        catch
        {
            return new FactCheckResult { VerifiedCount = 0, TotalCount = facts.Count };
        }
    }

    public async Task<ConsistencyCheckResult> CheckConsistencyAsync(List<string> statements)
    {
        try
        {
            var request = new { statements, checkedAt = DateTime.UtcNow };
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_lightningServerUrl}/api/verl/check-consistency",
                request
            );
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            var result = System.Text.Json.JsonSerializer.Deserialize<ConsistencyCheckResult>(content);
            return result ?? new ConsistencyCheckResult { IsConsistent = true, Score = 1.0 };
        }
        catch
        {
            return new ConsistencyCheckResult { IsConsistent = false, Score = 0 };
        }
    }
}

// VERL Models
public class ReasoningStep
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("logic")]
    public string Logic { get; set; } = string.Empty;

    [JsonPropertyName("conclusions")]
    public List<string> Conclusions { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class ReasoningChainValidation
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("validatedAt")]
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class ConfidenceScore
{
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("factors")]
    public Dictionary<string, double> Factors { get; set; } = new();

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;
}

public class FactCheckResult
{
    [JsonPropertyName("verifiedCount")]
    public int VerifiedCount { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("unreliableFacts")]
    public List<string> UnreliableFacts { get; set; } = new();

    [JsonPropertyName("verificationScore")]
    public double VerificationScore { get; set; }
}

public class ConsistencyCheckResult
{
    [JsonPropertyName("isConsistent")]
    public bool IsConsistent { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("contradictions")]
    public List<string> Contradictions { get; set; } = new();
}