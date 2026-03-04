namespace DeepResearch.Api.DTOs.Validators;

using FluentValidation;
using DeepResearch.Api.DTOs.Requests.Workflows;
using DeepResearch.Api.DTOs.Requests.Agents;
using DeepResearch.Api.DTOs.Requests.Services;

/// <summary>
/// Validator for MasterWorkflowRequest
/// </summary>
public class MasterWorkflowRequestValidator : AbstractValidator<MasterWorkflowRequest>
{
    public MasterWorkflowRequestValidator()
    {
        RuleFor(x => x.UserQuery)
            .NotEmpty().WithMessage("User query is required")
            .MinimumLength(5).WithMessage("User query must be at least 5 characters")
            .MaximumLength(5000).WithMessage("User query must not exceed 5000 characters");

        RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0).When(x => x.TimeoutSeconds.HasValue)
            .WithMessage("Timeout must be greater than 0");
    }
}

/// <summary>
/// Validator for SupervisorWorkflowRequest
/// </summary>
public class SupervisorWorkflowRequestValidator : AbstractValidator<SupervisorWorkflowRequest>
{
    public SupervisorWorkflowRequestValidator()
    {
        RuleFor(x => x.ResearchBrief)
            .NotEmpty().WithMessage("Research brief is required")
            .MinimumLength(10).WithMessage("Research brief must be at least 10 characters");

        RuleFor(x => x.MaxIterations)
            .GreaterThan(0).WithMessage("Max iterations must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Max iterations must not exceed 100");

        RuleFor(x => x.TargetQualityScore)
            .GreaterThanOrEqualTo(0).When(x => x.TargetQualityScore.HasValue)
            .LessThanOrEqualTo(1).When(x => x.TargetQualityScore.HasValue)
            .WithMessage("Target quality score must be between 0 and 1");
    }
}

/// <summary>
/// Validator for ResearcherWorkflowRequest
/// </summary>
public class ResearcherWorkflowRequestValidator : AbstractValidator<ResearcherWorkflowRequest>
{
    public ResearcherWorkflowRequestValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Topic is required")
            .MinimumLength(3).WithMessage("Topic must be at least 3 characters")
            .MaximumLength(500).WithMessage("Topic must not exceed 500 characters");
    }
}

/// <summary>
/// Validator for ClarifyAgentRequest
/// </summary>
public class ClarifyAgentRequestValidator : AbstractValidator<ClarifyAgentRequest>
{
    public ClarifyAgentRequestValidator()
    {
        RuleFor(x => x.ConversationHistory)
            .NotEmpty().WithMessage("Conversation history is required")
            .Must(x => x.Count > 0).WithMessage("Conversation history must contain at least one message");

        RuleForEach(x => x.ConversationHistory)
            .Must(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("Each message must have a role")
            .Must(x => !string.IsNullOrWhiteSpace(x.Content))
            .WithMessage("Each message must have content");
    }
}

/// <summary>
/// Validator for ResearchBriefAgentRequest
/// </summary>
public class ResearchBriefAgentRequestValidator : AbstractValidator<ResearchBriefAgentRequest>
{
    public ResearchBriefAgentRequestValidator()
    {
        RuleFor(x => x.UserQuery)
            .NotEmpty().WithMessage("User query is required")
            .MinimumLength(5).WithMessage("User query must be at least 5 characters")
            .MaximumLength(2000).WithMessage("User query must not exceed 2000 characters");
    }
}

/// <summary>
/// Validator for DraftReportAgentRequest
/// </summary>
public class DraftReportAgentRequestValidator : AbstractValidator<DraftReportAgentRequest>
{
    public DraftReportAgentRequestValidator()
    {
        RuleFor(x => x.ResearchBrief)
            .NotEmpty().WithMessage("Research brief is required")
            .MinimumLength(10).WithMessage("Research brief must be at least 10 characters");
    }
}

/// <summary>
/// Validator for ResearcherAgentRequest
/// </summary>
public class ResearcherAgentRequestValidator : AbstractValidator<ResearcherAgentRequest>
{
    public ResearcherAgentRequestValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Topic is required")
            .MinimumLength(3).WithMessage("Topic must be at least 3 characters");

        RuleFor(x => x.MaxIterations)
            .GreaterThan(0).WithMessage("Max iterations must be greater than 0")
            .LessThanOrEqualTo(10).WithMessage("Max iterations must not exceed 10");
    }
}

