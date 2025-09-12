using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;

/// <summary>
/// Command to update user's dashboard settings and preferences
/// </summary>
public sealed record UpdateDashboardSettingsCommand(
    Guid UserId,
    DashboardSettingsDto Settings) : IRequest<Result<UpdateDashboardSettingsResponse>>;

/// <summary>
/// Dashboard settings data transfer object
/// </summary>
public sealed record DashboardSettingsDto(
    string Theme,
    string Language,
    bool ShowCompletionStats,
    bool ShowProductivityStreak,
    bool ShowOverdueTasks,
    bool ShowMotivationalContent,
    int RefreshInterval,
    List<string> VisibleWidgets,
    Dictionary<string, object> WidgetSettings,
    NotificationSettingsDto NotificationSettings,
    DisplaySettingsDto DisplaySettings
);

/// <summary>
/// Notification settings for dashboard
/// </summary>
public sealed record NotificationSettingsDto(
    bool EnableOverdueAlerts,
    bool EnableStreakReminders,
    bool EnableDailyDigest,
    int OverdueAlertThreshold,
    string DigestFrequency,
    List<int> QuietHours
);

/// <summary>
/// Display settings for dashboard
/// </summary>
public sealed record DisplaySettingsDto(
    string ChartType,
    string DateFormat,
    string TimeFormat,
    bool Use24HourFormat,
    int ItemsPerPage,
    string DefaultSortOrder,
    bool ShowAnimations,
    bool CompactMode
);

/// <summary>
/// Response after updating dashboard settings
/// </summary>
public sealed record UpdateDashboardSettingsResponse(
    bool Success,
    DashboardSettingsDto UpdatedSettings,
    List<string> ValidationWarnings
);