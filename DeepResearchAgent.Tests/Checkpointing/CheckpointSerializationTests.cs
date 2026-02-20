using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using Xunit;

namespace DeepResearchAgent.Tests.Checkpointing;

/// <summary>
/// Tests for checkpoint serialization and deserialization.
/// Validates that complex workflow state can be round-tripped through JSON without data loss.
/// </summary>
public class CheckpointSerializationTests
{
    [Fact]
    public void SerializeCheckpoint_AllFields_SerializesCorrectly()
    {
        // Arrange
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_20240115_abc123",
            WorkflowId = "wf_20240115_xyz789",
            WorkflowType = "ResearcherWorkflow",
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc),
            AgentId = "ResearchBriefAgent",
            StepIndex = 3,
            StateSnapshot = JsonSerializer.Serialize(new { question = "What is quantum computing?" }),
            SchemaVersion = 1,
            StateSizeBytes = 1024,
            Label = "Before research phase",
            Metadata = new CheckpointMetadata
            {
                IsAutomated = true,
                Reason = "scheduled",
                UserId = "user_123",
                Context = new Dictionary<string, string>
                {
                    { "phase", "research" },
                    { "progress", "50%" }
                },
                CompletedAgents = new List<string> { "ClarifyAgent", "ResearchBriefAgent" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(checkpoint.CheckpointId, deserialized.CheckpointId);
        Assert.Equal(checkpoint.WorkflowId, deserialized.WorkflowId);
        Assert.Equal(checkpoint.WorkflowType, deserialized.WorkflowType);
        Assert.Equal(checkpoint.CreatedAt, deserialized.CreatedAt);
        Assert.Equal(checkpoint.AgentId, deserialized.AgentId);
        Assert.Equal(checkpoint.StepIndex, deserialized.StepIndex);
        Assert.Equal(checkpoint.StateSnapshot, deserialized.StateSnapshot);
        Assert.Equal(checkpoint.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(checkpoint.StateSizeBytes, deserialized.StateSizeBytes);
        Assert.Equal(checkpoint.Label, deserialized.Label);
    }

    [Fact]
    public void SerializeCheckpointMetadata_ComplexData_PreservesAllFields()
    {
        // Arrange
        var metadata = new CheckpointMetadata
        {
            IsAutomated = false,
            Reason = "user-pause",
            UserId = "user_456",
            Context = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "error", "Connection timeout" }
            },
            CompletedAgents = new List<string>
            {
                "ClarifyAgent",
                "ResearchBriefAgent",
                "ResearcherAgent"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(metadata);
        var deserialized = JsonSerializer.Deserialize<CheckpointMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(metadata.IsAutomated, deserialized.IsAutomated);
        Assert.Equal(metadata.Reason, deserialized.Reason);
        Assert.Equal(metadata.UserId, deserialized.UserId);
        Assert.Equal(3, deserialized.Context.Count);
        Assert.Equal("value1", deserialized.Context["key1"]);
        Assert.Equal("Connection timeout", deserialized.Context["error"]);
        Assert.Equal(3, deserialized.CompletedAgents.Count);
        Assert.Contains("ResearcherAgent", deserialized.CompletedAgents);
    }

    [Fact]
    public void SerializeCheckpoint_MinimalFields_HandlesNulls()
    {
        // Arrange
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_minimal",
            WorkflowId = "wf_minimal",
            WorkflowType = "TestWorkflow",
            CreatedAt = DateTime.UtcNow,
            StepIndex = 0
            // AgentId, StateSnapshot, Label are null
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(checkpoint.CheckpointId, deserialized.CheckpointId);
        Assert.Null(deserialized.AgentId);
        Assert.Null(deserialized.StateSnapshot);
        Assert.Null(deserialized.Label);
    }

    [Fact]
    public void SerializeResearcherWorkflowState_ComplexObject_RoundTripsCorrectly()
    {
        // Arrange
        var workflowState = new
        {
            CurrentQuestion = "What is quantum computing?",
            BriefCompletedAt = DateTime.UtcNow,
            ResearchStartedAt = DateTime.UtcNow.AddMinutes(-5),
            AgentStates = new Dictionary<string, object>
            {
                { "ClarifyAgent", new { Clarifications = new[] { "Focus on basics", "Include applications" } } },
                { "ResearchBriefAgent", new { Brief = "Quantum computing research brief..." } },
                { "ResearcherAgent", new { IterationCount = 3, FactsCollected = 15 } }
            },
            FactStore = new[]
            {
                new { Id = "fact1", Content = "Quantum computers use qubits", Source = "wikipedia.org" },
                new { Id = "fact2", Content = "Superposition is a key principle", Source = "nature.com" }
            }
        };

        var stateJson = JsonSerializer.Serialize(workflowState);

        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_test",
            WorkflowId = "wf_test",
            WorkflowType = "ResearcherWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = stateJson
        };

        // Act
        var checkpointJson = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(checkpointJson);

        // Parse the state snapshot
        var deserializedState = JsonDocument.Parse(deserialized!.StateSnapshot!);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("What is quantum computing?", deserializedState.RootElement.GetProperty("CurrentQuestion").GetString());
        Assert.True(deserializedState.RootElement.GetProperty("AgentStates").GetProperty("ResearcherAgent").GetProperty("IterationCount").GetInt32() == 3);
        Assert.Equal(2, deserializedState.RootElement.GetProperty("FactStore").GetArrayLength());
    }

    [Fact]
    public void SerializeCheckpoint_LargeStateSnapshot_HandlesEfficiently()
    {
        // Arrange
        var largeFacts = new List<object>();
        for (int i = 0; i < 1000; i++)
        {
            largeFacts.Add(new
            {
                Id = $"fact_{i}",
                Content = $"This is fact number {i} with some additional content to make it realistic",
                Source = $"source_{i}.com",
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        var largeState = new
        {
            Question = "Complex research question requiring many facts",
            Facts = largeFacts,
            Metadata = new { TotalFacts = largeFacts.Count }
        };

        var stateJson = JsonSerializer.Serialize(largeState);
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_large",
            WorkflowId = "wf_large",
            WorkflowType = "ResearcherWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = stateJson,
            StateSizeBytes = System.Text.Encoding.UTF8.GetByteCount(stateJson)
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(checkpoint.StateSizeBytes, deserialized.StateSizeBytes);
        Assert.True(deserialized.StateSizeBytes > 50000); // Should be reasonably large

        var deserializedState = JsonDocument.Parse(deserialized.StateSnapshot!);
        Assert.Equal(1000, deserializedState.RootElement.GetProperty("Facts").GetArrayLength());
    }

    [Fact]
    public void SerializeCheckpointStatistics_AllFields_RoundTripsCorrectly()
    {
        // Arrange
        var stats = new CheckpointStatistics
        {
            TotalCheckpoints = 42,
            AverageCheckpointSizeBytes = 512000,
            LargestCheckpointSizeBytes = 2048000,
            TotalStorageUsedBytes = 21504000,
            RecentCheckpointsCount = 7,
            OldestCheckpointAt = DateTime.UtcNow.AddDays(-30),
            NewestCheckpointAt = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(stats);
        var deserialized = JsonSerializer.Deserialize<CheckpointStatistics>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(stats.TotalCheckpoints, deserialized.TotalCheckpoints);
        Assert.Equal(stats.AverageCheckpointSizeBytes, deserialized.AverageCheckpointSizeBytes);
        Assert.Equal(stats.LargestCheckpointSizeBytes, deserialized.LargestCheckpointSizeBytes);
        Assert.Equal(stats.TotalStorageUsedBytes, deserialized.TotalStorageUsedBytes);
        Assert.Equal(stats.RecentCheckpointsCount, deserialized.RecentCheckpointsCount);
        Assert.Equal(stats.OldestCheckpointAt, deserialized.OldestCheckpointAt);
        Assert.Equal(stats.NewestCheckpointAt, deserialized.NewestCheckpointAt);
    }

    [Fact]
    public void SerializeCheckpoint_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var stateWithSpecialChars = new
        {
            Question = "What's the \"difference\" between <quantum> & {classical} computing?",
            Notes = "Line 1\nLine 2\tTabbed\r\nWindows line ending",
            Unicode = "Hello 世界 🌍 Привет مرحبا"
        };

        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_special",
            WorkflowId = "wf_special",
            WorkflowType = "TestWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = JsonSerializer.Serialize(stateWithSpecialChars),
            Label = "Checkpoint with \"quotes\" and special chars"
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(checkpoint.Label, deserialized.Label);

        var deserializedState = JsonDocument.Parse(deserialized.StateSnapshot!);
        Assert.Contains("quantum", deserializedState.RootElement.GetProperty("Question").GetString()!);
        Assert.Contains("世界", deserializedState.RootElement.GetProperty("Unicode").GetString()!);
        Assert.Contains("\n", deserializedState.RootElement.GetProperty("Notes").GetString()!);
    }

    [Fact]
    public void SerializeCheckpoint_WithEmptyCollections_PreservesEmptyState()
    {
        // Arrange
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_empty",
            WorkflowId = "wf_empty",
            WorkflowType = "TestWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = JsonSerializer.Serialize(new { Facts = new List<object>() }),
            Metadata = new CheckpointMetadata
            {
                Context = new Dictionary<string, string>(),
                CompletedAgents = new List<string>()
            }
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Metadata.Context);
        Assert.Empty(deserialized.Metadata.CompletedAgents);

        var deserializedState = JsonDocument.Parse(deserialized.StateSnapshot!);
        Assert.Equal(0, deserializedState.RootElement.GetProperty("Facts").GetArrayLength());
    }

    [Fact]
    public void SerializeCheckpoint_DateTimeHandling_PreservesUtcTimestamps()
    {
        // Arrange
        var utcNow = DateTime.UtcNow;
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_datetime",
            WorkflowId = "wf_datetime",
            WorkflowType = "TestWorkflow",
            CreatedAt = utcNow
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        // Allow small tolerance for serialization (milliseconds)
        Assert.True(Math.Abs((deserialized.CreatedAt - utcNow).TotalMilliseconds) < 100);
        Assert.Equal(DateTimeKind.Utc, deserialized.CreatedAt.Kind);
    }

    [Fact]
    public void SerializeCheckpoint_NestedComplexObjects_MaintainsStructure()
    {
        // Arrange
        var complexState = new
        {
            Workflow = new
            {
                Id = "wf_nested",
                Type = "ResearcherWorkflow",
                Pipeline = new[]
                {
                    new { AgentId = "ClarifyAgent", Status = "completed", Duration = 5.2 },
                    new { AgentId = "ResearchBriefAgent", Status = "completed", Duration = 12.7 },
                    new { AgentId = "ResearcherAgent", Status = "in-progress", Duration = 0.0 }
                }
            },
            State = new
            {
                CurrentAgent = "ResearcherAgent",
                Progress = new { Percentage = 65, EstimatedRemaining = "15 minutes" },
                Resources = new Dictionary<string, object>
                {
                    { "memory_mb", 512 },
                    { "api_calls", 47 },
                    { "cache_hits", new[] { "search_1", "search_2" } }
                }
            }
        };

        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = "ckpt_nested",
            WorkflowId = "wf_nested",
            WorkflowType = "ResearcherWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = JsonSerializer.Serialize(complexState)
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);

        // Assert
        Assert.NotNull(deserialized);
        var deserializedState = JsonDocument.Parse(deserialized.StateSnapshot!);

        Assert.Equal("ResearcherWorkflow", deserializedState.RootElement.GetProperty("Workflow").GetProperty("Type").GetString());
        Assert.Equal(3, deserializedState.RootElement.GetProperty("Workflow").GetProperty("Pipeline").GetArrayLength());
        Assert.Equal(65, deserializedState.RootElement.GetProperty("State").GetProperty("Progress").GetProperty("Percentage").GetInt32());
        Assert.Equal(512, deserializedState.RootElement.GetProperty("State").GetProperty("Resources").GetProperty("memory_mb").GetInt32());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SerializeCheckpoint_VariousStateSizes_PerformsAcceptably(int itemCount)
    {
        // Arrange
        var items = new List<object>();
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(new { Id = i, Data = $"Item {i}" });
        }

        var stateJson = JsonSerializer.Serialize(new { Items = items });
        var checkpoint = new WorkflowCheckpoint
        {
            CheckpointId = $"ckpt_perf_{itemCount}",
            WorkflowId = $"wf_perf_{itemCount}",
            WorkflowType = "TestWorkflow",
            CreatedAt = DateTime.UtcNow,
            StateSnapshot = stateJson
        };

        // Act
        var startTime = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(checkpoint);
        var deserialized = JsonSerializer.Deserialize<WorkflowCheckpoint>(json);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(elapsed.TotalMilliseconds < 1000, $"Serialization took {elapsed.TotalMilliseconds}ms for {itemCount} items");

        var deserializedState = JsonDocument.Parse(deserialized.StateSnapshot!);
        Assert.Equal(itemCount, deserializedState.RootElement.GetProperty("Items").GetArrayLength());
    }
}
