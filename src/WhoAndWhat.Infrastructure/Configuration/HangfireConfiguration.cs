using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WhoAndWhat.Infrastructure.BackgroundJobs;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration for Hangfire background job processing
/// </summary>
public static class HangfireConfiguration
{
    /// <summary>
    /// Adds Hangfire services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is required for Hangfire");

        // Add Hangfire services
        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    JobExpirationCheckInterval = TimeSpan.FromHours(1),
                    CountersAggregateInterval = TimeSpan.FromMinutes(5),
                    PrepareSchemaIfNecessary = true,
                    TransactionSynchronisationTimeout = TimeSpan.FromMinutes(5),
                    SchemaName = "hangfire"
                })
                .UseSerilogLogProvider();
        });

        // Add the processing server
        services.AddHangfireServer(options =>
        {
            options.ServerName = Environment.MachineName;
            options.Queues = new[] { "default", "archive", "cleanup", "maintenance" };
            options.WorkerCount = Math.Max(Environment.ProcessorCount, 2);
            options.ServerTimeout = TimeSpan.FromMinutes(30);
            options.SchedulePollingInterval = TimeSpan.FromMinutes(1);
        });

        // Register background jobs
        services.AddTransient<ArchiveTasksJob>();
        services.AddSingleton<JobSchedulerService>();
        services.AddHostedService<JobSchedulerService>();

        return services;
    }

    /// <summary>
    /// Configures Hangfire middleware and dashboard
    /// </summary>
    public static void UseHangfireServices(this IServiceProvider serviceProvider)
    {
        // Initialize the job scheduler
        var jobScheduler = serviceProvider.GetRequiredService<JobSchedulerService>();
    }

    /// <summary>
    /// Gets Hangfire dashboard options based on environment
    /// </summary>
    public static DashboardOptions GetDashboardOptions(IConfiguration configuration)
    {
        var isDevelopment = configuration.GetValue<bool>("IsDevelopment");

        return new DashboardOptions
        {
            DashboardTitle = "WhoAndWhat - Background Jobs",
            Authorization = isDevelopment
                ? new[] { new AllowAllDashboardAuthorizationFilter() }
                : new[] { new RestrictedDashboardAuthorizationFilter() },
            IsReadOnlyFunc = _ => false, // Allow job management in dashboard
            IgnoreAntiforgeryToken = isDevelopment,
            AppPath = "/",
            StatsPollingInterval = 2000 // 2 seconds
        };
    }
}

/// <summary>
/// Authorization filter that allows all users (for development only)
/// </summary>
public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

/// <summary>
/// Authorization filter that restricts access to authenticated admin users
/// </summary>
public class RestrictedDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Check if user is authenticated
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return false;
        }

        // Check if user has admin role or specific claim
        return httpContext.User.IsInRole("Admin") ||
               httpContext.User.HasClaim("permission", "hangfire-dashboard");
    }
}
