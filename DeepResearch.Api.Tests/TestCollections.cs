using Xunit;

namespace DeepResearch.Api.Tests.Collections;

/// <summary>
/// Collection definition for unit tests.
/// Unit tests can run in parallel as they don't share state.
/// </summary>
[CollectionDefinition("Unit Tests")]
public class UnitTestCollection
{
    // No fixture needed - tests run independently
}

/// <summary>
/// Collection definition for integration tests that can run in parallel.
/// These tests don't require exclusive access to shared resources.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection
{
    // No fixture needed - tests can run independently
}

/// <summary>
/// Collection definition for Lightning Server tests.
/// These tests CANNOT run in parallel as they share a Lightning Server instance.
/// DisableParallelization = true prevents concurrent execution.
/// </summary>
[CollectionDefinition("Lightning Server Collection", DisableParallelization = true)]
public class LightningServerCollection
{
    // Simplified for API tests - no shared fixture
}

/// <summary>
/// Collection definition for tests requiring Docker services.
/// These tests CANNOT run in parallel as they share Docker container instances.
/// </summary>
[CollectionDefinition("Docker Services Collection", DisableParallelization = true)]
public class DockerServicesCollection
{
    // Multiple services might be started, need to prevent concurrent access
}

/// <summary>
/// Collection definition for performance/benchmark tests.
/// These tests CANNOT run in parallel as they require consistent system state for measurements.
/// </summary>
[CollectionDefinition("Performance Benchmarks", DisableParallelization = true)]
public class PerformanceBenchmarksCollection
{
    // Performance tests need exclusive access to measure accurately
}

/// <summary>
/// Collection definition for load/stress tests.
/// These tests CANNOT run in parallel as they stress-test the system.
/// </summary>
[CollectionDefinition("Load Tests", DisableParallelization = true)]
public class LoadTestsCollection
{
    // Load tests would interfere with each other if run in parallel
}
