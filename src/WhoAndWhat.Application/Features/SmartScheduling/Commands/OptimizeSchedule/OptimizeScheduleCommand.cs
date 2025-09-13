using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.OptimizeSchedule;

public record OptimizeScheduleCommand(
    Guid UserId,
    Guid ScheduleId,
    List<SmartScheduledItem> CurrentSchedule,
    OptimizationGoals Goals,
    List<ScheduleConstraint> Constraints
) : IRequest<Result<ScheduleOptimizationResponse>>;
