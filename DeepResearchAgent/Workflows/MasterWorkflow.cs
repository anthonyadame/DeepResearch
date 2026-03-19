using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Caching;
using DeepResearchAgent.Services.LLM;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Observability;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace DeepResearchAgent.Workflows;

/// <summary>
/// Master Workflow: Orchestrates the entire Deep Research Agent pipeline.
/// 
/// 5-Step Process:
/// 1. ClarifyWithUser - Ensure user query is sufficiently detailed
/// 2. WriteResearchBrief - Transform query into structured research brief
/// 3. WriteDraftReport - Generate initial "noisy" draft (diffusion starting point)
/// 4. ExecuteSupervisor - Hand off to supervisor for iterative refinement
/// 5. GenerateFinalReport - Polish and synthesize findings into final report
/// 
/// Phase 5 Enhancement: Add complex agents for research → analysis → report pipeline
/// 
/// Maps to Python lines 870-920 (scoping) and 2119-2140 (full integration)
/// </summary>
public class MasterWorkflow
{
    private readonly ILightningStateService _stateService;
    private readonly SupervisorWorkflow _supervisor;
    private readonly ILlmProvider _llmService;
    private readonly ILogger<MasterWorkflow>? _logger;
    private readonly StateManager? _stateManager;
    private readonly IWebSearchProvider _searchProvider;
    private readonly AsyncMetricsCollector? _asyncMetricsCollector;
    private readonly LlmResponseCache? _llmCache;

    // Phase 2 Agents
    private readonly ClarifyAgent _clarifyAgent;
    private readonly ResearchBriefAgent _briefAgent;
    private readonly DraftReportAgent _draftAgent;

    // Phase 4 Complex Agents
    private readonly ResearcherAgent _researcherAgent;
    private readonly AnalystAgent _analystAgent;
    private readonly ReportAgent _reportAgent;

    public MasterWorkflow(
        ILightningStateService stateService,
        SupervisorWorkflow supervisor,
        ILlmProvider llmService,
        IWebSearchProvider searchProvider,
        ILogger<MasterWorkflow>? logger = null,
        StateManager? stateManager = null,
        ResearcherAgent? researcherAgent = null,
        AnalystAgent? analystAgent = null,
        ReportAgent? reportAgent = null,
        AsyncMetricsCollector? asyncMetricsCollector = null,
        LlmResponseCache? llmCache = null)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _logger = logger;
        _stateManager = stateManager;
        _asyncMetricsCollector = asyncMetricsCollector;
        _llmCache = llmCache;

        // Initialize Phase 2 agents with cache
        _clarifyAgent = new ClarifyAgent(_llmService, null, llmCache);
        _briefAgent = new ResearchBriefAgent(_llmService, null, llmCache);
        _draftAgent = new DraftReportAgent(_llmService, null, llmCache);

