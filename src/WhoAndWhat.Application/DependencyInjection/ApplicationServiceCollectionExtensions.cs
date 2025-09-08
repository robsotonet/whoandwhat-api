using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WhoAndWhat.Application.Behaviors;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.Services;

namespace WhoAndWhat.Application.DependencyInjection;

/// <summary>
/// Extension methods for configuring application services
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application layer services including MediatR, FluentValidation, and pipeline behaviors
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register pipeline behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Register application services
        services.AddScoped<ITaskApplicationService, TaskApplicationService>();

        // Register domain services
        services.AddScoped<CategoryBusinessRuleService>();
        services.AddScoped<CategoryWorkflowService>();

        return services;
    }

    /// <summary>
    /// Registers only FluentValidation services without MediatR
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationValidation(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}