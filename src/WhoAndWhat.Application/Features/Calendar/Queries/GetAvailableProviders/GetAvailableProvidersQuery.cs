using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Features.Calendar.Queries.GetAvailableProviders;

public record GetAvailableProvidersQuery(
    Guid UserId
) : IRequest<Result<AvailableProvidersResponse>>;

public record AvailableProvidersResponse(
    List<AvailableCalendarProvider> Providers,
    List<CalendarProvider> ConnectedProviders,
    DateTime RetrievedAt
);