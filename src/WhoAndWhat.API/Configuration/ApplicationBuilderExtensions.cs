using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using WhoAndWhat.API.Middleware;

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
        // Security headers (should be early in pipeline)
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Response compression
        app.UseResponseCompression();

        // Development-specific middleware
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Global exception handling (should be early but after dev exception page)
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Serilog request logging
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

        // HTTPS redirection (can be disabled for Docker development)
        if (!env.IsDevelopment() || Environment.GetEnvironmentVariable("ASPNETCORE_FORCE_HTTPS") == "true")
        {
            app.UseHttpsRedirection();
        }

        // CORS
        app.UseCors(env.IsDevelopment() ? "DevelopmentPolicy" : "DefaultPolicy");

        // Authentication & Authorization (prepared for future implementation)
        // app.UseAuthentication();
        // app.UseAuthorization();

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
    /// </summary>
    public static IApplicationBuilder UseStaticFilesConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment() || env.IsStaging())
        {
            // Create custom CSS content for Swagger UI
            var customCssPath = Path.Combine(env.WebRootPath ?? "wwwroot", "swagger-ui");
            Directory.CreateDirectory(customCssPath);

            var cssFilePath = Path.Combine(customCssPath, "custom.css");
            if (!File.Exists(cssFilePath))
            {
                var customCss = @"
.swagger-ui .topbar {
    background-color: #2c5282;
}
.swagger-ui .topbar .download-url-wrapper {
    display: none;
}
.swagger-ui .info .title {
    color: #2d3748;
}
.swagger-ui .scheme-container {
    background: #f7fafc;
    border-radius: 4px;
    padding: 10px;
}";
                File.WriteAllText(cssFilePath, customCss);
            }

            app.UseStaticFiles();
        }

        return app;
    }
}