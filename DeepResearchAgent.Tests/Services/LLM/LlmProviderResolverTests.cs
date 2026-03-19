using System;
using System.Collections.Generic;
using System.Linq;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using DeepResearchAgent.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DeepResearchAgent.Tests.Services.LLM;

/// <summary>
/// Unit tests for LlmProviderResolver
/// Tests provider resolution and selection logic
/// </summary>
public class LlmProviderResolverTests
{
    private readonly Mock<ILogger<LlmProviderResolver>> _mockLogger;
    private readonly Mock<IOptionsMonitor<LlmProviderOptions>> _mockOptions;

    public LlmProviderResolverTests()
    {
        _mockLogger = new Mock<ILogger<LlmProviderResolver>>();
        _mockOptions = new Mock<IOptionsMonitor<LlmProviderOptions>>();
    }

    [Fact]
    public void Constructor_ShouldRegisterProviders()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        // Act
        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Assert
        var availableProviders = resolver.GetAvailableProviders();
        Assert.Equal(2, availableProviders.Count());
        Assert.Contains("ollama", availableProviders);
        Assert.Contains("litellm", availableProviders);
    }

    [Fact]
    public void Resolve_ShouldReturnDefaultProvider_WhenNoNameSpecified()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ollama", result.ProviderName);
    }

    [Fact]
    public void Resolve_ShouldReturnOllamaProvider_WhenRequested()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "litellm" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve("ollama");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ollama", result.ProviderName);
        Assert.Equal("gpt-oss:20b", result.DefaultModel);
    }

    [Fact]
    public void Resolve_ShouldReturnLiteLlmProvider_WhenRequested()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve("litellm");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("litellm", result.ProviderName);
        Assert.Equal("qwen3.5-2b", result.DefaultModel);
    }

    [Fact]
    public void Resolve_ShouldBeCaseInsensitive()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var providers = new List<ILlmProvider> { ollamaProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result1 = resolver.Resolve("OLLAMA");
        var result2 = resolver.Resolve("Ollama");
        var result3 = resolver.Resolve("ollama");

        // Assert
        Assert.Equal("ollama", result1.ProviderName);
        Assert.Equal("ollama", result2.ProviderName);
        Assert.Equal("ollama", result3.ProviderName);
    }

    [Fact]
    public void Resolve_ShouldFallbackToDefault_WhenRequestedProviderNotFound()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var providers = new List<ILlmProvider> { ollamaProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve("nonexistent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ollama", result.ProviderName);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenNoProvidersAvailable()
    {
        // Arrange
        var providers = new List<ILlmProvider>();

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve());
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenDefaultProviderNotFound()
    {
        // Arrange
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };  // Default doesn't exist
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("nonexistent"));
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnAllRegisteredProviders()
    {
        // Arrange
        var ollamaProvider = CreateMockProvider("ollama", "gpt-oss:20b");
        var liteLlmProvider = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { ollamaProvider, liteLlmProvider };

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var availableProviders = resolver.GetAvailableProviders().ToList();

        // Assert
        Assert.Equal(2, availableProviders.Count);
        Assert.Contains("ollama", availableProviders);
        Assert.Contains("litellm", availableProviders);
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnEmpty_WhenNoProvidersRegistered()
    {
        // Arrange
        var providers = new List<ILlmProvider>();

        var options = new LlmProviderOptions { Provider = "ollama" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var availableProviders = resolver.GetAvailableProviders().ToList();

        // Assert
        Assert.Empty(availableProviders);
    }

    [Fact]
    public void Resolve_ShouldSupportMultipleModelsPerProvider()
    {
        // Arrange - LiteLLM with qwen3.5-2b
        var liteLlm2b = CreateMockProvider("litellm", "qwen3.5-2b");
        var providers = new List<ILlmProvider> { liteLlm2b };

        var options = new LlmProviderOptions { Provider = "litellm" };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve("litellm");

        // Assert
        Assert.Equal("qwen3.5-2b", result.DefaultModel);

        // Can override model at invocation time
        // (This is tested in provider-specific tests)
    }

    [Theory]
    [InlineData("ollama", "gpt-oss:20b")]
    [InlineData("litellm", "qwen3.5-2b")]
    [InlineData("litellm", "qwen3.5-4b")]
    public void Resolve_ShouldReturnCorrectProvider_ForDifferentConfigurations(
        string providerName, 
        string modelName)
    {
        // Arrange
        var provider = CreateMockProvider(providerName, modelName);
        var providers = new List<ILlmProvider> { provider };

        var options = new LlmProviderOptions { Provider = providerName };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var resolver = new LlmProviderResolver(providers, _mockOptions.Object, _mockLogger.Object);

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Equal(providerName, result.ProviderName);
        Assert.Equal(modelName, result.DefaultModel);
    }

    private ILlmProvider CreateMockProvider(string providerName, string modelName)
    {
        var mock = new Mock<ILlmProvider>();
        mock.Setup(x => x.ProviderName).Returns(providerName);
        mock.Setup(x => x.DefaultModel).Returns(modelName);
        mock.Setup(x => x.InvokeAsync(
            It.IsAny<List<OllamaChatMessage>>(),
            It.IsAny<string>(),
            It.IsAny<LlmModelTier?>(),
            It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new OllamaChatMessage 
            { 
                Role = "assistant", 
                Content = $"Response from {providerName}" 
            });
        return mock.Object;
    }
}
