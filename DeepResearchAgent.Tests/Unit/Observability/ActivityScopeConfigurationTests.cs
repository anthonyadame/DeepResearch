using DeepResearchAgent.Observability;
using System.Diagnostics;
using Xunit;

namespace DeepResearchAgent.Tests.Unit.Observability;

/// <summary>
/// Unit tests for ActivityScope configuration-aware behavior
/// </summary>
public class ActivityScopeConfigurationTests
{
    [Fact]
    public void Start_WhenTracingDisabled_ReturnsNoOpScope()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = false
        });

        // Act
        using var scope = ActivityScope.Start("TestOperation");

        // Assert - No exception should be thrown, scope should work but not create activity
        scope.AddTag("test.key", "test.value");
        scope.SetStatus(ActivityStatusCode.Ok);
        // If this completes without error, no-op mode is working
    }

    [Fact]
    public void Start_WhenTracingEnabled_CreatesActivity()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            TraceSamplingRate = 1.0
        });

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticConfig.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var scope = ActivityScope.Start("TestOperation");

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Equal("TestOperation", capturedActivity.OperationName);
    }

    [Fact]
    public void AddEvent_WhenEventsDisabled_DoesNotAddEvent()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            EnableActivityEvents = false
        });

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticConfig.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var scope = ActivityScope.Start("TestOperation");
        scope.AddEvent("TestEvent");

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Empty(capturedActivity.Events);
    }

    [Fact]
    public void AddEvent_WhenEventsEnabled_AddsEvent()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            EnableActivityEvents = true
        });

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticConfig.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var scope = ActivityScope.Start("TestOperation");
        scope.AddEvent("TestEvent");

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Single(capturedActivity.Events);
        Assert.Equal("TestEvent", capturedActivity.Events.First().Name);
    }

    [Fact]
    public void RecordException_WhenExceptionRecordingDisabled_DoesNotRecordException()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            EnableExceptionRecording = false
        });

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticConfig.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var scope = ActivityScope.Start("TestOperation");
        scope.RecordException(new InvalidOperationException("Test exception"));

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Empty(capturedActivity.Events); // No exception event should be recorded
    }

    [Fact]
    public void RecordException_WhenExceptionRecordingEnabled_RecordsException()
    {
        // Arrange
        ActivityScope.Configure(new ObservabilityConfiguration
        {
            EnableTracing = true,
            EnableExceptionRecording = true
        });

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticConfig.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var scope = ActivityScope.Start("TestOperation");
        scope.RecordException(new InvalidOperationException("Test exception"));

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Equal(ActivityStatusCode.Error, capturedActivity.Status);
        Assert.Single(capturedActivity.Events);
        Assert.Equal("exception", capturedActivity.Events.First().Name);
    }

    [Fact]
    public void Configure_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ActivityScope.Configure(null!));
    }

    [Fact]
    public void Configure_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new ObservabilityConfiguration
        {
            TraceSamplingRate = 2.0 // Invalid: > 1.0
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ActivityScope.Configure(invalidConfig));
    }

    [Fact]
    public void GetConfiguration_ReturnsCurrentConfiguration()
    {
        // Arrange
        var expectedConfig = new ObservabilityConfiguration
        {
            EnableTracing = false,
            TraceSamplingRate = 0.25
        };
        ActivityScope.Configure(expectedConfig);

        // Act
        var actualConfig = ActivityScope.GetConfiguration();

        // Assert
        Assert.Same(expectedConfig, actualConfig);
        Assert.Equal(0.25, actualConfig.TraceSamplingRate);
    }
}
