using WhoAndWhat.API.Hubs;

namespace WhoAndWhat.API.Configuration;

/// <summary>
/// Extensions for configuring SignalR services in the DI container
/// </summary>
public static class SignalRServiceCollectionExtensions
{
    /// <summary>
    /// Configure SignalR services with proper authentication and scaling options
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddSignalRConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var signalRBuilder = services.AddSignalR(options =>
        {
            // Configure SignalR options
            options.EnableDetailedErrors = configuration.GetValue<bool>("SignalR:EnableDetailedErrors", false);
            options.KeepAliveInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:KeepAliveIntervalSeconds", 15));
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:ClientTimeoutSeconds", 30));
            options.HandshakeTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:HandshakeTimeoutSeconds", 15));
            options.MaximumReceiveMessageSize = configuration.GetValue<long>("SignalR:MaxMessageSize", 1024 * 1024); // 1MB default
        });

        // Configure Redis backplane for production scaling (optional)
        var redisConnectionString = configuration.GetConnectionString("RedisSignalR");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("whoandwhat:signalr:");
            });
        }

        // Configure Azure SignalR Service for cloud scaling (optional)
        var azureSignalRConnectionString = configuration.GetConnectionString("AzureSignalR");
        if (!string.IsNullOrEmpty(azureSignalRConnectionString))
        {
            signalRBuilder.AddAzureSignalR(azureSignalRConnectionString);
        }

        return services;
    }

    /// <summary>
    /// Configure SignalR hub endpoints in the application pipeline
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application for method chaining</returns>
    public static WebApplication UseSignalRConfiguration(this WebApplication app)
    {
        // Map SignalR hubs
        app.MapHub<DashboardHub>("/hubs/dashboard");

        return app;
    }
}