        // Initialize Phase 4 complex agents
        _researcherAgent = researcherAgent ?? new ResearcherAgent(_llmService, new ToolInvocationService(_searchProvider, _llmService, null, llmCache), null);
        _analystAgent = analystAgent ?? new AnalystAgent(_llmService, new ToolInvocationService(_searchProvider, _llmService, null, llmCache), null);
        _reportAgent = reportAgent ?? new ReportAgent(_llmService, new ToolInvocationService(_searchProvider, _llmService, null, llmCache), null);
    }

    /// <summary>
    /// Helper method to record metrics using async collector if available, otherwise synchronous
    /// </summary>
    private void RecordStepDuration(double durationMs, string workflow, string step)
    {
        if (_asyncMetricsCollector != null)
        {
            _asyncMetricsCollector.RecordHistogram(
                DiagnosticConfig.WorkflowStepDuration,
                durationMs,
                new KeyValuePair<string, object?>("workflow", workflow),
                new KeyValuePair<string, object?>("step", step));
        }
        else
        {
            DiagnosticConfig.WorkflowStepDuration.Record(durationMs,
                new KeyValuePair<string, object?>("workflow", workflow),
                new KeyValuePair<string, object?>("step", step));
        }
    }

    /// <summary>
    /// Helper method to increment step counter using async collector if available
    /// </summary>
    private void RecordStepComplete(string workflow, string step, string status)
    {
        if (_asyncMetricsCollector != null)
        {
            _asyncMetricsCollector.RecordCounter(
                DiagnosticConfig.WorkflowStepsCounter,
                1L,
                new KeyValuePair<string, object?>("workflow", workflow),
                new KeyValuePair<string, object?>("step", step),
                new KeyValuePair<string, object?>("status", status));
        }
        else
        {
            DiagnosticConfig.WorkflowStepsCounter.Add(1,
                new KeyValuePair<string, object?>("workflow", workflow),
                new KeyValuePair<string, object?>("step", step),
                new KeyValuePair<string, object?>("status", status));
        }
    }

    /// <summary>
    /// Execute the complete master workflow.
    /// Entry point for the entire research pipeline.
    /// </summary>
    public async Task<string> RunAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        var researchId = Guid.NewGuid().ToString();
        _logger?.LogInformation("Starting MasterWorkflow with research ID: {researchId}", researchId);

        DiagnosticConfig.IncrementActiveWorkflows();
        try
        {
            // Initialize research state
            var researchState = new ResearchStateModel
            {
                ResearchId = researchId,
                Query = userQuery,
                Status = ResearchStatus.Pending,
                StartedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { { "phase", "initialization" } }
            };
            
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            _logger?.LogInformation("Research {ResearchId} initialized", researchId);

            // Step 1: Clarify with user (check if query is detailed enough)
            _logger?.LogInformation("Step 1: Clarifying user intent");
            researchState.Status = ResearchStatus.InProgress;
            researchState.Metadata["phase"] = "clarification";
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            
            var (needsClarification, clarificationQuestion) = await ClarifyWithUserAsync(userQuery, cancellationToken);
            
            if (needsClarification)
            {
                _logger?.LogInformation("User clarification needed");
                researchState.Status = ResearchStatus.Failed;
                researchState.Metadata["failure_reason"] = "Clarification required";
                await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
                return $"Clarification needed:\n\n{clarificationQuestion}";
            }

            // Step 2: Write research brief
            _logger?.LogInformation("Step 2: Writing research brief");
            researchState.Metadata["phase"] = "brief_writing";
            researchState.IterationCount = 1;
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            
            var researchBrief = await WriteResearchBriefAsync(userQuery, cancellationToken);
            researchState.Metadata["researchBrief"] = researchBrief;

            // Step 3: Write initial draft
            _logger?.LogInformation("Step 3: Generating initial draft report");
            researchState.Metadata["phase"] = "draft_writing";
            researchState.IterationCount = 2;
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            
            var draftReport = await WriteDraftReportAsync(researchBrief, cancellationToken);
            researchState.Metadata["draftReport"] = draftReport;

            // Step 4: Execute supervisor loop
            _logger?.LogInformation("Step 4: Executing supervisor loop (diffusion process)");
            researchState.Status = ResearchStatus.Verifying;
            researchState.Metadata["phase"] = "supervision";
            researchState.IterationCount = 3;
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            
            var refinedSummary = await _supervisor.SuperviseAsync(researchBrief, draftReport, cancellationToken: cancellationToken);
            researchState.Metadata["refinedSummary"] = refinedSummary;

            // Step 5: Generate final report
            _logger?.LogInformation("Step 5: Generating final polished report");
            researchState.Metadata["phase"] = "final_report";
            researchState.IterationCount = 4;
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);
            
            var finalReport = await GenerateFinalReportAsync(userQuery, researchBrief, draftReport, refinedSummary, cancellationToken);
            researchState.Metadata["finalReport"] = finalReport;

            // Mark completed
            researchState.Status = ResearchStatus.Completed;
            researchState.CompletedAt = DateTime.UtcNow;
            researchState.IterationCount = 5;
            await _stateService.SetResearchStateAsync(researchId, researchState, cancellationToken);

            _logger?.LogInformation("MasterWorkflow {ResearchId} completed successfully", researchId);
            var metrics = _stateService.GetMetrics();
            _logger?.LogInformation("State service metrics - Cache hit rate: {HitRate:P}", metrics.CacheHitRate);
            
            return finalReport;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MasterWorkflow {ResearchId} failed", researchId);
            try
            {
                var failedState = await _stateService.GetResearchStateAsync(researchId, cancellationToken);
                failedState.Status = ResearchStatus.Failed;
                failedState.Metadata["error"] = ex.Message;
                await _stateService.SetResearchStateAsync(researchId, failedState, cancellationToken);
            }
            catch (Exception stateEx)
            {
                _logger?.LogWarning(stateEx, "Failed to update error state for {ResearchId}", researchId);
            }
            throw;
        }
        finally
        {
            DiagnosticConfig.DecrementActiveWorkflows();
        }
    }

    /// <summary>
    /// Execute the complete master workflow with AgentState input/output.
    /// Overload for test compatibility.
    /// </summary>
    public async Task<AgentState> ExecuteAsync(AgentState input, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting MasterWorkflow.ExecuteAsync with AgentState");

        DiagnosticConfig.IncrementActiveWorkflows();
        try
        {
            // Extract query from initial message
            var userQuery = input.Messages?.FirstOrDefault()?.Content ?? "Conduct research";
            
            // Step 1: Clarify with user
            _logger?.LogInformation("Step 1: Clarifying user intent");
            var (needsClarification, clarificationQuestion) = await ClarifyWithUserAsync(userQuery, cancellationToken);
            
            if (needsClarification)
            {
                _logger?.LogInformation("User clarification needed");
                input.ResearchBrief = $"Clarification needed: {clarificationQuestion}";
                return input;
            }

            // Step 2: Write research brief
            _logger?.LogInformation("Step 2: Writing research brief");
            var researchBrief = await WriteResearchBriefAsync(userQuery, cancellationToken);
            input.ResearchBrief = researchBrief;

            // Step 3: Write initial draft
            _logger?.LogInformation("Step 3: Generating initial draft report");
            var draftReport = await WriteDraftReportAsync(researchBrief, cancellationToken);
            input.DraftReport = draftReport;

            // Step 4: Execute supervisor loop
            _logger?.LogInformation("Step 4: Executing supervisor loop (diffusion process)");
            var supervisorState = StateFactory.CreateSupervisorState(researchBrief, draftReport, input.SupervisorMessages);
            var refinedState = await _supervisor.ExecuteAsync(supervisorState, cancellationToken);
            input.SupervisorMessages = refinedState.SupervisorMessages;
            input.RawNotes = refinedState.RawNotes;

            // Step 5: Generate final report
            _logger?.LogInformation("Step 5: Generating final polished report");
            var refinedSummary = refinedState.DraftReport;
            var finalReport = await GenerateFinalReportAsync(userQuery, researchBrief, draftReport, refinedSummary, cancellationToken);
            input.FinalReport = finalReport;

            _logger?.LogInformation("MasterWorkflow.ExecuteAsync completed successfully");
            return input;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MasterWorkflow.ExecuteAsync failed");
            throw;
        }
        finally
        {
            DiagnosticConfig.DecrementActiveWorkflows();
        }
    }


    public async Task<AgentState> ExecuteByStepAsync(AgentState input, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting MasterWorkflow.ExecuteByStepAsync with AgentState");

        try
        {
            // Extract query from initial message
            var userQuery = input.Messages?.FirstOrDefault()?.Content ?? "Conduct research";

            // Step 1: Clarify with user (only if NeedsQualityRepair flag is set)
            if (input.NeedsQualityRepair)
            {
                _logger?.LogInformation("Step 1: Clarifying user intent");
                var (needsClarification, clarificationQuestion) = await ClarifyWithUserAsync(userQuery, cancellationToken);

                if (needsClarification)
                {
                    _logger?.LogInformation("User clarification needed - returning with question");
                    input.ResearchBrief = $"Clarification needed: {clarificationQuestion}";
                    input.NeedsQualityRepair = true; // Keep flag set until clarification resolved
                    return input;
                }

                // Query is sufficiently detailed - clear the repair flag and continue
                _logger?.LogInformation("Query clarified - proceeding to step 2");
                input.NeedsQualityRepair = false;
            }

            // Step 2: Write research brief (only if not already generated)
            if (string.IsNullOrEmpty(input.ResearchBrief))
            {
                _logger?.LogInformation("Step 2: Writing research brief");
                var researchBrief = await WriteResearchBriefAsync(userQuery, cancellationToken);
                input.ResearchBrief = researchBrief;
                _logger?.LogInformation("Research brief generated: {Length} chars", researchBrief.Length);
                return input; // Return after each step for UI to display progress
            }

            // Step 3: Write initial draft (only if ResearchBrief exists and DraftReport doesn't)
            if (string.IsNullOrEmpty(input.DraftReport) && !string.IsNullOrEmpty(input.ResearchBrief))
            {
                _logger?.LogInformation("Step 3: Generating initial draft report");
                var draftReport = await WriteDraftReportAsync(input.ResearchBrief, cancellationToken);
                input.DraftReport = draftReport;
                _logger?.LogInformation("Draft report generated: {Length} chars", draftReport.Length);
                return input;
            }

            // Step 4: Execute supervisor loop (only if we have brief and draft, but no supervisor messages yet)
            if (!input.SupervisorMessages.Any() && !string.IsNullOrEmpty(input.ResearchBrief) && !string.IsNullOrEmpty(input.DraftReport))
            {
                _logger?.LogInformation("Step 4: Executing supervisor loop (diffusion process)");
                input.SupervisorState = StateFactory.CreateSupervisorState(input.ResearchBrief, input.DraftReport, input.SupervisorMessages);
                
                // Execute supervisor refinement
                input.SupervisorState = await _supervisor.ExecuteAsync(input.SupervisorState, cancellationToken);
                input.SupervisorMessages = input.SupervisorState.SupervisorMessages;
                input.RawNotes = input.SupervisorState.RawNotes;
                
                _logger?.LogInformation("Supervisor loop completed - {NoteCount} raw notes generated", input.RawNotes.Count);
                return input;
            }

            // Step 5: Generate final report (only if we have all previous steps but no final report)
            if (string.IsNullOrEmpty(input.FinalReport) && 
                !string.IsNullOrEmpty(input.ResearchBrief) && 
                !string.IsNullOrEmpty(input.DraftReport) && 
                input.SupervisorMessages.Any())
            {
                _logger?.LogInformation("Step 5: Generating final polished report");
                var refinedSummary = input.SupervisorState?.DraftReport ?? input.DraftReport;
                var finalReport = await GenerateFinalReportAsync(userQuery, input.ResearchBrief, input.DraftReport, refinedSummary, cancellationToken);
                input.FinalReport = finalReport;
                _logger?.LogInformation("Final report generated: {Length} chars", finalReport.Length);
                return input;
            }

            _logger?.LogInformation("MasterWorkflow.ExecuteByStepAsync completed - all steps finished");
            return input;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MasterWorkflow.ExecuteByStepAsync failed");
            throw;
        }
    }

    /// <summary>
    /// Stream real-time updates from master and supervisor workflows.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(string userQuery, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting MasterWorkflow stream");
        yield return Json("status", "connected", "timestamp", DateTime.UtcNow.ToString("O"));

        if (!userQuery.Contains("clarification_provided:"))
        {
            // Step 1: Clarify
            _logger?.LogInformation("Stream: Step 1 - Clarifying");
            yield return Json("step", "1", "status", "clarifying user intent");

            var (needsClarification, clarificationQuestion) = await ClarifyWithUserAsync(userQuery, cancellationToken);

            if (needsClarification)
            {
                _logger?.LogInformation("Stream: Clarification needed");
                yield return Json("step", "1", "status", "clarification_needed", "message", clarificationQuestion);
                yield break;
            }
        }
        _logger?.LogInformation("Stream: Query clarified");
        yield return Json("step", "1", "status", "completed", "message", "query is sufficiently detailed");

        // Step 2: Research brief
        _logger?.LogInformation("Stream: Step 2 - Writing research brief");
        yield return Json("step", "2", "status", "writing research brief");
        
        var researchBrief = await WriteResearchBriefAsync(userQuery, cancellationToken);
        var briefPreview = researchBrief.Substring(0, Math.Min(150, researchBrief.Length)).Replace("\n", " ");
        _logger?.LogInformation("Stream: Research brief completed ({Length} chars)", researchBrief.Length);
        yield return Json("step", "2", "status", "completed", "preview", briefPreview, "length", researchBrief.Length.ToString());

        // Step 3: Initial draft
        _logger?.LogInformation("Stream: Step 3 - Generating initial draft");
        yield return Json("step", "3", "status", "generating initial draft report");
        
        var draftReport = await WriteDraftReportAsync(researchBrief, cancellationToken);
        _logger?.LogInformation("Stream: Draft report completed ({Length} chars)", draftReport.Length);
        yield return Json("step", "3", "status", "completed", "length", draftReport.Length.ToString());

        // Step 4: Supervisor loop (stream its progress)
        _logger?.LogInformation("Stream: Step 4 - Starting supervisor loop");
        yield return Json("step", "4", "status", "starting supervisor loop (diffusion process)");
        
        int supervisorUpdateCount = 0;
        await foreach (var supervisorUpdate in _supervisor.StreamSuperviseAsync(researchBrief, draftReport, cancellationToken: cancellationToken))
        {
            supervisorUpdateCount++;
            _logger?.LogDebug("Stream: Supervisor update #{Count}", supervisorUpdateCount);
            yield return supervisorUpdate;
            
            // Yield heartbeat every 10 updates to keep connection alive
            if (supervisorUpdateCount % 10 == 0)
            {
                yield return Json("heartbeat", "true", "supervisor_updates", supervisorUpdateCount.ToString());
            }
        }
        _logger?.LogInformation("Stream: Supervisor loop completed ({UpdateCount} updates)", supervisorUpdateCount);
        yield return Json("step", "4", "status", "completed", "supervisor_updates", supervisorUpdateCount.ToString());

        // Step 5: Final report
        _logger?.LogInformation("Stream: Step 5 - Generating final report");
        yield return Json("step", "5", "status", "generating final polished report");
        
        var refinedSummary = await _supervisor.SuperviseAsync(researchBrief, draftReport, cancellationToken: cancellationToken);
        var finalReport = await GenerateFinalReportAsync(userQuery, researchBrief, draftReport, refinedSummary, cancellationToken);
        _logger?.LogInformation("Stream: Final report completed ({Length} chars)", finalReport.Length);
        yield return Json("step", "5", "status", "completed", "length", finalReport.Length.ToString());

        _logger?.LogInformation("Stream: Workflow complete");
        yield return Json("status", "completed", "totalSteps", "5");
    }


    public async IAsyncEnumerable<StreamState> StreamStateAsync(string userQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Start workflow-level tracing and metrics
        using var workflowActivity = ActivityScope.Start("MasterWorkflow.StreamStateAsync", ActivityKind.Server);
        using var workflowMetrics = MetricsCollector.TrackExecution("StreamStateAsync", workflow: "MasterWorkflow");
        var workflowStopwatch = Stopwatch.StartNew();

        workflowActivity
            .AddTag("workflow.name", "MasterWorkflow")
            .AddTag("query.length", userQuery.Length)
            .AddTag("query.preview", userQuery.Substring(0, Math.Min(100, userQuery.Length)));

        _logger?.LogInformation("Starting MasterWorkflow stream");
        yield return new StreamState() 
        {
            Status = Json("status", "connected", "timestamp", DateTime.UtcNow.ToString("O")) 
        };

        workflowActivity.AddEvent("workflow_connected");

        // Step 1: Clarify
        if (!userQuery.Contains("clarification_provided:"))
        {
            using var step1Activity = ActivityScope.Start("Step1.Clarify", ActivityKind.Internal);
            using var step1Metrics = MetricsCollector.TrackExecution("ClarifyWithUserAsync", 
                workflow: "MasterWorkflow", step: "Step1");
            var step1Stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Stream: Step 1 - Clarifying");
            yield return new StreamState()
            {
                Status = Json("step", "1", "status", "clarifying user intent") 
            };

            step1Activity.AddTag("step.number", 1).AddTag("step.name", "Clarify");

            (bool needsClarification, string clarificationQuestion) clarifyResult = (false, "");
            Exception? step1Exception = null;

            try
            {
                clarifyResult = await ClarifyWithUserAsync(userQuery, cancellationToken);
            }
            catch (Exception ex)
            {
                step1Exception = ex;
                _logger?.LogError(ex, "Step 1 failed");
            }

            step1Stopwatch.Stop();
            RecordStepDuration(step1Stopwatch.Elapsed.TotalMilliseconds, "MasterWorkflow", "1_clarify");
            RecordStepComplete("MasterWorkflow", "1_clarify", step1Exception == null ? "completed" : "failed");

            step1Activity.AddTag("step.duration.ms", step1Stopwatch.Elapsed.TotalMilliseconds);

            if (step1Exception != null)
            {
                DiagnosticConfig.WorkflowErrors.Add(1,
                    new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                    new KeyValuePair<string, object?>("error.type", step1Exception.GetType().Name));
                step1Activity.RecordException(step1Exception).SetStatus(ActivityStatusCode.Error);
                yield return new StreamState { Status = Json("status", "error", "message", step1Exception.Message, "step", "1") };
                yield break;
            }

            if (clarifyResult.needsClarification)
            {
                _logger?.LogInformation("Stream: Clarification needed");
                step1Activity.AddTag("needs_clarification", true);
                yield return new StreamState
                {
                    Status = Json("step", "1", "status", "clarification_needed", "message", clarifyResult.clarificationQuestion)
                };
                workflowActivity.SetStatus(ActivityStatusCode.Ok, "Clarification needed");
                yield break;
            }

            step1Activity.AddTag("needs_clarification", false).SetStatus(ActivityStatusCode.Ok);
        }

        _logger?.LogInformation("Stream: Query clarified");
        yield return new StreamState
        {
            Status = Json("step", "1", "status", "completed", "message", "query is sufficiently detailed")
        };

        workflowActivity.AddEvent("step1_completed");

        // Step 2: Research brief
        string researchBrief = "";
        using (var step2Activity = ActivityScope.Start("Step2.ResearchBrief", ActivityKind.Internal))
        using (var step2Metrics = MetricsCollector.TrackExecution("WriteResearchBriefAsync",
                workflow: "MasterWorkflow", step: "Step2"))
        {
            var step2Stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Stream: Step 2 - Writing research brief");
            yield return new StreamState
            {
                Status = Json("step", "2", "status", "writing research brief")
            };

            step2Activity.AddTag("step.number", 2).AddTag("step.name", "ResearchBrief");

            Exception? step2Exception = null;
            try
            {
                researchBrief = await WriteResearchBriefAsync(userQuery, cancellationToken);
            }
            catch (Exception ex)
            {
                step2Exception = ex;
                _logger?.LogError(ex, "Step 2 failed");
            }

            step2Stopwatch.Stop();
            RecordStepDuration(step2Stopwatch.Elapsed.TotalMilliseconds, "MasterWorkflow", "2_research_brief");
            RecordStepComplete("MasterWorkflow", "2_research_brief", step2Exception == null ? "completed" : "failed");

            if (step2Exception != null)
            {
                DiagnosticConfig.WorkflowErrors.Add(1,
                    new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                    new KeyValuePair<string, object?>("error.type", step2Exception.GetType().Name));
                step2Activity.RecordException(step2Exception).SetStatus(ActivityStatusCode.Error);
                yield return new StreamState { Status = Json("status", "error", "message", step2Exception.Message, "step", "2") };
                yield break;
            }

            var briefPreview = researchBrief.Substring(0, Math.Min(150, researchBrief.Length)).Replace("\n", " ");
            _logger?.LogInformation("Stream: Research brief completed ({Length} chars)", researchBrief.Length);

            step2Activity
                .AddTag("step.duration.ms", step2Stopwatch.Elapsed.TotalMilliseconds)
                .AddTag("brief.length", researchBrief.Length)
                .SetStatus(ActivityStatusCode.Ok);

            yield return new StreamState
            {
                Status = Json("step", "2", "status", "completed", "preview", briefPreview, "length", researchBrief.Length.ToString()),
                ResearchBrief = researchBrief,
                BriefPreview = briefPreview
            };

            workflowActivity.AddEvent("step2_completed", new Dictionary<string, object?>
            {
                ["brief.length"] = researchBrief.Length
            });
        }

        // Step 3: Initial draft
        string draftReport = "";
        using (var step3Activity = ActivityScope.Start("Step3.InitialDraft", ActivityKind.Internal))
        using (var step3Metrics = MetricsCollector.TrackExecution("WriteDraftReportAsync",
                workflow: "MasterWorkflow", step: "Step3"))
        {
            var step3Stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Stream: Step 3 - Generating initial draft");
            yield return new StreamState
            {
                Status = Json("step", "3", "status", "generating initial draft report")
            };

            step3Activity.AddTag("step.number", 3).AddTag("step.name", "InitialDraft");

            Exception? step3Exception = null;
            try
            {
                draftReport = await WriteDraftReportAsync(researchBrief, cancellationToken);
            }
            catch (Exception ex)
            {
                step3Exception = ex;
                _logger?.LogError(ex, "Step 3 failed");
            }

            step3Stopwatch.Stop();
            DiagnosticConfig.WorkflowStepDuration.Record(step3Stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                new KeyValuePair<string, object?>("step", "3_initial_draft"));
            DiagnosticConfig.WorkflowStepsCounter.Add(1,
                new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                new KeyValuePair<string, object?>("step", "3_initial_draft"),
                new KeyValuePair<string, object?>("status", step3Exception == null ? "completed" : "failed"));

            if (step3Exception != null)
            {
                DiagnosticConfig.WorkflowErrors.Add(1,
                    new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                    new KeyValuePair<string, object?>("error.type", step3Exception.GetType().Name));
                step3Activity.RecordException(step3Exception).SetStatus(ActivityStatusCode.Error);
                yield return new StreamState { Status = Json("status", "error", "message", step3Exception.Message, "step", "3") };
                yield break;
            }

            _logger?.LogInformation("Stream: Draft report completed ({Length} chars)", draftReport.Length);

            step3Activity
                .AddTag("step.duration.ms", step3Stopwatch.Elapsed.TotalMilliseconds)
                .AddTag("draft.length", draftReport.Length)
                .SetStatus(ActivityStatusCode.Ok);

            yield return new StreamState
            {
                Status = Json("step", "3", "status", "completed", "length", draftReport.Length.ToString()),
                DraftReport = draftReport
            };

            workflowActivity.AddEvent("step3_completed", new Dictionary<string, object?>
            {
                ["draft.length"] = draftReport.Length
            });
        }

        // Step 4: Supervisor loop (stream its progress)
        using (var step4Activity = ActivityScope.Start("Step4.SupervisorLoop", ActivityKind.Internal))
        using (var step4Metrics = MetricsCollector.TrackExecution("SupervisorLoop",
                workflow: "MasterWorkflow", step: "Step4"))
        {
            var step4Stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Stream: Step 4 - Starting supervisor loop");
            yield return new StreamState
            {
                Status = Json("step", "4", "status", "starting supervisor loop (diffusion process)")
            };

            step4Activity.AddTag("step.number", 4).AddTag("step.name", "SupervisorLoop");

            int supervisorUpdateCount = 0;
            await foreach (var supervisorUpdate in _supervisor.StreamSuperviseAsync(researchBrief, draftReport, cancellationToken: cancellationToken))
            {
                supervisorUpdateCount++;
                _logger?.LogDebug("Stream: Supervisor update #{Count}", supervisorUpdateCount);

                yield return new StreamState
                {
                    SupervisorUpdate = supervisorUpdate
                };

                if (supervisorUpdateCount % 10 == 0)
                {
                    yield return new StreamState
                    {
                        Status = Json("heartbeat", "true", "supervisor_updates", supervisorUpdateCount.ToString())
                    };
                }
            }

            step4Stopwatch.Stop();
            RecordStepDuration(step4Stopwatch.Elapsed.TotalMilliseconds, "MasterWorkflow", "4_supervisor_loop");
            RecordStepComplete("MasterWorkflow", "4_supervisor_loop", "completed");

            _logger?.LogInformation("Stream: Supervisor loop completed ({UpdateCount} updates)", supervisorUpdateCount);

            step4Activity
                .AddTag("step.duration.ms", step4Stopwatch.Elapsed.TotalMilliseconds)
                .AddTag("supervisor.updates", supervisorUpdateCount)
                .SetStatus(ActivityStatusCode.Ok);

            yield return new StreamState
            {
                Status = Json("step", "4", "status", "completed", "supervisor_updates", supervisorUpdateCount.ToString())
            };

            workflowActivity.AddEvent("step4_completed", new Dictionary<string, object?>
            {
                ["supervisor.updates"] = supervisorUpdateCount
            });
        }

        // Step 5: Final report
        using (var step5Activity = ActivityScope.Start("Step5.FinalReport", ActivityKind.Internal))
        using (var step5Metrics = MetricsCollector.TrackExecution("GenerateFinalReportAsync",
                workflow: "MasterWorkflow", step: "Step5"))
        {
            var step5Stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Stream: Step 5 - Generating final report");
            yield return new StreamState
            {
                Status = Json("step", "5", "status", "generating final polished report")
            };

            step5Activity.AddTag("step.number", 5).AddTag("step.name", "FinalReport");

            string refinedSummary = "";
            string finalReport = "";
            Exception? step5Exception = null;

            try
            {
                refinedSummary = await _supervisor.SuperviseAsync(researchBrief, draftReport, cancellationToken: cancellationToken);
                finalReport = await GenerateFinalReportAsync(userQuery, researchBrief, draftReport, refinedSummary, cancellationToken);
            }
            catch (Exception ex)
            {
                step5Exception = ex;
                _logger?.LogError(ex, "Step 5 failed");
            }

            step5Stopwatch.Stop();
            RecordStepDuration(step5Stopwatch.Elapsed.TotalMilliseconds, "MasterWorkflow", "5_final_report");
            RecordStepComplete("MasterWorkflow", "5_final_report", step5Exception == null ? "completed" : "failed");

            if (step5Exception != null)
            {
                DiagnosticConfig.WorkflowErrors.Add(1,
                    new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
                    new KeyValuePair<string, object?>("error.type", step5Exception.GetType().Name));
                step5Activity.RecordException(step5Exception).SetStatus(ActivityStatusCode.Error);
                yield return new StreamState { Status = Json("status", "error", "message", step5Exception.Message, "step", "5") };
                yield break;
            }

            _logger?.LogInformation("Stream: Final report completed ({Length} chars)", finalReport.Length);

            step5Activity
                .AddTag("step.duration.ms", step5Stopwatch.Elapsed.TotalMilliseconds)
                .AddTag("final_report.length", finalReport.Length)
                .SetStatus(ActivityStatusCode.Ok);

            yield return new StreamState
            {
                Status = Json("step", "5", "status", "completed", "length", finalReport.Length.ToString()),
                RefinedSummary = refinedSummary,
                FinalReport = finalReport
            };

            workflowActivity.AddEvent("step5_completed", new Dictionary<string, object?>
            {
                ["final_report.length"] = finalReport.Length
            });
        }

        workflowStopwatch.Stop();
        DiagnosticConfig.WorkflowTotalDuration.Record(workflowStopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("workflow", "MasterWorkflow"),
            new KeyValuePair<string, object?>("status", "success"));

        _logger?.LogInformation("Stream: Workflow complete in {Duration}ms", workflowStopwatch.Elapsed.TotalMilliseconds);

        workflowActivity
            .AddTag("workflow.duration.ms", workflowStopwatch.Elapsed.TotalMilliseconds)
            .SetStatus(ActivityStatusCode.Ok, "Workflow completed successfully");

        yield return new StreamState
        {
            Status = Json("status", "completed", "totalSteps", "5", "duration_ms", workflowStopwatch.Elapsed.TotalMilliseconds.ToString())
        };
    }




    /// <summary>
    /// Helper to format JSON responses for streaming.
    /// </summary>
    private static string Json(params string[] pairs)
    {
        if (pairs.Length % 2 != 0)
            throw new ArgumentException("Pairs must be key-value pairs");

        var dict = new Dictionary<string, object>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            dict[pairs[i]] = pairs[i + 1];
        }

        return System.Text.Json.JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Step 1: Clarify with user - Check if query is detailed enough.
    /// Uses ClarifyAgent to evaluate query clarity.
    /// </summary>
    public async Task<(bool needsClarification, string message)> ClarifyWithUserAsync(
        string userQuery, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Step 1: ClarifyWithUser - evaluating query clarity");
            
            // Simple heuristic check first
            if (string.IsNullOrWhiteSpace(userQuery) || userQuery.Length < 10)
            {
                return (true, "Please provide a more detailed research query (at least 10 characters). Include what you want to learn about and any specific focus areas.");
            }

            // Convert user query to ChatMessage for the agent
            var conversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = userQuery }
            };

            // Use ClarifyAgent to evaluate clarity
            var clarification = await _clarifyAgent.ClarifyAsync(conversationHistory, cancellationToken);
            
            _logger?.LogDebug("ClarifyAgent response - NeedClarification: {NeedClarification}", 
                clarification.NeedClarification);

            if (clarification.NeedClarification)
            {
                return (true, clarification.Question);
            }

            return (false, clarification.Verification);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error in ClarifyWithUserAsync - proceeding anyway");
            return (false, ""); // Don't block workflow on clarification errors
        }
    }

    /// <summary>
    /// Step 2: Write research brief - Transform query into structured research brief.
    /// Uses ResearchBriefAgent to create detailed research direction.
    /// </summary>
    public async Task<string> WriteResearchBriefAsync(string userQuery, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Step 2: WriteResearchBrief - transforming query to structured brief");
            
            // Convert user query to ChatMessage for the agent
            var conversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = userQuery }
            };

            // Use ResearchBriefAgent to generate research brief
            var researchQuestion = await _briefAgent.GenerateResearchBriefAsync(
                conversationHistory, cancellationToken);
            
            var researchBrief = researchQuestion.ResearchBrief;

            _logger?.LogInformation("Research brief generated with {ObjectiveCount} objectives: {length} chars", 
                researchQuestion.Objectives?.Count ?? 0, researchBrief.Length);
            
            return researchBrief;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating research brief - using query as fallback");
            return $"Research Brief: {userQuery}";
        }
    }

    /// <summary>
    /// Step 3: Write initial draft report - Generate "noisy" starting point for diffusion.
    /// Uses DraftReportAgent to create initial draft without external research.
    /// </summary>
    public async Task<string> WriteDraftReportAsync(string researchBrief, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Step 3: WriteDraftReport - generating initial draft outline");
            
            // Create empty conversation history for context (brief is the primary input)
            var conversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = $"Research Brief: {researchBrief}" }
            };

            // Use DraftReportAgent to generate draft
            var draftReport = await _draftAgent.GenerateDraftReportAsync(
                researchBrief, conversationHistory, cancellationToken);

            _logger?.LogInformation("Draft report generated with {SectionCount} sections: {length} chars", 
                draftReport.Sections?.Count ?? 0, draftReport.Content.Length);
            
            return draftReport.Content;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating draft report - using fallback");
            return $"Initial draft based on: {researchBrief}";
        }
    }

    /// <summary>
    /// Step 4: Execute supervisor loop (handled by SupervisorWorkflow).
    /// Already implemented via _supervisor.SuperviseAsync()
    /// </summary>

    /// <summary>
    /// Step 5: Generate final report - Polish and synthesize findings.
    /// Uses LLM to create polished final report.
    /// </summary>
    public async Task<string> GenerateFinalReportAsync(
        string userQuery, string researchBrief, string draftReport, 
        string refinedSummary, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Step 5: GenerateFinalReport - synthesizing and polishing findings");
            
            var currentDate = GetTodayString();
            var finalPrompt = $@"You are a professional research report writer.
Your task is to synthesize research findings into a polished, well-structured final report.

Original User Query:
{userQuery}

Research Brief:
{researchBrief}

Initial Draft:
{draftReport}

Research Findings:
{refinedSummary}

Current Date: {currentDate}

Create a professional, comprehensive final report that:
1. Directly addresses the original user query
2. Incorporates the research findings naturally
3. Maintains clear structure and flow
4. Includes proper citations where mentioned
5. Provides clear conclusions and insights
6. Is suitable for professional presentation

Write the final report:";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = finalPrompt }
            };

            var response = await _llmService.InvokeAsync(messages, cancellationToken: cancellationToken);
            var finalReport = response.Content ?? 
                $@"=== Final Research Report ===

