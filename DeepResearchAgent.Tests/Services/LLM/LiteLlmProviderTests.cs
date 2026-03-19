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
/// Unit tests for LiteLlmProvider
/// Tests the LiteLLM provider implementation with qwen models
/// </summary>
public class LiteLlmProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<LiteLlmProvider>> _mockLogger;

    public LiteLlmProviderTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<LiteLlmProvider>>();
    }

    [Fact]
    public void Constructor_ShouldSetProviderName()
    {
        // Arrange & Act
        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
            _mockLogger.Object);

        // Assert
        Assert.Equal("litellm", provider.ProviderName);
    }

    [Fact]
    public void Constructor_ShouldSetDefaultModel()
    {
        // Arrange & Act
        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-4b",
            _httpClient,
            null,
            _mockLogger.Object);

        // Assert
        Assert.Equal("qwen3.5-4b", provider.DefaultModel);
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullBaseUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiteLlmProvider(null!, "model", _httpClient));
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullModel()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiteLlmProvider("http://localhost:4000", null!, _httpClient));
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnAssistantMessage_WithQwen2B()
    {
        // Arrange
        var mockResponse = new
        {
            id = "chatcmpl-123",
            model = "qwen3.5-2b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "This is a qwen3.5-2b response"
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
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
        Assert.Equal("This is a qwen3.5-2b response", result.Content);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnAssistantMessage_WithQwen4B()
    {
        // Arrange
        var mockResponse = new
        {
            id = "chatcmpl-456",
            model = "qwen3.5-4b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "This is a qwen3.5-4b response with more capability"
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-4b",
            _httpClient,
            null,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Explain quantum computing" }
        };

        // Act
        var result = await provider.InvokeAsync(messages);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("qwen3.5-4b", result.Content);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseDefaultModel_WhenModelNotSpecified()
    {
        // Arrange
        var mockResponse = new
        {
            model = "qwen3.5-2b",
            choices = new[]
            {
                new { message = new { role = "assistant", content = "Response" } }
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        await provider.InvokeAsync(messages);

        // Assert - Verify the request was made to the correct endpoint
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/chat/completions")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ShouldIncludeApiKey_WhenProvided()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = "Response" } }
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            apiKey: "test-api-key",
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        await provider.InvokeAsync(messages);

        // Assert - Verify Authorization header was included
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Authorization != null &&
                req.Headers.Authorization.Scheme == "Bearer" &&
                req.Headers.Authorization.Parameter == "test-api-key"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ShouldThrowOnHttpError()
    {
        // Arrange
        SetupFailedHttpResponse(HttpStatusCode.Unauthorized);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
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
    public async Task InvokeStreamingAsync_ShouldYieldChunks_WithQwen2B()
    {
        // Arrange
        var streamLines = new[]
        {
            @"data: {""choices"":[{""delta"":{""content"":""Hello""}}]}",
            @"data: {""choices"":[{""delta"":{""content"":"" from""}}]}",
            @"data: {""choices"":[{""delta"":{""content"":"" qwen3.5-2b""}}]}",
            @"data: [DONE]"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
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
        Assert.Equal(" from", chunks[1]);
        Assert.Equal(" qwen3.5-2b", chunks[2]);
    }

    [Fact]
    public async Task InvokeStreamingAsync_ShouldYieldChunks_WithQwen4B()
    {
        // Arrange
        var streamLines = new[]
        {
            @"data: {""choices"":[{""delta"":{""content"":""Advanced""}}]}",
            @"data: {""choices"":[{""delta"":{""content"":"" response""}}]}",
            @"data: {""choices"":[{""delta"":{""content"":"" from qwen3.5-4b""}}]}",
            @"data: [DONE]"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-4b",
            _httpClient,
            null,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Complex question" }
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in provider.InvokeStreamingAsync(messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Contains("qwen3.5-4b", string.Join("", chunks));
    }

    [Fact]
    public async Task InvokeStreamingAsync_ShouldStopAtDoneMarker()
    {
        // Arrange
        var streamLines = new[]
        {
            @"data: {""choices"":[{""delta"":{""content"":""Hello""}}]}",
            @"data: [DONE]",
            @"data: {""choices"":[{""delta"":{""content"":""Should not appear""}}]}"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
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
        Assert.Single(chunks);
        Assert.Equal("Hello", chunks[0]);
    }

    [Fact]
    public async Task InvokeStreamingAsync_ShouldSkipEmptyLines()
    {
        // Arrange
        var streamLines = new[]
        {
            @"data: {""choices"":[{""delta"":{""content"":""Hello""}}]}",
            "",  // Empty line
            @"data: {""choices"":[{""delta"":{""content"":""World""}}]}",
            @"data: [DONE]"
        };

        SetupStreamingHttpResponse(streamLines);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
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
    public async Task IsHealthyAsync_ShouldReturnTrue_WhenLiteLLMIsRunning()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""status"":""healthy""}")
            });

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
            _mockLogger.Object);

        // Act
        var isHealthy = await provider.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnFalse_WhenLiteLLMIsDown()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            "qwen3.5-2b",
            _httpClient,
            null,
            _mockLogger.Object);

        // Act
        var isHealthy = await provider.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);
    }

    [Theory]
    [InlineData("qwen3.5-2b")]
    [InlineData("qwen3.5-4b")]
    public async Task InvokeAsync_ShouldWorkWithDifferentQwenModels(string modelName)
    {
        // Arrange
        var mockResponse = new
        {
            model = modelName,
            choices = new[]
            {
                new { message = new { role = "assistant", content = $"Response from {modelName}" } }
            }
        };

        SetupSuccessfulHttpResponse(mockResponse);

        var provider = new LiteLlmProvider(
            "http://localhost:4000",
            modelName,
            _httpClient,
            null,
            _mockLogger.Object);

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        // Act
        var result = await provider.InvokeAsync(messages);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(modelName, result.Content);
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
                StatusCode = statusCode,
                Content = new StringContent(@"{""error"":{""message"":""Test error""}}")
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
                Content = new StringContent(streamContent, System.Text.Encoding.UTF8, "text/event-stream")
            });
    }
}
