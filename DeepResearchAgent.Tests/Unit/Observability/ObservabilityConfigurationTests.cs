using DeepResearchAgent.Observability;
using Xunit;

namespace DeepResearchAgent.Tests.Unit.Observability;

/// <summary>
/// Unit tests for ObservabilityConfiguration
/// </summary>
public class ObservabilityConfigurationTests
{
    [Fact]
    public void Validate_WithValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var config = new ObservabilityConfiguration
        {
            TraceSamplingRate = 0.5,
            SlowOperationThresholdMs = 1000,
            AsyncMetricsQueueSize = 10000
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_WithInvalidTraceSamplingRate_ThrowsArgumentException(double invalidRate)
    {
        // Arrange
        var config = new ObservabilityConfiguration
        {
            TraceSamplingRate = invalidRate
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("TraceSamplingRate must be between 0.0 and 1.0", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeSlowOperationThreshold_ThrowsArgumentException()
    {
        // Arrange
        var config = new ObservabilityConfiguration
        {
            SlowOperationThresholdMs = -1
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("SlowOperationThresholdMs must be non-negative", exception.Message);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(1000001)]
    public void Validate_WithInvalidAsyncMetricsQueueSize_ThrowsArgumentException(int invalidSize)
    {
        // Arrange
        var config = new ObservabilityConfiguration
        {
            AsyncMetricsQueueSize = invalidSize
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("AsyncMetricsQueueSize must be between 100 and 1,000,000", exception.Message);
    }

    [Fact]
    public void Development_ReturnsFullObservabilityConfiguration()
    {
        // Act
        var config = ObservabilityConfiguration.Development();

        // Assert
        Assert.True(config.EnableTracing);
        Assert.True(config.EnableMetrics);
        Assert.True(config.EnableDetailedTracing);
        Assert.Equal(1.0, config.TraceSamplingRate);
        Assert.Equal(0, config.SlowOperationThresholdMs);
        Assert.False(config.UseAsyncMetrics);
        Assert.True(config.EnableActivityEvents);
        Assert.True(config.EnableExceptionRecording);
    }

    [Fact]
    public void Production_ReturnsMinimalOverheadConfiguration()
    {
        // Act
        var config = ObservabilityConfiguration.Production();

        // Assert
        Assert.True(config.EnableTracing);
        Assert.True(config.EnableMetrics);
        Assert.False(config.EnableDetailedTracing);
        Assert.Equal(0.1, config.TraceSamplingRate); // 10% sampling
        Assert.Equal(10000, config.SlowOperationThresholdMs); // 10s threshold
        Assert.True(config.UseAsyncMetrics);
        Assert.Equal(50000, config.AsyncMetricsQueueSize);
        Assert.False(config.EnableActivityEvents);
        Assert.True(config.EnableExceptionRecording);
    }

    [Fact]
    public void Staging_ReturnsBalancedConfiguration()
    {
        // Act
        var config = ObservabilityConfiguration.Staging();

        // Assert
        Assert.True(config.EnableTracing);
        Assert.True(config.EnableMetrics);
        Assert.True(config.EnableDetailedTracing);
        Assert.Equal(0.5, config.TraceSamplingRate); // 50% sampling
        Assert.Equal(5000, config.SlowOperationThresholdMs); // 5s threshold
        Assert.True(config.UseAsyncMetrics);
        Assert.Equal(25000, config.AsyncMetricsQueueSize);
        Assert.True(config.EnableActivityEvents);
        Assert.True(config.EnableExceptionRecording);
    }

    [Fact]
    public void GetSummary_ReturnsFormattedConfigurationString()
    {
        // Arrange
        var config = new ObservabilityConfiguration
        {
            EnableTracing = true,
            TraceSamplingRate = 0.75
        };

        // Act
        var summary = config.GetSummary();

        // Assert
        Assert.Contains("Tracing: Enabled", summary);
        Assert.Contains("Sampling Rate: 75%", summary);
    }
}
