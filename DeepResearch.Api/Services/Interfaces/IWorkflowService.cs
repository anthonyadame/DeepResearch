namespace DeepResearch.Api.Services;

using DTOs.Common;
using DTOs.Requests.Workflows;
using DTOs.Responses.Workflows;
using DTOs.Requests.Agents;
using DTOs.Responses.Agents;
using DTOs.Requests.Services;
using DTOs.Responses.Services;

/// <summary>
/// Orchestration service for workflow operations.
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Execute MasterWorkflow - Complete 5-step research pipeline.
    /// </summary>
    Task<ApiResponse<MasterWorkflowResponse>> ExecuteMasterWorkflowAsync(
        MasterWorkflowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute SupervisorWorkflow - Iterative refinement loop.
    /// </summary>
    Task<ApiResponse<SupervisorWorkflowResponse>> ExecuteSupervisorWorkflowAsync(
        SupervisorWorkflowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute ResearcherWorkflow - Focused research phase.
    /// </summary>
    Task<ApiResponse<ResearcherWorkflowResponse>> ExecuteResearcherWorkflowAsync(
        ResearcherWorkflowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get async operation status.
    /// </summary>
    Task<ApiResponse<AsyncOperationResponse>> GetAsyncStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get async operation results.
    /// </summary>
    Task<ApiResponse<object>> GetAsyncResultsAsync(
        string jobId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestration service for agent operations.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Execute ClarifyAgent - Validate query clarity.
    /// </summary>
    Task<ApiResponse<ClarifyAgentResponse>> ExecuteClarifyAgentAsync(
        ClarifyAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute ResearchBriefAgent - Generate research brief.
    /// </summary>
    Task<ApiResponse<ResearchBriefAgentResponse>> ExecuteResearchBriefAgentAsync(
        ResearchBriefAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute DraftReportAgent - Generate draft report.
    /// </summary>
    Task<ApiResponse<DraftReportAgentResponse>> ExecuteDraftReportAgentAsync(
        DraftReportAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute ResearcherAgent - Conduct research.
    /// </summary>
    Task<ApiResponse<ResearcherAgentResponse>> ExecuteResearcherAgentAsync(
        ResearcherAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute AnalystAgent - Analyze findings.
    /// </summary>
    Task<ApiResponse<AnalystAgentResponse>> ExecuteAnalystAgentAsync(
        AnalystAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute ReportAgent - Generate final report.
    /// </summary>
    Task<ApiResponse<ReportAgentResponse>> ExecuteReportAgentAsync(
        ReportAgentRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestration service for core infrastructure operations.
/// </summary>
public interface ICoreService
{
    /// <summary>
    /// Invoke LLM with messages.
    /// </summary>
    Task<ApiResponse<LlmInvokeResponse>> InvokeLlmAsync(
        LlmInvokeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform web search.
    /// </summary>
    Task<ApiResponse<SearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrape web page.
    /// </summary>
    Task<ApiResponse<ScrapeResponse>> ScrapeAsync(
        ScrapeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manage agent state.
    /// </summary>
    Task<ApiResponse<StateManagementResponse>> ManageStateAsync(
        StateManagementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query agent state.
    /// </summary>
    Task<ApiResponse<StateQueryResponse>> QueryStateAsync(
        StateQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform store operation.
    /// </summary>
    Task<ApiResponse<StoreOperationResponse>> StoreOperationAsync(
        StoreOperationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search vectors.
    /// </summary>
    Task<ApiResponse<VectorSearchResponse>> SearchVectorsAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add vector to database.
    /// </summary>
    Task<ApiResponse<VectorAddResponse>> AddVectorAsync(
        VectorAddRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoke research tool.
    /// </summary>
    Task<ApiResponse<ToolInvocationResponse>> InvokeToolAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query metrics.
    /// </summary>
    Task<ApiResponse<MetricsResponse>> GetMetricsAsync(
        MetricsRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check service.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Check API health.
    /// </summary>
    Task<ApiResponse<HealthStatusDto>> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check individual service health.
    /// </summary>
    Task<ApiResponse<ServiceHealthDto>> CheckServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status DTO.
/// </summary>
public class HealthStatusDto
{
    public string Status { get; set; } = "Healthy";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Services { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Service health DTO.
/// </summary>
public class ServiceHealthDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = "Healthy";
    public long ResponseTimeMs { get; set; }
    public string? Details { get; set; }
}
