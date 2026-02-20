using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeepResearchAgent.Tests.Checkpointing;

/// <summary>
/// Integration tests for checkpoint workflow scenarios.
/// Tests end-to-end checkpoint save/resume cycles and edge cases.
/// </summary>
public class CheckpointIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CheckpointService _checkpointService;
    private readonly Mock<ILightningStore> _mockLightningStore;
    private readonly Mock<ILogger<CheckpointService>> _mockLogger;

    public CheckpointIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"checkpoint_integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _mockLightningStore = new Mock<ILightningStore>();
        _mockLogger = new Mock<ILogger<CheckpointService>>();

        var options = new CheckpointServiceOptions
        {
            LocalStorageDirectory = _testDirectory,
            MaxCheckpointSizeBytes = 10 * 1024 * 1024, // 10MB
            StorageBackend = "file"
        };

        _checkpointService = new CheckpointService(
            _mockLightningStore.Object,
            options,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task E2E_SaveCheckpointAtStep3_ResumeFromCheckpoint_StateMatches()
    {
        // Arrange - Simulate workflow progressing to step 3
        var workflowId = "wf_e2e_test";
        var workflowState = new
        {
            CurrentQuestion = "What is quantum computing?",
            BriefCompletedAt = DateTime.UtcNow.AddMinutes(-5),
            ResearchStartedAt = DateTime.UtcNow.AddMinutes(-2),
            CompletedAgents = new[] { "ClarifyAgent", "ResearchBriefAgent" },
            CurrentAgent = "ResearcherAgent",
            FactsCollected = 15,
            SearchQueriesExecuted = 8
        };

        // Act - Save checkpoint at step 3
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: workflowId,
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 3,
            stateSnapshot: JsonSerializer.Serialize(workflowState),
            metadata: new CheckpointMetadata
            {
                IsAutomated = true,
                Reason = "before-agent",
                CompletedAgents = new List<string> { "ClarifyAgent", "ResearchBriefAgent" }
            });

        // Simulate resumption - Load checkpoint
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);

        // Assert - Verify state matches
        Assert.NotNull(loadedCheckpoint);
        Assert.Equal(workflowId, loadedCheckpoint.WorkflowId);
        Assert.Equal(3, loadedCheckpoint.StepIndex);
        Assert.Equal("ResearcherAgent", loadedCheckpoint.AgentId);

        var loadedState = JsonDocument.Parse(loadedCheckpoint.StateSnapshot!);
        Assert.Equal("What is quantum computing?", loadedState.RootElement.GetProperty("CurrentQuestion").GetString());
        Assert.Equal(15, loadedState.RootElement.GetProperty("FactsCollected").GetInt32());
        Assert.Equal(8, loadedState.RootElement.GetProperty("SearchQueriesExecuted").GetInt32());
        Assert.Equal(2, loadedState.RootElement.GetProperty("CompletedAgents").GetArrayLength());
    }

    [Fact]
    public async Task MultiWorkflow_ConcurrentCheckpoints_IsolatesCorrectly()
    {
        // Arrange - Create multiple workflows running concurrently
        var workflows = new[]
        {
            new { Id = "wf_concurrent_1", Question = "What is AI?", Step = 2 },
            new { Id = "wf_concurrent_2", Question = "What is ML?", Step = 3 },
            new { Id = "wf_concurrent_3", Question = "What is DL?", Step = 1 }
        };

        // Act - Save checkpoints for all workflows
        var checkpointTasks = workflows.Select(async wf =>
        {
            return await _checkpointService.SaveCheckpointAsync(
                workflowId: wf.Id,
                workflowType: "ResearcherWorkflow",
                agentId: $"Agent_{wf.Step}",
                stepIndex: wf.Step,
                stateSnapshot: JsonSerializer.Serialize(new { Question = wf.Question }));
        });

        var checkpoints = await Task.WhenAll(checkpointTasks);

        // Assert - Verify each workflow has isolated checkpoints
        foreach (var workflow in workflows)
        {
            var workflowCheckpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflow.Id);
            Assert.Single(workflowCheckpoints);

            var checkpoint = workflowCheckpoints[0];
            Assert.Equal(workflow.Id, checkpoint.WorkflowId);
            Assert.Equal(workflow.Step, checkpoint.StepIndex);

            var state = JsonDocument.Parse(checkpoint.StateSnapshot!);
            Assert.Equal(workflow.Question, state.RootElement.GetProperty("Question").GetString());
        }
    }

    [Fact]
    public async Task StressTest_Save100Checkpoints_RetrievalPerformance()
    {
        // Arrange
        var workflowId = "wf_stress_test";
        var checkpoints = new List<WorkflowCheckpoint>();

        // Act - Save 100 checkpoints
        var saveStart = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            var checkpoint = await _checkpointService.SaveCheckpointAsync(
                workflowId: workflowId,
                workflowType: "TestWorkflow",
                agentId: $"Agent_{i}",
                stepIndex: i,
                stateSnapshot: JsonSerializer.Serialize(new { Index = i, Data = $"Checkpoint {i}" }));

            checkpoints.Add(checkpoint);
        }
        var saveElapsed = DateTime.UtcNow - saveStart;

        // Retrieve all checkpoints for workflow
        var retrieveStart = DateTime.UtcNow;
        var retrievedCheckpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);
        var retrieveElapsed = DateTime.UtcNow - retrieveStart;

        // Assert
        Assert.Equal(100, retrievedCheckpoints.Count);
        Assert.True(saveElapsed.TotalSeconds < 30, $"Save took {saveElapsed.TotalSeconds}s (should be < 30s)");
        Assert.True(retrieveElapsed.TotalSeconds < 5, $"Retrieve took {retrieveElapsed.TotalSeconds}s (should be < 5s)");

        // Verify statistics
        var stats = await _checkpointService.GetStatisticsAsync();
        Assert.Equal(100, stats.TotalCheckpoints);
        Assert.True(stats.TotalStorageUsedBytes > 0);
    }

    [Fact]
    public async Task EdgeCase_VeryLargeWorkflowState_HandlesGracefully()
    {
        // Arrange - Create a large but realistic workflow state (< 5MB)
        var largeFacts = Enumerable.Range(0, 5000).Select(i => new
        {
            Id = $"fact_{i}",
            Content = $"This is fact number {i} with detailed content about research topic. " +
                     $"It includes multiple sentences to simulate realistic fact data. " +
                     $"Facts are typically 100-300 characters in length.",
            Source = $"source_{i}.com",
            Confidence = 0.85,
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
            Tags = new[] { "quantum", "computing", "physics" }
        }).ToList();

        var largeState = new
        {
            Question = "Comprehensive research on quantum computing",
            Facts = largeFacts,
            Metadata = new { TotalFacts = largeFacts.Count, AverageConfidence = 0.85 }
        };

        var stateJson = JsonSerializer.Serialize(largeState);
        var stateSizeBytes = System.Text.Encoding.UTF8.GetByteCount(stateJson);

        // Act
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_large_state",
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 10,
            stateSnapshot: stateJson);

        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(loadedCheckpoint);
        Assert.True(stateSizeBytes > 1_000_000, "State should be > 1MB");
        Assert.True(stateSizeBytes < 5_000_000, "State should be < 5MB");
        Assert.Equal(checkpoint.StateSizeBytes, loadedCheckpoint.StateSizeBytes);

        var loadedState = JsonDocument.Parse(loadedCheckpoint.StateSnapshot!);
        Assert.Equal(5000, loadedState.RootElement.GetProperty("Facts").GetArrayLength());
    }

    [Fact]
    public async Task EdgeCase_MalformedCheckpointJson_ValidationDetectsIssue()
    {
        // Arrange - Create checkpoint then corrupt the file
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_corrupt_test",
            workflowType: "TestWorkflow",
            agentId: "TestAgent",
            stepIndex: 1,
            stateSnapshot: JsonSerializer.Serialize(new { test = "data" }));

        // Corrupt the checkpoint file
        var filePath = Path.Combine(_testDirectory, $"{checkpoint.CheckpointId}.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json content without closing");

        // Act
        var (isValid, errorMessage) = await _checkpointService.ValidateCheckpointAsync(checkpoint.CheckpointId);

        // Assert
        Assert.False(isValid);
        Assert.Contains("not valid JSON", errorMessage);
    }

    [Fact]
    public async Task ClockSkew_TimestampHandling_UsesUtc()
    {
        // Arrange & Act
        var beforeUtc = DateTime.UtcNow;

        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_timestamp_test",
            workflowType: "TestWorkflow",
            agentId: "TestAgent",
            stepIndex: 1,
            stateSnapshot: JsonSerializer.Serialize(new { test = "data" }));

        var afterUtc = DateTime.UtcNow;

        // Assert
        Assert.Equal(DateTimeKind.Utc, checkpoint.CreatedAt.Kind);
        Assert.True(checkpoint.CreatedAt >= beforeUtc);
        Assert.True(checkpoint.CreatedAt <= afterUtc);
    }

    [Fact]
    public async Task ResumeSemantics_LoadCheckpoint_SkipsCompletedAgents()
    {
        // Arrange - Save checkpoint with completed agents metadata
        var completedAgents = new List<string> { "ClarifyAgent", "ResearchBriefAgent" };
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_resume_semantics",
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 3,
            stateSnapshot: JsonSerializer.Serialize(new { currentStep = 3 }),
            metadata: new CheckpointMetadata
            {
                CompletedAgents = completedAgents,
                Reason = "user-pause"
            });

        // Act - Load checkpoint for resumption
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);

        // Assert - Verify resume metadata indicates which agents to skip
        Assert.NotNull(loadedCheckpoint);
        Assert.Equal(2, loadedCheckpoint.Metadata.CompletedAgents.Count);
        Assert.Contains("ClarifyAgent", loadedCheckpoint.Metadata.CompletedAgents);
        Assert.Contains("ResearchBriefAgent", loadedCheckpoint.Metadata.CompletedAgents);
        Assert.Equal(3, loadedCheckpoint.StepIndex);
        Assert.Equal("ResearcherAgent", loadedCheckpoint.AgentId);
    }

    [Fact]
    public async Task CheckpointGranularity_SaveAfterEachAgent_TracksProgress()
    {
        // Arrange - Simulate workflow with checkpoints after each agent
        var workflowId = "wf_granularity_test";
        var agents = new[] { "ClarifyAgent", "ResearchBriefAgent", "ResearcherAgent", "ReportAgent" };

        // Act - Save checkpoint after each agent completes
        var checkpoints = new List<WorkflowCheckpoint>();
        for (int i = 0; i < agents.Length; i++)
        {
            await Task.Delay(50); // Ensure different timestamps

            var checkpoint = await _checkpointService.SaveCheckpointAsync(
                workflowId: workflowId,
                workflowType: "ResearcherWorkflow",
                agentId: agents[i],
                stepIndex: i + 1,
                stateSnapshot: JsonSerializer.Serialize(new { completedAgents = agents.Take(i + 1).ToArray() }),
                metadata: new CheckpointMetadata
                {
                    IsAutomated = true,
                    Reason = "after-agent",
                    CompletedAgents = agents.Take(i + 1).ToList()
                });

            checkpoints.Add(checkpoint);
        }

        // Assert - Verify checkpoint history
        var workflowCheckpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);
        Assert.Equal(4, workflowCheckpoints.Count);

        // Verify ordered by recency (newest first)
        Assert.Equal("ReportAgent", workflowCheckpoints[0].AgentId);
        Assert.Equal("ResearcherAgent", workflowCheckpoints[1].AgentId);
        Assert.Equal("ResearchBriefAgent", workflowCheckpoints[2].AgentId);
        Assert.Equal("ClarifyAgent", workflowCheckpoints[3].AgentId);

        // Verify each checkpoint has correct completed agents
        Assert.Single(workflowCheckpoints[3].Metadata.CompletedAgents);
        Assert.Equal(2, workflowCheckpoints[2].Metadata.CompletedAgents.Count);
        Assert.Equal(3, workflowCheckpoints[1].Metadata.CompletedAgents.Count);
        Assert.Equal(4, workflowCheckpoints[0].Metadata.CompletedAgents.Count);
    }

    [Fact]
    public async Task ErrorRecovery_CheckpointBeforeFailure_EnablesRetry()
    {
        // Arrange - Save checkpoint before potentially failing operation
        var workflowId = "wf_error_recovery";
        var preErrorState = new
        {
            CurrentAgent = "ResearcherAgent",
            AttemptNumber = 1,
            LastSuccessfulQuery = "quantum computing basics"
        };

        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: workflowId,
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 5,
            stateSnapshot: JsonSerializer.Serialize(preErrorState),
            metadata: new CheckpointMetadata
            {
                IsAutomated = true,
                Reason = "before-risky-operation",
                Context = new Dictionary<string, string>
                {
                    { "operation", "external-api-call" },
                    { "risk-level", "high" }
                }
            });

        // Simulate error occurred, need to retry from checkpoint
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);

        // Assert - Can resume from safe checkpoint
        Assert.NotNull(loadedCheckpoint);
        Assert.Equal("before-risky-operation", loadedCheckpoint.Metadata.Reason);
        Assert.Equal("external-api-call", loadedCheckpoint.Metadata.Context["operation"]);

        var loadedState = JsonDocument.Parse(loadedCheckpoint.StateSnapshot!);
        Assert.Equal(1, loadedState.RootElement.GetProperty("AttemptNumber").GetInt32());
        Assert.Equal("quantum computing basics", loadedState.RootElement.GetProperty("LastSuccessfulQuery").GetString());
    }

    [Fact]
    public async Task PerformanceBaseline_SaveLoadCycle_MeetsCriteria()
    {
        // Arrange - Typical workflow state (not minimal, not huge)
        var typicalState = new
        {
            Question = "What is quantum computing?",
            BriefCompleted = true,
            Facts = Enumerable.Range(0, 50).Select(i => new
            {
                Id = $"fact_{i}",
                Content = $"Fact content {i}",
                Source = $"source_{i}.com"
            }).ToArray(),
            AgentStates = new Dictionary<string, object>
            {
                { "ClarifyAgent", new { Status = "completed" } },
                { "ResearchBriefAgent", new { Status = "completed" } },
                { "ResearcherAgent", new { Status = "in-progress", IterationCount = 3 } }
            }
        };

        var stateJson = JsonSerializer.Serialize(typicalState);

        // Act & Measure
        var saveStart = DateTime.UtcNow;
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_perf_baseline",
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 3,
            stateSnapshot: stateJson);
        var saveElapsed = DateTime.UtcNow - saveStart;

        var loadStart = DateTime.UtcNow;
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);
        var loadElapsed = DateTime.UtcNow - loadStart;

        // Assert - Meet performance criteria from spike document
        // Success Criteria: Serialize < 100ms, Write < 500ms, Read < 200ms
        Assert.True(saveElapsed.TotalMilliseconds < 500, $"Save took {saveElapsed.TotalMilliseconds}ms (should be < 500ms)");
        Assert.True(loadElapsed.TotalMilliseconds < 200, $"Load took {loadElapsed.TotalMilliseconds}ms (should be < 200ms)");
        Assert.NotNull(loadedCheckpoint);
        Assert.True(checkpoint.StateSizeBytes < 5 * 1024 * 1024, $"Checkpoint size {checkpoint.StateSizeBytes} bytes (should be < 5MB)");
    }
}
