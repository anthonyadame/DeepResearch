namespace DeepResearch.Api.Extensions;

using DeepResearch.Api.Services.ChatHistory;
using DeepResearch.Api.Services.BackgroundTasks;
using DeepResearch.Api.Services.Experiments;
using DTOs.Validators;
using Services;
using Services.Implementations;
using FluentValidation;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for service registration in Dependency Injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all API services, validators, and mappings to the DI container.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<MasterWorkflowRequestValidator>();

        // Register orchestration service implementations
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<ICoreService, CoreService>();
        services.AddScoped<IHealthService, HealthService>();

        // Register experiment metrics logger
        services.AddSingleton<IExperimentMetricsLogger, FileExperimentMetricsLogger>();

        // Register legacy in-memory chat session service (for migration)
        services.AddSingleton<ChatSessionService>();

        // Register chat integration service
        services.AddScoped<ChatIntegrationService>();

        return services;
    }
    
    /// <summary>
    /// Add chat history services with LightningStore persistence, AI categorization, and auto-archiving
    /// </summary>
    public static IServiceCollection AddChatHistoryServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Chat history service using LightningStore
        services.AddSingleton<ICategorizationService, OllamaCategorizationService>();
        services.AddSingleton<IChatHistoryService, LightningChatHistoryService>();
        
        // Migration service for converting in-memory sessions
        services.AddSingleton<ChatSessionMigrationService>();
        
        // Background service for automatic archiving
        services.AddHostedService<ChatArchiveBackgroundService>();

        return services;
    }

    /// <summary>
    /// Add CORS configuration for API.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, string policyName = "ApiCorsPolicy")
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    /// <summary>
    /// Add Swagger/OpenAPI documentation.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Deep Research Agent API",
                Version = "v1.0",
                Description = @"
**Comprehensive API for research automation with 5-tier architecture:**

## Architecture Tiers
1. **Workflows** - Master, Supervisor, and Researcher workflows
2. **Agents** - Specialized agents (Clarify, ResearchBrief, Researcher, Analyst, DraftReport, Report)
3. **Core Services** - LLM, Search, Scraping, State Management, Vector Operations
4. **Operations** - Tool invocations and metrics
5. **Health** - System health monitoring

## Key Features
- Multi-agent research orchestration
- Vector-based knowledge retrieval
- State management with Lightning
- Real-time metrics and monitoring
- Extensible tool framework

## Getting Started
1. Check `/health` endpoint
2. Start with `/api/workflows/master` for complete research pipeline
3. Use individual agents for specialized tasks
",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "Deep Research Agent Team",
                    Url = new Uri("https://github.com/anthonyadame/DeepResearchAgent")
                },
                License = new Microsoft.OpenApi.Models.OpenApiLicense
                {
                    Name = "MIT License"
                }
            });

            // Custom schema IDs to avoid conflicts between namespaces
            options.CustomSchemaIds(type => 
            {
                if (type.FullName != null && type.FullName.Contains("DeepResearchAgent.Api.DTOs"))
                {
                    // For API DTOs, use simple name if in Common, otherwise include parent folder
                    if (type.FullName.Contains(".Common."))
                    {
                        return type.Name;
                    }
                    else
                    {
                        // Extract parent folder name (e.g., "Services", "Agents", "Workflows")
                        var parts = type.FullName.Split('.');
                        var dtoIndex = Array.IndexOf(parts, "DTOs");
                        if (dtoIndex >= 0 && dtoIndex + 2 < parts.Length)
                        {
                            var category = parts[dtoIndex + 1]; // Requests/Responses
                            var subcategory = parts[dtoIndex + 2]; // Services/Agents/Workflows
                            return $"{subcategory}{category}{type.Name}";
                        }
                    }
                }
                return type.FullName?.Replace("+", ".") ?? type.Name;
            });

            // Group endpoints by tags
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                {
                    return new[] { api.GroupName };
                }

                var controllerActionDescriptor = api.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
                if (controllerActionDescriptor != null)
                {
                    return new[] { controllerActionDescriptor.ControllerName };
                }

                return new[] { "Unknown" };
            });

            options.DocInclusionPredicate((name, api) => true);

            // Include XML comments from DTOs
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Order actions by method
            options.OrderActionsBy(apiDesc => $"{apiDesc.RelativePath}");
        });

        return services;
    }

    /// <summary>
    /// Add request/response compression.
    /// </summary>
    public static IServiceCollection AddApiCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        return services;
    }

    /// <summary>
    /// Add health checks.
    /// </summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}
