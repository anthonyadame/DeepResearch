using System.Text;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.WebSearch;
using DeepResearchAgent.Services.StateManagement;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Workflows;

/// <summary>
/// Supervisor Workflow: Manages the diffusion loop for iterative report refinement.
/// 
/// Core Functions:
/// 1. Brain: LLM-based decision making for research direction
/// 2. Tools: Execute research tasks in parallel (ConductResearch, RefineReport, ThinkTool)
/// 3. Red Team: Adversarial critique for self-correction
/// 4. Context Pruner: Knowledge base management and fact extraction
/// 5. Evaluator: Quality scoring and convergence checking
/// 
/// Maps to Python lines 625-1050 (supervisor + tools + red team + context pruner)
/// </summary>
public class SupervisorWorkflow
{
    private readonly ILightningStateService _stateService;
    private readonly ResearcherWorkflow _researcher;
    private readonly OllamaService _llmService;
    private readonly ToolInvocationService _toolService;
    private readonly LightningStore? _store;
    private readonly ILogger<SupervisorWorkflow>? _logger;
    private readonly StateManager? _stateManager;
    private readonly WorkflowModelConfiguration _modelConfig;

    public SupervisorWorkflow(
        ILightningStateService stateService,
        ResearcherWorkflow researcher,
        OllamaService llmService,
        IWebSearchProvider? searchProvider = null,
        LightningStore? store = null,
        ILogger<SupervisorWorkflow>? logger = null,
        StateManager? stateManager = null,
        WorkflowModelConfiguration? modelConfig = null)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _researcher = researcher ?? throw new ArgumentNullException(nameof(researcher));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _store = store;
        _logger = logger;
        _stateManager = stateManager;
        _modelConfig = modelConfig ?? new WorkflowModelConfiguration();
        
        // Initialize tool invocation service for research execution
        if (searchProvider == null)
        {
            throw new ArgumentNullException(nameof(searchProvider), 
                "IWebSearchProvider must be provided. Use dependency injection or provide an explicit instance.");
        }
        _toolService = new ToolInvocationService(searchProvider, llmService, null);

