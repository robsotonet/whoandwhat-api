using Microsoft.Extensions.DependencyInjection;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Data.Seeding;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Extensions for configuring data seeding services in the DI container
/// </summary>
public static class SeedingServiceCollectionExtensions
{
    /// <summary>
    /// Configure data seeding services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddDataSeeding(this IServiceCollection services)
    {
        // Register individual seeders
        services.AddScoped<MotivationalContentSeeder>();

        // Register main seeder
        services.AddScoped<IDataSeeder, DatabaseSeeder>();

        return services;
    }
}
