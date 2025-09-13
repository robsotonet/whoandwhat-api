using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Queries.GetCalendarView;

public record GetCalendarViewQuery(
    Guid UserId,
    DateTime StartDate,
    DateTime EndDate,
    CalendarViewType ViewType,
    bool IncludeEvents = true,
    bool IncludeTasks = true,
    bool IncludeTimeBlocks = false
) : IRequest<Result<CalendarViewResponse>>;

public record CalendarViewResponse(
    DateTime StartDate,
    DateTime EndDate,
    CalendarViewType ViewType,
    List<CalendarItem> Items,
    CalendarViewMetadata Metadata,
    DateTime GeneratedAt
);

public record CalendarItem(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    CalendarItemType Type,
    string? Category,
    string? Priority,
    CalendarItemStatus Status,
    Dictionary<string, object> Metadata
);

public record CalendarViewMetadata(
    int TotalItems,
    int TaskCount,
    int EventCount,
    int ConflictCount,
    List<string> ConnectedProviders,
    DateTime LastSyncTime
);

public enum CalendarViewType
{
    Daily,
    Weekly,
    Monthly
}

public enum CalendarItemType
{
    Task,
    Event,
    TimeBlock,
    Reminder
}

public enum CalendarItemStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    Overdue
}