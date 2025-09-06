using Microsoft.EntityFrameworkCore;
using Serilog;
using WhoAndWhat.API.Configuration;
using WhoAndWhat.Application;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.DependencyInjection;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Infrastructure;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using WhoAndWhat.Application.Features.Auth;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

// Configure Serilog early for logging during bootstrap
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bootstrap-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WhoAndWhat API application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Azure Key Vault (only in production or if endpoint is configured)
    var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential(),
            new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = TimeSpan.FromMinutes(30) // Reload secrets every 30 minutes
            });
            
        Log.Information("Azure Key Vault configuration enabled for endpoint: {Endpoint}", keyVaultEndpoint);
    }
    else
    {
        Log.Information("Azure Key Vault not configured - using local configuration");
    }

    // Configure Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Core services - Environment-conditional database configuration
    if (builder.Environment.IsEnvironment("Testing"))
    {
        // Use InMemory database for integration tests
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("IntegrationTestDb"));
    }
    else
    {
        // Use PostgreSQL for Development and Production
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    }

    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IUserDomainService, UserDomainService>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAccountVerificationService, AccountVerificationService>();
    builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

    // Application services (MediatR, FluentValidation, Pipeline Behaviors)
    builder.Services.AddApplicationServices();

    // API Foundation Services
    builder.Services.AddApiVersioningConfiguration();
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddHealthCheckConfiguration(builder.Environment);
    builder.Services.AddCorsConfiguration();
    builder.Services.AddResponseCompressionConfiguration();
    builder.Services.AddApplicationInsightsConfiguration(builder.Configuration);
    
    // JWT Authentication
    builder.Services.AddJwtAuthenticationConfiguration(builder.Configuration);
    
    // OAuth Authentication
    builder.Services.AddOAuthConfiguration(builder.Configuration);
    
    // Rate Limiting
    builder.Services.AddRateLimitingConfiguration(builder.Configuration);
    
    // DDoS Protection
    builder.Services.AddDDoSProtectionConfiguration(builder.Configuration);
    
    // Security Headers
    builder.Services.AddSecurityHeadersConfiguration(builder.Configuration);
    
    // Azure Key Vault
    builder.Services.AddAzureKeyVaultConfiguration(builder.Configuration);

    builder.Services.AddControllers(options =>
    {
        options.SuppressAsyncSuffixInActionNames = false;
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    Log.Information("Configuring HTTP request pipeline");

    // Use comprehensive middleware pipeline
    app.UseComprehensiveMiddleware(app.Environment);

    // Configure Swagger documentation
    app.UseSwaggerConfiguration(app.Environment);

    // Configure static files for Swagger custom styling
    app.UseStaticFilesConfiguration(app.Environment);

    // Configure health checks
    app.UseHealthCheckConfiguration();

    // Map controllers with versioning
    app.MapControllers();

    // Database seeding (development only, skip if DB unavailable)
    if (app.Environment.IsDevelopment())
    {
        try
        {
            Log.Information("Attempting to seed development database");
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DataSeeder.SeedDatabaseAsync(context);
            Log.Information("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database seeding failed - continuing without seeding. This is expected if PostgreSQL is not available.");
        }
    }

    Log.Information("WhoAndWhat API application configured successfully");
    Log.Information("Available endpoints: /health, /health/live, /health/ready, /swagger");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Program entry point for WhoAndWhat API
/// Configured with comprehensive middleware pipeline, API versioning, structured logging,
/// health checks, Swagger documentation, and monitoring capabilities.
/// </summary>
public partial class Program { }