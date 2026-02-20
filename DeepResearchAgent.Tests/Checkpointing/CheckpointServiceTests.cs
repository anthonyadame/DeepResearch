using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeepResearchAgent.Tests.Checkpointing;

/// <summary>
/// Unit tests for CheckpointService.
/// Validates checkpoint save, load, delete, and query operations.
/// </summary>
public class CheckpointServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CheckpointService _checkpointService;
    private readonly Mock<ILightningStore> _mockLightningStore;
    private readonly Mock<ILogger<CheckpointService>> _mockLogger;
    private readonly CheckpointServiceOptions _options;

    public CheckpointServiceTests()
    {
        // Setup test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"checkpoint_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Setup mocks
        _mockLightningStore = new Mock<ILightningStore>();
        _mockLogger = new Mock<ILogger<CheckpointService>>();

        // Setup options
        _options = new CheckpointServiceOptions
        {
            LocalStorageDirectory = _testDirectory,
            MaxCheckpointSizeBytes = 1024 * 1024, // 1MB for tests
            StorageBackend = "file" // Use file storage for tests
        };

        // Create service
        _checkpointService = new CheckpointService(
            _mockLightningStore.Object,
            _options,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCheckpointAsync_ValidCheckpoint_SavesSuccessfully()
    {
        // Arrange
        var workflowId = "wf_test_123";
        var workflowType = "ResearcherWorkflow";
        var agentId = "ResearchBriefAgent";
        var stepIndex = 3;
        var stateSnapshot = JsonSerializer.Serialize(new { question = "What is quantum computing?" });
        var metadata = new CheckpointMetadata
        {
            IsAutomated = true,
            Reason = "scheduled"
        };

        // Act
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId,
            workflowType,
            agentId,
            stepIndex,
            stateSnapshot,
            metadata);

        // Assert
        Assert.NotNull(checkpoint);
        Assert.NotEmpty(checkpoint.CheckpointId);
        Assert.Equal(workflowId, checkpoint.WorkflowId);
        Assert.Equal(workflowType, checkpoint.WorkflowType);
        Assert.Equal(agentId, checkpoint.AgentId);
        Assert.Equal(stepIndex, checkpoint.StepIndex);
        Assert.Equal(stateSnapshot, checkpoint.StateSnapshot);
        Assert.True(checkpoint.StateSizeBytes > 0);
        Assert.Equal(metadata.IsAutomated, checkpoint.Metadata.IsAutomated);
        Assert.Equal(metadata.Reason, checkpoint.Metadata.Reason);

        // Verify file was created
        var expectedFile = Path.Combine(_testDirectory, $"{checkpoint.CheckpointId}.json");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task SaveCheckpointAsync_NullWorkflowId_ThrowsArgumentException()
    {
        // Arrange
        var stateSnapshot = JsonSerializer.Serialize(new { test = "data" });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _checkpointService.SaveCheckpointAsync(
                workflowId: null!,
                workflowType: "TestWorkflow",
                agentId: "TestAgent",
                stepIndex: 0,
                stateSnapshot: stateSnapshot));
    }

    [Fact]
    public async Task SaveCheckpointAsync_EmptyWorkflowType_ThrowsArgumentException()
    {
        // Arrange
        var stateSnapshot = JsonSerializer.Serialize(new { test = "data" });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _checkpointService.SaveCheckpointAsync(
                workflowId: "wf_123",
                workflowType: "",
                agentId: "TestAgent",
                stepIndex: 0,
                stateSnapshot: stateSnapshot));
    }

    [Fact]
    public async Task SaveCheckpointAsync_ExceedsMaxSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var largeState = new string('x', 2 * 1024 * 1024); // 2MB (exceeds 1MB limit)

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checkpointService.SaveCheckpointAsync(
                workflowId: "wf_123",
                workflowType: "TestWorkflow",
                agentId: "TestAgent",
                stepIndex: 0,
                stateSnapshot: largeState));
    }

    [Fact]
    public async Task LoadCheckpointAsync_ExistingCheckpoint_LoadsSuccessfully()
    {
        // Arrange
        var savedCheckpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_load_test",
            workflowType: "TestWorkflow",
            agentId: "TestAgent",
            stepIndex: 5,
            stateSnapshot: JsonSerializer.Serialize(new { step = 5 }));

        // Act
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(savedCheckpoint.CheckpointId);

        // Assert
        Assert.NotNull(loadedCheckpoint);
        Assert.Equal(savedCheckpoint.CheckpointId, loadedCheckpoint.CheckpointId);
        Assert.Equal(savedCheckpoint.WorkflowId, loadedCheckpoint.WorkflowId);
        Assert.Equal(savedCheckpoint.WorkflowType, loadedCheckpoint.WorkflowType);
        Assert.Equal(savedCheckpoint.AgentId, loadedCheckpoint.AgentId);
        Assert.Equal(savedCheckpoint.StepIndex, loadedCheckpoint.StepIndex);
        Assert.Equal(savedCheckpoint.StateSnapshot, loadedCheckpoint.StateSnapshot);
    }

    [Fact]
    public async Task LoadCheckpointAsync_NonExistentCheckpoint_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "ckpt_nonexistent_12345678";

        // Act
        var checkpoint = await _checkpointService.LoadCheckpointAsync(nonExistentId);

        // Assert
        Assert.Null(checkpoint);
    }

    [Fact]
    public async Task LoadCheckpointAsync_NullCheckpointId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _checkpointService.LoadCheckpointAsync(null!));
    }

    [Fact]
    public async Task GetCheckpointsForWorkflowAsync_MultipleCheckpoints_ReturnsAllInOrder()
    {
        // Arrange
        var workflowId = "wf_multi_test";
        var checkpoint1 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { step = 1 }));

        await Task.Delay(100); // Ensure different timestamps

        var checkpoint2 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent2", 2,
            JsonSerializer.Serialize(new { step = 2 }));

        await Task.Delay(100);

        var checkpoint3 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent3", 3,
            JsonSerializer.Serialize(new { step = 3 }));

        // Act
        var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);

        // Assert
        Assert.Equal(3, checkpoints.Count);
        // Should be ordered by creation time (most recent first)
        Assert.Equal(checkpoint3.CheckpointId, checkpoints[0].CheckpointId);
        Assert.Equal(checkpoint2.CheckpointId, checkpoints[1].CheckpointId);
        Assert.Equal(checkpoint1.CheckpointId, checkpoints[2].CheckpointId);
    }

    [Fact]
    public async Task GetCheckpointsForWorkflowAsync_NoCheckpoints_ReturnsEmptyList()
    {
        // Arrange
        var workflowId = "wf_nonexistent";

        // Act
        var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);

        // Assert
        Assert.NotNull(checkpoints);
        Assert.Empty(checkpoints);
    }

    [Fact]
    public async Task GetCheckpointsForWorkflowAsync_FiltersCorrectly()
    {
        // Arrange
        var workflowId1 = "wf_filter_test_1";
        var workflowId2 = "wf_filter_test_2";

        await _checkpointService.SaveCheckpointAsync(
            workflowId1, "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { step = 1 }));

        await _checkpointService.SaveCheckpointAsync(
            workflowId2, "TestWorkflow", "Agent2", 1,
            JsonSerializer.Serialize(new { step = 1 }));

        await _checkpointService.SaveCheckpointAsync(
            workflowId1, "TestWorkflow", "Agent3", 2,
            JsonSerializer.Serialize(new { step = 2 }));

        // Act
        var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId1);

        // Assert
        Assert.Equal(2, checkpoints.Count);
        Assert.All(checkpoints, cp => Assert.Equal(workflowId1, cp.WorkflowId));
    }

    [Fact]
    public async Task GetLatestCheckpointAsync_ReturnsNewestCheckpoint()
    {
        // Arrange
        var workflowId = "wf_latest_test";

        await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { step = 1 }));

        await Task.Delay(100);

        await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent2", 2,
            JsonSerializer.Serialize(new { step = 2 }));

        await Task.Delay(100);

        var newestCheckpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent3", 3,
            JsonSerializer.Serialize(new { step = 3 }));

        // Act
        var latestCheckpoint = await _checkpointService.GetLatestCheckpointAsync(workflowId);

        // Assert
        Assert.NotNull(latestCheckpoint);
        Assert.Equal(newestCheckpoint.CheckpointId, latestCheckpoint.CheckpointId);
        Assert.Equal(3, latestCheckpoint.StepIndex);
    }

    [Fact]
    public async Task GetLatestCheckpointAsync_NoCheckpoints_ReturnsNull()
    {
        // Arrange
        var workflowId = "wf_nonexistent";

        // Act
        var checkpoint = await _checkpointService.GetLatestCheckpointAsync(workflowId);

        // Assert
        Assert.Null(checkpoint);
    }

    [Fact]
    public async Task DeleteCheckpointAsync_ExistingCheckpoint_DeletesSuccessfully()
    {
        // Arrange
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_delete_test",
            workflowType: "TestWorkflow",
            agentId: "TestAgent",
            stepIndex: 1,
            stateSnapshot: JsonSerializer.Serialize(new { test = "data" }));

        var filePath = Path.Combine(_testDirectory, $"{checkpoint.CheckpointId}.json");
        Assert.True(File.Exists(filePath));

        // Act
        await _checkpointService.DeleteCheckpointAsync(checkpoint.CheckpointId);

        // Assert
        Assert.False(File.Exists(filePath));
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);
        Assert.Null(loadedCheckpoint);
    }

    [Fact]
    public async Task DeleteCheckpointsForWorkflowAsync_DeletesAllCheckpoints()
    {
        // Arrange
        var workflowId = "wf_delete_all_test";

        var checkpoint1 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { step = 1 }));

        var checkpoint2 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent2", 2,
            JsonSerializer.Serialize(new { step = 2 }));

        var checkpoint3 = await _checkpointService.SaveCheckpointAsync(
            workflowId, "TestWorkflow", "Agent3", 3,
            JsonSerializer.Serialize(new { step = 3 }));

        // Act
        await _checkpointService.DeleteCheckpointsForWorkflowAsync(workflowId);

        // Assert
        var checkpoints = await _checkpointService.GetCheckpointsForWorkflowAsync(workflowId);
        Assert.Empty(checkpoints);

        Assert.Null(await _checkpointService.LoadCheckpointAsync(checkpoint1.CheckpointId));
        Assert.Null(await _checkpointService.LoadCheckpointAsync(checkpoint2.CheckpointId));
        Assert.Null(await _checkpointService.LoadCheckpointAsync(checkpoint3.CheckpointId));
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsAccurateStats()
    {
        // Arrange
        await _checkpointService.SaveCheckpointAsync(
            "wf_stats_1", "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { data = new string('x', 100) }));

        await _checkpointService.SaveCheckpointAsync(
            "wf_stats_2", "TestWorkflow", "Agent2", 2,
            JsonSerializer.Serialize(new { data = new string('x', 200) }));

        await _checkpointService.SaveCheckpointAsync(
            "wf_stats_3", "TestWorkflow", "Agent3", 3,
            JsonSerializer.Serialize(new { data = new string('x', 300) }));

        // Act
        var stats = await _checkpointService.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalCheckpoints);
        Assert.True(stats.AverageCheckpointSizeBytes > 0);
        Assert.True(stats.LargestCheckpointSizeBytes >= stats.AverageCheckpointSizeBytes);
        Assert.True(stats.TotalStorageUsedBytes > 0);
        Assert.Equal(3, stats.RecentCheckpointsCount); // All created recently
    }

    [Fact]
    public async Task ValidateCheckpointAsync_ValidCheckpoint_ReturnsTrue()
    {
        // Arrange
        var checkpoint = await _checkpointService.SaveCheckpointAsync(
            "wf_validate_test", "TestWorkflow", "Agent1", 1,
            JsonSerializer.Serialize(new { valid = "json" }));

        // Act
        var (isValid, errorMessage) = await _checkpointService.ValidateCheckpointAsync(checkpoint.CheckpointId);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task ValidateCheckpointAsync_NonExistentCheckpoint_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = "ckpt_nonexistent_12345678";

        // Act
        var (isValid, errorMessage) = await _checkpointService.ValidateCheckpointAsync(nonExistentId);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Checkpoint not found", errorMessage);
    }

    [Fact]
    public async Task SaveAndLoadCheckpoint_PreservesAllFields()
    {
        // Arrange
        var metadata = new CheckpointMetadata
        {
            IsAutomated = false,
            Reason = "user-pause",
            UserId = "user_123",
            Context = new Dictionary<string, string>
            {
                { "phase", "research" },
                { "progress", "50%" }
            },
            CompletedAgents = new List<string> { "ClarifyAgent", "ResearchBriefAgent" }
        };

        var originalCheckpoint = await _checkpointService.SaveCheckpointAsync(
            workflowId: "wf_preserve_test",
            workflowType: "ResearcherWorkflow",
            agentId: "ResearcherAgent",
            stepIndex: 7,
            stateSnapshot: JsonSerializer.Serialize(new
            {
                question = "What is quantum computing?",
                facts = new[] { "fact1", "fact2", "fact3" }
            }),
            metadata: metadata);

        // Act
        var loadedCheckpoint = await _checkpointService.LoadCheckpointAsync(originalCheckpoint.CheckpointId);

        // Assert
        Assert.NotNull(loadedCheckpoint);
        Assert.Equal(originalCheckpoint.WorkflowId, loadedCheckpoint.WorkflowId);
        Assert.Equal(originalCheckpoint.WorkflowType, loadedCheckpoint.WorkflowType);
        Assert.Equal(originalCheckpoint.AgentId, loadedCheckpoint.AgentId);
        Assert.Equal(originalCheckpoint.StepIndex, loadedCheckpoint.StepIndex);
        Assert.Equal(originalCheckpoint.StateSnapshot, loadedCheckpoint.StateSnapshot);
        Assert.Equal(originalCheckpoint.SchemaVersion, loadedCheckpoint.SchemaVersion);

        Assert.Equal(metadata.IsAutomated, loadedCheckpoint.Metadata.IsAutomated);
        Assert.Equal(metadata.Reason, loadedCheckpoint.Metadata.Reason);
        Assert.Equal(metadata.UserId, loadedCheckpoint.Metadata.UserId);
        Assert.Equal(2, loadedCheckpoint.Metadata.Context.Count);
        Assert.Equal("research", loadedCheckpoint.Metadata.Context["phase"]);
        Assert.Equal(2, loadedCheckpoint.Metadata.CompletedAgents.Count);
        Assert.Contains("ClarifyAgent", loadedCheckpoint.Metadata.CompletedAgents);
    }

    [Fact]
    public async Task ConcurrentSaveOperations_HandlesMultipleWorkflows()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            return await _checkpointService.SaveCheckpointAsync(
                workflowId: $"wf_concurrent_{i}",
                workflowType: "TestWorkflow",
                agentId: $"Agent{i}",
                stepIndex: i,
                stateSnapshot: JsonSerializer.Serialize(new { index = i }));
        });

        // Act
        var checkpoints = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, checkpoints.Length);
        Assert.Equal(10, checkpoints.Select(cp => cp.CheckpointId).Distinct().Count());

        // Verify all checkpoints can be loaded
        foreach (var checkpoint in checkpoints)
        {
            var loaded = await _checkpointService.LoadCheckpointAsync(checkpoint.CheckpointId);
            Assert.NotNull(loaded);
        }
    }
}
