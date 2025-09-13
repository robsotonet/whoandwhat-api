using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Features.Calendar.Queries.GetCalendarConflicts;

public record GetCalendarConflictsQuery(
    Guid UserId,
    ConflictFilterOptions FilterOptions
) : IRequest<Result<CalendarConflictsResponse>>;

public record CalendarConflictsResponse(
    List<CalendarSyncConflict> Conflicts,
    int TotalConflicts,
    int UnresolvedCount,
    ConflictStatistics Statistics,
    DateTime GeneratedAt
);