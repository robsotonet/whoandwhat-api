using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Features.Calendar.Commands.TriggerSync;

public record TriggerCalendarSyncCommand(
    Guid UserId,
    CalendarProvider? Provider,
    bool ForceFullSync,
    SyncDirection SyncDirection
) : IRequest<Result<CalendarSyncResult>>;