        _logger?.LogInformation("SupervisorWorkflow initialized with model configuration: Brain={brain}, Tools={tools}, QualityEvaluator={evaluator}, RedTeam={redteam}, ContextPruner={pruner}",
            _modelConfig.SupervisorBrainModel,
            _modelConfig.SupervisorToolsModel,
            _modelConfig.QualityEvaluatorModel,
            _modelConfig.RedTeamModel,
            _modelConfig.ContextPrunerModel);
    }

    /// <summary>
    /// Execute supervisor loop: Brain → Tools → Evaluate → Repeat until quality acceptable
    /// Main entry point for the diffusion process.
    /// </summary>
    public async Task<string> SuperviseAsync(
        string researchBrief,
        string draftReport = "",
        int maxIterations = 5,
        string? researchId = null,
        CancellationToken cancellationToken = default)
    {
        var supervisionId = Guid.NewGuid().ToString();
        // _metrics.RecordRequest("supervisor", "started");
        // _metrics.TrackResearchRequest("supervisor", supervisionId, "started");
        //var stopwatch = Stopwatch.StartNew();

        _logger?.LogInformation("SupervisorWorkflow starting - Supervision ID: {supervisionId}", supervisionId);

        try
        {
            var supervisorState = StateFactory.CreateSupervisorState();
            supervisorState.ResearchBrief = researchBrief;
            supervisorState.DraftReport = draftReport ?? $"Initial draft for: {researchBrief}";

            // Track quality progression
            var qualityScores = new List<double>();

            // Execute diffusion loop
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                _logger?.LogInformation("Supervisor iteration {iter}/{max}", iteration + 1, maxIterations);

                // Step 1: Supervisor Brain - Decide next actions
                var brainDecision = await SupervisorBrainAsync(supervisorState, cancellationToken);
                supervisorState.SupervisorMessages.Add(brainDecision);
                // _metrics.RecordLlmRequest("supervisor", _llmService.DefaultModel ?? "ollama", true);
                // _metrics.TrackLlmRequest("supervisor", _llmService.DefaultModel ?? "ollama", "success");

                // Step 2: Execute Tools based on brain decision
                await SupervisorToolsAsync(supervisorState, brainDecision, null, cancellationToken);
                // _metrics.RecordAgentExecution("supervisor", "tools", true);

                // Step 3: Quality Evaluation
                var quality = await EvaluateDraftQualityAsync(supervisorState, cancellationToken);
                supervisorState.QualityHistory.Add(
                    StateFactory.CreateQualityMetric(quality, "Iteration quality", iteration)
                );
                supervisorState.ResearchIterations = iteration + 1;
                qualityScores.Add(quality);

                // Update research state if we have a research ID
                if (!string.IsNullOrEmpty(researchId))
                {
                    await _stateService.UpdateResearchProgressAsync(
                        researchId,
                        iteration + 1,
                        quality / 10.0,  // Normalize to 0-1
                        cancellationToken
                    );
                    // _metrics.RecordStateOperation("update_research_progress", true);
                    // _metrics.TrackStateOperation("update_research_progress", "success");
                }

                _logger?.LogInformation("Iteration {iter} quality: {quality:F1}/10", iteration + 1, quality);

                // Step 4: Check convergence
                if (quality >= 8.0f || (iteration > 0 && quality >= 7.5f && iteration >= 2))
                {
                    _logger?.LogInformation("Supervisor loop converged at iteration {iter}", iteration + 1);
                    break;
                }

                // Step 5: Red Team critique (in parallel with iteration)
                if (iteration > 0)
                {
                    var critique = await RunRedTeamAsync(supervisorState.DraftReport, cancellationToken);
                    if (critique != null)
                    {
                        supervisorState.ActiveCritiques.Add(critique);
                        _logger?.LogInformation("Red team critique: {concern}", 
                            critique.Concern.Substring(0, Math.Min(60, critique.Concern.Length)));
                        // _metrics.RecordAgentExecution("supervisor", "redteam", true);
                    }
                }

                // Step 6: Context Pruning (extract and deduplicate facts)
                await ContextPrunerAsync(supervisorState, cancellationToken);
                // _metrics.RecordAgentExecution("supervisor", "context_pruner", true);
            }

            _logger?.LogInformation("Supervisor complete - iterations: {count}, quality: {quality:F1}/10",
                supervisorState.ResearchIterations,
                supervisorState.QualityHistory.LastOrDefault()?.Score ?? 0);

            // Synthesize findings
            var summary = SummarizeFacts(supervisorState.ResearchBrief, supervisorState.KnowledgeBase.AsReadOnly());
            // _metrics.RecordRequest("supervisor", "succeeded", stopwatch.Elapsed.TotalMilliseconds);
            // _metrics.TrackResearchRequest("supervisor", supervisionId, "succeeded");
            // _metrics.RecordAgentExecution("supervisor", "complete", true);
            return summary;
        }
        catch (Exception ex)
        {
            // _metrics.RecordError("supervisor", ex.GetType().Name);
            // _metrics.TrackError("supervisor", ex.GetType().Name);
            // _metrics.RecordRequest("supervisor", "failed", stopwatch.Elapsed.TotalMilliseconds);
            // _metrics.TrackResearchRequest("supervisor", supervisionId, "failed");
            // _metrics.RecordAgentExecution("supervisor", "complete", false);
            throw;
        }
    }

    /// <summary>
    /// Execute supervisor workflow with SupervisorState input/output.
    /// Overload for test compatibility.
    /// </summary>
    public async Task<SupervisorState> ExecuteAsync(SupervisorState input, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SupervisorWorkflow.ExecuteAsync starting");

        try
        {
            const int maxIterations = 5;
            
            // Execute diffusion loop
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                _logger?.LogInformation("Supervisor iteration {iter}/{max}", iteration + 1, maxIterations);

                // Step 1: Supervisor Brain - Decide next actions
                var brainDecision = await SupervisorBrainAsync(input, cancellationToken);
                input.SupervisorMessages.Add(brainDecision);

                // Step 2: Execute Tools based on brain decision
                await SupervisorToolsAsync(input, brainDecision, null, cancellationToken);

                // Step 3: Quality Evaluation
                var quality = await EvaluateDraftQualityAsync(input, cancellationToken);
                input.QualityHistory.Add(
                    StateFactory.CreateQualityMetric(quality, "Iteration quality", iteration)
                );
                input.ResearchIterations = iteration + 1;

                _logger?.LogInformation("Iteration {iter} quality: {quality:F1}/10", iteration + 1, quality);

                // Step 4: Check convergence
                if (quality >= 8.0f || (iteration > 0 && quality >= 7.5f && iteration >= 2))
                {
                    _logger?.LogInformation("Supervisor loop converged at iteration {iter}", iteration + 1);
                    break;
                }

                // Step 5: Red Team critique
                if (iteration > 0)
                {
                    var critique = await RunRedTeamAsync(input.DraftReport, cancellationToken);
                    if (critique != null)
                    {
                        input.ActiveCritiques.Add(critique);
                        _logger?.LogInformation("Red team critique recorded");
                    }
                }

                // Step 6: Context Pruning
                await ContextPrunerAsync(input, cancellationToken);
            }

            _logger?.LogInformation("Supervisor.ExecuteAsync complete");
            return input;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SupervisorWorkflow.ExecuteAsync failed");
            throw;
        }
    }

    /// <summary>
    /// Stream supervision progress for real-time output.
    /// </summary>
    public async IAsyncEnumerable<string> StreamSuperviseAsync(
        string researchBrief,
        string draftReport = "",
        int maxIterations = 5,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SupervisorWorkflow streaming started");
        yield return "[supervisor] starting diffusion loop...";

        var supervisorState = StateFactory.CreateSupervisorState();
        supervisorState.ResearchBrief = researchBrief;
        supervisorState.DraftReport = draftReport ?? $"Initial draft for: {researchBrief}";

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            yield return $"[supervisor] iteration {iteration + 1}/{maxIterations}";

            // Brain decision
            yield return "[supervisor] supervisor brain: analyzing state and deciding next actions...";
            var brainDecision = await SupervisorBrainAsync(supervisorState, cancellationToken);
            supervisorState.SupervisorMessages.Add(brainDecision);
            yield return "[supervisor] brain decision recorded";

            // Tool execution
            yield return "[supervisor] executing tools...";
            await SupervisorToolsAsync(supervisorState, brainDecision, null, cancellationToken);
            yield return $"[supervisor] {supervisorState.KnowledgeBase.Count} facts in knowledge base";

            // Quality evaluation
            var quality = await EvaluateDraftQualityAsync(supervisorState, cancellationToken);
            supervisorState.QualityHistory.Add(
                StateFactory.CreateQualityMetric(quality, "Iteration quality", iteration)
            );
            supervisorState.ResearchIterations = iteration + 1;
            yield return $"[supervisor] quality score: {quality:F1}/10";

            // Convergence check
            if (quality >= 8.0f || (iteration > 0 && quality >= 7.5f && iteration >= 2))
            {
                yield return $"[supervisor] converged at iteration {iteration + 1}";
                break;
            }

            // Red team
            if (iteration > 0)
            {
                yield return "[supervisor] red team: generating adversarial critique...";
                var critique = await RunRedTeamAsync(supervisorState.DraftReport, cancellationToken);
                if (critique != null)
                {
                    supervisorState.ActiveCritiques.Add(critique);
                    yield return $"[supervisor] critique: {critique.Concern.Substring(0, Math.Min(50, critique.Concern.Length))}...";
                }
                else
                {
                    yield return "[supervisor] red team: PASS - no issues found";
                }
            }

            // Context pruning
            yield return "[supervisor] context pruning: extracting and deduplicating facts...";
            await ContextPrunerAsync(supervisorState, cancellationToken);
            yield return $"[supervisor] knowledge base refined";
        }

        yield return "[supervisor] diffusion loop complete";
    }

    /// <summary>
    /// Step 1: Supervisor Brain - High-level decision making.
    /// Injects unaddressed critiques and quality warnings into the decision context.
    /// Uses the SupervisorBrainModel for decision-making.
    /// Maps to Python lines 650-750
    /// </summary>
    public async Task<Models.ChatMessage> SupervisorBrainAsync(
        SupervisorState state,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("SupervisorBrain: making decisions");

            var currentDate = GetTodayString();
            
            // Build context with all relevant information
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("=== SUPERVISOR BRAIN CONTEXT ===");
            contextBuilder.AppendLine($"Date: {currentDate}");
            contextBuilder.AppendLine($"Research Brief: {state.ResearchBrief}");
            contextBuilder.AppendLine($"Current Draft Quality: {(state.QualityHistory.LastOrDefault()?.Score ?? 5.0f):F1}/10");
            contextBuilder.AppendLine($"Iteration: {state.ResearchIterations}");
            contextBuilder.AppendLine();

            // Inject unaddressed critiques
            var unaddressed = state.ActiveCritiques.Where(c => !c.Addressed).ToList();
            if (unaddressed.Any())
            {
                contextBuilder.AppendLine("=== CRITICAL ISSUES TO ADDRESS ===");
                foreach (var critique in unaddressed)
                {
                    contextBuilder.AppendLine($"• [{critique.Author}] {critique.Concern}");
                }
                contextBuilder.AppendLine();
            }

            // Quality improvement warnings
            if (state.QualityHistory.Count >= 2)
            {
                var recentScores = state.QualityHistory.TakeLast(2).ToList();
                if (recentScores[1].Score < 6.0f)
                {
                    contextBuilder.AppendLine("⚠ WARNING: Quality is below 6.0 - Significant improvement needed!");
                    contextBuilder.AppendLine();
                }
            }

            var systemPrompt = PromptTemplates.SupervisorBrainPrompt ?? $@"You are the lead research supervisor managing a diffusion-based research refinement loop.

{contextBuilder}

Your task: Decide what to do next:
1. Should we conduct more research? If yes, what topics?
2. Should we refine the current draft based on findings?
3. What are your thoughts on the current research direction?

Respond concisely with your decision and reasoning.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            // Add conversation history
            messages.AddRange(
                state.SupervisorMessages.Select(m => new OllamaChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                })
            );

            var brainModel = _modelConfig.GetModelForFunction(WorkflowFunction.SupervisorBrain);
            var response = await _llmService.InvokeAsync(messages, model: brainModel, cancellationToken: cancellationToken);
            
            _logger?.LogDebug("Brain decision: {length} chars using model {model}", response.Content.Length, brainModel);
            
            // Convert OllamaChatMessage to Models.ChatMessage
            return new Models.ChatMessage 
            { 
                Role = response.Role, 
                Content = response.Content 
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SupervisorBrain failed");
            return new Models.ChatMessage 
            { 
                Role = "assistant", 
                Content = "Continue research on key topics. Refine current draft based on gathered information."
            };
        }
    }

    /// <summary>
    /// Step 2: Execute Supervisor Tools: Research, refinement, reflection based on brain decision.
    /// Handles tool routing and parallel execution using ToolInvocationService.
    /// Executes WebSearch → Summarization → FactExtraction pipeline.
    /// Maps to Python lines 750-850
    /// </summary>
    public async Task SupervisorToolsAsync(
        SupervisorState state,
        Models.ChatMessage brainDecision,
        string? researchId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("SupervisorTools: executing research tasks based on brain decision");

            // Parse brain decision to identify needed research topics
            var researchTopics = ExtractResearchTopics(brainDecision.Content, state.ResearchBrief);

            if (researchTopics.Any())
            {
                _logger?.LogInformation("Executing tools for {count} research topics", researchTopics.Count);
                
                // Execute tools for each topic
                foreach (var topic in researchTopics.Take(3)) // Max 3 topics per iteration
                {
                    try
                    {
                        // Step 1: WebSearch for information on topic
                        _logger?.LogInformation("WebSearch: searching for '{Topic}'", topic);
                        var searchParams = new Dictionary<string, object>
                        {
                            { "query", topic },
                            { "maxResults", 5 }
                        };
                        
                        var searchResults = await _toolService.InvokeToolAsync(
                            "websearch", searchParams, cancellationToken);
                        
                        if (searchResults is not List<WebSearchResult> results)
                        {
                            _logger?.LogWarning("WebSearch returned unexpected type");
                            continue;
                        }

                        _logger?.LogInformation("WebSearch found {count} results for '{Topic}'", 
                            results.Count, topic);

                        // Step 2: For each result, summarize and extract facts
                        foreach (var result in results)
                        {
                            try
                            {
                                // Summarize the content
                                var summaryParams = new Dictionary<string, object>
                                {
                                    { "pageContent", result.Content },
                                    { "maxLength", 300 }
                                };
                                
                                var summarized = await _toolService.InvokeToolAsync(
                                    "summarize", summaryParams, cancellationToken);

                                if (summarized is not PageSummaryResult summary)
                                {
                                    _logger?.LogWarning("Summarization returned unexpected type");
                                    continue;
                                }

                                _logger?.LogDebug("Summarized content from: {Url}", result.Url);

                                // Extract facts from the summary
                                var factParams = new Dictionary<string, object>
                                {
                                    { "content", summary.Summary },
                                    { "topic", topic }
                                };
                                
                                var factResult = await _toolService.InvokeToolAsync(
                                    "extractfacts", factParams, cancellationToken);

                                if (factResult is not FactExtractionResult extracted)
                                {
                                    _logger?.LogWarning("FactExtraction returned unexpected type");
                                    continue;
                                }

                                // Add extracted facts to knowledge base
                                foreach (var fact in extracted.Facts)
                                {
                                    var factModel = new Models.FactState
                                    {
                                        Content = fact.Statement,
                                        SourceUrl = result.Url,
                                        Confidence = fact.Confidence,
                                        ExtractedAt = DateTime.UtcNow,
                                        IsDisputed = false
                                    };
                                    
                                    state.KnowledgeBase.Add(factModel);
                                    _logger?.LogDebug("Added fact: {Statement} (confidence: {Confidence})", 
                                        fact.Statement.Substring(0, Math.Min(50, fact.Statement.Length)), 
                                        fact.Confidence);
                                }

                                _logger?.LogInformation("Extracted {count} facts from {Source}", 
                                    extracted.Facts.Count, result.Url);
                            }
                            catch (Exception resultEx)
                            {
                                _logger?.LogWarning(resultEx, "Error processing search result: {Url}", 
                                    result.Url);
                            }
                        }
                    }
                    catch (Exception topicEx)
                    {
                        _logger?.LogWarning(topicEx, "Error processing topic: {Topic}", topic);
                    }
                }

                _logger?.LogInformation("SupervisorTools complete - {count} facts in knowledge base", 
                    state.KnowledgeBase.Count);
            }

            // Record supervisor tool execution
            state.SupervisorMessages.Add(new Models.ChatMessage
            {
                Role = "tool",
                Content = $"Executed research tools on {researchTopics.Count} topics. Extracted {state.KnowledgeBase.Count} facts into knowledge base."
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SupervisorTools execution failed");
            state.SupervisorMessages.Add(new Models.ChatMessage
            {
                Role = "tool",
                Content = $"Tool execution failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Evaluate current draft quality using LLM.
    /// Scores quality on 0-10 scale across multiple dimensions.
    /// Uses the QualityEvaluatorModel for assessment.
    /// Maps to Python lines 900-950
    /// </summary>
    public async Task<float> EvaluateDraftQualityAsync(
        SupervisorState state,
        CancellationToken cancellationToken)
    {
        try
        {
            // Heuristic scoring
            float score = 5.0f;

            // Factor 1: Knowledge base size
            score += Math.Min(2.5f, state.KnowledgeBase.Count / 4.0f);

            // Factor 2: Average confidence
            if (state.KnowledgeBase.Any())
            {
                var avgConfidence = (float)(state.KnowledgeBase.Average(f => f.ConfidenceScore) / 100.0);
                score += avgConfidence * 1.5f;
            }

            // Factor 3: Critiques addressed
            var addressed = state.ActiveCritiques.Count(c => c.Addressed);
            var total = state.ActiveCritiques.Count;
            if (total > 0)
            {
                score += (addressed / (float)total) * 1.5f;
            }

            // Factor 4: Quality history trend
            if (state.QualityHistory.Count >= 2)
            {
                var recent = state.QualityHistory.TakeLast(2).ToList();
                if (recent[1].Score > recent[0].Score)
                {
                    score += 0.5f; // Improvement bonus
                }
            }

            // Optional: LLM-based quality assessment for final iteration
            if (state.ResearchIterations >= 3 && state.KnowledgeBase.Count > 0)
            {
                score = await GetLLMQualityScoreAsync(state, score, cancellationToken);
            }

            return Math.Clamp(score, 0, 10);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Quality evaluation failed");
            return 5.0f; // Default neutral score
        }
    }

    /// <summary>
    /// Get LLM-based quality assessment using the QualityEvaluatorModel.
    /// </summary>
    private async Task<float> GetLLMQualityScoreAsync(
        SupervisorState state,
        float currentScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var evaluationPrompt = $@"You are a research quality evaluator.

Research Brief: {state.ResearchBrief}

Current Draft: {state.DraftReport.Substring(0, Math.Min(500, state.DraftReport.Length))}...

Knowledge Base Size: {state.KnowledgeBase.Count} facts

On a scale of 0-10, rate the research quality considering:
1. Comprehensiveness (do we have enough information?)
2. Accuracy (are sources credible?)
3. Organization (is the draft well-structured?)
4. Depth (how thorough is the research?)

Respond with ONLY a number between 0 and 10.";

            var evaluatorModel = _modelConfig.GetModelForFunction(WorkflowFunction.QualityEvaluator);
            var response = await _llmService.InvokeAsync(
                new List<OllamaChatMessage>
                {
                    new() { Role = "system", Content = evaluationPrompt }
                },
                model: evaluatorModel,
                cancellationToken: cancellationToken
            );

            // Try to extract numeric score
            var content = response.Content.Trim();
            if (float.TryParse(content.First(c => char.IsDigit(c) || c == '.').ToString(), out float score))
            {
                _logger?.LogDebug("LLM quality score: {score} using model {model}", score, evaluatorModel);
                return Math.Clamp(score, 0, 10);
            }

            return currentScore; // Return current score if parsing fails
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "LLM quality assessment failed");
            return currentScore;
        }
    }

    /// <summary>
    /// Run red team critique: Have LLM critique the draft as an adversary.
    /// Identifies weaknesses and areas for improvement.
    /// Uses the RedTeamModel for critique.
    /// Maps to Python lines 950-1000
    /// </summary>
    public async Task<CritiqueState?> RunRedTeamAsync(
        string draftReport,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("RedTeam: generating critique");

            var redTeamPrompt = $@"You are a critical Red Team adversary. Your job is to find weaknesses in research reports.

Review this draft and identify:
1. Unsupported claims (assertions without evidence)
2. Logical fallacies or leaps in reasoning
3. Missing alternative perspectives
4. Outdated or questionable sources
5. Bias or one-sided arguments

Draft to critique:
{draftReport.Substring(0, Math.Min(800, draftReport.Length))}...

If the draft is solid with no major issues, respond with: PASS

Otherwise, provide specific, actionable critique.";

            var redTeamModel = _modelConfig.GetModelForFunction(WorkflowFunction.RedTeam);
            var response = await _llmService.InvokeAsync(
                new List<OllamaChatMessage>
                {
                    new() { Role = "system", Content = redTeamPrompt }
                },
                model: redTeamModel,
                cancellationToken: cancellationToken
            );

            var content = response.Content ?? "";
            if (content.Contains("PASS", StringComparison.OrdinalIgnoreCase) && content.Length < 30)
            {
                _logger?.LogDebug("RedTeam: PASS - no issues found using model {model}", redTeamModel);
                return null; // No critique needed
            }

            var critique = StateFactory.CreateCritique("Red Team", content, severity: 8);
            _logger?.LogInformation("RedTeam: critique generated using model {model}", redTeamModel);
            return critique;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "RedTeam failed");
            return null;
        }
    }

    /// <summary>
    /// Context Pruner: Extract facts from notes and deduplicate.
    /// Populates knowledge base with verified findings.
    /// Uses the ContextPrunerModel for fact extraction.
    /// Maps to Python lines 1000-1050
    /// </summary>
    public async Task ContextPrunerAsync(
        SupervisorState state,
        CancellationToken cancellationToken)
    {
        try
        {
            if (state.RawNotes.Count == 0)
                return;

            _logger?.LogDebug("ContextPruner: processing {count} raw notes", state.RawNotes.Count);

            var pruningPrompt = $@"You are a Knowledge Graph Engineer. Extract key facts from these research notes.

For each fact, identify:
1. The factual claim
2. The source (if mentioned)
3. Confidence level (0-100)

Notes to process:
{string.Join("\n", state.RawNotes.Take(5))}

Format each fact as: [FACT] claim | source | confidence

If you identify no new facts, respond with: NO_NEW_FACTS";

            var prunerModel = _modelConfig.GetModelForFunction(WorkflowFunction.ContextPruner);
            var response = await _llmService.InvokeAsync(
                new List<OllamaChatMessage>
                {
                    new() { Role = "system", Content = pruningPrompt }
                },
                model: prunerModel,
                cancellationToken: cancellationToken
            );

            var content = response.Content ?? "";
            if (content.Contains("NO_NEW_FACTS", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("ContextPruner: no new facts extracted using model {model}", prunerModel);
                return;
            }

            // Parse extracted facts
            var factLines = content.Split('\n')
                .Where(l => l.Contains("[FACT]"))
                .Take(10); // Limit to 10 new facts per iteration

            int newFactCount = 0;
            foreach (var line in factLines)
            {
                var parts = line.Replace("[FACT]", "").Split('|');
                if (parts.Length >= 3)
                {
                    var factContent = parts[0].Trim();
                    var source = parts[1].Trim();
                    if (int.TryParse(parts[2].Trim().Replace("%", ""), out int confidence))
                    {
                        // Check if similar fact already exists
                        var exists = state.KnowledgeBase.Any(f => 
                            f.Content.Contains(factContent.Substring(0, Math.Min(20, factContent.Length))));
                        
                        if (!exists)
                        {
                            var fact = StateFactory.CreateFact(factContent, source, confidence);
                            state.KnowledgeBase.Add(fact);
                            newFactCount++;
                        }
                    }
                }
            }

            // Clear processed notes
            state.RawNotes.Clear();

            _logger?.LogInformation("ContextPruner: {count} new facts added using model {model}", newFactCount, prunerModel);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ContextPruner failed");
        }
    }

    /// <summary>
    /// Extract research topics from brain decision text using simple heuristics.
    /// </summary>
    private static List<string> ExtractResearchTopics(string brainDecision, string researchBrief)
    {
        var topics = new List<string>();

        // Simple extraction: look for key phrases
        var keywords = new[] { "research", "investigate", "explore", "analyze", "study" };
        
        // For now, use the research brief + some variations
        topics.Add(researchBrief);
        
        // Add topic variations
        if (researchBrief.Length > 20)
        {
            topics.Add($"{researchBrief} trends");
            topics.Add($"{researchBrief} applications");
        }

        return topics.Distinct().ToList();
    }

    /// <summary>
    /// Summarize facts into a coherent research summary.
    /// </summary>
    private static string SummarizeFacts(string topic, IReadOnlyList<Models.FactState> facts)
    {
        if (facts.Count == 0)
        {
            return $"No facts extracted for topic: {topic}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== Research Summary: {topic} ===");
        sb.AppendLine();

        // Group by confidence
        var grouped = facts
            .GroupBy(f => f.ConfidenceScore >= 80 ? "High Confidence" : "Standard")
            .OrderByDescending(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key} Facts");
            sb.AppendLine();

            int idx = 1;
            foreach (var fact in group.Take(15))
            {
                sb.AppendLine($"{idx}. {fact.Content}");
                sb.AppendLine($"   Source: {fact.SourceUrl} | Confidence: {fact.ConfidenceScore}%");
                if (fact.IsDisputed)
                    sb.AppendLine($"   ⚠️  DISPUTED");
                sb.AppendLine();
                idx++;
            }
        }

        sb.AppendLine($"Total facts compiled: {facts.Count}");
        return sb.ToString();
    }

    /// <summary>
    /// Format today's date for prompt consistency.
    /// </summary>
    private static string GetTodayString()
    {
        return DateTime.Now.ToString("ddd MMM d, yyyy");
    }
}
