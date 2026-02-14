using System.Diagnostics;
using System.Globalization;
using DeepResearchAgent.Models;
using DeepResearchAgent.Prompts;
using DeepResearchAgent.Services;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Agents;

/// <summary>
/// ClarifyIterativeAgent: Advanced clarification with PromptWizard-inspired feedback loops.
/// 
/// Implements iterative refinement through:
/// 1. Generate initial clarification question
/// 2. Critique the question for quality and focus
/// 3. Refine based on critique feedback
/// 4. Evaluate quality metrics against threshold
/// 5. Repeat until quality threshold met or max iterations reached
/// 
/// Based on PromptWizard's feedback-driven self-evolving mechanism:
/// - Stage 1: Iterative instruction refinement (3-5 iterations)
/// - Feedback Loop: Generate → Critique → Refine → Synthesize
/// </summary>
public class ClarifyIterativeAgent : ClarifyAgent
{
    private readonly IterativeClarificationConfig _config;

    protected override string AgentName => "ClarifyIterativeAgent";

    public ClarifyIterativeAgent(
        OllamaService llmService,
        IterativeClarificationConfig config,
        ILogger<ClarifyIterativeAgent>? logger = null)
        : base(llmService, logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Perform iterative clarification with critique and refinement.
    /// </summary>
    public async Task<IterativeClarificationResult> ClarifyWithIterationsAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var iterationHistory = new List<IterationSnapshot>();
        
        _logger?.LogInformation(
            "ClarifyIterativeAgent: Starting iterative clarification (maxIterations={MaxIterations}, threshold={Threshold})",
            _config.MaxIterations,
            _config.QualityThreshold);

        ClarificationResult? currentResult = null;
        QualityMetrics? currentMetrics = null;
        CritiqueFeedback? currentCritique = null;
        int iteration = 0;

        for (iteration = 1; iteration <= _config.MaxIterations; iteration++)
        {
            var iterationStopwatch = Stopwatch.StartNew();
            
            _logger?.LogInformation("ClarifyIterativeAgent: Iteration {Iteration} starting", iteration);

            try
            {
                // Step 1: Generate clarification (or refine based on previous critique)
                if (iteration == 1)
                {
                    currentResult = await GenerateInitialClarificationAsync(conversationHistory, cancellationToken);
                }
                else if (currentCritique != null)
                {
                    currentResult = await RefineClarificationAsync(
                        currentResult!,
                        currentCritique,
                        conversationHistory,
                        cancellationToken);
                }

                // Step 2: Evaluate quality metrics
                if (_config.EnableQualityMetrics)
                {
                    currentMetrics = await EvaluateQualityAsync(
                        conversationHistory,
                        cancellationToken);
                }

                // Step 3: Critique the clarification question
                if (_config.EnableCritique && iteration < _config.MaxIterations)
                {
                    currentCritique = await CritiqueClarificationAsync(
                        currentResult!,
                        conversationHistory,
                        cancellationToken);
                }

                iterationStopwatch.Stop();

                // Store iteration snapshot
                if (_config.StoreIterationHistory)
                {
                    iterationHistory.Add(new IterationSnapshot
                    {
                        Iteration = iteration,
                        Question = currentResult?.Question ?? "",
                        QualityScore = currentMetrics?.OverallScore ?? 0,
                        CritiqueSummary = currentCritique?.Reasoning ?? "",
                        DurationMs = iterationStopwatch.Elapsed.TotalMilliseconds
                    });
                }

                _logger?.LogInformation(
                    "ClarifyIterativeAgent: Iteration {Iteration} complete - QualityScore={Score:F1}, IsFocused={IsFocused}",
                    iteration,
                    currentMetrics?.OverallScore ?? 0,
                    currentCritique?.IsSufficientlyFocused ?? true);

                // Check if quality threshold met
                if (currentMetrics != null && currentMetrics.MeetsThreshold(_config.QualityThreshold))
                {
                    _logger?.LogInformation(
                        "ClarifyIterativeAgent: Quality threshold met after {Iteration} iterations (score={Score:F1})",
                        iteration,
                        currentMetrics.OverallScore);
                    break;
                }

                // Check if critique indicates sufficient focus
                if (currentCritique != null && 
                    currentCritique.IsSufficientlyFocused && 
                    currentCritique.Confidence >= _config.MinCritiqueConfidence)
                {
                    _logger?.LogInformation(
                        "ClarifyIterativeAgent: Critique indicates sufficient focus after {Iteration} iterations (confidence={Confidence:F2})",
                        iteration,
                        currentCritique.Confidence);
                    break;
                }

                // Check timeout
                if (overallStopwatch.Elapsed.TotalSeconds >= _config.MaxTimeoutSeconds)
                {
                    _logger?.LogWarning(
                        "ClarifyIterativeAgent: Max timeout reached ({Timeout}s), stopping at iteration {Iteration}",
                        _config.MaxTimeoutSeconds,
                        iteration);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, 
                    "ClarifyIterativeAgent: Error during iteration {Iteration}", 
                    iteration);
                
                // If first iteration fails, rethrow
                if (iteration == 1)
                {
                    throw;
                }
                
                // Otherwise use best result so far
                _logger?.LogWarning(
                    "ClarifyIterativeAgent: Continuing with result from iteration {PrevIteration}",
                    iteration - 1);
                break;
            }
        }

        overallStopwatch.Stop();

        // Build final result
        var finalResult = new IterativeClarificationResult
        {
            NeedClarification = currentResult?.NeedClarification ?? true,
            Question = currentResult?.Question ?? "Could you provide more details about your research question?",
            Verification = currentResult?.Verification ?? "",
            QualityMetrics = currentMetrics,
            IterationCount = iteration,
            LastCritique = currentCritique,
            TotalDurationMs = overallStopwatch.Elapsed.TotalMilliseconds,
            IterationHistory = _config.StoreIterationHistory ? iterationHistory : new List<IterationSnapshot>()
        };

        _logger?.LogInformation(
            "ClarifyIterativeAgent: Complete - {Iterations} iterations, {Duration:F0}ms, finalScore={Score:F1}",
            iteration,
            finalResult.TotalDurationMs,
            finalResult.QualityMetrics?.OverallScore ?? 0);

        return finalResult;
    }

