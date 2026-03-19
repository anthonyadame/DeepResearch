using DeepResearchAgent.Configuration;
using Xunit;

namespace DeepResearchAgent.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for LlmModelTierConfiguration.
/// Validates tier-to-model mappings, factory methods, and configuration validation.
/// </summary>
public class LlmModelTierConfigurationTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var config = new LlmModelTierConfiguration();

        // Assert
        Assert.Equal("llama3.2:3b", config.FastModel);
        Assert.Equal("llama3.1:13b", config.BalancedModel);
        Assert.Equal("qwen2.5:32b", config.PowerModel);
        Assert.True(config.EnableTierSelection);
        Assert.True(config.EnableTierMetrics);
    }

    [Fact]
    public void GetModelForTier_Fast_ReturnsCorrectModel()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "test-fast"
        };

        // Act
        var model = config.GetModelForTier(LlmModelTier.Fast);

        // Assert
        Assert.Equal("test-fast", model);
    }

    [Fact]
    public void GetModelForTier_Balanced_ReturnsCorrectModel()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            BalancedModel = "test-balanced"
        };

        // Act
        var model = config.GetModelForTier(LlmModelTier.Balanced);

        // Assert
        Assert.Equal("test-balanced", model);
    }

    [Fact]
    public void GetModelForTier_Power_ReturnsCorrectModel()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            PowerModel = "test-power"
        };

        // Act
        var model = config.GetModelForTier(LlmModelTier.Power);

        // Assert
        Assert.Equal("test-power", model);
    }

    [Fact]
    public void GetModelForTier_InvalidTier_ThrowsArgumentException()
    {
        // Arrange
        var config = new LlmModelTierConfiguration();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.GetModelForTier((LlmModelTier)999));
    }

    [Fact]
    public void Validate_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "model1",
            BalancedModel = "model2",
            PowerModel = "model3"
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_EmptyFastModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "",
            BalancedModel = "model2",
            PowerModel = "model3"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("FastModel", exception.Message);
    }

    [Fact]
    public void Validate_EmptyBalancedModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "model1",
            BalancedModel = "",
            PowerModel = "model3"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("BalancedModel", exception.Message);
    }

    [Fact]
    public void Validate_EmptyPowerModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "model1",
            BalancedModel = "model2",
            PowerModel = ""
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
        Assert.Contains("PowerModel", exception.Message);
    }

    [Fact]
    public void Development_ReturnsCorrectConfiguration()
    {
        // Act
        var config = LlmModelTierConfiguration.Development();

        // Assert
        Assert.Equal("llama3.2:3b", config.FastModel);
        Assert.Equal("llama3.1:8b", config.BalancedModel);
        Assert.Equal("llama3.1:13b", config.PowerModel);
        Assert.True(config.EnableTierSelection);
        Assert.True(config.EnableTierMetrics);
    }

    [Fact]
    public void Production_ReturnsCorrectConfiguration()
    {
        // Act
        var config = LlmModelTierConfiguration.Production();

        // Assert
        Assert.Equal("llama3.2:3b", config.FastModel);
        Assert.Equal("llama3.1:13b", config.BalancedModel);
        Assert.Equal("qwen2.5:32b", config.PowerModel);
        Assert.True(config.EnableTierSelection);
        Assert.True(config.EnableTierMetrics);
    }

    [Fact]
    public void HighPerformance_ReturnsCorrectConfiguration()
    {
        // Act
        var config = LlmModelTierConfiguration.HighPerformance();

        // Assert
        Assert.Equal("mistral:7b", config.FastModel);
        Assert.Equal("mixtral:8x7b", config.BalancedModel);
        Assert.Equal("llama3.1:70b", config.PowerModel);
        Assert.True(config.EnableTierSelection);
        Assert.True(config.EnableTierMetrics);
    }

    [Fact]
    public void GetSummary_ReturnsFormattedString()
    {
        // Arrange
        var config = new LlmModelTierConfiguration
        {
            FastModel = "fast-model",
            BalancedModel = "balanced-model",
            PowerModel = "power-model",
            EnableTierSelection = true,
            EnableTierMetrics = false
        };

        // Act
        var summary = config.GetSummary();

        // Assert
        Assert.Contains("fast-model", summary);
        Assert.Contains("balanced-model", summary);
        Assert.Contains("power-model", summary);
        Assert.Contains("Enabled", summary);
        Assert.Contains("Disabled", summary);
    }

    [Fact]
    public void GetSummary_ContainsAllFields()
    {
        // Arrange
        var config = LlmModelTierConfiguration.Production();

        // Act
        var summary = config.GetSummary();

        // Assert
        Assert.Contains("Fast", summary);
        Assert.Contains("Balanced", summary);
        Assert.Contains("Power", summary);
        Assert.Contains("Tier Selection", summary);
        Assert.Contains("Tier Metrics", summary);
    }
}
