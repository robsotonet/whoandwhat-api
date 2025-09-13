using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Features.Calendar.Commands.ConnectProvider;

public record ConnectCalendarProviderCommand(
    Guid UserId,
    CalendarProvider Provider,
    CalendarProviderConfiguration Configuration
) : IRequest<Result<CalendarProviderConfigResult>>;