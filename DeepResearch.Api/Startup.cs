using DeepResearchAgent.Configuration;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using DeepResearchAgent.Observability;
using DeepResearch.Api.Services.Auth;
using DeepResearch.Api.Services.Chat;
using DeepResearch.Api.Extensions;
using DeepResearch.Api.Middleware;
using DeepResearch.Api.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.IO;
using System.Text;

namespace DeepResearch.Api;

/// <summary>
/// API startup and configuration for DeepResearch.Api project.
/// Registers services and configures middleware for workflow management API.
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Configure services required by the API.
    /// This method is called by the runtime. Use this method to add services to the DI container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure Observability (must be first to initialize ActivityScope)
        ConfigureObservability(services);

        // Configure OpenTelemetry exporters
        ConfigureOpenTelemetry(services);

        // Add Core Services
        services.AddScoped<IWorkflowPauseResumeService, WorkflowPauseResumeService>();
        services.AddCheckpointService(options =>
        {
            options.EnableAutoCheckpoints = true;
            options.CheckpointAfterEachAgent = true;
            options.MaxCheckpointsPerWorkflow = 10;
            options.LocalStorageDirectory = "data/checkpoints";
        });

        services.AddHttpClient();
        services.AddMemoryCache(); // Required by LightningStateService
        RegisterLightningStore(services);

        // Add DeepResearchAgent Services (Agents, Workflows, Core Services)
        services.AddDeepResearchAgentServices(_configuration);

        // Add API Services (Controllers, Validators, Orchestration Services)
        services.AddApiServices();

        // Add Chat History Services with LightningStore persistence
        services.AddChatHistoryServices(_configuration);

        // Add Authentication Services
        ConfigureAuthentication(services);

        // Add API Controllers
        services.AddControllers();

        // Add API Documentation (Swagger)
        services.AddApiDocumentation();

        // Add CORS
        services.AddApiCors("AllowAll");

        services.AddLogging(configure =>
        {
            configure.AddConsole();
        });

        var keysPath = _configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
    }

    private void RegisterLightningStore(IServiceCollection services)
    {
        var lightningServerUrl = _configuration["Lightning:ServerUrl"]
            ?? Environment.GetEnvironmentVariable("LIGHTNING_SERVER_URL")
            ?? "http://localhost:8090";

        services.AddSingleton(new LightningStoreOptions
        {
            DataDirectory = _configuration["LightningStore:DataDirectory"] ?? "data",
            FileName = _configuration["LightningStore:FileName"] ?? "lightningstore.json",
            LightningServerUrl = lightningServerUrl,
            UseLightningServer = _configuration.GetValue("LightningStore:UseLightningServer", true),
            ResourceNamespace = _configuration["LightningStore:ResourceNamespace"] ?? "facts"
        });

        services.AddSingleton<LightningStore>(sp => new LightningStore(
            sp.GetRequiredService<LightningStoreOptions>(),
            sp.GetRequiredService<HttpClient>()));

        // Also register as ILightningStore for compatibility
        services.AddSingleton<ILightningStore>(sp => sp.GetRequiredService<LightningStore>());
    }

    /// <summary>
    /// Configure JWT authentication.
    /// </summary>
    private void ConfigureAuthentication(IServiceCollection services)
    {
        // Get JWT configuration
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"] ?? "deepresearch-api";
        var audience = jwtSettings["Audience"] ?? "deepresearch-api";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        // Validate secret key (allow test/dev environment to use default key)
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            // Use default key for test/dev environments
            secretKey = "test-secret-key-minimum-32-characters-long-for-security-purposes-only-not-for-production";
        }

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 characters long for security");
        }

        // Register authentication services
        services.AddSingleton<ITokenService>(new JwtTokenService(secretKey, issuer, audience, expirationMinutes));
        services.AddScoped<IAuthenticationService, InMemoryAuthenticationService>();

        // Configure JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var key = Encoding.ASCII.GetBytes(secretKey);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true
            };

            // Log authentication failures for debugging
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers["Token-Expired"] = "true";
                    }
                    return Task.CompletedTask;
                }
            };
        });
    }

    /// <summary>
    /// Configure observability settings from appsettings.json
    /// </summary>
    private void ConfigureObservability(IServiceCollection services)
    {
        // Load ObservabilityConfiguration from appsettings
        var observabilityConfig = new ObservabilityConfiguration();
        _configuration.GetSection("Observability").Bind(observabilityConfig);

        // Validate configuration
        observabilityConfig.Validate();

        // Configure ActivityScope globally
        ActivityScope.Configure(observabilityConfig);

        // Register as singleton for DI (optional, for services that need configuration)
        services.AddSingleton(observabilityConfig);

        // Register AsyncMetricsCollector if enabled
        if (observabilityConfig.UseAsyncMetrics)
        {
            services.AddHostedService<AsyncMetricsCollector>();
            services.AddSingleton<AsyncMetricsCollector>(sp => 
                sp.GetServices<IHostedService>()
                    .OfType<AsyncMetricsCollector>()
                    .FirstOrDefault()!);
        }

        // Add health check for metrics queue
        services.AddHealthChecks()
            .AddCheck<MetricsQueueHealthCheck>(
                "metrics_queue",
                tags: new[] { "observability", "metrics" });

        // Log configuration summary
        var logger = services.BuildServiceProvider().GetService<ILogger<Startup>>();
        logger?.LogInformation("Observability Configuration Loaded:\n{Summary}", observabilityConfig.GetSummary());
    }

    /// <summary>
    /// Configure OpenTelemetry tracing and metrics exporters
    /// </summary>
    private void ConfigureOpenTelemetry(IServiceCollection services)
    {
        // Get configuration
        var observabilityConfig = services.BuildServiceProvider().GetService<ObservabilityConfiguration>();

        if (observabilityConfig == null || !observabilityConfig.EnableTracing && !observabilityConfig.EnableMetrics)
        {
            return; // Skip if observability is disabled
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder
                    .AddService(
                        serviceName: DiagnosticConfig.ServiceName,
                        serviceVersion: DiagnosticConfig.ServiceVersion)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "development",
                        ["host.name"] = Environment.MachineName
                    });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                if (observabilityConfig.EnableTracing)
                {
                    tracerProviderBuilder
                        .AddSource(DiagnosticConfig.ServiceName)
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = observabilityConfig.EnableExceptionRecording;
                        })
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(options =>
                        {
                            // Export to Jaeger (OTLP endpoint)
                            options.Endpoint = new Uri(_configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317");
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        })
                        .SetSampler(new TraceIdRatioBasedSampler(observabilityConfig.TraceSamplingRate));
                }
            })
            .WithMetrics(meterProviderBuilder =>
            {
                if (observabilityConfig.EnableMetrics)
                {
                    meterProviderBuilder
                        .AddMeter(DiagnosticConfig.ServiceName)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                }
            });
    }

    /// <summary>
    /// Configure the HTTP request pipeline.
    /// This method is called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        logger.LogInformation("Configuring DeepResearch.Api");

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Add Error Handling Middleware
        app.UseMiddleware<ErrorHandlingMiddleware>();

        // Swagger - Always enabled for API documentation
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeepResearch API v1");
            c.RoutePrefix = string.Empty; // Set Swagger UI at root
            c.DisplayOperationId();
            c.DisplayRequestDuration();
            c.EnableTryItOutByDefault();
        });

        var httpsRedirectionEnabled = _configuration.GetValue("HttpsRedirection:Enabled", true);
        if (httpsRedirectionEnabled && !env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseCors("AllowAll");

        // Add authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Disable response compression for streaming endpoints to allow real-time delivery
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/chat/sessions") && context.Request.Path.Value?.Contains("/stream") == true ||
                context.Request.Path.StartsWithSegments("/api/workflows/master/stream"))
            {
                // Disable response compression for streaming
                context.Response.Headers["Content-Encoding"] = "";
            }
            await next();
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow })
                .WithName("Health")
                .WithOpenApi()
                .Produces<object>(StatusCodes.Status200OK)
                .AllowAnonymous();  // Health check should not require authentication
        });

        logger.LogInformation("DeepResearch.Api configured successfully");
    }
}
