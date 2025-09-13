using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Background;

/// <summary>
/// Background service that continuously learns from user scheduling patterns
/// </summary>
public class SchedulingPatternLearningService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchedulingPatternLearningService> _logger;
    private readonly SmartSchedulingSettings _settings;

    public SchedulingPatternLearningService(
        IServiceProvider serviceProvider,
        IOptions<SmartSchedulingSettings> settings,
        ILogger<SchedulingPatternLearningService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduling Pattern Learning Service started");

        // Only run if pattern learning is enabled
        if (!_settings.EnablePatternLearning)
        {
            _logger.LogInformation("Pattern learning is disabled, service will not run");
            return;
        }

        var interval = TimeSpan.FromDays(_settings.MachineLearning.PatternUpdateIntervalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformPatternLearningCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during pattern learning cycle");
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

        _logger.LogInformation("Scheduling Pattern Learning Service stopped");
    }

    private async Task PerformPatternLearningCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pattern learning cycle");

        using var scope = _serviceProvider.CreateScope();
        var userPreferenceService = scope.ServiceProvider.GetRequiredService<IUserSchedulingPreferenceService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        try
        {
            // Get all active users (this would be optimized in a real implementation)
            var activeUsers = await GetActiveUsersAsync(userRepository, cancellationToken);

            _logger.LogInformation("Processing pattern learning for {UserCount} active users", activeUsers.Count);

            var processedUsers = 0;
            var updatedUsers = 0;

            foreach (var userId in activeUsers)
            {
                try
                {
                    // Learn and update preferences for each user
                    var originalPreferences = await userPreferenceService.GetUserPreferencesAsync(userId, cancellationToken);
                    var updatedPreferences = await userPreferenceService.LearnAndUpdatePreferencesAsync(userId, cancellationToken);

                    if (HasSignificantChanges(originalPreferences, updatedPreferences))
                    {
                        updatedUsers++;
                        _logger.LogDebug("Updated preferences for user {UserId} based on learned patterns", userId);
                    }

                    processedUsers++;

                    // Add small delay to avoid overwhelming the system
                    await Task.Delay(TimeSpan.FromMilliseconds(_settings.BackgroundServices.PatternLearningBatchDelayMs), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing pattern learning for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Pattern learning cycle completed. Processed: {ProcessedUsers}, Updated: {UpdatedUsers}",
                processedUsers, updatedUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pattern learning cycle");
        }
    }

    private async Task<List<Guid>> GetActiveUsersAsync(IUserRepository userRepository, CancellationToken cancellationToken)
    {
        try
        {
            // Use pagination to efficiently get active users without loading all into memory
            var pageSize = _settings.BackgroundServices.PatternLearningMaxUsersPerCycle;
            const int pageNumber = 1; // Get first page of results

            var users = await userRepository.GetActiveUsersPagedAsync(pageSize, pageNumber, cancellationToken);
            return users.Select(u => u.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active users for pattern learning");
            return new List<Guid>();
        }
    }

    private bool HasSignificantChanges(object originalPreferences, object updatedPreferences)
    {
        // Simple comparison - in a real implementation this would be more sophisticated
        return !originalPreferences.Equals(updatedPreferences);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduling Pattern Learning Service is stopping");
        return base.StopAsync(cancellationToken);
    }
}
