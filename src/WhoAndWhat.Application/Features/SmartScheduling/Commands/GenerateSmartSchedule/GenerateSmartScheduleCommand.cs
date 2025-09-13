using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.GenerateSmartSchedule;

public record GenerateSmartScheduleCommand(
    Guid UserId,
    DateTime StartDate,
    DateTime EndDate,
    List<Guid> TaskIds,
    SmartSchedulingPreferences Preferences,
    bool IncludeCalendarEvents = true,
    bool OptimizeForProductivity = true
) : IRequest<Result<SmartScheduleResponse>>;