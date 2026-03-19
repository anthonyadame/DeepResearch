# LLM Provider Tests

This directory contains comprehensive tests for the LLM provider pattern implementation.

## Test Structure

### Unit Tests
- **OllamaLlmProviderTests.cs** - Tests for Ollama provider implementation
- **LiteLlmProviderTests.cs** - Tests for LiteLLM provider with qwen models
- **LlmProviderResolverTests.cs** - Tests for provider resolution logic

### Integration Tests
- **LiteLlmIntegrationTests.cs** - Real-world tests with LiteLLM proxy and qwen models

## Prerequisites

### For Unit Tests
No external dependencies required. All unit tests use mocks.

```bash
dotnet test --filter "Category!=Integration"
```

### For Integration Tests

#### LiteLLM Setup
1. Install LiteLLM:
```bash
pip install litellm[proxy]
```

2. Create a LiteLLM config file (`litellm_config.yaml`):
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

general_settings:
  master_key: sk-1234  # Optional for testing
  database_url: "sqlite:///litellm.db"
```

3. Start LiteLLM proxy:
```bash
litellm --config litellm_config.yaml --port 4000
```

4. Verify LiteLLM is running:
```bash
curl http://localhost:4000/health
```

#### Ollama Setup (for qwen models)
1. Install Ollama from https://ollama.ai

2. Pull the qwen models:
```bash
ollama pull qwen2.5:0.5b  # For qwen3.5-2b
ollama pull qwen2.5:3b    # For qwen3.5-4b
```

3. Verify Ollama is running:
```bash
curl http://localhost:11434/api/tags
```

## Running Tests

### Run All Tests
```bash
cd DeepResearchAgent.Tests
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test --filter "FullyQualifiedName~LLM&Category!=Integration"
```

### Run Integration Tests Only
```bash
dotnet test --filter "FullyQualifiedName~LiteLlmIntegrationTests"
```

### Run Specific Provider Tests
```bash
# Ollama provider tests
dotnet test --filter "FullyQualifiedName~OllamaLlmProviderTests"

# LiteLLM provider tests
dotnet test --filter "FullyQualifiedName~LiteLlmProviderTests"

# Resolver tests
dotnet test --filter "FullyQualifiedName~LlmProviderResolverTests"
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Coverage

### OllamaLlmProviderTests
- ✓ Constructor validation
- ✓ Provider name and model properties
- ✓ InvokeAsync with default and custom models
- ✓ InvokeStreamingAsync with chunk streaming
- ✓ Error handling for HTTP failures
- ✓ Health check functionality

### LiteLlmProviderTests
- ✓ Constructor validation
- ✓ Provider name and model properties
- ✓ InvokeAsync with qwen3.5-2b model
- ✓ InvokeAsync with qwen3.5-4b model
- ✓ API key authentication
- ✓ InvokeStreamingAsync with SSE format
- ✓ Stream termination with [DONE] marker
- ✓ Error handling for HTTP failures
- ✓ Health check functionality
- ✓ Model switching between qwen variants

### LlmProviderResolverTests
- ✓ Provider registration and discovery
- ✓ Default provider resolution
- ✓ Explicit provider resolution (ollama/litellm)
- ✓ Case-insensitive resolution
- ✓ Fallback to default when provider not found
- ✓ Error handling for missing providers
- ✓ GetAvailableProviders functionality
- ✓ Multi-model support per provider

### LiteLlmIntegrationTests
- ✓ Real responses from qwen3.5-2b
- ✓ Real responses from qwen3.5-4b
- ✓ Streaming with qwen3.5-2b
- ✓ Streaming with qwen3.5-4b
- ✓ Provider switching (ollama ↔ litellm)
- ✓ Multi-turn conversations
- ✓ Model comparison (2b vs 4b)
- ✓ Automatic skip if LiteLLM unavailable

## Test Configuration

Configuration is loaded from `appsettings.test.json`:

```json
{
  "LlmProvider": {
    "Provider": "litellm",
    "LiteLLM": {
      "BaseUrl": "http://localhost:4000",
      "DefaultModel": "qwen3.5-2b"
    }
  },
  "TestModels": {
    "LiteLLM": {
      "Small": "qwen3.5-2b",
      "Medium": "qwen3.5-4b"
    }
  }
}
```

## Troubleshooting

### Integration Tests Skipped
If integration tests are skipped with message "LiteLLM not available":
1. Verify LiteLLM is running: `curl http://localhost:4000/health`
2. Check Ollama is running: `curl http://localhost:11434/api/tags`
3. Verify models are available: `ollama list`
4. Check LiteLLM config has qwen models configured

### Connection Refused
- Ensure LiteLLM proxy is running on port 4000
- Check firewall settings
- Verify no other service is using port 4000

### Model Not Found
- Pull required models: `ollama pull qwen2.5:0.5b qwen2.5:3b`
- Update LiteLLM config to match available models
- Restart LiteLLM proxy after config changes

### Timeout Errors
- Increase timeout in test configuration
- Check model is loaded in Ollama (first request loads model)
- Verify system resources (qwen models need RAM)

## Performance Notes

### Model Comparison
- **qwen3.5-2b** (qwen2.5:0.5b)
  - Faster inference
  - Lower memory usage (~1-2GB)
  - Good for simple tasks
  - Suitable for high-throughput scenarios

- **qwen3.5-4b** (qwen2.5:3b)
  - Better quality responses
  - Higher memory usage (~3-4GB)
  - Better reasoning capability
  - Suitable for complex tasks

### Expected Test Duration
- Unit tests: < 1 second
- Integration tests: 10-30 seconds (model loading + inference)
- Full test suite: < 1 minute

## CI/CD Integration

### GitHub Actions Example
```yaml
name: LLM Provider Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Run Unit Tests
        run: dotnet test --filter "Category!=Integration"

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Install Ollama
        run: curl -fsSL https://ollama.ai/install.sh | sh
      - name: Pull Models
        run: |
          ollama pull qwen2.5:0.5b
          ollama pull qwen2.5:3b
      - name: Install LiteLLM
        run: pip install litellm[proxy]
      - name: Start LiteLLM
        run: litellm --config litellm_config.yaml --port 4000 &
      - name: Run Integration Tests
        run: dotnet test --filter "FullyQualifiedName~LiteLlmIntegrationTests"
```

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Use descriptive test names
3. Add comments explaining complex scenarios
4. Update this README with new test coverage
5. Ensure tests are deterministic (no random failures)
6. Mock external dependencies in unit tests
7. Skip integration tests gracefully when services unavailable

## Additional Resources

- [LiteLLM Documentation](https://docs.litellm.ai/)
- [Ollama API Reference](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Qwen Model Family](https://github.com/QwenLM/Qwen)
- [xUnit Documentation](https://xunit.net/)
