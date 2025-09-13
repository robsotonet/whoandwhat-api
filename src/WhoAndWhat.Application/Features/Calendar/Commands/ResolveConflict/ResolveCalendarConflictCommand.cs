using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Features.Calendar.Commands.ResolveConflict;

public record ResolveCalendarConflictCommand(
    Guid UserId,
    Guid ConflictId,
    ConflictResolution Resolution
) : IRequest<Result<ConflictResolutionResult>>;