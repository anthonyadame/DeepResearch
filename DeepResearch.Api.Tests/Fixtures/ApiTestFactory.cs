using DeepResearchAgent.Services;
using DeepResearchAgent.Services.LLM;
using DeepResearch.Api.Services.ChatHistory;
using DeepResearch.Api.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeepResearch.Api.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that provides mock services for testing
/// </summary>
public class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiTestFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add default test configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add default JWT settings for all tests
            var defaultSettings = new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-minimum-32-characters-long-for-security-purposes",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpirationMinutes"] = "60"
            };
            config.AddInMemoryCollection(defaultSettings);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real ILlmProvider
            var llmProviderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILlmProvider));
            if (llmProviderDescriptor != null)
            {
                services.Remove(llmProviderDescriptor);
            }

            // Remove real OllamaCategorizationService
            var categorizationDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(OllamaCategorizationService));
            if (categorizationDescriptor != null)
            {
                services.Remove(categorizationDescriptor);
            }

            // Remove ICategorizationService
            var iCategorizationDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategorizationService));
            if (iCategorizationDescriptor != null)
            {
                services.Remove(iCategorizationDescriptor);
            }

            // Remove IAgentLightningService
            var agentLightningDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentLightningService));
            if (agentLightningDescriptor != null)
            {
                services.Remove(agentLightningDescriptor);
            }

            // Remove ILightningRLCSService
            var rlcsDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILightningRLCSService));
            if (rlcsDescriptor != null)
            {
                services.Remove(rlcsDescriptor);
            }

            // Add mock services
            services.AddSingleton<ILlmProvider, MockLlmProvider>();
            services.AddSingleton<ICategorizationService, MockCategorizationService>();
            services.AddSingleton<IAgentLightningService, MockAgentLightningService>();
            services.AddSingleton<ILightningRLCSService, MockLightningRLCSService>();

            // Bypass authentication for all tests
            services.AddSingleton<IPolicyEvaluator, FakePolicyEvaluator>();

            // Allow additional test-specific configuration
            _configureServices?.Invoke(services);
        });

        // Use test environment
        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Disable service validation for tests to avoid validation before ConfigureServices runs
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = false;
            options.ValidateOnBuild = false; // This is the key - disable validation
        });

        return base.CreateHost(builder);
    }
}

/// <summary>
/// Fake policy evaluator that bypasses all authorization for integration tests
/// </summary>
public class FakePolicyEvaluator : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var principal = new System.Security.Claims.ClaimsPrincipal();
        principal.AddIdentity(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim("sub", "test-user"),
            new System.Security.Claims.Claim("name", "Test User")
        }, "Test"));

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, "Test")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(
        AuthorizationPolicy policy, 
        AuthenticateResult authenticationResult, 
        HttpContext context, 
        object? resource)
    {
        return Task.FromResult(PolicyAuthorizationResult.Success());
    }
}
