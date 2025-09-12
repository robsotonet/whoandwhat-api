using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IApplicationBuilder to configure infrastructure services
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Seeds the database with initial data
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="seedInDevelopment">Whether to seed in development environment</param>
    /// <param name="seedInProduction">Whether to seed in production environment</param>
    /// <returns>Application builder for method chaining</returns>
    public static async Task<IApplicationBuilder> SeedDatabaseAsync(
        this IApplicationBuilder app,
        bool seedInDevelopment = true,
        bool seedInProduction = false)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();

        try
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
            var isProduction = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

            var shouldSeed = (isDevelopment && seedInDevelopment) || (isProduction && seedInProduction) || (!isDevelopment && !isProduction);

            if (!shouldSeed)
            {
                logger.LogInformation("Skipping database seeding for {Environment} environment", environment);
                return app;
            }

            logger.LogInformation("Starting database seeding for {Environment} environment", environment);

            var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
            await seeder.SeedAsync();

            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed");
            throw;
        }

        return app;
    }
}
