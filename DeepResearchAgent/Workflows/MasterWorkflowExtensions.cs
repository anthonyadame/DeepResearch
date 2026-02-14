using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Services.VectorDatabase;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Workflows;

/// <summary>
/// Phase 5 Master Workflow Enhancement: Complete agent pipeline integration.
/// Extends MasterWorkflow with StateTransitioner and AgentErrorRecovery.
/// </summary>
public static class MasterWorkflowExtensions
{
    /// <summary>
    /// Execute complete research pipeline using Phase 4 agents with error recovery.
    /// Pipeline: ResearcherAgent → AnalystAgent → ReportAgent
    /// </summary>
    public static async Task<ReportOutput> ExecuteFullPipelineAsync(
        this MasterWorkflow workflow,
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        StateTransitioner transitioner,
        AgentErrorRecovery errorRecovery,
        string topic,
        string researchBrief,
        Microsoft.Extensions.Logging.ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("ExecuteFullPipeline starting for topic: {Topic}", topic);

        try
        {
            // Step 1: Research Phase
            logger?.LogInformation("Step 1: Research Phase");
            var researchInput = new ResearchInput
            {
                Topic = topic,
                ResearchBrief = researchBrief,
                MaxIterations = 3,
                MinQualityThreshold = 7.0f
            };

            var researchOutput = await errorRecovery.ExecuteWithRetryAsync(
                async (input, ct) => await researcherAgent.ExecuteAsync(input, ct),
                researchInput,
                (input) => errorRecovery.CreateFallbackResearchOutput(topic, "Research phase failed"),
                "ResearcherAgent",
                cancellationToken
            );

            // Validate and repair research output
            researchOutput = errorRecovery.ValidateAndRepairResearchOutput(researchOutput, topic);

            // Validate research output
            var researchValidation = transitioner.ValidateResearchOutput(researchOutput);
            if (!researchValidation.IsValid)
            {
                logger?.LogWarning("Research validation failed: {Errors}", 
                    string.Join(", ", researchValidation.Errors));
            }

            // Log research statistics
            var researchStats = transitioner.GetResearchStatistics(researchOutput);
            logger?.LogInformation("Research complete: {Facts} facts, quality {Quality:F1}/10",
                researchStats.TotalFacts, researchStats.AverageQuality);

            // Step 2: Analysis Phase
            logger?.LogInformation("Step 2: Analysis Phase");
            var analysisInput = transitioner.CreateAnalysisInput(researchOutput, topic, researchBrief);

            var analysisOutput = await errorRecovery.ExecuteWithRetryAsync(
                async (input, ct) => await analystAgent.ExecuteAsync(input, ct),
                analysisInput,
                (input) => errorRecovery.CreateFallbackAnalysisOutput(topic, "Analysis phase failed"),
                "AnalystAgent",
                cancellationToken
            );

            // Validate and repair analysis output
            analysisOutput = errorRecovery.ValidateAndRepairAnalysisOutput(analysisOutput, topic);

            // Validate analysis output
            var analysisValidation = transitioner.ValidateAnalysisOutput(analysisOutput);
            if (!analysisValidation.IsValid)
            {
                logger?.LogWarning("Analysis validation failed: {Errors}",
                    string.Join(", ", analysisValidation.Errors));
            }

            // Log analysis statistics
            var analysisStats = transitioner.GetAnalysisStatistics(analysisOutput);
            logger?.LogInformation("Analysis complete: {Insights} insights, confidence {Confidence:F2}",
                analysisStats.TotalInsights, analysisStats.ConfidenceScore);

            // Step 3: Report Generation Phase
            logger?.LogInformation("Step 3: Report Generation Phase");
            var reportInput = transitioner.CreateReportInput(researchOutput, analysisOutput, topic);

            var reportOutput = await errorRecovery.ExecuteWithRetryAsync(
                async (input, ct) => await reportAgent.ExecuteAsync(input, ct),
                reportInput,
                (input) => errorRecovery.CreateFallbackReportOutput(topic, "Report generation failed"),
                "ReportAgent",
                cancellationToken
            );

            // Validate and repair report output
            reportOutput = errorRecovery.ValidateAndRepairReportOutput(reportOutput, topic);

            logger?.LogInformation("Report generation complete: {Title}, quality {Quality:F2}",
                reportOutput.Title, reportOutput.QualityScore);

            // Validate complete pipeline
            var pipelineValidation = transitioner.ValidatePipelineState(researchOutput, analysisOutput, topic);
            if (!pipelineValidation.IsValid)
            {
                logger?.LogWarning("Pipeline validation warnings: {Warnings}",
                    string.Join(", ", pipelineValidation.Warnings));
            }

            logger?.LogInformation("ExecuteFullPipeline completed successfully");
            return reportOutput;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "ExecuteFullPipeline failed for topic: {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Execute full pipeline with state persistence.
    /// Includes Lightning state service integration.
    /// </summary>
    public static async Task<ReportOutput> ExecuteFullPipelineWithStateAsync(
        this MasterWorkflow workflow,
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        StateTransitioner transitioner,
        AgentErrorRecovery errorRecovery,
        ILightningStateService stateService,
        string topic,
        string researchBrief,
        string? researchId = null,
        Microsoft.Extensions.Logging.ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var localResearchId = researchId ?? Guid.NewGuid().ToString();
        logger?.LogInformation("ExecuteFullPipelineWithState starting, ID: {ResearchId}", localResearchId);

        try
        {
            // Initialize state
            var researchState = new ResearchStateModel
            {
                ResearchId = localResearchId,
                Query = topic,
                Status = ResearchStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["phase"] = "research",
                    ["researchBrief"] = researchBrief
                }
            };

            await stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);

            // Execute pipeline
            var result = await workflow.ExecuteFullPipelineAsync(
                researcherAgent,
                analystAgent,
                reportAgent,
                transitioner,
                errorRecovery,
                topic,
                researchBrief,
                logger,
                cancellationToken
            );

            // Update final state
            researchState.Status = ResearchStatus.Completed;
            researchState.CompletedAt = DateTime.UtcNow;
            researchState.Metadata["phase"] = "completed";
            researchState.Metadata["reportTitle"] = result.Title;
            researchState.Metadata["qualityScore"] = result.QualityScore;

            await stateService.SetResearchStateAsync(localResearchId, researchState, cancellationToken);

            logger?.LogInformation("ExecuteFullPipelineWithState completed, ID: {ResearchId}", localResearchId);
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "ExecuteFullPipelineWithState failed, ID: {ResearchId}", localResearchId);

            // Update error state
            try
            {
                var errorState = await stateService.GetResearchStateAsync(localResearchId, cancellationToken);
                errorState.Status = ResearchStatus.Failed;
                errorState.Metadata["error"] = ex.Message;
                errorState.Metadata["errorType"] = ex.GetType().Name;
                await stateService.SetResearchStateAsync(localResearchId, errorState, cancellationToken);
            }
            catch (Exception stateEx)
            {
                logger?.LogWarning(stateEx, "Failed to update error state");
            }

            throw;
        }
    }

    /// <summary>
    /// Stream full pipeline execution with real-time progress updates.
    /// Yields status messages as pipeline progresses through each phase.
    /// </summary>
    public static async IAsyncEnumerable<string> StreamFullPipelineAsync(
        this MasterWorkflow workflow,
        ResearcherAgent researcherAgent,
        AnalystAgent analystAgent,
        ReportAgent reportAgent,
        StateTransitioner transitioner,
        AgentErrorRecovery errorRecovery,
        string topic,
        string researchBrief,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return $"[pipeline] Starting research on: {topic}";

        // Step 1: Research
        yield return "[pipeline] Phase 1/3: Research";
        var researchInput = new ResearchInput
        {
            Topic = topic,
            ResearchBrief = researchBrief,
            MaxIterations = 3,
            MinQualityThreshold = 7.0f
        };

        ResearchOutput? researchOutput = null;
        string researchError = "";
        
        researchOutput = await errorRecovery.ExecuteWithRetryAsync(
            async (input, ct) => await researcherAgent.ExecuteAsync(input, ct),
            researchInput,
            (input) => errorRecovery.CreateFallbackResearchOutput(topic, "Research failed"),
            "ResearcherAgent",
            cancellationToken
        );

        researchOutput = errorRecovery.ValidateAndRepairResearchOutput(researchOutput, topic);
        var researchStats = transitioner.GetResearchStatistics(researchOutput);
        yield return $"[pipeline] Research complete: {researchStats.TotalFacts} facts extracted";

        // Step 2: Analysis
        yield return "[pipeline] Phase 2/3: Analysis";
        AnalysisOutput? analysisOutput = null;
        
        var analysisInput = transitioner.CreateAnalysisInput(researchOutput, topic, researchBrief);
        analysisOutput = await errorRecovery.ExecuteWithRetryAsync(
            async (input, ct) => await analystAgent.ExecuteAsync(input, ct),
            analysisInput,
            (input) => errorRecovery.CreateFallbackAnalysisOutput(topic, "Analysis failed"),
            "AnalystAgent",
            cancellationToken
        );

        analysisOutput = errorRecovery.ValidateAndRepairAnalysisOutput(analysisOutput, topic);
        var analysisStats = transitioner.GetAnalysisStatistics(analysisOutput);
        yield return $"[pipeline] Analysis complete: {analysisStats.TotalInsights} insights generated";

        // Step 3: Report
        yield return "[pipeline] Phase 3/3: Report Generation";
        
        var reportInput = transitioner.CreateReportInput(researchOutput, analysisOutput, topic);
        var reportOutput = await errorRecovery.ExecuteWithRetryAsync(
            async (input, ct) => await reportAgent.ExecuteAsync(input, ct),
            reportInput,
            (input) => errorRecovery.CreateFallbackReportOutput(topic, "Report generation failed"),
            "ReportAgent",
            cancellationToken
        );

        reportOutput = errorRecovery.ValidateAndRepairReportOutput(reportOutput, topic);
        yield return $"[pipeline] Report complete: {reportOutput.Title}";
        yield return $"[pipeline] Quality score: {reportOutput.QualityScore:F2}";
        yield return $"[pipeline] Sections: {reportOutput.Sections?.Count ?? 0}";

        yield return "[pipeline] Pipeline completed successfully!";
    }
}