Original Query: {userQuery}

Findings Summary:
{refinedSummary}

This report synthesizes the research findings on the requested topic.";

            _logger?.LogInformation("Final report generated: {length} chars", finalReport.Length);
            
            return finalReport;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating final report - using fallback");
            return $@"=== Final Research Report ===

Original Query: {userQuery}

Research Findings:
{refinedSummary}";
        }
    }

    /// <summary>
    /// Format today's date in "Day Mon Day, Year" format to match Python's strftime
    /// Example: "Mon Dec 23, 2024"
    /// </summary>
    private static string GetTodayString()
    {
        return DateTime.Now.ToString("ddd MMM d, yyyy");
    }

    /// <summary>
    /// Phase 5: Execute full pipeline with complex agents.
    /// Orchestrates ResearcherAgent → AnalystAgent → ReportAgent workflow.
    /// 
    /// This is the complete end-to-end pipeline:
    /// 1. ResearcherAgent - Conducts research and extracts findings
    /// 2. AnalystAgent - Analyzes findings and synthesizes insights
    /// 3. ReportAgent - Formats everything into a publication-ready report
    /// </summary>
    public async Task<ReportOutput> ExecuteFullPipelineAsync(
        string topic,
        string researchBrief,
        CancellationToken cancellationToken = default)
    {
        var researchId = Guid.NewGuid().ToString();
        _logger?.LogInformation("ExecuteFullPipeline: Starting complex agent pipeline for topic: {Topic}", topic);

        try
        {
            // Step 1: ResearcherAgent - Research the topic
            _logger?.LogInformation("ExecuteFullPipeline: Step 1 - ResearcherAgent");
            var researchInput = new ResearchInput
            {
                Topic = topic,
                ResearchBrief = researchBrief,
                MaxIterations = 3,
                MinQualityThreshold = 7.0f
            };

            var researchOutput = await _researcherAgent.ExecuteAsync(researchInput, cancellationToken);
            _logger?.LogInformation(
                "ExecuteFullPipeline: ResearcherAgent complete - {FactCount} facts extracted, quality: {Quality:F2}",
                researchOutput.TotalFactsExtracted,
                researchOutput.AverageQuality);

            // Step 2: AnalystAgent - Analyze the findings
            _logger?.LogInformation("ExecuteFullPipeline: Step 2 - AnalystAgent");
            var analysisInput = new AnalysisInput
            {
                Findings = researchOutput.Findings,
                Topic = topic,
                ResearchBrief = researchBrief
            };

            var analysisOutput = await _analystAgent.ExecuteAsync(analysisInput, cancellationToken);
            _logger?.LogInformation(
                "ExecuteFullPipeline: AnalystAgent complete - {InsightCount} insights, {ThemeCount} themes, confidence: {Confidence:F2}",
                analysisOutput.KeyInsights.Count,
                analysisOutput.ThemesIdentified.Count,
                analysisOutput.ConfidenceScore);

            // Step 3: ReportAgent - Format into final report
            _logger?.LogInformation("ExecuteFullPipeline: Step 3 - ReportAgent");
            var reportInput = new ReportInput
            {
                Research = researchOutput,
                Analysis = analysisOutput,
                Topic = topic,
                Author = "Deep Research Agent"
            };

            var reportOutput = await _reportAgent.ExecuteAsync(reportInput, cancellationToken);
            _logger?.LogInformation(
                "ExecuteFullPipeline: ReportAgent complete - {SectionCount} sections, {CitationCount} citations, quality: {Quality:F2}",
                reportOutput.Sections.Count,
                reportOutput.Citations.Count,
                reportOutput.QualityScore);

            _logger?.LogInformation("ExecuteFullPipeline: Complete - Full report generated");
            
            return reportOutput;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ExecuteFullPipeline: Failed");
            throw;
        }
    }
}
