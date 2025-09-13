using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Queries.GetCalendarView;

public class GetCalendarViewQueryHandler : IRequestHandler<GetCalendarViewQuery, Result<CalendarViewResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<GetCalendarViewQueryHandler> _logger;

    public GetCalendarViewQueryHandler(
        IAppTaskRepository taskRepository,
        ICalendarSyncService calendarSyncService,
        ISmartSchedulingService smartSchedulingService,
        ILogger<GetCalendarViewQueryHandler> logger)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CalendarViewResponse>> Handle(GetCalendarViewQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting {ViewType} calendar view for user {UserId} from {StartDate} to {EndDate}",
                request.ViewType, request.UserId, request.StartDate, request.EndDate);

            // Validate date range
            if (request.StartDate > request.EndDate)
            {
                return Result<CalendarViewResponse>.Failure("Start date must be before or equal to end date");
            }

            var dateRange = request.EndDate - request.StartDate;
            if (dateRange.TotalDays > 366)
            {
                return Result<CalendarViewResponse>.Failure("Date range cannot exceed 366 days");
            }

            var calendarItems = new List<CalendarItem>();
            var metadata = new CalendarViewMetadata(0, 0, 0, 0, new List<string>(), DateTime.UtcNow);

            // Get tasks if requested
            if (request.IncludeTasks)
            {
                var tasks = await GetTasksForDateRange(request.UserId, request.StartDate, request.EndDate, cancellationToken);
                var taskItems = ConvertTasksToCalendarItems(tasks);
                calendarItems.AddRange(taskItems);
                
                metadata = metadata with { TaskCount = taskItems.Count };
            }

            // Get external calendar events if requested
            if (request.IncludeEvents)
            {
                var (events, connectedProviders, lastSyncTime) = await GetCalendarEventsForDateRange(
                    request.UserId, request.StartDate, request.EndDate, cancellationToken);
                
                var eventItems = ConvertEventsToCalendarItems(events);
                calendarItems.AddRange(eventItems);
                
                metadata = metadata with 
                { 
                    EventCount = eventItems.Count,
                    ConnectedProviders = connectedProviders,
                    LastSyncTime = lastSyncTime
                };
            }

            // Get time blocks if requested (typically for daily view)
            if (request.IncludeTimeBlocks && request.ViewType == CalendarViewType.Daily)
            {
                var timeBlocks = await GetTimeBlockSuggestions(request.UserId, request.StartDate, cancellationToken);
                var timeBlockItems = ConvertTimeBlocksToCalendarItems(timeBlocks);
                calendarItems.AddRange(timeBlockItems);
            }

            // Sort items by start time
            calendarItems = calendarItems.OrderBy(item => item.StartTime).ToList();

            // Detect conflicts (overlapping items)
            var conflictCount = DetectTimeConflicts(calendarItems);

            var finalMetadata = metadata with 
            { 
                TotalItems = calendarItems.Count,
                ConflictCount = conflictCount
            };

            var response = new CalendarViewResponse(
                request.StartDate,
                request.EndDate,
                request.ViewType,
                calendarItems,
                finalMetadata,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated {ViewType} calendar view with {ItemCount} items for user {UserId}",
                request.ViewType, calendarItems.Count, request.UserId);

            return Result<CalendarViewResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating calendar view for user {UserId}", request.UserId);
            return Result<CalendarViewResponse>.Failure("An error occurred while generating the calendar view");
        }
    }

    private async Task<IEnumerable<dynamic>> GetTasksForDateRange(
        Guid userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get tasks that are due within the date range or are scheduled for the range
            var tasks = await _taskRepository.GetTasksForDateRangeAsync(userId, startDate, endDate, cancellationToken);
            
            // Also get tasks due today if we're looking at current dates
            if (startDate <= DateTime.Today && endDate >= DateTime.Today)
            {
                var todayTasks = await _taskRepository.GetTasksDueTodayAsync(userId, cancellationToken);
                tasks = tasks.Concat(todayTasks).DistinctBy(t => t.Id);
            }

            return tasks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting tasks for date range, returning empty list");
            return new List<dynamic>();
        }
    }

    private async Task<(List<dynamic> Events, List<string> Providers, DateTime LastSync)> GetCalendarEventsForDateRange(
        Guid userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get calendar sync status for the user
            var syncStatuses = await GetUserCalendarSyncStatuses(userId, cancellationToken);
            var connectedProviders = syncStatuses
                .Where(s => s.IsConnected)
                .Select(s => s.Provider.ToString())
                .ToList();

            var lastSyncTime = syncStatuses
                .Where(s => s.LastSyncTime.HasValue)
                .Select(s => s.LastSyncTime!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            // For now, return mock events - in a real implementation this would fetch from providers
            var mockEvents = GenerateMockCalendarEvents(userId, startDate, endDate);

            return (mockEvents, connectedProviders, lastSyncTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting calendar events, returning empty list");
            return (new List<dynamic>(), new List<string>(), DateTime.UtcNow);
        }
    }

    private async Task<List<CalendarSyncStatus>> GetUserCalendarSyncStatuses(Guid userId, CancellationToken cancellationToken)
    {
        // Mock implementation - in real app this would query the calendar sync service
        return new List<CalendarSyncStatus>
        {
            new CalendarSyncStatus(
                userId,
                CalendarProvider.Google,
                true, // IsConfigured
                true, // IsConnected
                DateTime.UtcNow.AddHours(-2), // LastSyncTime
                DateTime.UtcNow.AddMinutes(30), // NextScheduledSync
                "sync_token_123",
                45, // TotalEventsSynced
                0, // PendingConflicts
                new List<string> { "primary", "work" },
                SyncHealthStatus.Healthy,
                new Dictionary<string, object>()
            )
        };
    }

    private static List<dynamic> GenerateMockCalendarEvents(Guid userId, DateTime startDate, DateTime endDate)
    {
        // Generate some mock events for demonstration
        var events = new List<dynamic>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            // Add a few mock events per day
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                events.Add(new
                {
                    Id = Guid.NewGuid(),
                    Title = "Team Meeting",
                    Description = "Weekly team sync",
                    StartTime = currentDate.AddHours(10),
                    EndTime = currentDate.AddHours(11),
                    Type = "Event",
                    Category = "Work",
                    Status = "Confirmed"
                });

                if (currentDate.DayOfWeek == DayOfWeek.Wednesday)
                {
                    events.Add(new
                    {
                        Id = Guid.NewGuid(),
                        Title = "Client Call",
                        Description = "Project review call",
                        StartTime = currentDate.AddHours(14),
                        EndTime = currentDate.AddHours(15),
                        Type = "Event",
                        Category = "Work",
                        Status = "Confirmed"
                    });
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return events;
    }

    private async Task<List<dynamic>> GetTimeBlockSuggestions(Guid userId, DateTime date, CancellationToken cancellationToken)
    {
        try
        {
            // Use smart scheduling service to get time block suggestions
            // For now, return mock time blocks
            var timeBlocks = new List<dynamic>
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Title = "Deep Work Block",
                    Description = "Focused work time for important tasks",
                    StartTime = date.AddHours(9),
                    EndTime = date.AddHours(11),
                    Type = "TimeBlock",
                    Category = "Focus",
                    Status = "Suggested"
                },
                new
                {
                    Id = Guid.NewGuid(),
                    Title = "Administrative Tasks",
                    Description = "Time for emails and routine tasks",
                    StartTime = date.AddHours(14),
                    EndTime = date.AddHours(15),
                    Type = "TimeBlock",
                    Category = "Admin",
                    Status = "Suggested"
                }
            };

            return timeBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting time block suggestions, returning empty list");
            return new List<dynamic>();
        }
    }

    private static List<CalendarItem> ConvertTasksToCalendarItems(IEnumerable<dynamic> tasks)
    {
        return tasks.Select(task => new CalendarItem(
            task.Id,
            task.Title,
            task.Description,
            task.DueDate ?? DateTime.Today.AddHours(9), // Default to 9 AM if no due time
            (task.DueDate ?? DateTime.Today.AddHours(9)).AddHours(1), // Default 1-hour duration
            CalendarItemType.Task,
            task.Category?.ToString(),
            task.Priority?.ToString(),
            ConvertTaskStatusToCalendarStatus(task.Status?.ToString()),
            new Dictionary<string, object>
            {
                { "taskId", task.Id },
                { "originalStatus", task.Status?.ToString() ?? "Active" },
                { "createdAt", task.CreatedAt },
                { "tags", task.Tags ?? string.Empty }
            }
        )).ToList();
    }

    private static List<CalendarItem> ConvertEventsToCalendarItems(IEnumerable<dynamic> events)
    {
        return events.Select(evt => new CalendarItem(
            evt.Id,
            evt.Title,
            evt.Description,
            evt.StartTime,
            evt.EndTime,
            CalendarItemType.Event,
            evt.Category?.ToString(),
            null, // Events don't have priority
            CalendarItemStatus.Scheduled,
            new Dictionary<string, object>
            {
                { "eventId", evt.Id },
                { "type", evt.Type },
                { "status", evt.Status }
            }
        )).ToList();
    }

    private static List<CalendarItem> ConvertTimeBlocksToCalendarItems(IEnumerable<dynamic> timeBlocks)
    {
        return timeBlocks.Select(block => new CalendarItem(
            block.Id,
            block.Title,
            block.Description,
            block.StartTime,
            block.EndTime,
            CalendarItemType.TimeBlock,
            block.Category?.ToString(),
            null, // Time blocks don't have priority
            CalendarItemStatus.Scheduled,
            new Dictionary<string, object>
            {
                { "timeBlockId", block.Id },
                { "type", block.Type },
                { "status", block.Status }
            }
        )).ToList();
    }

    private static CalendarItemStatus ConvertTaskStatusToCalendarStatus(string? taskStatus)
    {
        return taskStatus?.ToLower() switch
        {
            "active" => CalendarItemStatus.Scheduled,
            "inprogress" => CalendarItemStatus.InProgress,
            "completed" => CalendarItemStatus.Completed,
            "cancelled" => CalendarItemStatus.Cancelled,
            "paused" => CalendarItemStatus.Scheduled,
            _ => CalendarItemStatus.Scheduled
        };
    }

    private static int DetectTimeConflicts(List<CalendarItem> items)
    {
        var conflicts = 0;
        
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                var item1 = items[i];
                var item2 = items[j];

                // Check if times overlap (both are not all-day events)
                if (item1.StartTime < item2.EndTime && item1.EndTime > item2.StartTime)
                {
                    conflicts++;
                }
            }
        }

        return conflicts;
    }
}