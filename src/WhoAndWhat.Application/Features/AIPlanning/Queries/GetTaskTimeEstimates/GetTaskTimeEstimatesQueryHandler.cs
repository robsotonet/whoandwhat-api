using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskTimeEstimates;

public class GetTaskTimeEstimatesQueryHandler : IRequestHandler<GetTaskTimeEstimatesQuery, Result<TaskTimeEstimatesResponse>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetTaskTimeEstimatesQueryHandler> _logger;

    public GetTaskTimeEstimatesQueryHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ILogger<GetTaskTimeEstimatesQueryHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TaskTimeEstimatesResponse>> Handle(GetTaskTimeEstimatesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating time estimates for user {UserId}", request.UserId);

            // If no specific task IDs provided, get recent active tasks
            var taskIds = request.TaskIds;
            if (!taskIds.Any())
            {
                var recentTasks = await _taskRepository.GetRecentTasksAsync(request.UserId, 20, cancellationToken);
                taskIds = recentTasks.Select(t => t.Id).ToList();
            }

            if (!taskIds.Any())
            {
                return Result<TaskTimeEstimatesResponse>.Failure("No tasks available for time estimation");
            }

            // Validate task ownership and get task details
            var tasks = new List<dynamic>();
            foreach (var taskId in taskIds.Take(50)) // Limit to 50 tasks for performance
            {
                var belongsToUser = await _taskRepository.TaskBelongsToUserAsync(taskId, request.UserId, cancellationToken);
                if (!belongsToUser)
                {
                    _logger.LogWarning("Task {TaskId} does not belong to user {UserId}", taskId, request.UserId);
                    continue;
                }

                var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
                if (task != null)
                {
                    tasks.Add(task);
                }
            }

            if (!tasks.Any())
            {
                return Result<TaskTimeEstimatesResponse>.Failure("No valid tasks found for estimation");
            }

            // Get user's historical performance data
            var userPerformance = await GetUserHistoricalPerformance(request.UserId, cancellationToken);

            // Prepare estimation requests
            var estimationRequests = tasks.Select(task => new TaskEstimationRequest(
                task.Id,
                task.Title,
                task.Description ?? string.Empty,
                task.Category.ToString(),
                task.Priority.ToString(),
                task.Tags?.Split(',').ToList() ?? new List<string>(),
                DetermineComplexityLevel(task)
            )).ToList();

            // Generate AI time estimates
            var aiEstimates = await _aiPlanningService.GenerateTaskTimeEstimatesAsync(
                request.UserId,
                estimationRequests,
                userPerformance,
                cancellationToken
            );

            if (aiEstimates == null || !aiEstimates.Any())
            {
                _logger.LogWarning("AI service returned no time estimates for user {UserId}", request.UserId);
                
                // Generate fallback estimates
                var fallbackEstimates = GenerateFallbackEstimates(tasks, userPerformance, request.IncludeConfidenceInterval);
                var fallbackNotes = new List<string> 
                { 
                    "AI service unavailable - using rule-based estimation",
                    "Estimates based on task category and historical averages"
                };

                return Result<TaskTimeEstimatesResponse>.Success(new TaskTimeEstimatesResponse(
                    fallbackEstimates,
                    userPerformance,
                    fallbackNotes,
                    DateTime.UtcNow
                ));
            }

            // Generate estimation notes
            var estimationNotes = GenerateEstimationNotes(tasks, aiEstimates, userPerformance);

            var response = new TaskTimeEstimatesResponse(
                aiEstimates.ToList(),
                userPerformance,
                estimationNotes,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated time estimates for {Count} tasks for user {UserId}", 
                response.TimeEstimates.Count, request.UserId);

            return Result<TaskTimeEstimatesResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time estimates for user {UserId}", request.UserId);
            return Result<TaskTimeEstimatesResponse>.Failure("An error occurred while generating time estimates");
        }
    }

    private async Task<UserHistoricalPerformance> GetUserHistoricalPerformance(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Get historical task completion data
            var completedTasks = await GetCompletedTasksAsync(userId, cancellationToken);
            
            // Calculate average times by category
            var averageTimesByCategory = completedTasks
                .Where(t => t.CompletedAt.HasValue && t.EstimatedDuration.HasValue && t.ActualDuration.HasValue)
                .GroupBy(t => t.Category)
                .ToDictionary(
                    g => g.Key,
                    g => TimeSpan.FromMinutes(g.Average(t => t.ActualDuration!.Value.TotalMinutes))
                );

            // Calculate accuracy by complexity (simplified)
            var accuracyByComplexity = new Dictionary<string, double>
            {
                { "Simple", 0.85 },
                { "Moderate", 0.75 },
                { "Complex", 0.65 },
                { "Expert_Level", 0.55 }
            };

            // Generate historical data points
            var historicalData = completedTasks
                .Where(t => t.CompletedAt.HasValue && t.EstimatedDuration.HasValue && t.ActualDuration.HasValue)
                .Select(t => new PerformanceDataPoint(
                    t.CompletedAt!.Value.Date,
                    t.Category,
                    t.EstimatedDuration!.Value,
                    t.ActualDuration!.Value,
                    CalculateAccuracyScore(t.EstimatedDuration!.Value, t.ActualDuration!.Value)
                ))
                .OrderByDescending(d => d.Date)
                .Take(100)
                .ToList();

            return new UserHistoricalPerformance(
                userId,
                averageTimesByCategory,
                accuracyByComplexity,
                historicalData,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting historical performance for user {UserId}", userId);
            
            // Return default performance data
            return new UserHistoricalPerformance(
                userId,
                GetDefaultCategoryTimes(),
                new Dictionary<string, double>
                {
                    { "Simple", 0.8 },
                    { "Moderate", 0.7 },
                    { "Complex", 0.6 },
                    { "Expert_Level", 0.5 }
                },
                new List<PerformanceDataPoint>(),
                DateTime.UtcNow
            );
        }
    }

    private async Task<List<TaskCompletionHistory>> GetCompletedTasksAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Get completed tasks from the last 6 months
        var fromDate = DateTime.Today.AddMonths(-6);
        var toDate = DateTime.Today;
        
        var filter = new TaskFilter
        {
            UserId = userId,
            Status = AppTaskStatus.Completed,
            FromDate = fromDate,
            ToDate = toDate,
            PageSize = 200,
            PageNumber = 1
        };

        var (completedTasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, filter, cancellationToken);
        
        return completedTasks.Select(t => new TaskCompletionHistory(
            t.Id,
            t.Title,
            t.Category.ToString(),
            t.Priority.ToString(),
            t.CreatedAt,
            t.CompletedAt,
            EstimateOriginalDuration(t), // We don't have stored estimates, so estimate
            CalculateActualDuration(t.CreatedAt, t.CompletedAt)
        )).ToList();
    }

    private static TimeSpan? EstimateOriginalDuration(dynamic task)
    {
        // Simplified estimation - in a real app this would be stored
        return task.Category.ToString().ToLower() switch
        {
            "todo" => TimeSpan.FromMinutes(45),
            "idea" => TimeSpan.FromMinutes(20),
            "appointment" => TimeSpan.FromHours(1),
            "billreminder" => TimeSpan.FromMinutes(15),
            "project" => TimeSpan.FromHours(3),
            _ => TimeSpan.FromMinutes(60)
        };
    }

    private static TimeSpan? CalculateActualDuration(DateTime createdAt, DateTime? completedAt)
    {
        if (!completedAt.HasValue)
            return null;

        // Simplified calculation - actual work time would need to be tracked differently
        var totalDuration = completedAt.Value - createdAt;
        
        // Assume actual work time is a fraction of total elapsed time
        var workTimeFraction = totalDuration.TotalDays switch
        {
            <= 1 => 0.6,  // Same day completion
            <= 7 => 0.2,  // Week completion
            <= 30 => 0.1, // Month completion
            _ => 0.05      // Longer than a month
        };

        return TimeSpan.FromMinutes(totalDuration.TotalMinutes * workTimeFraction);
    }

    private static double CalculateAccuracyScore(TimeSpan estimated, TimeSpan actual)
    {
        if (estimated.TotalMinutes == 0) return 0.0;
        
        var ratio = actual.TotalMinutes / estimated.TotalMinutes;
        
        // Perfect accuracy is when actual equals estimated (ratio = 1.0)
        // Accuracy decreases as the ratio moves away from 1.0
        var accuracy = ratio switch
        {
            >= 0.8 and <= 1.2 => 1.0 - Math.Abs(ratio - 1.0) * 2, // High accuracy
            >= 0.5 and <= 2.0 => 0.6 - Math.Abs(ratio - 1.0) * 0.5, // Medium accuracy
            _ => Math.Max(0.1, 0.4 - Math.Abs(ratio - 1.0) * 0.3) // Low accuracy
        };

        return Math.Max(0.0, Math.Min(1.0, accuracy));
    }

    private static ComplexityLevel DetermineComplexityLevel(dynamic task)
    {
        var category = task.Category.ToString().ToLower();
        var titleLength = task.Title?.Length ?? 0;
        var hasDescription = !string.IsNullOrEmpty(task.Description);
        
        return category switch
        {
            "idea" => ComplexityLevel.Simple,
            "billreminder" => ComplexityLevel.Simple,
            "todo" => titleLength > 50 || hasDescription ? ComplexityLevel.Moderate : ComplexityLevel.Simple,
            "appointment" => ComplexityLevel.Moderate,
            "project" => hasDescription ? ComplexityLevel.Complex : ComplexityLevel.Moderate,
            _ => ComplexityLevel.Moderate
        };
    }

    private static Dictionary<string, TimeSpan> GetDefaultCategoryTimes()
    {
        return new Dictionary<string, TimeSpan>
        {
            { "todo", TimeSpan.FromMinutes(45) },
            { "idea", TimeSpan.FromMinutes(20) },
            { "appointment", TimeSpan.FromHours(1) },
            { "billreminder", TimeSpan.FromMinutes(15) },
            { "project", TimeSpan.FromHours(3) }
        };
    }

    private static List<TaskTimeEstimate> GenerateFallbackEstimates(
        List<dynamic> tasks, 
        UserHistoricalPerformance userPerformance,
        bool includeConfidenceInterval)
    {
        var estimates = new List<TaskTimeEstimate>();

        foreach (var task in tasks)
        {
            var category = task.Category.ToString().ToLower();
            var baseEstimate = userPerformance.AverageTimesByCategory.TryGetValue(category, out var avgTime)
                ? avgTime
                : GetDefaultCategoryTimes().TryGetValue(category, out var defaultTime)
                    ? defaultTime
                    : TimeSpan.FromMinutes(60);

            var confidence = 0.7; // Default confidence for fallback estimates
            var minDuration = TimeSpan.FromMinutes(baseEstimate.TotalMinutes * 0.7);
            var maxDuration = TimeSpan.FromMinutes(baseEstimate.TotalMinutes * 1.5);

            var factors = new List<string> { "historical_average", "task_category" };
            
            if (includeConfidenceInterval)
            {
                factors.Add("confidence_interval");
            }

            estimates.Add(new TaskTimeEstimate(
                task.Id,
                baseEstimate,
                includeConfidenceInterval ? minDuration : baseEstimate,
                includeConfidenceInterval ? maxDuration : baseEstimate,
                confidence,
                factors
            ));
        }

        return estimates;
    }

    private static List<string> GenerateEstimationNotes(
        List<dynamic> tasks,
        IEnumerable<TaskTimeEstimate> estimates,
        UserHistoricalPerformance userPerformance)
    {
        var notes = new List<string>();

        var taskCount = tasks.Count;
        var avgConfidence = estimates.Average(e => e.ConfidenceLevel);
        var totalEstimatedTime = TimeSpan.FromMinutes(estimates.Sum(e => e.EstimatedDuration.TotalMinutes));

        notes.Add($"Generated estimates for {taskCount} tasks");
        notes.Add($"Average confidence level: {avgConfidence:F2}");
        notes.Add($"Total estimated time: {totalEstimatedTime:h\\:mm}");

        if (userPerformance.HistoricalData.Any())
        {
            var avgAccuracy = userPerformance.HistoricalData.Average(d => d.AccuracyScore);
            notes.Add($"Historical accuracy: {avgAccuracy:F2}");
        }

        var complexTasks = tasks.Count(t => DetermineComplexityLevel(t) >= ComplexityLevel.Complex);
        if (complexTasks > 0)
        {
            notes.Add($"Found {complexTasks} complex tasks that may require additional time");
        }

        return notes;
    }
}

// Supporting types for time estimation

/// <summary>
/// Task completion history for analysis
/// </summary>
public record TaskCompletionHistory(
    Guid TaskId,
    string Title,
    string Category,
    string Priority,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    TimeSpan? EstimatedDuration,
    TimeSpan? ActualDuration
);