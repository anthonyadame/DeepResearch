using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DeepResearchAgent.Tests.Integration;

/// <summary>
/// Integration tests for LiteLLM provider with real LiteLLM proxy
/// Tests with qwen3.5-2b and qwen3.5-4b models
/// 
/// Prerequisites:
/// - LiteLLM proxy running on http://localhost:4000
/// - Models qwen3.5-2b and qwen3.5-4b configured
/// 
/// Skip tests if LiteLLM is not available
/// </summary>
[Collection("LiteLLM Integration Tests")]
public class LiteLlmIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILlmProviderResolver? _resolver;
    private bool _liteLlmAvailable;

    public LiteLlmIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["LlmProvider:Provider"] = "litellm",
                ["LlmProvider:RequestTimeoutSeconds"] = "120",
                ["LlmProvider:Ollama:BaseUrl"] = "http://localhost:11434",
                ["LlmProvider:Ollama:DefaultModel"] = "gpt-oss:20b",
                ["LlmProvider:LiteLLM:BaseUrl"] = "http://localhost:4000",
                ["LlmProvider:LiteLLM:DefaultModel"] = "qwen3.5-2b",
                ["LlmProvider:LiteLLM:ApiKey"] = ""
            }!)
            .Build();

        // Setup services
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add LLM providers
        services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));
        services.AddHttpClient();

        services.AddSingleton<OllamaLlmProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            return new OllamaLlmProvider(
                "http://localhost:11434",
                "gpt-oss:20b",
                httpClient,
                sp.GetService<ILogger<OllamaLlmProvider>>());
        });

        services.AddSingleton<LiteLlmProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            return new LiteLlmProvider(
                "http://localhost:4000",
                "qwen3.5-2b",
                httpClient,
                null,
                sp.GetService<ILogger<LiteLlmProvider>>());
        });

        services.AddSingleton<ILlmProviderResolver, LlmProviderResolver>(sp =>
        {
            var providers = new List<ILlmProvider>
            {
                sp.GetRequiredService<OllamaLlmProvider>(),
                sp.GetRequiredService<LiteLlmProvider>()
            };
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LlmProviderOptions>>();
            return new LlmProviderResolver(providers, options, sp.GetService<ILogger<LlmProviderResolver>>());
        });

        _serviceProvider = services.BuildServiceProvider();
        _resolver = _serviceProvider.GetRequiredService<ILlmProviderResolver>();

        // Check if LiteLLM is available
        try
        {
            var provider = _resolver.Resolve("litellm") as LiteLlmProvider;
            _liteLlmAvailable = await provider!.IsHealthyAsync();

            if (_liteLlmAvailable)
            {
                _output.WriteLine("✓ LiteLLM proxy is available at http://localhost:4000");
            }
            else
            {
                _output.WriteLine("⚠ LiteLLM proxy is not available - integration tests will be skipped");
            }
        }
        catch
        {
            _liteLlmAvailable = false;
            _output.WriteLine("⚠ LiteLLM proxy is not available - integration tests will be skipped");
        }
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task LiteLlm_ShouldRespondWithQwen2B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Say 'Hello from qwen3.5-2b' and nothing else." }
        };

        // Act
        _output.WriteLine("Sending request to qwen3.5-2b...");
        var response = await provider.InvokeAsync(messages, model: "qwen3.5-2b");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("assistant", response.Role);
        Assert.NotEmpty(response.Content);

        _output.WriteLine($"Response from qwen3.5-2b: {response.Content}");
    }

    [Fact]
    public async Task LiteLlm_ShouldRespondWithQwen4B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Say 'Hello from qwen3.5-4b' and nothing else." }
        };

        // Act
        _output.WriteLine("Sending request to qwen3.5-4b...");
        var response = await provider.InvokeAsync(messages, model: "qwen3.5-4b");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("assistant", response.Role);
        Assert.NotEmpty(response.Content);

        _output.WriteLine($"Response from qwen3.5-4b: {response.Content}");
    }

    [Fact]
    public async Task LiteLlm_ShouldStreamResponseWithQwen2B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Count from 1 to 5, separated by spaces." }
        };

        // Act
        _output.WriteLine("Streaming request to qwen3.5-2b...");
        var chunks = new List<string>();
        await foreach (var chunk in provider.InvokeStreamingAsync(messages, model: "qwen3.5-2b"))
        {
            chunks.Add(chunk);
            _output.WriteLine($"Chunk: {chunk}");
        }

        // Assert
        Assert.NotEmpty(chunks);
        var fullResponse = string.Join("", chunks);
        Assert.NotEmpty(fullResponse);

        _output.WriteLine($"Full streamed response: {fullResponse}");
    }

    [Fact]
    public async Task LiteLlm_ShouldStreamResponseWithQwen4B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "List three primary colors, separated by commas." }
        };

        // Act
        _output.WriteLine("Streaming request to qwen3.5-4b...");
        var chunks = new List<string>();
        await foreach (var chunk in provider.InvokeStreamingAsync(messages, model: "qwen3.5-4b"))
        {
            chunks.Add(chunk);
            _output.WriteLine($"Chunk: {chunk}");
        }

        // Assert
        Assert.NotEmpty(chunks);
        var fullResponse = string.Join("", chunks);
        Assert.NotEmpty(fullResponse);

        _output.WriteLine($"Full streamed response: {fullResponse}");
    }

    [Fact]
    public async Task ProviderResolver_ShouldSwitchBetweenProviders()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Say 'test' and nothing else." }
        };

        // Act - Test LiteLLM
        _output.WriteLine("Testing LiteLLM provider...");
        var liteLlmProvider = _resolver!.Resolve("litellm");
        var liteLlmResponse = await liteLlmProvider.InvokeAsync(messages, model: "qwen3.5-2b");

        // Assert
        Assert.NotNull(liteLlmResponse);
        Assert.Equal("litellm", liteLlmProvider.ProviderName);

        _output.WriteLine($"LiteLLM response: {liteLlmResponse.Content}");

        // Note: Ollama test skipped if not available - integration test focuses on LiteLLM
    }

    [Fact]
    public async Task LiteLlm_ShouldHandleMultiTurnConversation_WithQwen2B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "What is 2+2?" },
            new() { Role = "assistant", Content = "2+2 equals 4." },
            new() { Role = "user", Content = "What about 3+3?" }
        };

        // Act
        _output.WriteLine("Testing multi-turn conversation with qwen3.5-2b...");
        var response = await provider.InvokeAsync(messages, model: "qwen3.5-2b");

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Content);

        _output.WriteLine($"Multi-turn response: {response.Content}");
    }

    [Fact]
    public async Task LiteLlm_ShouldHandleMultiTurnConversation_WithQwen4B()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "What is the capital of France?" },
            new() { Role = "assistant", Content = "The capital of France is Paris." },
            new() { Role = "user", Content = "What about Spain?" }
        };

        // Act
        _output.WriteLine("Testing multi-turn conversation with qwen3.5-4b...");
        var response = await provider.InvokeAsync(messages, model: "qwen3.5-4b");

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Content);

        _output.WriteLine($"Multi-turn response: {response.Content}");
    }

    [Fact]
    public async Task LiteLlm_CompareQwen2BAnd4BResponses()
    {
        if (!_liteLlmAvailable)
        {
            _output.WriteLine("Skipped: LiteLLM not available");
            return;
        }

        // Arrange
        var provider = _resolver!.Resolve("litellm");
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "user", Content = "Explain quantum computing in one sentence." }
        };

        // Act - Test both models
        _output.WriteLine("Comparing qwen3.5-2b and qwen3.5-4b...");

        var response2b = await provider.InvokeAsync(messages, model: "qwen3.5-2b");
        _output.WriteLine($"qwen3.5-2b: {response2b.Content}");

        var response4b = await provider.InvokeAsync(messages, model: "qwen3.5-4b");
        _output.WriteLine($"qwen3.5-4b: {response4b.Content}");

        // Assert
        Assert.NotNull(response2b);
        Assert.NotNull(response4b);
        Assert.NotEmpty(response2b.Content);
        Assert.NotEmpty(response4b.Content);

        // Both should provide valid responses (4b might be more detailed)
        _output.WriteLine($"2b length: {response2b.Content.Length}, 4b length: {response4b.Content.Length}");
    }
}
