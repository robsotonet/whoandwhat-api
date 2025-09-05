using Microsoft.EntityFrameworkCore;
using Serilog;
using WhoAndWhat.API.Configuration;
using WhoAndWhat.Application;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Infrastructure;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using WhoAndWhat.Application.Features.Auth;

// Configure Serilog early for logging during bootstrap
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bootstrap-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WhoAndWhat API application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Core services
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IUserDomainService, UserDomainService>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAccountVerificationService, AccountVerificationService>();
    builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

    // MediatR for CQRS
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(WhoAndWhat.Application.AssemblyReference).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(WhoAndWhat.Infrastructure.AssemblyReference).Assembly);
    });

    // API Foundation Services
    builder.Services.AddApiVersioningConfiguration();
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddHealthCheckConfiguration();
    builder.Services.AddCorsConfiguration();
    builder.Services.AddResponseCompressionConfiguration();
    builder.Services.AddApplicationInsightsConfiguration(builder.Configuration);
    
    // JWT Authentication
    builder.Services.AddJwtAuthenticationConfiguration(builder.Configuration);

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