using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Background service that continuously optimizes user schedules
/// </summary>
public class ScheduleOptimizationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleOptimizationBackgroundService> _logger;
    private readonly SmartSchedulingSettings _settings;

    public ScheduleOptimizationBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<SmartSchedulingSettings> settings,
        ILogger<ScheduleOptimizationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Schedule Optimization Background Service started");

        // Only run if optimization is enabled
        if (!_settings.EnableOptimization)
        {
            _logger.LogInformation("Schedule optimization is disabled, service will not run");
            return;
        }

        // Run optimization checks every hour
        var interval = TimeSpan.FromHours(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformOptimizationCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during optimization cycle");
            }

            // Wait for the next cycle
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("Schedule Optimization Background Service stopped");
    }

    private async Task PerformOptimizationCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting schedule optimization cycle");

        using var scope = _serviceProvider.CreateScope();
        var smartSchedulingService = scope.ServiceProvider.GetRequiredService<ISmartSchedulingService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        try
        {
            // Get users who might benefit from schedule optimization
            var usersForOptimization = await GetUsersForOptimizationAsync(userRepository, cancellationToken);

            _logger.LogInformation("Processing schedule optimization for {UserCount} users", usersForOptimization.Count);

            var processedUsers = 0;
            var optimizedSchedules = 0;

            foreach (var userId in usersForOptimization)
            {
                try
                {
                    // Check if user has an active schedule that could be optimized
                    var hasActiveSchedule = await HasActiveScheduleAsync(userId, cancellationToken);
                    
                    if (hasActiveSchedule)
                    {
                        // Perform optimization (this is a placeholder - real implementation would be more complex)
                        var optimizationResult = await PerformUserScheduleOptimizationAsync(userId, smartSchedulingService, cancellationToken);
                        
                        if (optimizationResult.WasOptimized)
                        {
                            optimizedSchedules++;
                            _logger.LogDebug("Optimized schedule for user {UserId}", userId);
                        }
                    }

                    processedUsers++;

                    // Add small delay to avoid overwhelming the system
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing schedule optimization for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Schedule optimization cycle completed. Processed: {ProcessedUsers}, Optimized: {OptimizedSchedules}",
                processedUsers, optimizedSchedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schedule optimization cycle");
        }
    }

    private async Task<List<Guid>> GetUsersForOptimizationAsync(IUserRepository userRepository, CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would get users who:
            // 1. Have active schedules
            // 2. Haven't been optimized recently
            // 3. Have enough activity data for meaningful optimization
            var activeUsers = await userRepository.GetAllActiveUsersAsync(cancellationToken);
            
            // Filter to users who might benefit from optimization
            var usersForOptimization = activeUsers
                .Where(u => ShouldOptimizeUserSchedule(u.Id))
                .Select(u => u.Id)
                .Take(50) // Limit to 50 users per cycle
                .ToList();

            return usersForOptimization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for optimization");
            return new List<Guid>();
        }
    }

    private bool ShouldOptimizeUserSchedule(Guid userId)
    {
        // In a real implementation, this would check various criteria:
        // - Last optimization time
        // - User activity level
        // - Schedule complexity
        // - User preferences for automatic optimization
        
        // For now, use simple time-based logic
        var random = new Random(userId.GetHashCode());
        return random.NextDouble() < 0.1; // 10% chance per cycle
    }

    private async Task<bool> HasActiveScheduleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // In a real implementation, this would check if the user has:
        // - Tasks scheduled for today or upcoming days
        // - Recent scheduling activity
        // - Pending tasks that could be rescheduled
        
        // For now, return true for demonstration
        return await Task.FromResult(true);
    }

    private async Task<OptimizationResult> PerformUserScheduleOptimizationAsync(
        Guid userId, 
        ISmartSchedulingService smartSchedulingService, 
        CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would:
            // 1. Get the user's current schedule
            // 2. Analyze optimization opportunities
            // 3. Apply optimizations if beneficial
            // 4. Notify user of improvements (if significant)

            _logger.LogDebug("Performing background optimization for user {UserId}", userId);

            // Placeholder logic - would contain actual optimization
            var wasOptimized = await Task.FromResult(false); // No actual optimization in this demo
            
            return new OptimizationResult(wasOptimized, "Background optimization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing optimization for user {UserId}", userId);
            return new OptimizationResult(false, $"Optimization failed: {ex.Message}");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Schedule Optimization Background Service is stopping");
        return base.StopAsync(cancellationToken);
    }

    private record OptimizationResult(bool WasOptimized, string Message);
}