using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Background service for scheduling and delivering motivational content
/// </summary>
public class ContentSchedulingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDashboardHub _dashboardHub;
    private readonly ILogger<ContentSchedulingService> _logger;

    private Timer? _schedulingTimer;
    private Timer? _deliveryTimer;
    private readonly SemaphoreSlim _schedulingSemaphore = new(1, 1);

    // Scheduling configuration
    private readonly TimeSpan _schedulingInterval = TimeSpan.FromMinutes(5);  // Check for scheduled content every 5 minutes
    private readonly TimeSpan _deliveryInterval = TimeSpan.FromMinutes(1);    // Process delivery queue every minute

    public ContentSchedulingService(
        IServiceProvider serviceProvider,
        IDashboardHub dashboardHub,
        ILogger<ContentSchedulingService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dashboardHub = dashboardHub ?? throw new ArgumentNullException(nameof(dashboardHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application to fully start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        _logger.LogInformation("Starting content scheduling service");

        try
        {
            // Start scheduling timer (check for content to schedule)
            _schedulingTimer = new Timer(async _ => await ProcessContentScheduling(stoppingToken), 
                null, TimeSpan.Zero, _schedulingInterval);

            // Start delivery timer (deliver scheduled content)
            _deliveryTimer = new Timer(async _ => await ProcessContentDelivery(stoppingToken), 
                null, TimeSpan.FromMinutes(1), _deliveryInterval);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Content scheduling service cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in content scheduling service");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping content scheduling service");

        _schedulingTimer?.Change(Timeout.Infinite, 0);
        _deliveryTimer?.Change(Timeout.Infinite, 0);

        await _schedulingSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Allow any ongoing operations to complete
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            _schedulingSemaphore.Release();
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Processes content scheduling for all eligible users
    /// </summary>
    private async Task ProcessContentScheduling(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (!await _schedulingSemaphore.WaitAsync(100, cancellationToken))
        {
            _logger.LogDebug("Skipping content scheduling - previous operation still running");
            return;
        }

        try
        {
            _logger.LogDebug("Starting content scheduling process");

            using var scope = _serviceProvider.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IMotivationalContentService>();
            var preferencesRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserContentPreferences>>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            // Get all users with content enabled
            var activePreferences = await preferencesRepository.GetAllByConditionAsync(
                p => p.IsContentEnabled && 
                     (p.ContentPausedUntil == null || p.ContentPausedUntil <= DateTime.UtcNow),
                cancellationToken);

            var processedUsers = 0;
            var scheduledContent = 0;

            foreach (var preferences in activePreferences)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Check if user needs content scheduling
                    if (await ShouldScheduleContentForUser(preferences, contentService, cancellationToken))
                    {
                        var scheduled = await ScheduleContentForUser(preferences.UserId, contentService, cancellationToken);
                        scheduledContent += scheduled;
                    }

                    processedUsers++;

                    // Process users in batches to avoid overwhelming the system
                    if (processedUsers % 100 == 0)
                    {
                        _logger.LogDebug("Processed content scheduling for {ProcessedUsers} users", processedUsers);
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken); // Brief pause
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scheduling content for user {UserId}", preferences.UserId);
                }
            }

            _logger.LogDebug("Content scheduling completed: {ProcessedUsers} users processed, {ScheduledContent} content items scheduled",
                processedUsers, scheduledContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during content scheduling process");
        }
        finally
        {
            _schedulingSemaphore.Release();
        }
    }

    /// <summary>
    /// Processes delivery of scheduled content
    /// </summary>
    private async Task ProcessContentDelivery(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            _logger.LogDebug("Starting content delivery process");

            using var scope = _serviceProvider.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IMotivationalContentService>();
            var dashboardCacheService = scope.ServiceProvider.GetRequiredService<IDashboardCacheService>();

            // Process real-time content delivery based on user activity
            await ProcessActivityTriggeredContent(contentService, dashboardCacheService, cancellationToken);

            // Process time-based scheduled content
            await ProcessScheduledContent(contentService, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during content delivery process");
        }
    }

    /// <summary>
    /// Delivers content triggered by user activity
    /// </summary>
    private async Task ProcessActivityTriggeredContent(
        IMotivationalContentService contentService,
        IDashboardCacheService dashboardCacheService,
        CancellationToken cancellationToken)
    {
        try
        {
            // This would integrate with real-time user activity detection
            // For now, we'll simulate activity-based triggers

            // Example: Deliver streak celebration when user completes a task
            // Example: Deliver encouragement when user hasn't been active
            // Example: Deliver insights when user views dashboard

            // Placeholder for activity-triggered content delivery
            await Task.CompletedTask;

            _logger.LogDebug("Activity-triggered content delivery completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing activity-triggered content");
        }
    }

    /// <summary>
    /// Processes time-based scheduled content delivery
    /// </summary>
    private async Task ProcessScheduledContent(IMotivationalContentService contentService, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var preferencesRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserContentPreferences>>();

            // Get users whose next delivery time has passed
            var now = DateTime.UtcNow;
            var eligiblePreferences = await preferencesRepository.GetAllByConditionAsync(
                p => p.IsContentEnabled && 
                     (p.ContentPausedUntil == null || p.ContentPausedUntil <= now),
                cancellationToken);

            var deliveredCount = 0;

            foreach (var preferences in eligiblePreferences)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var nextDeliveryTime = preferences.GetNextPreferredDeliveryTime();
                    if (nextDeliveryTime.HasValue && nextDeliveryTime.Value <= now)
                    {
                        // Check if user can receive content now
                        var canDeliver = preferences.CanDeliverContentNow(
                            ContentDeliveryChannel.SignalR, 
                            MotivationalContentType.Insight);

                        if (canDeliver && !await contentService.HasReachedContentLimitsAsync(
                            preferences.UserId, ContentLimitTimeWindow.Daily, cancellationToken))
                        {
                            await DeliverPersonalizedContent(preferences.UserId, contentService, cancellationToken);
                            deliveredCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error delivering scheduled content to user {UserId}", preferences.UserId);
                }
            }

            if (deliveredCount > 0)
            {
                _logger.LogDebug("Delivered scheduled content to {DeliveredCount} users", deliveredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled content");
        }
    }

    /// <summary>
    /// Determines if content should be scheduled for a user
    /// </summary>
    private async Task<bool> ShouldScheduleContentForUser(
        UserContentPreferences preferences,
        IMotivationalContentService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if user has reached limits
            if (await contentService.HasReachedContentLimitsAsync(preferences.UserId, ContentLimitTimeWindow.Daily, cancellationToken))
            {
                return false;
            }

            // Check if enough time has passed since last content
            if (preferences.LastContentDelivery.HasValue)
            {
                var timeSinceLastContent = DateTime.UtcNow - preferences.LastContentDelivery.Value;
                var minimumInterval = preferences.PreferredFrequency switch
                {
                    ContentFrequency.Low => TimeSpan.FromHours(8),
                    ContentFrequency.Moderate => TimeSpan.FromHours(4),
                    ContentFrequency.High => TimeSpan.FromHours(2),
                    ContentFrequency.VeryHigh => TimeSpan.FromHours(1),
                    _ => TimeSpan.FromHours(4)
                };

                if (timeSinceLastContent < minimumInterval)
                {
                    return false;
                }
            }

            // Check if it's an appropriate time for the user
            var nextDeliveryTime = preferences.GetNextPreferredDeliveryTime();
            if (nextDeliveryTime.HasValue && nextDeliveryTime.Value > DateTime.UtcNow.AddMinutes(30))
            {
                return false; // Wait for preferred time
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if content should be scheduled for user {UserId}", preferences.UserId);
            return false;
        }
    }

    /// <summary>
    /// Schedules content for a specific user
    /// </summary>
    private async Task<int> ScheduleContentForUser(
        Guid userId,
        IMotivationalContentService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get personalized content for the user
            var contentResult = await contentService.GetPersonalizedContentAsync(
                userId,
                new ContentSelectionContext
                {
                    DeliveryChannel = ContentDeliveryChannel.SignalR,
                    TriggerEvent = "scheduled_delivery"
                },
                cancellationToken);

            if (contentResult != null)
            {
                // Schedule for immediate or near-future delivery based on user preferences
                var deliveryTime = contentResult.OptimalDeliveryTime ?? DateTime.UtcNow.AddMinutes(5);
                
                // For now, we'll deliver immediately if it's the right time, or skip if not
                if (deliveryTime <= DateTime.UtcNow.AddMinutes(30))
                {
                    await DeliverPersonalizedContent(userId, contentService, cancellationToken);
                    return 1;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling content for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Delivers personalized content to a user via SignalR
    /// </summary>
    private async Task DeliverPersonalizedContent(
        Guid userId,
        IMotivationalContentService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentResult = await contentService.GetPersonalizedContentAsync(
                userId,
                new ContentSelectionContext
                {
                    DeliveryChannel = ContentDeliveryChannel.SignalR,
                    TriggerEvent = "scheduled_delivery"
                },
                cancellationToken);

            if (contentResult == null)
            {
                _logger.LogDebug("No suitable content found for user {UserId}", userId);
                return;
            }

            // Record the delivery
            var deliveryLogId = await contentService.RecordContentDeliveryAsync(
                userId,
                contentResult.Content.Id,
                ContentDeliveryChannel.SignalR,
                new Dictionary<string, object>
                {
                    ["trigger"] = "scheduled_delivery",
                    ["personalizationScore"] = contentResult.PersonalizationScore,
                    ["reasonCode"] = contentResult.ReasonCode
                },
                contentResult.PersonalizationScore,
                contentResult.ABTestGroup,
                cancellationToken);

            // Deliver via Dashboard Hub
            await _dashboardHub.SendMotivationalContentAsync(userId, new
                {
                    deliveryId = deliveryLogId,
                    content = contentResult.Content,
                    personalizationScore = contentResult.PersonalizationScore,
                    reasonCode = contentResult.ReasonCode,
                    abTestGroup = contentResult.ABTestGroup,
                    timestamp = DateTime.UtcNow
                }, cancellationToken);

            _logger.LogDebug("Delivered motivational content {ContentId} to user {UserId} via SignalR",
                contentResult.Content.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering personalized content to user {UserId}", userId);
        }
    }

    /// <summary>
    /// Public method to trigger immediate content delivery for a specific user
    /// </summary>
    public async Task TriggerContentDeliveryAsync(Guid userId, string triggerEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IMotivationalContentService>();

            // Check if user can receive content
            if (await contentService.HasReachedContentLimitsAsync(userId, ContentLimitTimeWindow.Daily, cancellationToken))
            {
                _logger.LogDebug("User {UserId} has reached daily content limits", userId);
                return;
            }

            var contentResult = await contentService.GetPersonalizedContentAsync(
                userId,
                new ContentSelectionContext
                {
                    DeliveryChannel = ContentDeliveryChannel.SignalR,
                    TriggerEvent = triggerEvent
                },
                cancellationToken);

            if (contentResult != null)
            {
                // Record the delivery
                var deliveryLogId = await contentService.RecordContentDeliveryAsync(
                    userId,
                    contentResult.Content.Id,
                    ContentDeliveryChannel.SignalR,
                    new Dictionary<string, object>
                    {
                        ["trigger"] = triggerEvent,
                        ["personalizationScore"] = contentResult.PersonalizationScore,
                        ["reasonCode"] = contentResult.ReasonCode
                    },
                    contentResult.PersonalizationScore,
                    contentResult.ABTestGroup,
                    cancellationToken);

                // Deliver via Dashboard Hub
                await _dashboardHub.SendMotivationalContentAsync(userId, new
                    {
                        deliveryId = deliveryLogId,
                        content = contentResult.Content,
                        personalizationScore = contentResult.PersonalizationScore,
                        reasonCode = contentResult.ReasonCode,
                        abTestGroup = contentResult.ABTestGroup,
                        trigger = triggerEvent,
                        timestamp = DateTime.UtcNow
                    }, cancellationToken);

                _logger.LogDebug("Triggered content delivery {ContentId} to user {UserId} for event '{TriggerEvent}'",
                    contentResult.Content.Id, userId, triggerEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering content delivery for user {UserId} and event '{TriggerEvent}'",
                userId, triggerEvent);
        }
    }

    public override void Dispose()
    {
        _schedulingTimer?.Dispose();
        _deliveryTimer?.Dispose();
        _schedulingSemaphore?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Extension methods for integrating content scheduling with other services
/// </summary>
public static class ContentSchedulingExtensions
{
    /// <summary>
    /// Triggers motivational content delivery based on task completion
    /// </summary>
    public static async Task TriggerTaskCompletionContentAsync(
        this IServiceProvider serviceProvider,
        Guid userId,
        bool isStreakExtension = false)
    {
        try
        {
            var schedulingService = serviceProvider.GetService<ContentSchedulingService>();
            if (schedulingService != null)
            {
                var triggerEvent = isStreakExtension ? "streak_extended" : "task_completed";
                await schedulingService.TriggerContentDeliveryAsync(userId, triggerEvent);
            }
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetService<ILogger<ContentSchedulingService>>();
            logger?.LogError(ex, "Error triggering task completion content for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Triggers motivational content delivery when user views dashboard
    /// </summary>
    public static async Task TriggerDashboardViewContentAsync(
        this IServiceProvider serviceProvider,
        Guid userId)
    {
        try
        {
            var schedulingService = serviceProvider.GetService<ContentSchedulingService>();
            if (schedulingService != null)
            {
                await schedulingService.TriggerContentDeliveryAsync(userId, "dashboard_viewed");
            }
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetService<ILogger<ContentSchedulingService>>();
            logger?.LogError(ex, "Error triggering dashboard view content for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Triggers motivational content delivery for achievement milestones
    /// </summary>
    public static async Task TriggerAchievementContentAsync(
        this IServiceProvider serviceProvider,
        Guid userId,
        string achievementType)
    {
        try
        {
            var schedulingService = serviceProvider.GetService<ContentSchedulingService>();
            if (schedulingService != null)
            {
                await schedulingService.TriggerContentDeliveryAsync(userId, $"achievement_{achievementType}");
            }
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetService<ILogger<ContentSchedulingService>>();
            logger?.LogError(ex, "Error triggering achievement content for user {UserId}", userId);
        }
    }
}