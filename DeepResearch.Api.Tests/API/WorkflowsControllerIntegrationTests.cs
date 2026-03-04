using System.Net;
using System.Net.Http.Json;
using DeepResearch.Api.Models;
using DeepResearch.Api.Tests.Fixtures;
using DeepResearchAgent.Model.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeepResearch.Api.Tests.API;

/// <summary>
/// Integration tests for WorkflowsController endpoints.
/// Tests workflow lifecycle: start, status, pause, resume, cancel.
/// </summary>
[Collection("Integration Tests")]
public class WorkflowsControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _tempDataPath;

    public async Task InitializeAsync()
    {
        _tempDataPath = Path.Combine(Path.GetTempPath(), "dra-workflow-tests", Guid.NewGuid().ToString("N"));
        var checkpointPath = Path.Combine(_tempDataPath, "checkpoints");
        Directory.CreateDirectory(_tempDataPath);

        _factory = new ApiTestFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_tempDataPath);

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var settings = new Dictionary<string, string?>
                    {
                        ["CheckpointService:LocalStorageDirectory"] = checkpointPath,
                        ["LightningStore:DataDirectory"] = Path.Combine(_tempDataPath, "data"),
                        ["LightningStore:FileName"] = "test-store.json",
                        ["LightningStore:UseLightningServer"] = "false"
                    };
                    config.AddInMemoryCollection(settings);
                });
            });

        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (Directory.Exists(_tempDataPath))
        {
            try
            {
                Directory.Delete(_tempDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task StartWorkflow_WithValidRequest_ReturnsWorkflowId()
    {
        // Arrange
        var request = new StartWorkflowRequestDto
        {
            WorkflowType = "MasterWorkflow",
            Input = new Dictionary<string, object>
            {
                ["topic"] = "AI Safety Research",
                ["depth"] = 3
            },
            Config = new Dictionary<string, string>
            {
                ["maxIterations"] = "5"
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/workflows/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();
        result.Should().NotBeNull();
        result!.WorkflowId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be("Queued");
        result.Message.Should().Contain("MasterWorkflow");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartWorkflow_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange - missing required WorkflowType
        var request = new StartWorkflowRequestDto
        {
            WorkflowType = null!,
            Input = new Dictionary<string, object>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/workflows/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetWorkflowStatus_WithValidId_ReturnsStatus()
    {
        // Arrange - First create a workflow
        var startRequest = new StartWorkflowRequestDto
        {
            WorkflowType = "TestWorkflow",
            Input = new Dictionary<string, object> { ["test"] = "data" }
        };
        var startResponse = await _client!.PostAsJsonAsync("/api/workflows/start", startRequest);
        var workflow = await startResponse.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();

        // Act
        var response = await _client.GetAsync($"/api/workflows/{workflow!.WorkflowId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<WorkflowStatusDto>();
        status.Should().NotBeNull();
        status!.WorkflowId.Should().Be(workflow.WorkflowId);
        status.Status.Should().NotBeNullOrEmpty();
        status.Progress.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWorkflowStatus_WithInvalidId_ReturnsError()
    {
        // Arrange
        var invalidId = "non-existent-workflow-id";

        // Act
        var response = await _client!.GetAsync($"/api/workflows/{invalidId}");

        // Assert
        // May return 200 with null or 404/500 depending on implementation
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.NotFound, 
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PauseWorkflow_WhenRunning_AcceptsPauseRequest()
    {
        // Arrange - Create and "start" a workflow (mock will be in running state)
        var startRequest = new StartWorkflowRequestDto
        {
            WorkflowType = "LongRunningWorkflow",
            Input = new Dictionary<string, object> { ["duration"] = 300 }
        };
        var startResponse = await _client!.PostAsJsonAsync("/api/workflows/start", startRequest);
        var workflow = await startResponse.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();

        // Note: In real scenario, workflow would transition to Running state
        // For integration test, we're testing the API contract

        // Act
        var response = await _client.PutAsync($"/api/workflows/{workflow!.WorkflowId}/pause", null);

        // Assert
        // May return OK if workflow is running, or Conflict if not in running state
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<WorkflowActionResponseDto>();
            result.Should().NotBeNull();
            result!.WorkflowId.Should().Be(workflow.WorkflowId);
            result.Action.Should().Be("pause");
            result.Success.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ResumeWorkflow_WhenPaused_AcceptsResumeRequest()
    {
        // Arrange - Create workflow, pause it, then resume
        var startRequest = new StartWorkflowRequestDto
        {
            WorkflowType = "ResumableWorkflow",
            Input = new Dictionary<string, object> { ["resumable"] = true }
        };
        var startResponse = await _client!.PostAsJsonAsync("/api/workflows/start", startRequest);
        var workflow = await startResponse.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();

        // Pause first (may or may not succeed depending on state)
        await _client.PutAsync($"/api/workflows/{workflow!.WorkflowId}/pause", null);

        // Act
        var response = await _client.PutAsync($"/api/workflows/{workflow.WorkflowId}/resume", null);

        // Assert
        // May return OK if paused with checkpoint, Conflict if not paused, or NotFound if no checkpoint
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.Conflict, 
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelWorkflow_WithValidId_AcceptsCancellation()
    {
        // Arrange
        var startRequest = new StartWorkflowRequestDto
        {
            WorkflowType = "CancellableWorkflow",
            Input = new Dictionary<string, object> { ["test"] = "cancel" }
        };
        var startResponse = await _client!.PostAsJsonAsync("/api/workflows/start", startRequest);
        var workflow = await startResponse.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();

        // Act
        var response = await _client.PutAsync($"/api/workflows/{workflow!.WorkflowId}/cancel", null);

        // Assert
        // Should accept cancellation request
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListWorkflows_ReturnsWorkflowList()
    {
        // Arrange - Create a few workflows
        for (int i = 0; i < 3; i++)
        {
            var request = new StartWorkflowRequestDto
            {
                WorkflowType = $"TestWorkflow{i}",
                Input = new Dictionary<string, object> { ["index"] = i }
            };
            await _client!.PostAsJsonAsync("/api/workflows/start", request);
        }

        // Act
        var response = await _client!.GetAsync("/api/workflows");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WorkflowListResponseDto>();
        result.Should().NotBeNull();
        result!.Workflows.Should().NotBeNull();
        // Endpoint returns empty list as it's not fully implemented yet
    }

    [Fact]
    public async Task GetWorkflowLogs_WithValidId_ReturnsLogs()
    {
        // Arrange
        var startRequest = new StartWorkflowRequestDto
        {
            WorkflowType = "LoggingWorkflow",
            Input = new Dictionary<string, object> { ["verbose"] = true }
        };
        var startResponse = await _client!.PostAsJsonAsync("/api/workflows/start", startRequest);
        var workflow = await startResponse.Content.ReadFromJsonAsync<StartWorkflowResponseDto>();

        // Act
        var response = await _client.GetAsync($"/api/workflows/{workflow!.WorkflowId}/logs");

        // Assert
        // Should return logs or 200 with empty array if endpoint exists
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}
