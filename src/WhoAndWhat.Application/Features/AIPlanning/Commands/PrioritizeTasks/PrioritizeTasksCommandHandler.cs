using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.AIPlanning.Commands.PrioritizeTasks;

public class PrioritizeTasksCommandHandler : IRequestHandler<PrioritizeTasksCommand, Result<TaskPrioritizationResponse>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<PrioritizeTasksCommandHandler> _logger;

    public PrioritizeTasksCommandHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ILogger<PrioritizeTasksCommandHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TaskPrioritizationResponse>> Handle(PrioritizeTasksCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Running AI task prioritization for user {UserId} with {TaskCount} tasks", 
                request.UserId, request.TaskAnalysisContexts.Count);

            // Validate input
            if (!request.TaskAnalysisContexts.Any())
            {
                return Result<TaskPrioritizationResponse>.Failure("No tasks provided for prioritization");
            }

            if (request.MaxPrioritySuggestions <= 0 || request.MaxPrioritySuggestions > 100)
            {
                return Result<TaskPrioritizationResponse>.Failure("MaxPrioritySuggestions must be between 1 and 100");
            }

            // Validate that all tasks belong to the user
            var taskIds = request.TaskAnalysisContexts.Select(t => t.TaskId).ToList();
            var userTaskValidation = await ValidateTaskOwnership(request.UserId, taskIds, cancellationToken);
            if (!userTaskValidation.IsValid)
            {
                return Result<TaskPrioritizationResponse>.Failure(userTaskValidation.ErrorMessage);
            }

            // Get user's productivity patterns and historical data for context
            var productivityPatterns = await _taskRepository.GetProductivityPatternsAsync(request.UserId, cancellationToken);
            var overdueTasks = await _taskRepository.GetOverdueTasksAsync(request.UserId, cancellationToken);
            
            // Enhance priority context with additional data
            var enhancedContext = new PriorityAnalysisContext(
                request.PriorityContext.AnalysisDate,
                request.PriorityContext.RelatedTaskIds.Concat(taskIds).Distinct().ToList(),
                productivityPatterns.ToDictionary(
                    kvp => kvp.Key.ToString(), 
                    kvp => kvp.Value.TotalMinutes
                ),
                request.PriorityContext.CurrentGoals,
                CalculateWorkloadIntensity(request.TaskAnalysisContexts.Count, overdueTasks.Count())
            );

            // Run AI prioritization analysis
            var aiSuggestions = await _aiPlanningService.AnalyzeTaskPriorityAsync(
                request.TaskAnalysisContexts,
                enhancedContext,
                request.MaxPrioritySuggestions,
                cancellationToken
            );

            if (aiSuggestions == null || !aiSuggestions.Any())
            {
                _logger.LogWarning("AI service returned no priority suggestions for user {UserId}", request.UserId);
                
                // Generate fallback suggestions based on due dates and categories
                var fallbackSuggestions = GenerateFallbackPrioritization(request.TaskAnalysisContexts, request.MaxPrioritySuggestions);
                
                return Result<TaskPrioritizationResponse>.Success(new TaskPrioritizationResponse(
                    fallbackSuggestions,
                    enhancedContext,
                    0.6, // Lower confidence for fallback
                    new List<string> { "AI service unavailable - using rule-based prioritization", "Prioritized by due date and category" },
                    DateTime.UtcNow
                ));
            }

            // Calculate overall confidence and generate analysis notes
            var overallConfidence = aiSuggestions.Average(s => s.ConfidenceScore);
            var analysisNotes = GenerateAnalysisNotes(request.TaskAnalysisContexts, aiSuggestions, enhancedContext);

            var response = new TaskPrioritizationResponse(
                aiSuggestions.Take(request.MaxPrioritySuggestions).ToList(),
                enhancedContext,
                overallConfidence,
                analysisNotes,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated {Count} priority suggestions for user {UserId} with confidence {Confidence:F2}", 
                response.PrioritySuggestions.Count, request.UserId, overallConfidence);

            return Result<TaskPrioritizationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI task prioritization for user {UserId}", request.UserId);
            return Result<TaskPrioritizationResponse>.Failure("An error occurred during task prioritization");
        }
    }

    private async Task<(bool IsValid, string ErrorMessage)> ValidateTaskOwnership(Guid userId, List<Guid> taskIds, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var taskId in taskIds)
            {
                var belongsToUser = await _taskRepository.TaskBelongsToUserAsync(taskId, userId, cancellationToken);
                if (!belongsToUser)
                {
                    return (false, $"Task {taskId} does not belong to user {userId}");
                }
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating task ownership for user {UserId}", userId);
            return (false, "Error validating task ownership");
        }
    }

    private static WorkloadIntensity CalculateWorkloadIntensity(int activeTasks, int overdueTasks)
    {
        var totalWorkload = activeTasks + (overdueTasks * 2); // Weight overdue tasks more heavily
        
        return totalWorkload switch
        {
            <= 5 => WorkloadIntensity.Low,
            <= 15 => WorkloadIntensity.Moderate,
            <= 30 => WorkloadIntensity.High,
            _ => WorkloadIntensity.Overwhelming
        };
    }

    private static List<TaskPrioritySuggestion> GenerateFallbackPrioritization(
        List<TaskAnalysisContext> tasks, 
        int maxSuggestions)
    {
        var suggestions = new List<TaskPrioritySuggestion>();

        // Sort by due date and current priority
        var prioritizedTasks = tasks
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => GetPriorityWeight(t.CurrentPriority))
            .Take(maxSuggestions);

        foreach (var task in prioritizedTasks)
        {
            var suggestedPriority = DetermineFallbackPriority(task);
            var confidence = CalculateFallbackConfidence(task);
            var reasoning = GenerateFallbackReasoning(task);

            suggestions.Add(new TaskPrioritySuggestion(
                task.TaskId,
                suggestedPriority,
                confidence,
                reasoning,
                new List<string> { "due_date", "current_priority", "category" },
                DateTime.UtcNow
            ));
        }

        return suggestions;
    }

    private static string DetermineFallbackPriority(TaskAnalysisContext task)
    {
        // Simple rule-based priority determination
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.Today.AddDays(1))
        {
            return "critical";
        }
        
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.Today.AddDays(7))
        {
            return "high";
        }

        return task.CurrentCategory.ToLower() switch
        {
            "appointment" => "high",
            "billreminder" => "high", 
            "project" => "medium",
            "todo" => "medium",
            "idea" => "low",
            _ => "medium"
        };
    }

    private static double CalculateFallbackConfidence(TaskAnalysisContext task)
    {
        var confidence = 0.5; // Base confidence

        // Increase confidence if due date is set
        if (task.DueDate.HasValue)
        {
            confidence += 0.2;
        }

        // Increase confidence for certain categories
        if (task.CurrentCategory.ToLower() is "appointment" or "billreminder")
        {
            confidence += 0.2;
        }

        return Math.Min(confidence, 0.9); // Cap at 0.9 for fallback suggestions
    }

    private static string GenerateFallbackReasoning(TaskAnalysisContext task)
    {
        var reasons = new List<string>();

        if (task.DueDate.HasValue)
        {
            var daysUntilDue = (task.DueDate.Value - DateTime.Today).Days;
            if (daysUntilDue <= 1)
            {
                reasons.Add("Due today or tomorrow");
            }
            else if (daysUntilDue <= 7)
            {
                reasons.Add("Due within the next week");
            }
        }

        reasons.Add($"Category: {task.CurrentCategory}");
        reasons.Add($"Current priority: {task.CurrentPriority}");

        return string.Join(". ", reasons);
    }

    private static int GetPriorityWeight(string priority)
    {
        return priority.ToLower() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };
    }

    private static List<string> GenerateAnalysisNotes(
        List<TaskAnalysisContext> tasks,
        IEnumerable<TaskPrioritySuggestion> suggestions,
        PriorityAnalysisContext context)
    {
        var notes = new List<string>();

        var taskCount = tasks.Count;
        var suggestionCount = suggestions.Count();
        var avgConfidence = suggestions.Average(s => s.ConfidenceScore);

        notes.Add($"Analyzed {taskCount} tasks and generated {suggestionCount} priority suggestions");
        notes.Add($"Average confidence score: {avgConfidence:F2}");
        notes.Add($"Current workload intensity: {context.CurrentWorkload}");

        var overdueCount = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today);
        if (overdueCount > 0)
        {
            notes.Add($"Found {overdueCount} overdue tasks requiring immediate attention");
        }

        var dueSoonCount = tasks.Count(t => t.DueDate.HasValue && 
            t.DueDate.Value >= DateTime.Today && 
            t.DueDate.Value <= DateTime.Today.AddDays(3));
        if (dueSoonCount > 0)
        {
            notes.Add($"Found {dueSoonCount} tasks due within the next 3 days");
        }

        return notes;
    }
}