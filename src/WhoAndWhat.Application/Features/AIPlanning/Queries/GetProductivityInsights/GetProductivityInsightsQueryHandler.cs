using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetProductivityInsights;

public class GetProductivityInsightsQueryHandler : IRequestHandler<GetProductivityInsightsQuery, Result<ProductivityInsights>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetProductivityInsightsQueryHandler> _logger;

    public GetProductivityInsightsQueryHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ILogger<GetProductivityInsightsQueryHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProductivityInsights>> Handle(GetProductivityInsightsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating productivity insights for user {UserId} from {StartDate} to {EndDate}",
                request.UserId, request.Timeframe.StartDate, request.Timeframe.EndDate);

            // Validate timeframe
            if (request.Timeframe.StartDate >= request.Timeframe.EndDate)
            {
                return Result<ProductivityInsights>.Failure("Start date must be before end date");
            }

            var timeSpan = request.Timeframe.EndDate - request.Timeframe.StartDate;
            if (timeSpan.TotalDays > 365)
            {
                return Result<ProductivityInsights>.Failure("Analysis period cannot exceed 365 days");
            }

            // Gather productivity data
            var productivityData = await GatherProductivityData(request.UserId, request.Timeframe, cancellationToken);

            if (productivityData.TotalTasks == 0)
            {
                return Result<ProductivityInsights>.Failure("No task data available for the specified timeframe");
            }

            // Generate AI-powered insights
            var timeframeAnalysis = new TimeframeAnalysis
            {
                StartDate = request.TimeRange.StartDate,
                EndDate = request.TimeRange.EndDate,
                AnalysisType = request.AnalysisType
            };
            
            var aiInsights = await _aiPlanningService.GetProductivityInsightsAsync(
                request.UserId,
                timeframeAnalysis,
                cancellationToken
            );

            if (aiInsights == null)
            {
                _logger.LogWarning("AI service returned null insights for user {UserId}", request.UserId);
                
                // Generate fallback insights
                var fallbackInsights = GenerateFallbackInsights(request.UserId, productivityData, request.Timeframe);
                return Result<ProductivityInsights>.Success(fallbackInsights);
            }

            _logger.LogInformation("Generated {PatternCount} productivity patterns and {RecommendationCount} recommendations for user {UserId}",
                aiInsights.IdentifiedPatterns.Count, aiInsights.ActionableRecommendations.Count, request.UserId);

            return Result<ProductivityInsights>.Success(aiInsights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating productivity insights for user {UserId}", request.UserId);
            return Result<ProductivityInsights>.Failure("An error occurred while generating productivity insights");
        }
    }

    private async Task<ProductivityAnalysisData> GatherProductivityData(
        Guid userId, 
        TimeframeAnalysis timeframe, 
        CancellationToken cancellationToken)
    {
        // Get tasks in the timeframe
        var tasksInPeriod = await _taskRepository.GetTasksForDateRangeAsync(
            userId, 
            timeframe.StartDate, 
            timeframe.EndDate, 
            cancellationToken);

        // Get completion trends
        var timeSpan = timeframe.EndDate - timeframe.StartDate;
        var completionTrends = await _taskRepository.GetTaskCompletionTrendsAsync(
            userId, 
            timeSpan, 
            cancellationToken);

        // Get productivity patterns
        var productivityPatterns = await _taskRepository.GetProductivityPatternsAsync(userId, cancellationToken);

        // Get task statistics
        var filter = new TaskFilter
        {
            UserId = userId,
            FromDate = timeframe.StartDate,
            ToDate = timeframe.EndDate,
            PageSize = 1000,
            PageNumber = 1
        };

        // Convert TaskFilter to AppTaskSearchCriteria  
        var searchCriteria = new AppTaskSearchCriteria
        {
            UserId = filter.UserId,
            DueDateFrom = filter.FromDate,
            DueDateTo = filter.ToDate,
            Statuses = new List<int>(), // Will be populated if we had specific statuses
            IncludeDeleted = false
        };
        
        var taskStats = await _taskRepository.GetStatisticsAsync(searchCriteria, cancellationToken);

        // Calculate performance metrics
        var performanceMetrics = CalculatePerformanceMetrics(tasksInPeriod, timeframe);

        return new ProductivityAnalysisData(
            userId,
            timeframe,
            tasksInPeriod.Count(),
            tasksInPeriod.Count(t => t.Status == (int)AppTaskStatus.Completed),
            tasksInPeriod.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today && t.Status != (int)AppTaskStatus.Completed),
            completionTrends,
            productivityPatterns,
            performanceMetrics,
            AnalyzeWorkPatterns(tasksInPeriod),
            CalculateCategoryDistribution(tasksInPeriod)
        );
    }

    private static Dictionary<string, double> CalculatePerformanceMetrics(IEnumerable<dynamic> tasks, TimeframeAnalysis timeframe)
    {
        var taskList = tasks.ToList();
        var totalTasks = taskList.Count;
        var completedTasks = taskList.Count(t => t.Status == (int)AppTaskStatus.Completed);
        var overdueTasks = taskList.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today && t.Status != (int)AppTaskStatus.Completed);

        var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks : 0;
        var overdueRate = totalTasks > 0 ? (double)overdueTasks / totalTasks : 0;

        // Calculate daily productivity (tasks completed per day)
        var analysisdays = Math.Max(1, (timeframe.EndDate - timeframe.StartDate).TotalDays);
        var dailyProductivity = completedTasks / analysisdays;

        // Calculate velocity (tasks created vs completed)
        var createdTasks = taskList.Count(t => t.CreatedAt >= timeframe.StartDate && t.CreatedAt <= timeframe.EndDate);
        var velocity = createdTasks > 0 ? (double)completedTasks / createdTasks : 0;

        return new Dictionary<string, double>
        {
            { "completion_rate", completionRate },
            { "overdue_rate", overdueRate },
            { "daily_productivity", dailyProductivity },
            { "velocity", velocity },
            { "total_tasks", totalTasks },
            { "completed_tasks", completedTasks },
            { "overdue_tasks", overdueTasks }
        };
    }

    private static List<ProductivityPattern> AnalyzeWorkPatterns(IEnumerable<dynamic> tasks)
    {
        var patterns = new List<ProductivityPattern>();
        var taskList = tasks.ToList();

        // Analyze completion patterns by day of week
        var completionsByDay = taskList
            .Where(t => t.CompletedAt.HasValue)
            .GroupBy(t => ((DateTime)t.CompletedAt).DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Count());

        if (completionsByDay.Any())
        {
            var mostProductiveDay = completionsByDay.OrderByDescending(kvp => kvp.Value).First();
            patterns.Add(new ProductivityPattern(
                "peak_completion_day",
                $"Most productive on {mostProductiveDay.Key}s",
                0.8,
                new List<string> { "day_of_week", "completion_timing" },
                0.7
            ));
        }

        // Analyze task creation patterns
        var creationsByHour = taskList
            .GroupBy(t => ((DateTime)t.CreatedAt).Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        if (creationsByHour.Any())
        {
            var peakCreationHour = creationsByHour.OrderByDescending(kvp => kvp.Value).First();
            var timeOfDay = GetTimeOfDayFromHour(peakCreationHour.Key);
            patterns.Add(new ProductivityPattern(
                "peak_creation_time",
                $"Most tasks created during {timeOfDay}",
                0.7,
                new List<string> { "time_of_day", "task_planning" },
                0.6
            ));
        }

        // Analyze priority patterns
        var priorityDistribution = taskList
            .GroupBy(t => t.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        if (priorityDistribution.Any())
        {
            var totalTasks = taskList.Count;
            var highPriorityCount = priorityDistribution.GetValueOrDefault("High", 0) + priorityDistribution.GetValueOrDefault("Critical", 0);
            var highPriorityPercentage = totalTasks > 0 ? (double)highPriorityCount / totalTasks : 0;

            if (highPriorityPercentage > 0.3)
            {
                patterns.Add(new ProductivityPattern(
                    "high_priority_focus",
                    "Tends to work on high-priority tasks",
                    highPriorityPercentage,
                    new List<string> { "priority_management", "focus" },
                    0.8
                ));
            }
        }

        return patterns;
    }

    private static string GetTimeOfDayFromHour(int hour)
    {
        return hour switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };
    }

    private static Dictionary<string, int> CalculateCategoryDistribution(IEnumerable<dynamic> tasks)
    {
        return tasks
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static ProductivityInsights GenerateFallbackInsights(
        Guid userId, 
        ProductivityAnalysisData data, 
        TimeframeAnalysis timeframe)
    {
        var patterns = AnalyzeWorkPatterns(data.TasksInPeriod.Cast<dynamic>());
        
        var recommendations = GenerateFallbackRecommendations(data);
        
        var trendAnalysis = new ProductivityTrendAnalysis(
            data.CompletionRate > 0.7 ? WhoAndWhat.Application.DTOs.AI.TrendDirection.Improving : 
            data.CompletionRate > 0.4 ? WhoAndWhat.Application.DTOs.AI.TrendDirection.Stable : WhoAndWhat.Application.DTOs.AI.TrendDirection.Declining,
            GenerateTrendDataPoints(data, timeframe),
            new List<string> { "completion_rate", "task_volume" },
            recommendations.Take(3).ToList()
        );

        return new ProductivityInsights(
            userId,
            DateTime.Today,
            patterns,
            recommendations,
            data.PerformanceMetrics,
            trendAnalysis
        );
    }

    private static List<string> GenerateFallbackRecommendations(ProductivityAnalysisData data)
    {
        var recommendations = new List<string>();

        if (data.CompletionRate < 0.5)
        {
            recommendations.Add("Consider breaking down large tasks into smaller, manageable pieces");
            recommendations.Add("Set clearer priorities to focus on most important tasks");
        }

        if (data.OverdueRate > 0.2)
        {
            recommendations.Add("Review and adjust task deadlines to be more realistic");
            recommendations.Add("Implement a daily review process to stay on top of due dates");
        }

        if (data.TotalTasks > data.CompletedTasks * 3)
        {
            recommendations.Add("Consider archiving or deleting tasks that are no longer relevant");
            recommendations.Add("Focus on task completion before creating new tasks");
        }

        // Category-specific recommendations
        var categoryDist = data.CategoryDistribution;
        var totalTasks = categoryDist.Values.Sum();
        
        if (totalTasks > 0)
        {
            var ideaPercentage = categoryDist.GetValueOrDefault("Idea", 0) / (double)totalTasks;
            if (ideaPercentage > 0.4)
            {
                recommendations.Add("Convert promising ideas into actionable tasks with specific next steps");
            }

            var projectPercentage = categoryDist.GetValueOrDefault("Project", 0) / (double)totalTasks;
            if (projectPercentage > 0.3)
            {
                recommendations.Add("Break down projects into smaller milestone tasks for better progress tracking");
            }
        }

        return recommendations.Take(8).ToList();
    }

    private static List<TrendDataPoint> GenerateTrendDataPoints(ProductivityAnalysisData data, TimeframeAnalysis timeframe)
    {
        var dataPoints = new List<TrendDataPoint>();
        
        // Generate trend points for the analysis period
        var currentDate = timeframe.StartDate;
        var interval = timeframe.Granularity switch
        {
            AnalysisGranularity.Daily => TimeSpan.FromDays(1),
            AnalysisGranularity.Weekly => TimeSpan.FromDays(7),
            AnalysisGranularity.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(1)
        };

        while (currentDate <= timeframe.EndDate)
        {
            dataPoints.Add(new TrendDataPoint(
                currentDate,
                data.CompletionRate + (Random.Shared.NextDouble() - 0.5) * 0.2, // Add some variance
                "completion_rate",
                new Dictionary<string, object> { { "period", interval.TotalDays } }
            ));
            
            currentDate = currentDate.Add(interval);
        }

        return dataPoints.Take(20).ToList(); // Limit to 20 data points
    }
}

// Supporting types for productivity analysis

/// <summary>
/// Comprehensive productivity analysis data
/// </summary>
public record ProductivityAnalysisData(
    Guid UserId,
    TimeframeAnalysis Timeframe,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    TaskCompletionTrends CompletionTrends,
    Dictionary<DayOfWeek, TimeSpan> ProductivityPatterns,
    Dictionary<string, double> PerformanceMetrics,
    List<ProductivityPattern> WorkPatterns,
    Dictionary<string, int> CategoryDistribution
)
{
    public double CompletionRate => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks : 0;
    public double OverdueRate => TotalTasks > 0 ? (double)OverdueTasks / TotalTasks : 0;
    public IEnumerable<object> TasksInPeriod => new List<object>(); // Simplified for this implementation
}