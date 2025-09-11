using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetOverdueTasks;

/// <summary>
/// Handler for retrieving user's overdue tasks with analytics
/// </summary>
public sealed class GetOverdueTasksQueryHandler 
    : IRequestHandler<GetOverdueTasksQuery, Result<GetOverdueTasksResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetOverdueTasksQueryHandler> _logger;

    public GetOverdueTasksQueryHandler(
        IAppTaskRepository taskRepository,
        ILogger<GetOverdueTasksQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<GetOverdueTasksResponse>> Handle(
        GetOverdueTasksQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting overdue tasks for user {UserId}", request.UserId);

            var today = DateTime.UtcNow.Date;

            // Get all tasks for the user
            var allTasks = await _taskRepository.GetTasksByUserIdAsync(request.UserId, cancellationToken);
            
            // Filter to overdue tasks (active tasks with due date in the past)
            var overdueTasks = allTasks
                .Where(t => !t.IsDeleted && 
                          t.Status != (int)AppTaskStatus.Completed &&
                          t.Status != (int)AppTaskStatus.Archived &&
                          t.DueDate.HasValue && 
                          t.DueDate.Value.Date < today)
                .ToList();

            // Apply additional filters
            if (!string.IsNullOrWhiteSpace(request.CategoryFilter) && 
                Enum.TryParse<AppTaskCategory>(request.CategoryFilter, true, out var categoryFilter))
            {
                overdueTasks = overdueTasks.Where(t => t.Category == (int)categoryFilter).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.PriorityFilter) && 
                Enum.TryParse<Priority>(request.PriorityFilter, true, out var priorityFilter))
            {
                overdueTasks = overdueTasks.Where(t => t.Priority == (int)priorityFilter).ToList();
            }

            // Sort by priority (Critical first) and then by days overdue
            var sortedTasks = overdueTasks
                .OrderByDescending(t => t.Priority)
                .ThenByDescending(t => (today - t.DueDate!.Value.Date).Days)
                .ToList();

            // Apply limit if specified
            var tasksToReturn = request.Limit.HasValue ? 
                sortedTasks.Take(request.Limit.Value).ToList() : 
                sortedTasks;

            // Convert to DTOs
            var taskDtos = tasksToReturn.Select(task => MapToOverdueTaskDto(task, today)).ToList();

            // Calculate summary
            var summary = CalculateSummary(overdueTasks, today);

            // Calculate analytics
            var analytics = await CalculateAnalytics(overdueTasks, allTasks, today, cancellationToken);

            var response = new GetOverdueTasksResponse(
                Tasks: taskDtos,
                Summary: summary,
                Analytics: analytics
            );

            _logger.LogInformation("Successfully retrieved {Count} overdue tasks for user {UserId}", 
                taskDtos.Count, request.UserId);

            return Result<GetOverdueTasksResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue tasks for user {UserId}", request.UserId);
            return Result<GetOverdueTasksResponse>.Failure($"Failed to retrieve overdue tasks: {ex.Message}");
        }
    }

    private OverdueTaskDto MapToOverdueTaskDto(Domain.Entities.AppTask task, DateTime today)
    {
        var daysOverdue = (today - task.DueDate!.Value.Date).Days;
        var urgencyLevel = CalculateUrgencyLevel(task.Priority, daysOverdue);

        return new OverdueTaskDto(
            Id: task.Id,
            Title: task.Title,
            Description: task.Description,
            Category: ((AppTaskCategory)task.Category).ToString(),
            Priority: ((Priority)task.Priority).ToString(),
            DueDate: task.DueDate.Value,
            DaysOverdue: daysOverdue,
            CreatedDate: task.CreatedDate,
            LastModifiedDate: task.LastModifiedDate,
            Tags: task.Tags ?? new List<string>(),
            HasReminders: task.ReminderDate.HasValue,
            UrgencyLevel: urgencyLevel
        );
    }

    private string CalculateUrgencyLevel(int priority, int daysOverdue)
    {
        // High urgency: Critical priority OR more than 7 days overdue
        if (priority == (int)Priority.Critical || daysOverdue > 7)
            return "High";

        // Medium urgency: High priority OR more than 3 days overdue
        if (priority == (int)Priority.High || daysOverdue > 3)
            return "Medium";

        // Everything else is low urgency
        return "Low";
    }

    private OverdueTasksSummary CalculateSummary(List<Domain.Entities.AppTask> overdueTasks, DateTime today)
    {
        if (!overdueTasks.Any())
        {
            return new OverdueTasksSummary(
                TotalOverdue: 0,
                CriticalPriorityCount: 0,
                HighPriorityCount: 0,
                MediumPriorityCount: 0,
                LowPriorityCount: 0,
                AverageDaysOverdue: 0,
                MostOverdueDays: 0,
                MostOverdueCategory: "None",
                OldestOverdueDate: null
            );
        }

        var daysOverdueList = overdueTasks.Select(t => (today - t.DueDate!.Value.Date).Days).ToList();
        var averageDaysOverdue = daysOverdueList.Average();
        var mostOverdueDays = daysOverdueList.Max();
        
        var categoryGroups = overdueTasks.GroupBy(t => (AppTaskCategory)t.Category);
        var mostOverdueCategory = categoryGroups
            .OrderByDescending(g => g.Count())
            .First().Key.ToString();

        var oldestOverdueTask = overdueTasks.MinBy(t => t.DueDate);

        return new OverdueTasksSummary(
            TotalOverdue: overdueTasks.Count,
            CriticalPriorityCount: overdueTasks.Count(t => t.Priority == (int)Priority.Critical),
            HighPriorityCount: overdueTasks.Count(t => t.Priority == (int)Priority.High),
            MediumPriorityCount: overdueTasks.Count(t => t.Priority == (int)Priority.Medium),
            LowPriorityCount: overdueTasks.Count(t => t.Priority == (int)Priority.Low),
            AverageDaysOverdue: Math.Round(averageDaysOverdue, 1),
            MostOverdueDays: mostOverdueDays,
            MostOverdueCategory: mostOverdueCategory,
            OldestOverdueDate: oldestOverdueTask?.DueDate
        );
    }

    private async Task<OverdueTasksAnalytics> CalculateAnalytics(
        List<Domain.Entities.AppTask> overdueTasks, 
        List<Domain.Entities.AppTask> allTasks, 
        DateTime today,
        CancellationToken cancellationToken)
    {
        // Category breakdown
        var categoryBreakdown = overdueTasks
            .GroupBy(t => ((AppTaskCategory)t.Category).ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Priority breakdown
        var priorityBreakdown = overdueTasks
            .GroupBy(t => ((Priority)t.Priority).ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate overdue rate
        var tasksWithDueDates = allTasks.Count(t => t.DueDate.HasValue && !t.IsDeleted);
        var overdueRate = tasksWithDueDates > 0 ? 
            (double)overdueTasks.Count / tasksWithDueDates * 100 : 0.0;

        // Tasks overdue this week/month
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        
        var tasksOverdueThisWeek = overdueTasks.Count(t => t.DueDate >= weekStart);
        var tasksOverdueThisMonth = overdueTasks.Count(t => t.DueDate >= monthStart);

        // Generate trend data (last 7 days)
        var trendData = GenerateTrendData(allTasks, today);

        // Generate recommendations
        var recommendations = GenerateRecommendations(overdueTasks, overdueRate);

        return new OverdueTasksAnalytics(
            CategoryBreakdown: categoryBreakdown,
            PriorityBreakdown: priorityBreakdown,
            TrendData: trendData,
            RecommendedActions: recommendations,
            OverdueRate: Math.Round(overdueRate, 1),
            TasksOverdueThisWeek: tasksOverdueThisWeek,
            TasksOverdueThisMonth: tasksOverdueThisMonth
        );
    }

    private List<OverdueTrendPoint> GenerateTrendData(List<Domain.Entities.AppTask> allTasks, DateTime today)
    {
        var trendData = new List<OverdueTrendPoint>();

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            
            // Tasks that became overdue on this date
            var newOverdue = allTasks.Count(t => t.DueDate?.Date == date && 
                                               t.Status != (int)AppTaskStatus.Completed &&
                                               t.Status != (int)AppTaskStatus.Archived &&
                                               !t.IsDeleted);

            // Tasks that were resolved on this date (completed overdue tasks)
            var resolved = allTasks.Count(t => t.CompletedDate?.Date == date && 
                                             t.DueDate.HasValue && 
                                             t.DueDate.Value.Date < date);

            // Total overdue at end of day (simplified calculation)
            var totalOverdue = allTasks.Count(t => t.DueDate.HasValue && 
                                                 t.DueDate.Value.Date <= date && 
                                                 (!t.CompletedDate.HasValue || t.CompletedDate.Value.Date > date) &&
                                                 t.Status != (int)AppTaskStatus.Archived &&
                                                 !t.IsDeleted);

            trendData.Add(new OverdueTrendPoint(
                Date: date,
                NewOverdueTasks: newOverdue,
                ResolvedOverdueTasks: resolved,
                TotalOverdue: totalOverdue
            ));
        }

        return trendData;
    }

    private List<string> GenerateRecommendations(List<Domain.Entities.AppTask> overdueTasks, double overdueRate)
    {
        var recommendations = new List<string>();

        if (!overdueTasks.Any())
        {
            recommendations.Add("Great job! You have no overdue tasks.");
            return recommendations;
        }

        if (overdueRate > 50)
        {
            recommendations.Add("Consider breaking large tasks into smaller, manageable subtasks");
            recommendations.Add("Review and adjust due dates to be more realistic");
        }

        var criticalCount = overdueTasks.Count(t => t.Priority == (int)Priority.Critical);
        if (criticalCount > 0)
        {
            recommendations.Add($"Focus on {criticalCount} critical priority tasks first");
        }

        var oldestOverdue = overdueTasks.MinBy(t => t.DueDate);
        if (oldestOverdue != null)
        {
            var daysOld = (DateTime.UtcNow.Date - oldestOverdue.DueDate!.Value.Date).Days;
            if (daysOld > 30)
            {
                recommendations.Add($"Address the task '{oldestOverdue.Title}' which is {daysOld} days overdue");
            }
        }

        if (overdueTasks.Count > 10)
        {
            recommendations.Add("Consider rescheduling or delegating some tasks to reduce the backlog");
        }

        var categoryGroup = overdueTasks.GroupBy(t => t.Category).OrderByDescending(g => g.Count()).First();
        var categoryName = ((AppTaskCategory)categoryGroup.Key).ToString();
        if (categoryGroup.Count() > 1)
        {
            recommendations.Add($"Most overdue tasks are in {categoryName} - consider time blocking for this category");
        }

        return recommendations;
    }
}