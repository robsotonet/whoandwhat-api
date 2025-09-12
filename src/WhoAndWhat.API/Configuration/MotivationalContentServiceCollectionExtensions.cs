using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Services;

namespace WhoAndWhat.API.Configuration;

/// <summary>
/// Extensions for configuring motivational content services in the DI container
/// </summary>
public static class MotivationalContentServiceCollectionExtensions
{
    /// <summary>
    /// Configure motivational content management services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddMotivationalContentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register motivational content service
        services.AddScoped<IMotivationalContentService, MotivationalContentService>();

        // Register A/B testing service
        services.AddScoped<IContentABTestingService, ContentABTestingService>();

        // Register content scheduling as both hosted service and singleton for dependency injection
        services.AddSingleton<ContentSchedulingService>();
        services.AddHostedService(provider => provider.GetRequiredService<ContentSchedulingService>());

        return services;
    }
}