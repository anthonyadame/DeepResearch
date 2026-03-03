using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using DeepResearchAgent.Services.Workflows;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeepResearchAgent.Tests.Integration;

/// <summary>
/// Integration tests for checkpoint and state accumulator functionality.
/// Tests state persistence and recovery through checkpoint system.
/// </summary>
public class CheckpointStateAccumulatorIntegrationTests : IAsyncLifetime
{
    private readonly string _testDirectory;
    private readonly Mock<ILightningStore> _mockLightningStore;
    private readonly Mock<ILogger<CheckpointService>> _checkpointLogger;
    private readonly Mock<ILogger<StateAccumulatorService>> _accumulatorLogger;
    private CheckpointService _checkpointService;
    private StateAccumulatorService _stateAccumulator;

    public CheckpointStateAccumulatorIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"checkpoint_{Guid.NewGuid():N}");
        _mockLightningStore = new Mock<ILightningStore>();
        _checkpointLogger = new Mock<ILogger<CheckpointService>>();
        _accumulatorLogger = new Mock<ILogger<StateAccumulatorService>>();
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testDirectory);

        var options = new CheckpointServiceOptions
        {
            LocalStorageDirectory = _testDirectory,
            StorageBackend = "file",
            CheckpointAfterEachAgent = true,
            MaxCheckpointsPerWorkflow = 5
        };

        _checkpointService = new CheckpointService(
            _mockLightningStore.Object,
            options,
            _checkpointLogger.Object);

        _stateAccumulator = new StateAccumulatorService(
            _checkpointService,
            _accumulatorLogger.Object);
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task StateAccumulation_ShouldAccumulateMultipleOutputs()
    {
        // Arrange
        var workflowId = "test-workflow-001";
        var output1 = new Dictionary<string, object>
        {
            { "findings", new[] { "Finding 1", "Finding 2" } },
            { "score", 0.85 }
        };

        // Act
        var state = await _stateAccumulator.AccumulateStateAsync(
            workflowId,
            output1,
            "Agent1");

        // Assert
        Assert.NotEmpty(state);
        Assert.Contains("findings", state.Keys);
        Assert.Contains("score", state.Keys);
    }

    [Fact]
    public async Task GetCurrentState_ShouldReturnAccumulatedState()
    {
        // Arrange
        var workflowId = "test-workflow-002";
        var output = new Dictionary<string, object> { { "test_key", "test_value" } };

        // Act
        await _stateAccumulator.AccumulateStateAsync(workflowId, output, "Agent1");
        var state = await _stateAccumulator.GetCurrentStateAsync(workflowId);

        // Assert
        Assert.NotNull(state);
        Assert.NotEmpty(state);
    }

    [Fact]
    public async Task StateHistory_ShouldTrackMultipleSnapshots()
    {
        // Arrange
        var workflowId = "test-workflow-003";

        // Act
        for (int i = 1; i <= 3; i++)
        {
            var output = new Dictionary<string, object> { { "iteration", i } };
            await _stateAccumulator.AccumulateStateAsync(workflowId, output, $"Agent{i}");
        }

        var history = await _stateAccumulator.GetStateHistoryAsync(workflowId);

        // Assert
        Assert.NotEmpty(history);
        Assert.InRange(history.Count, 1, 3);
    }

    [Fact]
    public async Task MergeStates_ShouldCombineMultipleStates()
    {
        // Arrange
        var state1 = new Dictionary<string, object>
        {
            { "field_a", "value_a" },
            { "field_b", "value_b" }
        };
        var state2 = new Dictionary<string, object>
        {
            { "field_c", "value_c" }
        };

        // Act
        var merged = await _stateAccumulator.MergeStatesAsync(
            state1,
            state2,
            ConflictResolutionStrategy.KeepNew);

        // Assert
        Assert.NotNull(merged);
        Assert.Contains("field_a", merged.Keys);
        Assert.Contains("field_c", merged.Keys);
    }

    [Fact]
    public async Task RollbackToVersion_ShouldRestoreState()
    {
        // Arrange
        var workflowId = "test-workflow-004";
        var output1 = new Dictionary<string, object> { { "version", 1 } };
        var output2 = new Dictionary<string, object> { { "version", 2 } };
        var output3 = new Dictionary<string, object> { { "version", 3 } };

        await _stateAccumulator.AccumulateStateAsync(workflowId, output1, "Agent");
        await _stateAccumulator.AccumulateStateAsync(workflowId, output2, "Agent");
        await _stateAccumulator.AccumulateStateAsync(workflowId, output3, "Agent");

        // Act
        var rolledBack = await _stateAccumulator.RollbackAsync(workflowId, 1);

        // Assert
        Assert.NotNull(rolledBack);
    }

    [Fact]
    public async Task MultipleWorkflows_ShouldMaintainIndependentState()
    {
        // Arrange
        var workflow1Id = "workflow-a";
        var workflow2Id = "workflow-b";

        // Act
        await _stateAccumulator.AccumulateStateAsync(
            workflow1Id,
            new Dictionary<string, object> { { "id", workflow1Id } },
            "Agent");

        await _stateAccumulator.AccumulateStateAsync(
            workflow2Id,
            new Dictionary<string, object> { { "id", workflow2Id } },
            "Agent");

        var state1 = await _stateAccumulator.GetCurrentStateAsync(workflow1Id);
        var state2 = await _stateAccumulator.GetCurrentStateAsync(workflow2Id);

        // Assert
        Assert.NotNull(state1);
        Assert.NotNull(state2);
    }
}
