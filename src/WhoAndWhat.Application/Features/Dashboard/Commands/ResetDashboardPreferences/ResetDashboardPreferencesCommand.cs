using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;

namespace WhoAndWhat.Application.Features.Dashboard.Commands.ResetDashboardPreferences;

/// <summary>
/// Command to reset user's dashboard preferences to default values
/// </summary>
public sealed record ResetDashboardPreferencesCommand(
    Guid UserId,
    bool ConfirmReset = false,
    List<string>? SpecificSettings = null) : IRequest<Result<ResetDashboardPreferencesResponse>>;

/// <summary>
/// Response after resetting dashboard preferences
/// </summary>
public sealed record ResetDashboardPreferencesResponse(
    bool Success,
    DashboardSettingsDto DefaultSettings,
    List<string> ResetSettings,
    DateTime ResetTimestamp
);