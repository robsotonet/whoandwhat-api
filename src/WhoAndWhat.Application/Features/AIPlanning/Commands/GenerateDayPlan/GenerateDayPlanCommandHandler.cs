using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.AIPlanning.Commands.GenerateDayPlan;

public class GenerateDayPlanCommandHandler : IRequestHandler<GenerateDayPlanCommand, Result<AIGeneratedPlan>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ILogger<GenerateDayPlanCommandHandler> _logger;

    public GenerateDayPlanCommandHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ICalendarSyncService calendarSyncService,
        ILogger<GenerateDayPlanCommandHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AIGeneratedPlan>> Handle(GenerateDayPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating AI day plan for user {UserId} on {Date} with {TaskCount} specific tasks",
                request.UserId, request.PlanDate, request.TaskIds.Count);

            // Validate the plan date
            if (request.PlanDate.Date < DateTime.Today.AddDays(-7))
            {
                return Result<AIGeneratedPlan>.Failure("Cannot generate plans for dates more than 7 days in the past");
            }

            if (request.PlanDate.Date > DateTime.Today.AddDays(365))
            {
                return Result<AIGeneratedPlan>.Failure("Cannot generate plans for dates more than 1 year in the future");
            }

            // Get tasks for the user using TaskFilter to get active tasks
            var filter = new TaskFilter
            {
                UserId = request.UserId,
                Statuses = new[] { AppTaskStatus.Pending, AppTaskStatus.InProgress, AppTaskStatus.Confirmed },
                PageSize = 50,
                PageNumber = 1
            };
            
            var (availableTasks, totalCount) = await _taskRepository.GetTasksByUserIdAsync(request.UserId, filter, cancellationToken);
            if (availableTasks == null || !availableTasks.Any())
            {
                _logger.LogWarning("No active tasks found for user {UserId}", request.UserId);
                return Result<AIGeneratedPlan>.Failure("No active tasks available for planning");
            }

            // Filter tasks if specific IDs provided
            var tasksToSchedule = request.TaskIds.Any()
                ? availableTasks.Where(t => request.TaskIds.Contains(t.Id)).ToList()
                : availableTasks.Take(20).ToList(); // Limit to 20 tasks for planning

            if (!tasksToSchedule.Any())
            {
                return Result<AIGeneratedPlan>.Failure("No valid tasks found for the specified task IDs");
            }

            // Get calendar events for the day if requested
            var calendarEvents = new List<object>();
            if (request.IncludeCalendarEvents)
            {
                try
                {
                    // In a real implementation, this would fetch actual calendar events
                    // For now, we'll simulate this
                    _logger.LogDebug("Including calendar events in day plan generation");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve calendar events, continuing without them");
                }
            }

            // Generate the AI plan
            var planningContext = new DayPlanningContext(
                request.UserId,
                request.PlanDate,
                tasksToSchedule.Select(t => new TaskPlanningInfo(
                    t.Id,
                    t.Title,
                    t.Description ?? string.Empty,
                    t.Category.ToString(),
                    t.Priority.ToString(),
                    t.DueDate,
                    EstimateTaskDuration(t),
                    t.Tags?.Split(',').ToList() ?? new List<string>()
                )).ToList(),
                request.Preferences,
                calendarEvents,
                request.FocusMode
            );

            // Create user preferences for AI planning
            var userPreferences = new UserPlanningPreferences
            {
                UserId = request.UserId,
                PreferredWorkingHours = new WorkingTimeRange
                {
                    StartTime = new TimeSpan(9, 0, 0), // 9 AM
                    EndTime = new TimeSpan(17, 0, 0), // 5 PM
                    IsEnabled = true
                },
                MaxTasksPerHour = 3,
                BreakDurationMinutes = 15,
                LunchBreakMinutes = 60,
                PreferMorningWork = request.FocusMode == FocusMode.DeepWork
            };

            var aiPlan = await _aiPlanningService.GenerateDayPlanAsync(
                request.UserId,
                request.PlanDate,
                userPreferences,
                cancellationToken
            );

            if (aiPlan == null)
            {
                return Result<AIGeneratedPlan>.Failure("AI service failed to generate a day plan");
            }

            _logger.LogInformation("Successfully generated AI day plan for user {UserId} with {ScheduledTasks} scheduled tasks",
                request.UserId, aiPlan.ScheduledTasks.Count);

            return Result<AIGeneratedPlan>.Success(aiPlan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI day plan for user {UserId}", request.UserId);
            return Result<AIGeneratedPlan>.Failure("An error occurred while generating the day plan");
        }
    }

    private static TimeSpan EstimateTaskDuration(dynamic task)
    {
        // Simple duration estimation logic - in a real implementation this would be more sophisticated
        return task.Category.ToString().ToLower() switch
        {
            "todo" => TimeSpan.FromMinutes(30),
            "idea" => TimeSpan.FromMinutes(15),
            "appointment" => TimeSpan.FromHours(1),
            "billreminder" => TimeSpan.FromMinutes(10),
            "project" => TimeSpan.FromHours(2),
            _ => TimeSpan.FromMinutes(45)
        };
    }
}

// Supporting types for day planning

/// <summary>
/// Context for day planning generation
/// </summary>
public record DayPlanningContext(
    Guid UserId,
    DateTime PlanDate,
    List<TaskPlanningInfo> AvailableTasks,
    UserPlanningPreferences Preferences,
    List<object> CalendarEvents,
    bool FocusMode
);

/// <summary>
/// Task information for planning
/// </summary>
public record TaskPlanningInfo(
    Guid TaskId,
    string Title,
    string Description,
    string Category,
    string Priority,
    DateTime? DueDate,
    TimeSpan EstimatedDuration,
    List<string> Tags
);