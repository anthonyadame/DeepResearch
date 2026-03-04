using System.Net.Http.Json;
using DeepResearch.Api.DTOs.Requests.Experiments;
using DeepResearch.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeepResearch.Api.Tests.API;

[Collection("Integration Tests")]
public class ExperimentMetricsIntegrationTests
{
    [Fact]
    public async Task PostMetrics_CreatesMetricsFile()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "dra-tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(rootPath, "data", "experiments");
        var filePath = Path.Combine(dataDirectory, "metrics.jsonl");

        await using var factory = new ApiTestFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var settings = new Dictionary<string, string?>
                    {
                        ["ExperimentMetrics:DataDirectory"] = dataDirectory,
                        ["ExperimentMetrics:FileName"] = "metrics.jsonl"
                    };

                    config.AddInMemoryCollection(settings);
                });
            });

        try
        {
            var client = factory.CreateClient();
            var request = new ExperimentMetricEntryRequest
            {
                RunId = $"run-{Guid.NewGuid():N}",
                Task = "GSM8K",
                Phase = "integration-test",
                Metric = "reward_mean",
                Value = 0.42,
                Step = 1,
                Unit = "score",
                TimestampUtc = DateTime.UtcNow,
                Tags = new Dictionary<string, string>
                {
                    ["model"] = "gpt-oss-20b",
                    ["engine"] = "sglang"
                }
            };

            var response = await client.PostAsJsonAsync("/api/experiments/metrics", request);

            response.EnsureSuccessStatusCode();
            Assert.True(File.Exists(filePath));

            var payload = await File.ReadAllTextAsync(filePath);
            Assert.Contains(request.RunId, payload, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
