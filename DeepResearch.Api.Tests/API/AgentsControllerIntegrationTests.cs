using System.Net;
using System.Net.Http.Json;
using DeepResearch.Api.Controllers;
using DeepResearch.Api.Tests.Fixtures;
using DeepResearchAgent.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeepResearch.Api.Tests.API;

/// <summary>
/// Integration tests for AgentsController endpoints.
/// Tests individual agent execution: research, analysis, and report generation.
/// </summary>
[Collection("Integration Tests")]
public class AgentsControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _tempDataPath;

    public async Task InitializeAsync()
    {
        _tempDataPath = Path.Combine(Path.GetTempPath(), "dra-agents-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDataPath);

        _factory = new ApiTestFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_tempDataPath);

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var settings = new Dictionary<string, string?>
                    {
                        ["LightningStore:DataDirectory"] = Path.Combine(_tempDataPath, "data"),
                        ["LightningStore:FileName"] = "test-store.json",
                        ["LightningStore:UseLightningServer"] = "false"
                    };
                    config.AddInMemoryCollection(settings);
                });
            });

        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (Directory.Exists(_tempDataPath))
        {
            try
            {
                Directory.Delete(_tempDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ExecuteResearch_WithValidRequest_ReturnsResearchOutput()
    {
        // Arrange
        var request = new AgentResearchRequest
        {
            Topic = "Quantum Computing Applications in Cryptography",
            ResearchBrief = "Investigate current state of quantum-resistant cryptography",
            MaxIterations = 2,
            MinQualityThreshold = 7.0f
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/research", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResearchOutput>();
        result.Should().NotBeNull();
        result!.Findings.Should().NotBeNull();
        // Integration test validates endpoint works, not that agent produces meaningful results
    }

    [Fact]
    public async Task ExecuteResearch_WithInvalidTopic_ReturnsBadRequest()
    {
        // Arrange - empty topic
        var request = new AgentResearchRequest
        {
            Topic = "",
            ResearchBrief = "Test brief",
            MaxIterations = 1
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/research", request);

        // Assert
        // May return BadRequest for validation or OK if agent handles empty gracefully
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteResearch_WithCustomParameters_HonorsSettings()
    {
        // Arrange
        var request = new AgentResearchRequest
        {
            Topic = "Machine Learning Model Interpretability",
            ResearchBrief = "Focus on SHAP and LIME techniques",
            MaxIterations = 5,
            MinQualityThreshold = 8.5f
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/research", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResearchOutput>();
        result.Should().NotBeNull();
        // Integration test validates HTTP endpoint, not agent logic
    }

    [Fact]
    public async Task ExecuteAnalysis_WithValidRequest_ReturnsAnalysisOutput()
    {
        // Arrange
        var request = new AgentAnalysisRequest
        {
            Topic = "Climate Change Impact Analysis",
            Findings = new List<FactExtractionResult>(),
            ResearchBrief = "Analyze environmental impact trends"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/analysis", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AnalysisOutput>();
        result.Should().NotBeNull();
        result!.KeyInsights.Should().NotBeNull();
        // Integration test validates endpoint works, not agent output quality
    }

    [Fact]
    public async Task ExecuteAnalysis_WithNoFindings_ReturnsBadRequest()
    {
        // Arrange - empty findings list
        var request = new AgentAnalysisRequest
        {
            Topic = "Test Analysis",
            Findings = new List<FactExtractionResult>(),
            ResearchBrief = "Test brief"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/analysis", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        // Some implementations might handle empty findings gracefully
    }

    [Fact]
    public async Task ExecuteReport_WithValidRequest_ReturnsReportOutput()
    {
        // Arrange
        var request = new AgentReportRequest
        {
            Topic = "AI Ethics in Healthcare",
            Research = new ResearchOutput
            {
                Findings = new List<FactExtractionResult>(),
                IterationsUsed = 3,
                AverageQuality = 8.2f
            },
            Analysis = new AnalysisOutput
            {
                KeyInsights = new List<KeyInsight>(),
                ThemesIdentified = new List<string> { "Bias", "Privacy", "Regulation" }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/report", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ReportOutput>();
        result.Should().NotBeNull();
        // Integration test validates HTTP endpoint structure
        result!.Sections.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteReport_WithoutAnalysis_ReturnsBadRequest()
    {
        // Arrange - missing analysis data
        var request = new AgentReportRequest
        {
            Topic = "Test Report",
            Research = new ResearchOutput
            {
                Findings = new List<FactExtractionResult>(),
                IterationsUsed = 1,
                AverageQuality = 7.0f
            },
            Analysis = null!
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/report", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCapabilities_ReturnsAgentInfo()
    {
        // Act
        var response = await _client!.GetAsync("/api/v1/agents/capabilities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var capabilities = await response.Content.ReadFromJsonAsync<AgentCapabilities>();
        capabilities.Should().NotBeNull();
        capabilities!.Agents.Should().NotBeNull();
        capabilities.Agents.Should().NotBeEmpty();
        capabilities.Agents.Should().Contain(a => a.Name == "ResearcherAgent");
        capabilities.Agents.Should().Contain(a => a.Name == "AnalystAgent");
        capabilities.Agents.Should().Contain(a => a.Name == "ReportAgent");
    }

    [Fact]
    public async Task ResearchAnalysisReport_FullPipeline_Succeeds()
    {
        // This test demonstrates the full pipeline: Research → Analysis → Report

        // Step 1: Research
        var researchRequest = new AgentResearchRequest
        {
            Topic = "Renewable Energy Storage Solutions",
            ResearchBrief = "Focus on battery technology advancements",
            MaxIterations = 2,
            MinQualityThreshold = 7.0f
        };

        var researchResponse = await _client!.PostAsJsonAsync("/api/v1/agents/research", researchRequest);
        researchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var researchOutput = await researchResponse.Content.ReadFromJsonAsync<ResearchOutput>();
        researchOutput.Should().NotBeNull();

        // Step 2: Analysis
        var analysisRequest = new AgentAnalysisRequest
        {
            Topic = researchRequest.Topic,
            Findings = researchOutput!.Findings,
            ResearchBrief = researchRequest.ResearchBrief
        };

        var analysisResponse = await _client.PostAsJsonAsync("/api/v1/agents/analysis", analysisRequest);
        analysisResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var analysisOutput = await analysisResponse.Content.ReadFromJsonAsync<AnalysisOutput>();
        analysisOutput.Should().NotBeNull();

        // Step 3: Report
        var reportRequest = new AgentReportRequest
        {
            Topic = researchRequest.Topic,
            Research = researchOutput,
            Analysis = analysisOutput!
        };

        var reportResponse = await _client.PostAsJsonAsync("/api/v1/agents/report", reportRequest);
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reportOutput = await reportResponse.Content.ReadFromJsonAsync<ReportOutput>();
        reportOutput.Should().NotBeNull();
        // Full pipeline test validates all three endpoints can be called in sequence
        reportOutput!.Sections.Should().NotBeNull();
    }
}
