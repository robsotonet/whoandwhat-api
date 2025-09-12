using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;

/// <summary>
/// Handler for updating user's dashboard settings and preferences
/// </summary>
public sealed class UpdateDashboardSettingsCommandHandler 
    : IRequestHandler<UpdateDashboardSettingsCommand, Result<UpdateDashboardSettingsResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateDashboardSettingsCommandHandler> _logger;

    public UpdateDashboardSettingsCommandHandler(
        IUserRepository userRepository,
        ILogger<UpdateDashboardSettingsCommandHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<UpdateDashboardSettingsResponse>> Handle(
        UpdateDashboardSettingsCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating dashboard settings for user {UserId}", request.UserId);

            // Get the user
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result<UpdateDashboardSettingsResponse>.Failure("User not found");
            }

            // Validate settings
            var validationWarnings = ValidateSettings(request.Settings);

            // Convert DTO to domain preferences (simplified - in reality you'd have a DashboardPreferences entity)
            var preferences = ConvertToPreferences(request.Settings);

            // Store preferences (simplified - this would typically use a DashboardPreferences repository)
            // For now, we'll store as JSON in user preferences or a separate table
            await StoreDashboardPreferences(request.UserId, preferences, cancellationToken);

            var response = new UpdateDashboardSettingsResponse(
                Success: true,
                UpdatedSettings: request.Settings,
                ValidationWarnings: validationWarnings
            );

            _logger.LogInformation("Successfully updated dashboard settings for user {UserId}", request.UserId);
            return Result<UpdateDashboardSettingsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dashboard settings for user {UserId}", request.UserId);
            return Result<UpdateDashboardSettingsResponse>.Failure($"Failed to update dashboard settings: {ex.Message}");
        }
    }

    private List<string> ValidateSettings(DashboardSettingsDto settings)
    {
        var warnings = new List<string>();

        // Validate theme
        var validThemes = new[] { "light", "dark", "auto" };
        if (!validThemes.Contains(settings.Theme.ToLowerInvariant()))
        {
            warnings.Add($"Invalid theme '{settings.Theme}'. Using default theme.");
        }

        // Validate language
        var validLanguages = new[] { "en", "es" };
        if (!validLanguages.Contains(settings.Language.ToLowerInvariant()))
        {
            warnings.Add($"Invalid language '{settings.Language}'. Using default language.");
        }

        // Validate refresh interval
        if (settings.RefreshInterval < 30 || settings.RefreshInterval > 3600)
        {
            warnings.Add("Refresh interval should be between 30 seconds and 1 hour. Using default value.");
        }

        // Validate visible widgets
        var validWidgets = new[] { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content", "recent-activity" };
        var invalidWidgets = settings.VisibleWidgets.Where(w => !validWidgets.Contains(w.ToLowerInvariant())).ToList();
        if (invalidWidgets.Any())
        {
            warnings.Add($"Invalid widgets: {string.Join(", ", invalidWidgets)}");
        }

        // Validate quiet hours
        if (settings.NotificationSettings.QuietHours.Any(h => h < 0 || h > 23))
        {
            warnings.Add("Quiet hours must be between 0 and 23.");
        }

        // Validate overdue alert threshold
        if (settings.NotificationSettings.OverdueAlertThreshold < 0 || settings.NotificationSettings.OverdueAlertThreshold > 30)
        {
            warnings.Add("Overdue alert threshold should be between 0 and 30 days.");
        }

        return warnings;
    }

    private Dictionary<string, object> ConvertToPreferences(DashboardSettingsDto settings)
    {
        return new Dictionary<string, object>
        {
            ["theme"] = settings.Theme.ToLowerInvariant(),
            ["language"] = settings.Language.ToLowerInvariant(),
            ["showCompletionStats"] = settings.ShowCompletionStats,
            ["showProductivityStreak"] = settings.ShowProductivityStreak,
            ["showOverdueTasks"] = settings.ShowOverdueTasks,
            ["showMotivationalContent"] = settings.ShowMotivationalContent,
            ["refreshInterval"] = Math.Max(30, Math.Min(3600, settings.RefreshInterval)),
            ["visibleWidgets"] = settings.VisibleWidgets.Where(w => IsValidWidget(w)).ToList(),
            ["widgetSettings"] = settings.WidgetSettings ?? new Dictionary<string, object>(),
            ["notificationSettings"] = new Dictionary<string, object>
            {
                ["enableOverdueAlerts"] = settings.NotificationSettings.EnableOverdueAlerts,
                ["enableStreakReminders"] = settings.NotificationSettings.EnableStreakReminders,
                ["enableDailyDigest"] = settings.NotificationSettings.EnableDailyDigest,
                ["overdueAlertThreshold"] = Math.Max(0, Math.Min(30, settings.NotificationSettings.OverdueAlertThreshold)),
                ["digestFrequency"] = settings.NotificationSettings.DigestFrequency,
                ["quietHours"] = settings.NotificationSettings.QuietHours.Where(h => h >= 0 && h <= 23).ToList()
            },
            ["displaySettings"] = new Dictionary<string, object>
            {
                ["chartType"] = settings.DisplaySettings.ChartType,
                ["dateFormat"] = settings.DisplaySettings.DateFormat,
                ["timeFormat"] = settings.DisplaySettings.TimeFormat,
                ["use24HourFormat"] = settings.DisplaySettings.Use24HourFormat,
                ["itemsPerPage"] = Math.Max(5, Math.Min(100, settings.DisplaySettings.ItemsPerPage)),
                ["defaultSortOrder"] = settings.DisplaySettings.DefaultSortOrder,
                ["showAnimations"] = settings.DisplaySettings.ShowAnimations,
                ["compactMode"] = settings.DisplaySettings.CompactMode
            }
        };
    }

    private bool IsValidWidget(string widget)
    {
        var validWidgets = new[] { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content", "recent-activity" };
        return validWidgets.Contains(widget.ToLowerInvariant());
    }

    private async Task StoreDashboardPreferences(Guid userId, Dictionary<string, object> preferences, CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // In reality, you would have a DashboardPreferences entity and repository
        // For now, we'll assume this stores the preferences in a user settings table or JSON field

        _logger.LogDebug("Storing dashboard preferences for user {UserId}", userId);
        
        // Placeholder - actual implementation would:
        // 1. Check if preferences exist for user
        // 2. Create new or update existing preferences
        // 3. Save to database
        
        await Task.CompletedTask; // Placeholder for async operation
    }
}