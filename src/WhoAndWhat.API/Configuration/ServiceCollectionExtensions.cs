using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Services;

namespace WhoAndWhat.API.Configuration;

/// <summary>
/// Extensions for configuring services in the DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure API versioning with URL path strategy (v1, v2, etc.)
    /// </summary>
    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
            options.ReportApiVersions = true;
        })
        .AddMvc();

        return services;
    }

    /// <summary>
    /// Configure comprehensive Swagger/OpenAPI documentation
    /// </summary>
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // API Information
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Version = "v1",
                Title = "WhoAndWhat Task Management API",
                Description = "A comprehensive bilingual task management platform with AI-powered features and social connectivity.",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "WhoAndWhat Development Team",
                    Email = "dev@whoandwhat.com"
                }
            });

            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // JWT Authentication configuration (prepared for future)
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

            // Custom operation filters can be added here for future enhancements
        });

        return services;
    }

    /// <summary>
    /// Configure health checks for monitoring
    /// </summary>
    public static IServiceCollection AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "database", "postgresql" })
            .AddCheck("api", () => HealthCheckResult.Healthy("API is running"), 
                tags: new[] { "api", "self" });

        return services;
    }

    /// <summary>
    /// Configure CORS policies
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("DefaultPolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("X-Pagination", "X-API-Version");
            });

            options.AddPolicy("DevelopmentPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:8080", "https://localhost:3000")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("X-Pagination", "X-API-Version");
            });
        });

        return services;
    }

    /// <summary>
    /// Configure response compression
    /// </summary>
    public static IServiceCollection AddResponseCompressionConfiguration(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        return services;
    }

    /// <summary>
    /// Configure Application Insights telemetry
    /// </summary>
    public static IServiceCollection AddApplicationInsightsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationInsights");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = connectionString;
            });
        }

        return services;
    }

    /// <summary>
    /// Configure JWT authentication and authorization
    /// </summary>
    public static IServiceCollection AddJwtAuthenticationConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure JWT settings
        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);
        
        // Validate JWT settings
        if (string.IsNullOrEmpty(jwtSettings.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }
        if (string.IsNullOrEmpty(jwtSettings.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is not configured");
        }
        if (string.IsNullOrEmpty(jwtSettings.Audience))
        {
            throw new InvalidOperationException("JWT Audience is not configured");
        }

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Register JWT service
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Configure JWT authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false; // Set to true in production
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtSettings.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateIssuer = jwtSettings.ValidateIssuer,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = jwtSettings.ValidateAudience,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = jwtSettings.ValidateLifetime,
                RequireExpirationTime = jwtSettings.RequireExpirationTime,
                ClockSkew = TimeSpan.FromMinutes(jwtSettings.ClockSkewMinutes)
            };

            // Handle JWT events
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogError(context.Exception, "JWT authentication failed");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogDebug("JWT token validated for user: {UserId}", 
                        context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("JWT authentication challenge: {Error} - {Description}", 
                        context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });

        // Add authorization
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireVerifiedEmail", policy =>
                policy.RequireClaim("email_verified", "true"));
            
            options.AddPolicy("RequireAuthentication", policy =>
                policy.RequireAuthenticatedUser());
        });

        return services;
    }
}