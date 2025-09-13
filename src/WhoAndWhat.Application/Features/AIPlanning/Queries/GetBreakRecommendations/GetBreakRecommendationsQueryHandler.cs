using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetBreakRecommendations;

public class GetBreakRecommendationsQueryHandler : IRequestHandler<GetBreakRecommendationsQuery, Result<BreakRecommendationsResponse>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetBreakRecommendationsQueryHandler> _logger;

    public GetBreakRecommendationsQueryHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ILogger<GetBreakRecommendationsQueryHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<BreakRecommendationsResponse>> Handle(GetBreakRecommendationsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating break recommendations for user {UserId} on {Date}",
                request.UserId, request.WorkloadAnalysis.AnalysisDate);

            // Validate workload analysis
            if (request.WorkloadAnalysis.AnalysisDate > DateTime.Today.AddDays(1))
            {
                return Result<BreakRecommendationsResponse>.Failure("Cannot analyze workload for future dates beyond tomorrow");
            }

            // Enhance workload analysis with current task data
            var enhancedWorkload = await EnhanceWorkloadAnalysis(request.UserId, request.WorkloadAnalysis, cancellationToken);

            // Generate AI-powered break recommendations
            var aiRecommendations = await _aiPlanningService.GenerateBreakRecommendationsAsync(
                enhancedWorkload,
                request.IncludeActivitySuggestions,
                cancellationToken
            );

            if (aiRecommendations == null || !aiRecommendations.Any())
            {
                _logger.LogWarning("AI service returned no break recommendations for user {UserId}", request.UserId);
                
                // Generate fallback recommendations
                var fallbackRecommendations = GenerateFallbackBreakRecommendations(enhancedWorkload, request.IncludeActivitySuggestions);
                var fallbackTips = GetGeneralBreakTips(enhancedWorkload.StressLevel);

                return Result<BreakRecommendationsResponse>.Success(new BreakRecommendationsResponse(
                    fallbackRecommendations,
                    enhancedWorkload,
                    fallbackTips,
                    DateTime.UtcNow
                ));
            }

            // Generate general tips based on workload
            var generalTips = GeneratePersonalizedTips(enhancedWorkload, aiRecommendations);

            var response = new BreakRecommendationsResponse(
                aiRecommendations.ToList(),
                enhancedWorkload,
                generalTips,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated {Count} break recommendations for user {UserId}",
                response.Recommendations.Count, request.UserId);

            return Result<BreakRecommendationsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating break recommendations for user {UserId}", request.UserId);
            return Result<BreakRecommendationsResponse>.Failure("An error occurred while generating break recommendations");
        }
    }

    private async Task<WorkloadAnalysis> EnhanceWorkloadAnalysis(
        Guid userId, 
        WorkloadAnalysis originalAnalysis, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current task data to enhance the analysis
            var todayTasks = await _taskRepository.GetTasksDueTodayAsync(userId, cancellationToken);
            var overdueTasks = await _taskRepository.GetOverdueTasksAsync(userId, cancellationToken);

            // Get recent productivity patterns
            var productivityPatterns = await _taskRepository.GetProductivityPatternsAsync(userId, cancellationToken);

            // Calculate enhanced metrics
            var tasksCompleted = originalAnalysis.TasksCompleted;
            var tasksRemaining = Math.Max(originalAnalysis.TasksRemaining, todayTasks.Count() + overdueTasks.Count());
            
            // Adjust stress level based on task data
            var enhancedStressLevel = CalculateEnhancedStressLevel(
                originalAnalysis.StressLevel,
                tasksRemaining,
                overdueTasks.Count(),
                originalAnalysis.ContinuousWorkTime
            );

            // Generate intensity indicators
            var intensityIndicators = GenerateIntensityIndicators(
                tasksCompleted,
                tasksRemaining,
                overdueTasks.Count(),
                originalAnalysis.ContinuousWorkTime,
                enhancedStressLevel
            );

            return new WorkloadAnalysis(
                originalAnalysis.AnalysisDate,
                tasksCompleted,
                tasksRemaining,
                enhancedStressLevel,
                originalAnalysis.ContinuousWorkTime,
                intensityIndicators
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enhancing workload analysis for user {UserId}, using original", userId);
            return originalAnalysis;
        }
    }

    private static double CalculateEnhancedStressLevel(
        double originalStress,
        int tasksRemaining,
        int overdueTasks,
        TimeSpan continuousWorkTime)
    {
        var stressFactors = new List<double> { originalStress };

        // Task volume stress
        var taskStress = tasksRemaining switch
        {
            <= 3 => 0.2,
            <= 8 => 0.4,
            <= 15 => 0.6,
            <= 25 => 0.8,
            _ => 1.0
        };
        stressFactors.Add(taskStress);

        // Overdue task stress
        var overdueStress = overdueTasks switch
        {
            0 => 0.0,
            1 => 0.3,
            <= 3 => 0.6,
            <= 5 => 0.8,
            _ => 1.0
        };
        stressFactors.Add(overdueStress);

        // Continuous work time stress
        var workTimeStress = continuousWorkTime.TotalHours switch
        {
            <= 1 => 0.1,
            <= 2 => 0.3,
            <= 4 => 0.5,
            <= 6 => 0.7,
            <= 8 => 0.9,
            _ => 1.0
        };
        stressFactors.Add(workTimeStress);

        // Return weighted average with original stress having more weight
        var weightedStress = (originalStress * 0.4) + (stressFactors.Skip(1).Average() * 0.6);
        return Math.Max(0.0, Math.Min(1.0, weightedStress));
    }

    private static List<string> GenerateIntensityIndicators(
        int tasksCompleted,
        int tasksRemaining,
        int overdueTasks,
        TimeSpan continuousWorkTime,
        double stressLevel)
    {
        var indicators = new List<string>();

        if (continuousWorkTime.TotalHours >= 3)
        {
            indicators.Add("extended_work_session");
        }

        if (overdueTasks > 0)
        {
            indicators.Add("overdue_tasks_present");
        }

        if (tasksRemaining > tasksCompleted * 2)
        {
            indicators.Add("high_task_backlog");
        }

        if (stressLevel >= 0.7)
        {
            indicators.Add("high_stress_level");
        }

        if (tasksCompleted >= 5)
        {
            indicators.Add("high_productivity_session");
        }

        var taskRatio = tasksRemaining > 0 ? (double)tasksCompleted / tasksRemaining : 1.0;
        if (taskRatio < 0.3)
        {
            indicators.Add("low_completion_rate");
        }

        return indicators;
    }

    private static List<BreakRecommendation> GenerateFallbackBreakRecommendations(
        WorkloadAnalysis workload,
        bool includeActivitySuggestions)
    {
        var recommendations = new List<BreakRecommendation>();
        var currentTime = DateTime.Now.TimeOfDay;

        // Immediate break if high stress or long work time
        if (workload.StressLevel >= 0.7 || workload.ContinuousWorkTime.TotalHours >= 2)
        {
            recommendations.Add(new BreakRecommendation(
                currentTime.Add(TimeSpan.FromMinutes(5)),
                TimeSpan.FromMinutes(15),
                BreakType.Active,
                includeActivitySuggestions ? "Take a 5-minute walk or do some light stretching" : "Take an active break",
                "High stress level detected - immediate break recommended",
                0.9
            ));
        }

        // Lunch break if appropriate time
        if (currentTime.Hours >= 11 && currentTime.Hours <= 14)
        {
            recommendations.Add(new BreakRecommendation(
                TimeSpan.FromHours(12),
                TimeSpan.FromMinutes(45),
                BreakType.Long,
                includeActivitySuggestions ? "Take a proper lunch break away from your workspace" : "Take a lunch break",
                "Midday nutrition and rest break",
                0.8
            ));
        }

        // Afternoon break
        if (currentTime.Hours >= 14 && currentTime.Hours <= 16)
        {
            recommendations.Add(new BreakRecommendation(
                TimeSpan.FromHours(15),
                TimeSpan.FromMinutes(10),
                BreakType.Short,
                includeActivitySuggestions ? "Have a healthy snack and hydrate" : "Take a short refresh break",
                "Afternoon energy maintenance",
                0.7
            ));
        }

        // Creative break for high task volume
        if (workload.TasksRemaining > 10)
        {
            recommendations.Add(new BreakRecommendation(
                currentTime.Add(TimeSpan.FromHours(1)),
                TimeSpan.FromMinutes(20),
                BreakType.Creative,
                includeActivitySuggestions ? "Listen to music or practice deep breathing" : "Take a creative break",
                "High task volume - creative break to reset focus",
                0.6
            ));
        }

        // End of day wind-down
        if (currentTime.Hours >= 17)
        {
            recommendations.Add(new BreakRecommendation(
                TimeSpan.FromHours(18),
                TimeSpan.FromMinutes(30),
                BreakType.Passive,
                includeActivitySuggestions ? "Review your day and plan for tomorrow" : "Take a wind-down break",
                "End of workday transition",
                0.8
            ));
        }

        return recommendations.OrderByDescending(r => r.ImportanceScore).Take(4).ToList();
    }

    private static List<string> GetGeneralBreakTips(double stressLevel)
    {
        var tips = new List<string>
        {
            "Take breaks before you feel you need them",
            "Stay hydrated throughout the day",
            "Change your physical position regularly"
        };

        if (stressLevel >= 0.7)
        {
            tips.AddRange(new[]
            {
                "Practice deep breathing exercises during breaks",
                "Consider taking longer breaks when stress is high",
                "Prioritize tasks to reduce overwhelming feelings"
            });
        }
        else if (stressLevel >= 0.4)
        {
            tips.AddRange(new[]
            {
                "Use breaks to step away from screens",
                "Take a few minutes to organize your workspace",
                "Practice mindfulness during break time"
            });
        }
        else
        {
            tips.AddRange(new[]
            {
                "Use productive breaks to maintain momentum",
                "Consider short social interactions during breaks",
                "Take advantage of good energy levels"
            });
        }

        return tips.Take(6).ToList();
    }

    private static List<string> GeneratePersonalizedTips(
        WorkloadAnalysis workload,
        IEnumerable<BreakRecommendation> recommendations)
    {
        var tips = GetGeneralBreakTips(workload.StressLevel);
        
        // Add personalized tips based on recommendations
        var hasActiveBreaks = recommendations.Any(r => r.BreakType == BreakType.Active);
        if (hasActiveBreaks)
        {
            tips.Add("Physical movement during breaks can significantly boost energy and focus");
        }

        var hasCreativeBreaks = recommendations.Any(r => r.BreakType == BreakType.Creative);
        if (hasCreativeBreaks)
        {
            tips.Add("Creative breaks help reset your mind and can lead to new insights");
        }

        if (workload.ContinuousWorkTime.TotalHours >= 4)
        {
            tips.Add("Long work sessions require more frequent and longer breaks");
        }

        if (workload.IntensityIndicators.Contains("overdue_tasks_present"))
        {
            tips.Add("When dealing with overdue tasks, breaks help prevent rushed mistakes");
        }

        return tips.Distinct().Take(8).ToList();
    }
}