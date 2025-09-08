using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using WhoAndWhat.API.Filters;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Services;
using Microsoft.Extensions.Options;
using WhoAndWhat.Infrastructure.Repositories;
using AspNetCoreRateLimit;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using WhoAndWhat.Infrastructure.Configuration;

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
                Description = @"A comprehensive bilingual task management platform with AI-powered features and social connectivity.

## Authentication
This API supports multiple authentication methods:
- **JWT Bearer Token**: For regular API access
- **OAuth 2.0**: Google, Facebook, and Apple Sign-In
- **Password Reset**: Email-based password recovery

## Features
- User registration and authentication
- Password management (change, reset, forgot)
- OAuth 2.0 integration (Google, Facebook, Apple)
- User account management (profile, deactivation, data export)
- Email verification system
- Rate limiting and DDoS protection
- Comprehensive security headers

## Rate Limiting
API requests are limited to:
- 100 requests per minute per IP
- 1000 requests per hour per IP
- Higher limits for authenticated users

## Data Export
Users can export their data in JSON or CSV format including:
- Profile information
- Tasks and projects
- Contacts
- OAuth account connections",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "WhoAndWhat Development Team",
                    Email = "dev@whoandwhat.com"
                },
                License = new Microsoft.OpenApi.Models.OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                },
                TermsOfService = new Uri("https://whoandwhat.com/terms"),
                Extensions = new Dictionary<string, Microsoft.OpenApi.Interfaces.IOpenApiExtension>
                {
                    ["x-logo"] = new Microsoft.OpenApi.Any.OpenApiObject
                    {
                        ["url"] = new Microsoft.OpenApi.Any.OpenApiString("https://whoandwhat.com/logo.png"),
                        ["altText"] = new Microsoft.OpenApi.Any.OpenApiString("WhoAndWhat Logo")
                    }
                }
            });

            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // JWT Authentication configuration
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            // OAuth 2.0 Google Authentication
            options.AddSecurityDefinition("Google", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
                Description = "Google OAuth 2.0 authentication",
                Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
                {
                    AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
                        TokenUrl = new Uri("https://oauth2.googleapis.com/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect",
                            ["profile"] = "Access to user profile information",
                            ["email"] = "Access to user email address"
                        }
                    }
                }
            });

            // OAuth 2.0 Facebook Authentication
            options.AddSecurityDefinition("Facebook", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
                Description = "Facebook OAuth 2.0 authentication",
                Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
                {
                    AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("https://www.facebook.com/v18.0/dialog/oauth"),
                        TokenUrl = new Uri("https://graph.facebook.com/v18.0/oauth/access_token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["email"] = "Access to user email address",
                            ["public_profile"] = "Access to user public profile information"
                        }
                    }
                }
            });

            // OAuth 2.0 Apple Authentication
            options.AddSecurityDefinition("Apple", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
                Description = "Apple Sign In OAuth 2.0 authentication",
                Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
                {
                    AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("https://appleid.apple.com/auth/authorize"),
                        TokenUrl = new Uri("https://appleid.apple.com/auth/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["name"] = "Access to user name",
                            ["email"] = "Access to user email address"
                        }
                    }
                }
            });

            // Default JWT security requirement
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

            // Configure response examples and schemas
            options.EnableAnnotations();
            
            // Add servers for different environments
            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
            {
                Url = "https://localhost:7071",
                Description = "Development Server"
            });
            
            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
            {
                Url = "https://api-dev.whoandwhat.com",
                Description = "Development Environment"
            });
            
            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
            {
                Url = "https://api-staging.whoandwhat.com", 
                Description = "Staging Environment"
            });
            
            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
            {
                Url = "https://api.whoandwhat.com",
                Description = "Production Environment"
            });

            // Configure operation sorting
            options.OrderActionsBy(apiDesc => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.HttpMethod}");

            // Custom schema filters for better documentation
            options.SchemaFilter<EnumSchemaFilter>();
            options.OperationFilter<AuthResponseOperationFilter>();
            options.OperationFilter<FileUploadOperationFilter>();
        });

        return services;
    }

    /// <summary>
    /// Configure Redis caching infrastructure with performance monitoring
    /// </summary>
    public static IServiceCollection AddRedisCachingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Redis caching with health checks and performance monitoring
        services.AddRedisCaching(configuration);
        
        // Optionally add cache warming (can be disabled via configuration)
        services.AddCacheWarming();
        
        return services;
    }

    /// <summary>
    /// Configure task search services with PostgreSQL full-text search and Redis caching
    /// </summary>
    public static IServiceCollection AddTaskSearchConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure cache settings from Redis configuration
        services.Configure<WhoAndWhat.Application.Configuration.CacheSettings>(options =>
        {
            var redisSettings = configuration.GetSection(RedisCacheSettings.SectionName).Get<RedisCacheSettings>() ?? new RedisCacheSettings();
            options.DefaultExpirationMinutes = redisSettings.DefaultExpirationMinutes;
            options.TaskListCacheExpirationMinutes = redisSettings.TaskListCacheExpirationMinutes;
            options.KeyPrefix = redisSettings.KeyPrefix;
        });
        
        // Register the interface with the concrete implementation
        services.AddSingleton<WhoAndWhat.Application.Configuration.ICacheSettings>(provider =>
            provider.GetRequiredService<IOptions<WhoAndWhat.Application.Configuration.CacheSettings>>().Value);
        
        // Register search repository and service
        services.AddScoped<ITaskSearchRepository, TaskSearchRepository>();
        services.AddScoped<ITaskSearchService, TaskSearchService>();
        
        return services;
    }

    /// <summary>
    /// Configure task archiving services with Hangfire background job processing
    /// </summary>
    public static IServiceCollection AddTaskArchiveConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure archive settings
        services.Configure<ArchiveSettings>(configuration.GetSection(ArchiveSettings.SectionName));
        
        // Register archive service
        services.AddScoped<ITaskArchiveService, TaskArchiveService>();
        
        // Add Hangfire background job services
        services.AddHangfireServices(configuration);
        
        return services;
    }

    /// <summary>
    /// Configure health checks for monitoring
    /// </summary>
    public static IServiceCollection AddHealthCheckConfiguration(this IServiceCollection services, IWebHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            // For testing environment, use simple health checks without database dependency
            services.AddHealthChecks()
                .AddCheck("database", () => HealthCheckResult.Healthy("InMemory database is always healthy"), 
                    tags: new[] { "database", "inmemory" })
                .AddCheck("api", () => HealthCheckResult.Healthy("API is running"), 
                    tags: new[] { "api", "self" })
                .AddCheck("cache", () => HealthCheckResult.Healthy("In-memory cache is always healthy"), 
                    tags: new[] { "cache", "inmemory" });
        }
        else
        {
            // For development and production, use PostgreSQL database checks
            // Redis health checks are automatically added via AddRedisCaching() extension method
            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>(
                    name: "database",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "database", "postgresql" })
                .AddCheck("api", () => HealthCheckResult.Healthy("API is running"), 
                    tags: new[] { "api", "self" });
        }

        return services;
    }

    /// <summary>
    /// Configure CORS policies with comprehensive support for web and mobile clients
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            // Default policy for production web clients
            options.AddPolicy("DefaultPolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("X-Pagination", "X-API-Version", "X-Rate-Limit-Remaining", "X-Rate-Limit-Reset");
            });

            // Development policy for local web development
            options.AddPolicy("DevelopmentPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:8080", "https://localhost:3000")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("X-Pagination", "X-API-Version", "X-Rate-Limit-Remaining", "X-Rate-Limit-Reset");
            });

            // Mobile client policy with support for custom URI schemes and native app domains
            options.AddPolicy("MobilePolicy", policy =>
            {
                policy.WithOrigins(
                          // iOS app custom schemes
                          "whoandwhat://oauth/callback",
                          "whoandwhat-dev://oauth/callback",
                          
                          // Android app custom schemes
                          "com.whoandwhat.app://oauth/callback",
                          "com.whoandwhat.app.dev://oauth/callback",
                          
                          // Capacitor/Ionic local development
                          "http://localhost",
                          "http://localhost:3000",
                          "http://localhost:8080",
                          "http://localhost:8100",
                          "http://localhost:4200",
                          
                          // Mobile app domains (production)
                          "https://app.whoandwhat.com",
                          "https://mobile.whoandwhat.com",
                          
                          // Development app domains
                          "https://app-dev.whoandwhat.com",
                          "https://app-staging.whoandwhat.com")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders(
                          "X-Pagination", 
                          "X-API-Version", 
                          "X-Rate-Limit-Remaining", 
                          "X-Rate-Limit-Reset",
                          "X-Device-Type",
                          "X-App-Version")
                      .SetPreflightMaxAge(TimeSpan.FromHours(24)); // Cache preflight requests for 24 hours
            });

            // Strict policy for production with known origins only
            options.AddPolicy("ProductionPolicy", policy =>
            {
                policy.WithOrigins(
                          "https://whoandwhat.com",
                          "https://www.whoandwhat.com",
                          "https://app.whoandwhat.com",
                          "https://mobile.whoandwhat.com")
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
                      .WithHeaders(
                          "Authorization",
                          "Content-Type",
                          "X-Requested-With",
                          "X-API-Version",
                          "X-Device-Type",
                          "X-App-Version",
                          "X-Client-Id")
                      .AllowCredentials()
                      .WithExposedHeaders(
                          "X-Pagination", 
                          "X-API-Version", 
                          "X-Rate-Limit-Remaining", 
                          "X-Rate-Limit-Reset")
                      .SetPreflightMaxAge(TimeSpan.FromHours(1));
            });

            // OAuth callback policy specifically for OAuth providers
            options.AddPolicy("OAuthPolicy", policy =>
            {
                policy.WithOrigins(
                          // OAuth provider callbacks
                          "https://accounts.google.com",
                          "https://www.facebook.com",
                          "https://login.microsoftonline.com",
                          "https://appleid.apple.com",
                          
                          // Local development
                          "http://localhost:5000",
                          "https://localhost:7071",
                          
                          // Production domains
                          "https://api.whoandwhat.com",
                          "https://api-dev.whoandwhat.com")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("X-API-Version")
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
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

        // Add HttpContextAccessor for IP address tracking
        services.AddHttpContextAccessor();

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

    /// <summary>
    /// Configure OAuth authentication providers (Google, Facebook, Apple, Microsoft)
    /// </summary>
    public static IServiceCollection AddOAuthConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure OAuth settings
        var oauthSettings = new OAuthSettings();
        configuration.GetSection(OAuthSettings.SectionName).Bind(oauthSettings);
        
        services.Configure<OAuthSettings>(configuration.GetSection(OAuthSettings.SectionName));

        // Register OAuth services and repositories
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();

        var authenticationBuilder = services.AddAuthentication();

        // Configure Google OAuth
        if (!string.IsNullOrEmpty(oauthSettings.Google.ClientId) && !string.IsNullOrEmpty(oauthSettings.Google.ClientSecret))
        {
            authenticationBuilder.AddGoogle(options =>
            {
                options.ClientId = oauthSettings.Google.ClientId;
                options.ClientSecret = oauthSettings.Google.ClientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.CallbackPath = oauthSettings.CallbackUrl.Google;
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<string>>();
                    logger.LogDebug("Google OAuth ticket created for user: {Email}", 
                        context.Principal?.FindFirst(ClaimTypes.Email)?.Value);
                    return System.Threading.Tasks.Task.CompletedTask;
                };
            });
        }

        // Configure Facebook OAuth
        if (!string.IsNullOrEmpty(oauthSettings.Facebook.AppId) && !string.IsNullOrEmpty(oauthSettings.Facebook.AppSecret))
        {
            authenticationBuilder.AddFacebook(options =>
            {
                options.AppId = oauthSettings.Facebook.AppId;
                options.AppSecret = oauthSettings.Facebook.AppSecret;
                options.Scope.Add("email");
                options.Scope.Add("public_profile");
                options.CallbackPath = oauthSettings.CallbackUrl.Facebook;
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<string>>();
                    logger.LogDebug("Facebook OAuth ticket created for user: {Email}", 
                        context.Principal?.FindFirst(ClaimTypes.Email)?.Value);
                    return System.Threading.Tasks.Task.CompletedTask;
                };
            });
        }

        // Configure Apple OAuth
        if (!string.IsNullOrEmpty(oauthSettings.Apple.ClientId) && 
            !string.IsNullOrEmpty(oauthSettings.Apple.TeamId) && 
            !string.IsNullOrEmpty(oauthSettings.Apple.KeyId) && 
            !string.IsNullOrEmpty(oauthSettings.Apple.PrivateKey))
        {
            authenticationBuilder.AddApple(options =>
            {
                options.ClientId = oauthSettings.Apple.ClientId;
                options.TeamId = oauthSettings.Apple.TeamId;
                options.KeyId = oauthSettings.Apple.KeyId;
                options.PrivateKey = oauthSettings.Apple.PrivateKey;
                options.CallbackPath = oauthSettings.CallbackUrl.Apple;
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<string>>();
                    logger.LogDebug("Apple OAuth ticket created for user: {Email}", 
                        context.Principal?.FindFirst(ClaimTypes.Email)?.Value);
                    return System.Threading.Tasks.Task.CompletedTask;
                };
            });
        }

        // Configure Microsoft OAuth
        authenticationBuilder.AddMicrosoftAccount(options =>
        {
            options.ClientId = "microsoft-client-id-placeholder";
            options.ClientSecret = "microsoft-client-secret-placeholder";
            options.CallbackPath = "/api/v1/oauth/microsoft/callback";
            options.SaveTokens = true;
        });

        return services;
    }

    /// <summary>
    /// Configure rate limiting and DDoS protection
    /// </summary>
    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Add memory cache for rate limiting
        services.AddMemoryCache();

        // Configure IP rate limiting
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        
        // Configure client rate limiting
        services.Configure<ClientRateLimitOptions>(configuration.GetSection("ClientRateLimiting"));

        // Add rate limiting stores
        services.AddInMemoryRateLimiting();

        // Register rate limiting services
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IClientPolicyStore, MemoryCacheClientPolicyStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

        return services;
    }

    /// <summary>
    /// Configure Azure Key Vault integration for secrets management
    /// </summary>
    public static IServiceCollection AddAzureKeyVaultConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var keyVaultEndpoint = configuration["KeyVault:Endpoint"];
        
        if (!string.IsNullOrEmpty(keyVaultEndpoint))
        {
            // Register Azure Key Vault services for runtime access
            services.AddScoped<SecretClient>(provider =>
            {
                var credential = new DefaultAzureCredential();
                return new SecretClient(new Uri(keyVaultEndpoint), credential);
            });
        }
        
        return services;
    }

    /// <summary>
    /// Configure DDoS protection middleware with advanced threat detection
    /// </summary>
    public static IServiceCollection AddDDoSProtectionConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DDoS protection settings
        services.Configure<DDoSProtectionSettings>(configuration.GetSection(DDoSProtectionSettings.SectionName));
        
        return services;
    }

    /// <summary>
    /// Configure enhanced security headers with comprehensive modern web security policies
    /// </summary>
    public static IServiceCollection AddSecurityHeadersConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure security headers settings
        services.Configure<SecurityHeadersSettings>(configuration.GetSection(SecurityHeadersSettings.SectionName));
        
        return services;
    }
}