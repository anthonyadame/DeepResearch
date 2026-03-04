using System.Net;
using System.Net.Http.Json;
using DeepResearch.Api.DTOs;
using DeepResearch.Api.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using ChatHistoryStatistics = DeepResearch.Api.Models.Chat.ChatHistoryStatistics;

namespace DeepResearch.Api.Tests.API;

[Collection("Integration Tests")]
public class ChatControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string _tempDataPath = string.Empty;

    public async Task InitializeAsync()
    {
        _tempDataPath = Path.Combine(Path.GetTempPath(), "dra-chat-tests", Guid.NewGuid().ToString("N"));
        var lightningDataPath = Path.Combine(_tempDataPath, "data");
        Directory.CreateDirectory(_tempDataPath);

        // Create factory and configure it - WithWebHostBuilder returns a new configured factory
        _factory = new ApiTestFactory()
            .WithWebHostBuilder(builder =>
            {
                // Set content root to temp directory so relative paths like "data/chat-sessions" work
                builder.UseContentRoot(_tempDataPath);

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var settings = new Dictionary<string, string?>
                    {
                        ["LightningStore:DataDirectory"] = lightningDataPath,
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
    public async Task CreateSession_ReturnsCreatedSessionWithId()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            Title = "Integration Test Session"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/chat/sessions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await response.Content.ReadFromJsonAsync<ChatSession>();
        session.Should().NotBeNull();
        session!.Id.Should().NotBeNullOrEmpty();
        session.Title.Should().Be("Integration Test Session");
    }

    [Fact]
    public async Task GetSessions_ReturnsListOfSessions()
    {
        // Arrange - Create a test session first
        var createRequest = new CreateSessionRequest
        {
            Title = "Test Session for List"
        };
        await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);

        // Act
        var response = await _client.GetAsync("/api/chat/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<List<ChatSession>>();
        sessions.Should().NotBeNull();
        sessions!.Should().NotBeEmpty();
        sessions.Should().Contain(s => s.Title == "Test Session for List");
    }

    [Fact]
    public async Task GetSession_WithValidId_ReturnsSession()
    {
        // Arrange - Create a session
        var createRequest = new CreateSessionRequest
        {
            Title = "Session to Retrieve"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var createdSession = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        // Act
        var response = await _client.GetAsync($"/api/chat/sessions/{createdSession!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<ChatSession>();
        session.Should().NotBeNull();
        session!.Id.Should().Be(createdSession.Id);
        session.Title.Should().Be("Session to Retrieve");
    }

    [Fact]
    public async Task GetSession_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client!.GetAsync("/api/chat/sessions/non-existent-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendMessage_WithValidSession_ReturnsUserMessage()
    {
        // Arrange - Create a session
        var createRequest = new CreateSessionRequest
        {
            Title = "Message Test Session"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        var messageRequest = new SendMessageRequest
        {
            Message = "What is machine learning?",
            Config = null
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/chat/sessions/{session!.Id}/query", messageRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var chatMessage = await response.Content.ReadFromJsonAsync<ChatMessage>();
        chatMessage.Should().NotBeNull();
        chatMessage!.Role.Should().Be("user");
        chatMessage.Content.Should().Be("What is machine learning?");
    }

    [Fact]
    public async Task DeleteSession_RemovesSession()
    {
        // Arrange - Create a session
        var createRequest = new CreateSessionRequest
        {
            Title = "Session to Delete"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        // Act - Delete the session
        var deleteResponse = await _client.DeleteAsync($"/api/chat/sessions/{session!.Id}");

        // Assert - Verify deleted
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify session no longer exists
        var getResponse = await _client.GetAsync($"/api/chat/sessions/{session.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveSession_MarksSessionAsArchived()
    {
        // Arrange - Create a session
        var createRequest = new CreateSessionRequest
        {
            Title = "Session to Archive"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        // Act
        var archiveResponse = await _client.PostAsJsonAsync($"/api/chat/sessions/{session!.Id}/archive", new { });

        // Assert
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify session is not in active list
        var sessionsResponse = await _client.GetAsync("/api/chat/sessions?includeArchived=false");
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<List<ChatSession>>();
        sessions.Should().NotContain(s => s.Id == session.Id);

        // Verify session is in archived list
        var archivedResponse = await _client.GetAsync("/api/chat/sessions?includeArchived=true");
        var archivedSessions = await archivedResponse.Content.ReadFromJsonAsync<List<ChatSession>>();
        archivedSessions.Should().Contain(s => s.Id == session.Id);
    }

    [Fact]
    public async Task GetSessionsByCategory_ReturnsFilteredSessions()
    {
        // Arrange - Create sessions
        var researchRequest = new CreateSessionRequest
        {
            Title = "Research on AI"
        };
        await _client!.PostAsJsonAsync("/api/chat/sessions", researchRequest);

        // Act
        var response = await _client.GetAsync("/api/chat/sessions/category/research");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<List<ChatSession>>();
        sessions.Should().NotBeNull();
    }

    [Fact]
    public async Task CategorizeSession_WithValidId_ReturnsCategories()
    {
        // Arrange - Create a session with a message
        var createRequest = new CreateSessionRequest
        {
            Title = "Technical Research Session"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        var messageRequest = new SendMessageRequest
        {
            Message = "Tell me about quantum computing research",
            Config = null
        };
        await _client.PostAsJsonAsync($"/api/chat/sessions/{session!.Id}/query", messageRequest);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/chat/sessions/{session.Id}/categorize", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<string[]>();
        categories.Should().NotBeNull();
        categories!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateCategories_WithValidData_UpdatesSessionCategories()
    {
        // Arrange - Create a session
        var createRequest = new CreateSessionRequest
        {
            Title = "Session for Category Update"
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/chat/sessions", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSession>();

        var categories = new[] { "machine-learning", "ai", "research" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/chat/sessions/{session!.Id}/categories", categories);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatistics_ReturnsStats()
    {
        // Arrange - Create some sessions
        await _client!.PostAsJsonAsync("/api/chat/sessions", new CreateSessionRequest { Title = "Stats Test 1" });
        await _client.PostAsJsonAsync("/api/chat/sessions", new CreateSessionRequest { Title = "Stats Test 2" });

        // Act
        var response = await _client.GetAsync("/api/chat/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<ChatHistoryStatistics>();
        stats.Should().NotBeNull();
    }
}
