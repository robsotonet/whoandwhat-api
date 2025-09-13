using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Services;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Extension methods for registering smart scheduling services
/// </summary>
public static class SmartSchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Add smart scheduling services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSmartScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.Configure<SmartSchedulingSettings>(
            configuration.GetSection(SmartSchedulingSettings.SectionName));

        // Register core smart scheduling services
        services.AddScoped<ISmartSchedulingService, SmartSchedulingService>();
        services.AddScoped<IScheduleOptimizationEngine, ScheduleOptimizationEngine>();
        services.AddScoped<ITimeBlockManager, TimeBlockManager>();
        services.AddScoped<IUserSchedulingPreferenceService, UserSchedulingPreferenceService>();

        // Register background services for smart scheduling
        services.AddHostedService<SchedulingPatternLearningService>();
        services.AddHostedService<ScheduleOptimizationBackgroundService>();

        return services;
    }

    /// <summary>
    /// Add smart scheduling with custom settings
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureSettings">Action to configure settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSmartScheduling(this IServiceCollection services, Action<SmartSchedulingSettings> configureSettings)
    {
        // Register configuration with custom settings
        services.Configure(configureSettings);

        // Register core smart scheduling services
        services.AddScoped<ISmartSchedulingService, SmartSchedulingService>();
        services.AddScoped<IScheduleOptimizationEngine, ScheduleOptimizationEngine>();
        services.AddScoped<ITimeBlockManager, TimeBlockManager>();
        services.AddScoped<IUserSchedulingPreferenceService, UserSchedulingPreferenceService>();

        // Register background services for smart scheduling
        services.AddHostedService<SchedulingPatternLearningService>();
        services.AddHostedService<ScheduleOptimizationBackgroundService>();

        return services;
    }

    /// <summary>
    /// Add only basic smart scheduling services (without background services)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSmartSchedulingCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.Configure<SmartSchedulingSettings>(
            configuration.GetSection(SmartSchedulingSettings.SectionName));

        // Register core smart scheduling services only
        services.AddScoped<ISmartSchedulingService, SmartSchedulingService>();
        services.AddScoped<IScheduleOptimizationEngine, ScheduleOptimizationEngine>();
        services.AddScoped<ITimeBlockManager, TimeBlockManager>();
        services.AddScoped<IUserSchedulingPreferenceService, UserSchedulingPreferenceService>();

        return services;
    }
}