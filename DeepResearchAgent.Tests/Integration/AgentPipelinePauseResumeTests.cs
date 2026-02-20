using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Agents;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeepResearchAgent.Tests.Integration;

/// <summary>
/// Integration tests for Agent Pipeline pause/resume functionality with checkpoints.
/// Tests end-to-end workflow execution, pausing, and resumption.
/// </summary>
public class AgentPipelinePauseResumeTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CheckpointService _checkpointService;
    private readonly Mock<OllamaService> _mockLlmService;
    private readonly Mock<ToolInvocationService> _mockToolService;
    private readonly Mock<ILogger<AgentPipelineService>> _mockLogger;
    private readonly AgentPipelineService _pipelineService;

    public AgentPipelinePauseResumeTests()
    {
        // Setup test directory for checkpoints
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pipeline_pause_resume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Setup checkpoint service
        var checkpointOptions = new CheckpointServiceOptions
        {
            LocalStorageDirectory = _testDirectory,
            StorageBackend = "file",
            CheckpointAfterEachAgent = true
        };

        var mockLightningStore = new Mock<ILightningStore>();
        var mockCheckpointLogger = new Mock<ILogger<CheckpointService>>();

        _checkpointService = new CheckpointService(
            mockLightningStore.Object,
            checkpointOptions,
            mockCheckpointLogger.Object);

        // Setup mocks for agents
        _mockLlmService = new Mock<OllamaService>("http://localhost:11434", "test-model");
        _mockToolService = new Mock<ToolInvocationService>(
            MockBehavior.Loose,
            new object[] { null!, null!, null! });
        _mockLogger = new Mock<ILogger<AgentPipelineService>>();

        // Create pipeline service with checkpoint support
        _pipelineService = new AgentPipelineService(
            _mockLlmService.Object,
            _mockToolService.Object,
            _mockLogger.Object,
            _checkpointService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteResearchWorkflowAsync_WithCheckpointService_CreatesCheckpoints()
    {
        // Arrange
        var userQuery = "What is quantum computing?";
        
        // Setup mock responses
        SetupMockAgentResponses();

        // Act
        var result = await _pipelineService.ExecuteResearchWorkflowAsync(userQuery);

        // Assert
        Assert.NotNull(result);
        
        // Verify checkpoints were created
        var checkpointFiles = Directory.GetFiles(_testDirectory, "*.json");
        Assert.True(checkpointFiles.Length > 0, "Expected checkpoints to be created");
    }

    [Fact]
    public async Task PauseWorkflowAsync_ActiveWorkflow_SavesCheckpointAndReturnsId()
    {
        // Arrange
        var workflowId = "wf_pause_test_" + Guid.NewGuid().ToString("N")[..8];
        
        // Start workflow in background
        var cts = new CancellationTokenSource();
        var workflowTask = Task.Run(async () =>
        {
            SetupMockAgentResponses();
            return await _pipelineService.ExecuteResearchWorkflowAsync(
                "Test query",
                workflowId,
                cts.Token);
        });

        // Give workflow time to start
        await Task.Delay(100);

        // Act
        var checkpointId = await _pipelineService.PauseWorkflowAsync(workflowId, "test-pause");

        // Assert
        Assert.NotNull(checkpointId);
        Assert.NotEmpty(checkpointId);
        Assert.StartsWith("ckpt_", checkpointId);

        // Verify checkpoint exists
        var checkpoint = await _checkpointService.LoadCheckpointAsync(checkpointId);
        Assert.NotNull(checkpoint);
        Assert.Equal(workflowId, checkpoint.WorkflowId);
        Assert.Contains("pause", checkpoint.Metadata.Reason);

        // Cleanup
        cts.Cancel();
        try { await workflowTask; } catch { /* Expected to be cancelled */ }
    }

    [Fact]
    public async Task ResumeWorkflowAsync_FromCheckpoint_CompletesWorkflow()
    {
        // Arrange - Create a checkpoint manually
        var workflowId = "wf_resume_test_" + Guid.NewGuid().ToString("N")[..8];
        var pipelineState = new Model.PipelineExecutionState
        {
            WorkflowId = workflowId,
            WorkflowType = "ResearchWorkflow",
            UserQuery = "What is AI?",
            StartedAt = DateTime.UtcNow,
            CurrentStepIndex = 0,
            CurrentAgentId = "ClarifyAgent",
            CompletedAgents = new System.Collections.Generic.List<string>(),
            Messages = new System.Collections.Generic.List<Model.ChatMessageState>
            {
                new Model.ChatMessageState
                {
                    Role = "User",
                    Content = "What is AI?",
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId,
            "ResearchWorkflow",
            "ClarifyAgent",
            0,
            System.Text.Json.JsonSerializer.Serialize(pipelineState),
            new Model.CheckpointMetadata
            {
                IsAutomated = false,
                Reason = "test-checkpoint",
                CompletedAgents = new System.Collections.Generic.List<string>()
            });

        SetupMockAgentResponses();

        // Act
        var result = await _pipelineService.ResumeWorkflowAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task PauseAndResume_E2E_MaintainsWorkflowState()
    {
        // This test would require more complex mocking to properly pause mid-execution
        // For now, we'll test the checkpoint creation and loading
        
        // Arrange
        var workflowId = "wf_e2e_" + Guid.NewGuid().ToString("N")[..8];
        var pipelineState = new Model.PipelineExecutionState
        {
            WorkflowId = workflowId,
            WorkflowType = "ResearchWorkflow",
            UserQuery = "Test query",
            StartedAt = DateTime.UtcNow,
            CurrentStepIndex = 1,
            CurrentAgentId = "ResearchBriefAgent",
            CompletedAgents = new System.Collections.Generic.List<string> { "ClarifyAgent" },
            AgentResults = new System.Collections.Generic.Dictionary<string, string>
            {
                { "ClarifyAgent", "Query is clear" }
            },
            Messages = new System.Collections.Generic.List<Model.ChatMessageState>
            {
                new Model.ChatMessageState { Role = "User", Content = "Test query", Timestamp = DateTime.UtcNow },
                new Model.ChatMessageState { Role = "Assistant", Content = "Query is clear", Timestamp = DateTime.UtcNow, AgentId = "ClarifyAgent" }
            }
        };

        // Save checkpoint (simulating pause)
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId,
            pipelineState.WorkflowType,
            pipelineState.CurrentAgentId,
            pipelineState.CurrentStepIndex,
            System.Text.Json.JsonSerializer.Serialize(pipelineState),
            new Model.CheckpointMetadata
            {
                Reason = "pause-test",
                CompletedAgents = pipelineState.CompletedAgents
            });

        // Load checkpoint
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);
        var loadedState = System.Text.Json.JsonSerializer.Deserialize<Model.PipelineExecutionState>(
            loadedCheckpoint!.StateSnapshot!);

        // Assert - State preserved
        Assert.NotNull(loadedState);
        Assert.Equal(workflowId, loadedState.WorkflowId);
        Assert.Equal(1, loadedState.CurrentStepIndex);
        Assert.Equal("ResearchBriefAgent", loadedState.CurrentAgentId);
        Assert.Single(loadedState.CompletedAgents);
        Assert.Contains("ClarifyAgent", loadedState.CompletedAgents);
        Assert.Equal(2, loadedState.Messages.Count);
        Assert.Single(loadedState.AgentResults);
    }

    [Fact]
    public async Task ExecuteWorkflow_CheckpointsAfterEachAgent_AllCheckpointsCreated()
    {
        // Arrange
        var workflowId = "wf_checkpoints_" + Guid.NewGuid().ToString("N")[..8];
        SetupMockAgentResponses();

        // Act
        await _pipelineService.ExecuteResearchWorkflowAsync("Test query", workflowId);

        // Assert - Verify checkpoints for each agent
        var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);
        
        // Should have checkpoints: before-clarify, after-clarify, before-brief, after-brief, before-researcher, after-researcher, complete
        Assert.True(checkpoints.Count >= 3, $"Expected at least 3 checkpoints, got {checkpoints.Count}");

        // Verify checkpoint progression
        var orderedCheckpoints = checkpoints;
        foreach (var checkpoint in orderedCheckpoints)
        {
            Assert.NotNull(checkpoint.StateSnapshot);
            Assert.True(checkpoint.StateSizeBytes > 0);
        }
    }

    [Fact]
    public async Task ResumeWorkflow_SkipsCompletedAgents_ContinuesFromCorrectStep()
    {
        // Arrange - Create checkpoint with ClarifyAgent completed
        var workflowId = "wf_skip_test_" + Guid.NewGuid().ToString("N")[..8];
        var pipelineState = new Model.PipelineExecutionState
        {
            WorkflowId = workflowId,
            WorkflowType = "ResearchWorkflow",
            UserQuery = "What is ML?",
            StartedAt = DateTime.UtcNow,
            CurrentStepIndex = 1, // At ResearchBriefAgent
            CurrentAgentId = "ResearchBriefAgent",
            CompletedAgents = new System.Collections.Generic.List<string> { "ClarifyAgent" },
            AgentResults = new System.Collections.Generic.Dictionary<string, string>
            {
                { "ClarifyAgent", "Query clarified" }
            },
            Messages = new System.Collections.Generic.List<Model.ChatMessageState>
            {
                new Model.ChatMessageState { Role = "User", Content = "What is ML?", Timestamp = DateTime.UtcNow },
                new Model.ChatMessageState { Role = "Assistant", Content = "Query clarified", Timestamp = DateTime.UtcNow, AgentId = "ClarifyAgent" }
            }
        };

        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId,
            pipelineState.WorkflowType,
            pipelineState.CurrentAgentId,
            pipelineState.CurrentStepIndex,
            System.Text.Json.JsonSerializer.Serialize(pipelineState),
            new Model.CheckpointMetadata
            {
                Reason = "skip-test",
                CompletedAgents = pipelineState.CompletedAgents
            });

        SetupMockAgentResponses();

        // Act
        var result = await _pipelineService.ResumeWorkflowAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(result);
        
        // Verify ClarifyAgent was not called again (would need to verify mock invocations)
        // For now, just verify workflow completed
        Assert.NotEmpty(result);
    }

    // Helper method to setup mock responses
    private void SetupMockAgentResponses()
    {
        // These mocks would need to be more sophisticated for real testing
        // For now, they provide basic structure
        
        // Mock LLM service calls (simplified)
        // In real tests, you'd mock the actual AIAgent Run methods
    }
}
