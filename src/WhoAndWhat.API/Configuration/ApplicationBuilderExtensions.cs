using AspNetCoreRateLimit;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using WhoAndWhat.API.Middleware;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.API.Configuration;

/// <summary>
/// Extensions for configuring the application middleware pipeline
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configure the complete middleware pipeline
    /// </summary>
    public static IApplicationBuilder UseComprehensiveMiddleware(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Enhanced security headers (should be early in pipeline)
        app.UseMiddleware<EnhancedSecurityHeadersMiddleware>();

        // DDoS protection (before rate limiting for advanced threat detection)
        var ddosSettings = app.ApplicationServices.GetService<IOptions<DDoSProtectionSettings>>();
        if (ddosSettings?.Value?.Enabled == true)
        {
            app.UseMiddleware<DDoSProtectionMiddleware>();
        }

        // Response compression
        app.UseResponseCompression();

        // Development-specific middleware
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Global exception handling (should be early but after dev exception page)
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Serilog request logging (skip in testing environment)
        if (!env.IsEnvironment("Testing"))
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
                    diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
                };
            });
        }

        // HTTPS redirection (can be disabled for Docker development)
        if (!env.IsDevelopment() || Environment.GetEnvironmentVariable("ASPNETCORE_FORCE_HTTPS") == "true")
        {
            app.UseHttpsRedirection();
        }

        // CORS
        app.UseCors(env.IsDevelopment() ? "DevelopmentPolicy" : "DefaultPolicy");

        // Rate Limiting (before authentication)
        app.UseIpRateLimiting();
        app.UseClientRateLimiting();

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Configure Swagger UI with comprehensive documentation
    /// </summary>
    public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment() || env.IsStaging())
        {
            app.UseSwagger(options =>
            {
                options.RouteTemplate = "swagger/{documentName}/swagger.json";
            });

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WhoAndWhat API v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "WhoAndWhat API Documentation";
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
                options.DefaultModelsExpandDepth(-1);
                options.EnableDeepLinking();
                options.DisplayRequestDuration();
                options.EnableFilter();
                options.EnableValidator();

                // Custom CSS for better appearance
                options.InjectStylesheet("/swagger-ui/custom.css");
            });
        }

        return app;
    }

    /// <summary>
    /// Configure health check endpoints
    /// </summary>
    public static IApplicationBuilder UseHealthCheckConfiguration(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = report.TotalDuration,
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        duration = entry.Value.Duration,
                        description = entry.Value.Description,
                        tags = entry.Value.Tags
                    })
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            }
        });

        // Simple liveness probe
        app.UseHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("self")
        });

        // Readiness probe (includes database)
        app.UseHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("self")
        });

        return app;
    }

    /// <summary>
    /// Configure static files for Swagger custom CSS
    /// Uses pre-existing static CSS files from wwwroot directory
    /// </summary>
    public static IApplicationBuilder UseStaticFilesConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment() || env.IsStaging())
        {
            // Serve static files from wwwroot directory
            // Custom CSS for Swagger UI should be placed in wwwroot/swagger-ui/custom.css
            app.UseStaticFiles();
        }

        return app;
    }

    /// <summary>
    /// Configure Hangfire dashboard and background job services
    /// </summary>
    public static IApplicationBuilder UseHangfireConfiguration(this IApplicationBuilder app, IConfiguration configuration, IWebHostEnvironment env)
    {
        // Configure Hangfire Dashboard
        var dashboardOptions = HangfireConfiguration.GetDashboardOptions(configuration);
        app.UseHangfireDashboard("/hangfire", dashboardOptions);

        // Initialize job scheduler
        app.ApplicationServices.UseHangfireServices();

        return app;
    }
}
