using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Dashboard.Commands.ResetDashboardPreferences;

/// <summary>
/// Handler for resetting user's dashboard preferences to default values
/// </summary>
public sealed class ResetDashboardPreferencesCommandHandler 
    : IRequestHandler<ResetDashboardPreferencesCommand, Result<ResetDashboardPreferencesResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ResetDashboardPreferencesCommandHandler> _logger;

    public ResetDashboardPreferencesCommandHandler(
        IUserRepository userRepository,
        ILogger<ResetDashboardPreferencesCommandHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<ResetDashboardPreferencesResponse>> Handle(
        ResetDashboardPreferencesCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Resetting dashboard preferences for user {UserId}, ConfirmReset: {ConfirmReset}", 
                request.UserId, request.ConfirmReset);

            // Verify user exists
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result<ResetDashboardPreferencesResponse>.Failure("User not found");
            }

            // Require confirmation for full reset
            if (!request.ConfirmReset && (request.SpecificSettings == null || !request.SpecificSettings.Any()))
            {
                return Result<ResetDashboardPreferencesResponse>.Failure("Reset confirmation required. Set ConfirmReset to true to proceed.");
            }

            // Get default settings
            var defaultSettings = GetDefaultDashboardSettings();

            // Determine what settings to reset
            var settingsToReset = DetermineSettingsToReset(request.SpecificSettings);

            // Perform the reset
            await ResetPreferences(request.UserId, settingsToReset, cancellationToken);

            var response = new ResetDashboardPreferencesResponse(
                Success: true,
                DefaultSettings: defaultSettings,
                ResetSettings: settingsToReset,
                ResetTimestamp: DateTime.UtcNow
            );

            _logger.LogInformation("Successfully reset dashboard preferences for user {UserId}. Reset settings: {Settings}", 
                request.UserId, string.Join(", ", settingsToReset));

            return Result<ResetDashboardPreferencesResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting dashboard preferences for user {UserId}", request.UserId);
            return Result<ResetDashboardPreferencesResponse>.Failure($"Failed to reset dashboard preferences: {ex.Message}");
        }
    }

    private DashboardSettingsDto GetDefaultDashboardSettings()
    {
        return new DashboardSettingsDto(
            Theme: "light",
            Language: "en",
            ShowCompletionStats: true,
            ShowProductivityStreak: true,
            ShowOverdueTasks: true,
            ShowMotivationalContent: true,
            RefreshInterval: 300, // 5 minutes
            VisibleWidgets: new List<string> 
            { 
                "completion-stats", 
                "productivity-streak", 
                "overdue-tasks", 
                "motivational-content" 
            },
            WidgetSettings: new Dictionary<string, object>
            {
                ["completion-stats"] = new { showTrends = true, period = "month" },
                ["productivity-streak"] = new { showMilestones = true, showHistory = true },
                ["overdue-tasks"] = new { maxItems = 10, sortBy = "priority" },
                ["motivational-content"] = new { frequency = "daily", categories = new[] { "productivity", "motivation" } }
            },
            NotificationSettings: new NotificationSettingsDto(
                EnableOverdueAlerts: true,
                EnableStreakReminders: true,
                EnableDailyDigest: false,
                OverdueAlertThreshold: 3,
                DigestFrequency: "weekly",
                QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6, 7 }
            ),
            DisplaySettings: new DisplaySettingsDto(
                ChartType: "bar",
                DateFormat: "MM/dd/yyyy",
                TimeFormat: "12h",
                Use24HourFormat: false,
                ItemsPerPage: 20,
                DefaultSortOrder: "priority",
                ShowAnimations: true,
                CompactMode: false
            )
        );
    }

    private List<string> DetermineSettingsToReset(List<string>? specificSettings)
    {
        if (specificSettings != null && specificSettings.Any())
        {
            // Reset only specific settings
            var validSettings = new[]
            {
                "theme", "language", "widgets", "notifications", "display",
                "refresh-interval", "completion-stats", "productivity-streak",
                "overdue-tasks", "motivational-content"
            };

            return specificSettings
                .Where(s => validSettings.Contains(s.ToLowerInvariant()))
                .ToList();
        }

        // Reset all settings
        return new List<string>
        {
            "theme", "language", "widgets", "notifications", "display",
            "refresh-interval", "completion-stats", "productivity-streak",
            "overdue-tasks", "motivational-content"
        };
    }

    private async Task ResetPreferences(Guid userId, List<string> settingsToReset, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Resetting preferences for user {UserId}, settings: {Settings}", 
            userId, string.Join(", ", settingsToReset));

        // This would typically:
        // 1. Load current user preferences
        // 2. Reset specified settings to defaults
        // 3. Keep unchanged settings intact
        // 4. Save updated preferences

        // Get default settings
        var defaultSettings = GetDefaultDashboardSettings();

        // Create preference dictionary with defaults for specified settings
        var preferencesToUpdate = new Dictionary<string, object>();

        foreach (var setting in settingsToReset)
        {
            switch (setting.ToLowerInvariant())
            {
                case "theme":
                    preferencesToUpdate["theme"] = defaultSettings.Theme;
                    break;
                case "language":
                    preferencesToUpdate["language"] = defaultSettings.Language;
                    break;
                case "widgets":
                    preferencesToUpdate["visibleWidgets"] = defaultSettings.VisibleWidgets;
                    preferencesToUpdate["widgetSettings"] = defaultSettings.WidgetSettings;
                    break;
                case "notifications":
                    preferencesToUpdate["notificationSettings"] = new Dictionary<string, object>
                    {
                        ["enableOverdueAlerts"] = defaultSettings.NotificationSettings.EnableOverdueAlerts,
                        ["enableStreakReminders"] = defaultSettings.NotificationSettings.EnableStreakReminders,
                        ["enableDailyDigest"] = defaultSettings.NotificationSettings.EnableDailyDigest,
                        ["overdueAlertThreshold"] = defaultSettings.NotificationSettings.OverdueAlertThreshold,
                        ["digestFrequency"] = defaultSettings.NotificationSettings.DigestFrequency,
                        ["quietHours"] = defaultSettings.NotificationSettings.QuietHours
                    };
                    break;
                case "display":
                    preferencesToUpdate["displaySettings"] = new Dictionary<string, object>
                    {
                        ["chartType"] = defaultSettings.DisplaySettings.ChartType,
                        ["dateFormat"] = defaultSettings.DisplaySettings.DateFormat,
                        ["timeFormat"] = defaultSettings.DisplaySettings.TimeFormat,
                        ["use24HourFormat"] = defaultSettings.DisplaySettings.Use24HourFormat,
                        ["itemsPerPage"] = defaultSettings.DisplaySettings.ItemsPerPage,
                        ["defaultSortOrder"] = defaultSettings.DisplaySettings.DefaultSortOrder,
                        ["showAnimations"] = defaultSettings.DisplaySettings.ShowAnimations,
                        ["compactMode"] = defaultSettings.DisplaySettings.CompactMode
                    };
                    break;
                case "refresh-interval":
                    preferencesToUpdate["refreshInterval"] = defaultSettings.RefreshInterval;
                    break;
                case "completion-stats":
                    preferencesToUpdate["showCompletionStats"] = defaultSettings.ShowCompletionStats;
                    break;
                case "productivity-streak":
                    preferencesToUpdate["showProductivityStreak"] = defaultSettings.ShowProductivityStreak;
                    break;
                case "overdue-tasks":
                    preferencesToUpdate["showOverdueTasks"] = defaultSettings.ShowOverdueTasks;
                    break;
                case "motivational-content":
                    preferencesToUpdate["showMotivationalContent"] = defaultSettings.ShowMotivationalContent;
                    break;
            }
        }

        // Store the reset preferences
        await StoreDashboardPreferences(userId, preferencesToUpdate, cancellationToken);

        _logger.LogInformation("Reset {Count} dashboard preference settings for user {UserId}", 
            preferencesToUpdate.Count, userId);
    }

    private async Task StoreDashboardPreferences(Guid userId, Dictionary<string, object> preferences, CancellationToken cancellationToken)
    {
        // Placeholder for actual preference storage
        // In reality, this would interact with a DashboardPreferences repository
        _logger.LogDebug("Storing dashboard preferences for user {UserId}", userId);
        
        await Task.CompletedTask; // Placeholder for async operation
    }
}