/// <summary>
/// Validator for AnalystAgentRequest
/// </summary>
public class AnalystAgentRequestValidator : AbstractValidator<AnalystAgentRequest>
{
    public AnalystAgentRequestValidator()
    {
        RuleFor(x => x.Findings)
            .NotEmpty().WithMessage("Findings are required")
            .Must(x => x.Count > 0).WithMessage("Must provide at least one finding");
    }
}

/// <summary>
/// Validator for ReportAgentRequest
/// </summary>
public class ReportAgentRequestValidator : AbstractValidator<ReportAgentRequest>
{
    public ReportAgentRequestValidator()
    {
        RuleFor(x => x.ResearchContent)
            .NotEmpty().WithMessage("Research content is required");

        RuleFor(x => x.AnalysisContent)
            .NotEmpty().WithMessage("Analysis content is required");
    }
}

/// <summary>
/// Validator for SearchRequest
/// </summary>
public class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required")
            .MinimumLength(2).WithMessage("Search query must be at least 2 characters")
            .MaximumLength(500).WithMessage("Search query must not exceed 500 characters");

        RuleFor(x => x.MaxResults)
            .GreaterThan(0).WithMessage("Max results must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Max results must not exceed 100");
    }
}

/// <summary>
/// Validator for ScrapeRequest
/// </summary>
public class ScrapeRequestValidator : AbstractValidator<ScrapeRequest>
{
    public ScrapeRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .Must(BeValidUrl).WithMessage("URL must be a valid web address");
    }

    private bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
               (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Validator for LlmInvokeRequest
/// </summary>
public class LlmInvokeRequestValidator : AbstractValidator<LlmInvokeRequest>
{
    public LlmInvokeRequestValidator()
    {
        RuleFor(x => x.Messages)
            .NotEmpty().WithMessage("Messages are required")
            .Must(x => x.Count > 0).WithMessage("Must provide at least one message");

        RuleForEach(x => x.Messages)
            .Must(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("Each message must have a role")
            .Must(x => !string.IsNullOrWhiteSpace(x.Content))
            .WithMessage("Each message must have content");

        RuleFor(x => x.Temperature)
            .GreaterThanOrEqualTo(0).When(x => x.Temperature.HasValue)
            .LessThanOrEqualTo(1).When(x => x.Temperature.HasValue)
            .WithMessage("Temperature must be between 0 and 1");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).When(x => x.MaxTokens.HasValue)
            .LessThanOrEqualTo(4096).When(x => x.MaxTokens.HasValue)
            .WithMessage("Max tokens must be between 1 and 4096");
    }
}

/// <summary>
/// Validator for ToolInvocationRequest
/// </summary>
public class ToolInvocationRequestValidator : AbstractValidator<ToolInvocationRequest>
{
    public ToolInvocationRequestValidator()
    {
        RuleFor(x => x.ToolName)
            .NotEmpty().WithMessage("Tool name is required")
            .Must(BeValidToolName).WithMessage("Invalid tool name");
    }

    private bool BeValidToolName(string toolName)
    {
        var validTools = new[] { "ConductResearch", "ResearchComplete", "ThinkTool", "RefineDraftReport" };
        return validTools.Contains(toolName);
    }
}

/// <summary>
/// Validator for StateManagementRequest
/// </summary>
public class StateManagementRequestValidator : AbstractValidator<StateManagementRequest>
{
    public StateManagementRequestValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required");

        RuleFor(x => x.StateData)
            .NotEmpty().WithMessage("State data is required");

        RuleFor(x => x.TimeToLiveSeconds)
            .GreaterThan(0).When(x => x.TimeToLiveSeconds.HasValue)
            .WithMessage("Time to live must be greater than 0");
    }
}

/// <summary>
/// Validator for VectorSearchRequest
/// </summary>
public class VectorSearchRequestValidator : AbstractValidator<VectorSearchRequest>
{
    public VectorSearchRequestValidator()
    {
        RuleFor(x => x.QueryVector)
            .NotEmpty().WithMessage("Query vector is required")
            .Must(x => x.Count > 0).WithMessage("Query vector must not be empty");

        RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("Limit must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Limit must not exceed 1000");

        RuleFor(x => x.SimilarityThreshold)
            .GreaterThanOrEqualTo(0).When(x => x.SimilarityThreshold.HasValue)
            .LessThanOrEqualTo(1).When(x => x.SimilarityThreshold.HasValue)
            .WithMessage("Similarity threshold must be between 0 and 1");
    }
}
