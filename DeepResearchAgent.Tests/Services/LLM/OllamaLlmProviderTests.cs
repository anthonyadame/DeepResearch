using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace DeepResearchAgent.Tests.Services.LLM;

/// <summary>
/// Unit tests for OllamaLlmProvider
/// Tests the Ollama provider implementation without requiring a live Ollama server
/// </summary>
public class OllamaLlmProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OllamaLlmProvider>> _mockLogger;

    public OllamaLlmProviderTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<OllamaLlmProvider>>();
    }

    [Fact]
    public void Constructor_ShouldSetProviderName()
    {
        // Arrange & Act
        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        // Assert
        Assert.Equal("ollama", provider.ProviderName);
    }

    [Fact]
    public void Constructor_ShouldSetDefaultModel()
    {
        // Arrange & Act
        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        // Assert
        Assert.Equal("gpt-oss:20b", provider.DefaultModel);
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullBaseUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaLlmProvider(null!, "model", _httpClient));
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullModel()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaLlmProvider("http://localhost:11434", null!, _httpClient));
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnAssistantMessage()
    {
        // Arrange
        var mockResponse = new
        {
            model = "gpt-oss:20b",
            message = new
            {
                role = "assistant",
                content = "This is a test response"
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        // Act
        var result = await provider.InvokeAsync(messages);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Equal("This is a test response", result.Content);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseDefaultModel_WhenModelNotSpecified()
    {
        // Arrange
        var mockResponse = new
        {
            model = "gpt-oss:20b",
            message = new { role = "assistant", content = "Response" }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        await provider.InvokeAsync(messages);

        // Assert - Verify the request contained the default model
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/api/chat")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseSpecifiedModel_WhenProvided()
    {
        // Arrange
        var mockResponse = new
        {
            model = "custom-model",
            message = new { role = "assistant", content = "Response" }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        await provider.InvokeAsync(messages, model: "custom-model");

        // Assert
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ShouldThrowOnHttpError()
    {
        // Arrange
        SetupFailedHttpResponse(HttpStatusCode.InternalServerError);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.InvokeAsync(messages));
    }

    [Fact]
    public async Task InvokeStreamingAsync_ShouldYieldChunks()
    {
        // Arrange
        var streamLines = new[]
        {
            @"{""message"":{""content"":""Hello""}}",
            @"{""message"":{""content"":"" ""}}",
            @"{""message"":{""content"":""World""}}"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in provider.InvokeStreamingAsync(messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Hello", chunks[0]);
        Assert.Equal(" ", chunks[1]);
        Assert.Equal("World", chunks[2]);
    }

    [Fact]
    public async Task InvokeStreamingAsync_ShouldSkipEmptyLines()
    {
        // Arrange
        var streamLines = new[]
        {
            @"{""message"":{""content"":""Hello""}}",
            "",  // Empty line
            @"{""message"":{""content"":""World""}}"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in provider.InvokeStreamingAsync(messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnTrue_WhenOllamaIsRunning()
    {
        // Arrange
        var mockResponse = new
        {
            models = new[] { new { name = "gpt-oss:20b" } }
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/api/tags")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        // Act
        var isHealthy = await provider.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnFalse_WhenOllamaIsDown()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/api/tags")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        var provider = new OllamaLlmProvider(
            "http://localhost:11434",
            "gpt-oss:20b",
            _httpClient,
            _mockLogger.Object);

        // Act
        var isHealthy = await provider.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);
    }

    private void SetupSuccessfulHttpResponse(object responseObject)
    {
        var responseJson = JsonSerializer.Serialize(responseObject);
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void SetupFailedHttpResponse(HttpStatusCode statusCode)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode
            });
    }

    private void SetupStreamingHttpResponse(string[] lines)
    {
        var streamContent = string.Join("\n", lines);
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(streamContent, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
