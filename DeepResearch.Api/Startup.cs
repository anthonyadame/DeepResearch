using DeepResearchAgent.Configuration;
using DeepResearchAgent.Services;
using DeepResearchAgent.Services.Checkpointing;
using DeepResearch.Api.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
        RegisterLightningStore(services);

        // Add Authentication Services
        ConfigureAuthentication(services);

        // Add API Services
        services.AddControllers();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "DeepResearch API",
                Version = "v1",
                Description = "Long-running workflow management API with authentication",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "DeepResearch Team",
                    Url = new Uri("https://github.com/anthonyadame/DeepResearch")
                }
            });

            // Add JWT authentication to Swagger
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme"
            });

            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

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

        services.AddSingleton<ILightningStore>(sp => new LightningStore(
            sp.GetRequiredService<LightningStoreOptions>(),
            sp.GetRequiredService<HttpClient>()));
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

        // Validate secret key
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured. Set 'Jwt:SecretKey' in appsettings.json (minimum 32 characters)");
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
                        context.Response.Headers.Add("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });
    }

    /// <summary>
    /// Configure the HTTP request pipeline.
    /// This method is called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        logger.LogInformation("Configuring DeepResearch.Api");

        var swaggerEnabled = env.IsDevelopment() || _configuration.GetValue("Swagger:Enabled", false);
        if (swaggerEnabled)
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeepResearch API v1");
                c.RoutePrefix = "swagger";
            });
        }

        var httpsRedirectionEnabled = _configuration.GetValue("HttpsRedirection:Enabled", true);
        if (httpsRedirectionEnabled)
        {
            app.UseHttpsRedirection();
        }
        app.UseRouting();
        app.UseCors("AllowAll");

        // Add authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

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
