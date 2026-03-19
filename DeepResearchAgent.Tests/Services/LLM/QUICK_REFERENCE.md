# LLM Provider Testing - Quick Reference

## 🚀 Quick Start

### Run All Unit Tests (No Setup Required)
```bash
cd DeepResearchAgent.Tests
dotnet test --filter "Category!=Integration"
```

### Run Integration Tests (Requires LiteLLM)
```bash
# 1. Start LiteLLM
litellm --config litellm_config.yaml --port 4000

# 2. Run tests
dotnet test --filter "FullyQualifiedName~LiteLlmIntegrationTests"
```

## 📋 Test Files

| File | Tests | Purpose |
|------|-------|---------|
| OllamaLlmProviderTests.cs | 13 | Ollama provider unit tests |
| LiteLlmProviderTests.cs | 17 | LiteLLM/qwen provider unit tests |
| LlmProviderResolverTests.cs | 12 | Provider resolution tests |
| LiteLlmIntegrationTests.cs | 8 | Real LiteLLM/qwen integration tests |

## 🎯 Key Test Scenarios

### Qwen Model Tests
```csharp
// qwen3.5-2b (fast, lightweight)
await provider.InvokeAsync(messages, model: "qwen3.5-2b");

// qwen3.5-4b (more capable)
await provider.InvokeAsync(messages, model: "qwen3.5-4b");
```

### Provider Switching
```csharp
var ollamaProvider = resolver.Resolve("ollama");
var liteLlmProvider = resolver.Resolve("litellm");
```

### Streaming
```csharp
await foreach (var chunk in provider.InvokeStreamingAsync(messages))
{
    Console.Write(chunk);
}
```

## 🔧 LiteLLM Setup (One-Time)

### 1. Install
```bash
pip install litellm[proxy]
```

### 2. Create Config (litellm_config.yaml)
```yaml
model_list:
  - model_name: qwen3.5-2b
    litellm_params:
      model: ollama/qwen2.5:0.5b
      api_base: http://localhost:11434

  - model_name: qwen3.5-4b
    litellm_params:
      model: ollama/qwen2.5:3b
      api_base: http://localhost:11434
```

### 3. Pull Ollama Models
```bash
ollama pull qwen2.5:0.5b  # For qwen3.5-2b
ollama pull qwen2.5:3b    # For qwen3.5-4b
```

### 4. Start LiteLLM
```bash
litellm --config litellm_config.yaml --port 4000
```

### 5. Verify
```bash
curl http://localhost:4000/health
```

## 📊 Test Coverage

- ✅ **OllamaLlmProvider**: 100% (13/13 tests)
- ✅ **LiteLlmProvider**: 100% (17/17 tests)
- ✅ **LlmProviderResolver**: 100% (12/12 tests)
- ✅ **Integration**: 8 real-world scenarios

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| Tests skipped | Start LiteLLM: `litellm --config litellm_config.yaml --port 4000` |
| Connection refused | Check LiteLLM running: `curl http://localhost:4000/health` |
| Model not found | Pull models: `ollama pull qwen2.5:0.5b qwen2.5:3b` |
| Timeout | First request loads model (~10s), subsequent faster |

## 💡 Model Comparison

| Model | Size | Speed | Use Case |
|-------|------|-------|----------|
| qwen3.5-2b | 0.5B | Fast | Simple tasks, high throughput |
| qwen3.5-4b | 3B | Slower | Complex reasoning, quality |

## 📝 Common Test Commands

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter "Category!=Integration"

# Specific provider
dotnet test --filter "FullyQualifiedName~LiteLlmProviderTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Watch mode
dotnet watch test
```

## 🎓 Example Test

```csharp
[Fact]
public async Task LiteLlm_ShouldRespondWithQwen2B()
{
    // Arrange
    var provider = resolver.Resolve("litellm");
    var messages = new List<OllamaChatMessage>
    {
        new() { Role = "user", Content = "Hello" }
    };

    // Act
    var response = await provider.InvokeAsync(messages, model: "qwen3.5-2b");

    // Assert
    Assert.NotEmpty(response.Content);
}
```

## 🔗 Resources

- [Full Documentation](README.md)
- [Test Summary](../LLM_TESTING_SUMMARY.md)
- [LiteLLM Docs](https://docs.litellm.ai/)
- [Ollama API](https://github.com/ollama/ollama/blob/main/docs/api.md)