    /// <summary>
    /// Generate initial clarification using base agent logic.
    /// </summary>
    private async Task<ClarificationResult> GenerateInitialClarificationAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug("ClarifyIterativeAgent: Generating initial clarification");
        return await ClarifyAsync(conversationHistory, cancellationToken);
    }

    /// <summary>
    /// Critique a clarification question for quality and focus.
    /// Implements PromptWizard's critique mechanism.
    /// </summary>
    private async Task<CritiqueFeedback> CritiqueClarificationAsync(
        ClarificationResult clarification,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug("ClarifyIterativeAgent: Critiquing clarification question");

        var messagesText = FormatMessagesToString(conversationHistory);
        var prompt = PromptTemplates.CritiqueClarificationPrompt
            .Replace("{question}", clarification.Question)
            .Replace("{messages}", messagesText);

        var ollamaMessages = new List<OllamaChatMessage>
        {
            new OllamaChatMessage { Role = "user", Content = prompt }
        };

        var critique = await _llmService.InvokeWithStructuredOutputAsync<CritiqueFeedback>(
            ollamaMessages,
            cancellationToken: cancellationToken);

        return critique;
    }

    /// <summary>
    /// Evaluate quality metrics for the user's request.
    /// Assesses clarity, completeness, and actionability.
    /// </summary>
    private async Task<QualityMetrics> EvaluateQualityAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug("ClarifyIterativeAgent: Evaluating quality metrics");

        var messagesText = FormatMessagesToString(conversationHistory);
        var currentDate = GetTodayString();
        
        var prompt = PromptTemplates.EvaluateQualityPrompt
            .Replace("{messages}", messagesText)
            .Replace("{date}", currentDate);

        var ollamaMessages = new List<OllamaChatMessage>
        {
            new OllamaChatMessage { Role = "user", Content = prompt }
        };

        var metrics = await _llmService.InvokeWithStructuredOutputAsync<QualityMetrics>(
            ollamaMessages,
            cancellationToken: cancellationToken);

        return metrics;
    }

    /// <summary>
    /// Refine clarification question based on critique feedback.
    /// Implements PromptWizard's synthesis mechanism.
    /// </summary>
    private async Task<ClarificationResult> RefineClarificationAsync(
        ClarificationResult original,
        CritiqueFeedback critique,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug("ClarifyIterativeAgent: Refining clarification based on critique");

        var messagesText = FormatMessagesToString(conversationHistory);
        var prompt = PromptTemplates.RefineClarificationPrompt
            .Replace("{original_question}", original.Question)
            .Replace("{weaknesses}", string.Join(", ", critique.Weaknesses))
            .Replace("{suggestions}", string.Join(", ", critique.SuggestedImprovements))
            .Replace("{dimensions}", string.Join(", ", critique.DimensionsToImprove))
            .Replace("{messages}", messagesText);

        var ollamaMessages = new List<OllamaChatMessage>
        {
            new OllamaChatMessage { Role = "user", Content = prompt }
        };

        var refined = await _llmService.InvokeWithStructuredOutputAsync<ClarificationResult>(
            ollamaMessages,
            cancellationToken: cancellationToken);

        return refined;
    }

    /// <summary>
    /// Format messages into a readable string representation for the LLM.
    /// </summary>
    private static string FormatMessagesToString(List<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return "[No messages]";
        }

        var formattedMessages = new System.Text.StringBuilder();
        
        foreach (var msg in messages)
        {
            var roleLabel = msg.Role switch
            {
                "user" => "USER",
                "assistant" => "ASSISTANT",
                "system" => "SYSTEM",
                _ => msg.Role.ToUpper()
            };
            
            formattedMessages.AppendLine($"{roleLabel}: {msg.Content}");
            formattedMessages.AppendLine();
        }
        
        return formattedMessages.ToString().TrimEnd();
    }

    /// <summary>
    /// Get today's date in human-readable format.
    /// </summary>
    private static string GetTodayString()
    {
        var today = DateTime.Now;
        return today.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
