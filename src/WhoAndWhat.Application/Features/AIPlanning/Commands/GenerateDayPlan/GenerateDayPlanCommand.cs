using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Commands.GenerateDayPlan;

public record GenerateDayPlanCommand(
    Guid UserId,
    DateTime PlanDate,
    List<Guid> TaskIds,
    UserPlanningPreferences Preferences,
    bool IncludeCalendarEvents = true,
    bool FocusMode = false
) : IRequest<Result<AIGeneratedPlan>>;