using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskSuggestions;

public class GetTaskSuggestionsQueryHandler : IRequestHandler<GetTaskSuggestionsQuery, Result<TaskSuggestionsResponse>>
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetTaskSuggestionsQueryHandler> _logger;

    public GetTaskSuggestionsQueryHandler(
        IAIPlanningService aiPlanningService,
        IAppTaskRepository taskRepository,
        ILogger<GetTaskSuggestionsQueryHandler> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TaskSuggestionsResponse>> Handle(GetTaskSuggestionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating task suggestions for user {UserId} with context type: {ContextType}",
                request.UserId, request.ContextType ?? "general");

            // Validate max suggestions limit
            if (request.MaxSuggestions <= 0 || request.MaxSuggestions > 50)
            {
                return Result<TaskSuggestionsResponse>.Failure("MaxSuggestions must be between 1 and 50");
            }

            // Get user's task history for pattern analysis
            var filter = new TaskFilter
            {
                UserId = request.UserId,
                PageSize = 100,
                PageNumber = 1
            };

            var (recentTasks, totalCount) = await _taskRepository.GetTasksByUserIdAsync(request.UserId, filter, cancellationToken);

            // Get productivity patterns for the user
            var productivityPatterns = await _taskRepository.GetProductivityPatternsAsync(request.UserId, cancellationToken);

            // Build suggestion context
            var suggestionContext = new TaskSuggestionContext(
                request.UserId,
                request.ContextType ?? "general",
                DateTime.Now.TimeOfDay,
                DateTime.Now.DayOfWeek,
                recentTasks.Take(20).Select(t => new UserTaskHistory(
                    t.Id,
                    t.Title,
                    t.Category.ToString(),
                    t.Priority.ToString(),
                    t.Status.ToString(),
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.CompletedAt,
                    t.Tags?.Split(',').ToList() ?? new List<string>()
                )).ToList(),
                productivityPatterns,
                request.IncludeCategories,
                GetCurrentUserContext(request.UserId)
            );

            // Generate AI categorization suggestions
            var userCategoryHistory = new UserCategoryHistory
            {
                UserId = request.UserId,
                HistoricalCategories = completedTasks.Select(t => t.Category.ToString()).Distinct().ToList(),
                PreferredCategories = new List<string> { "Work", "Personal", "Administrative" }
            };
            
            var aiSuggestions = await _aiPlanningService.GetTaskCategorizationSuggestionsAsync(
                request.UserId,
                string.Join("; ", completedTasks.Select(t => t.Title).Take(5)),
                userCategoryHistory,
                cancellationToken);

            if (aiSuggestions == null || !aiSuggestions.Any())
            {
                _logger.LogWarning("No task suggestions generated for user {UserId}", request.UserId);
                
                // Return fallback suggestions
                var fallbackSuggestions = GenerateFallbackSuggestions(request.UserId, request.MaxSuggestions);
                return Result<TaskSuggestionsResponse>.Success(new TaskSuggestionsResponse(
                    fallbackSuggestions,
                    fallbackSuggestions.Count,
                    "fallback-suggestions",
                    DateTime.UtcNow
                ));
            }

            var response = new TaskSuggestionsResponse(
                aiSuggestions.ToList(),
                aiSuggestions.Count(),
                suggestionContext.ContextType,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated {Count} task suggestions for user {UserId}", 
                response.Suggestions.Count, request.UserId);

            return Result<TaskSuggestionsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating task suggestions for user {UserId}", request.UserId);
            return Result<TaskSuggestionsResponse>.Failure("An error occurred while generating task suggestions");
        }
    }

    private static Dictionary<string, object> GetCurrentUserContext(Guid userId)
    {
        // In a real implementation, this would fetch actual user context
        // For now, return basic context
        return new Dictionary<string, object>
        {
            { "currentTime", DateTime.Now },
            { "dayOfWeek", DateTime.Now.DayOfWeek.ToString() },
            { "timeOfDay", GetTimeOfDayCategory(DateTime.Now.TimeOfDay) },
            { "userId", userId }
        };
    }

    private static string GetTimeOfDayCategory(TimeSpan timeOfDay)
    {
        return timeOfDay.Hours switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };
    }

    private static List<TaskPrioritySuggestion> GenerateFallbackSuggestions(Guid userId, int maxSuggestions)
    {
        var fallbackSuggestions = new List<TaskPrioritySuggestion>
        {
            new TaskPrioritySuggestion(
                Guid.NewGuid(),
                "high",
                0.8,
                "Start your day with the most important task",
                new List<string> { "time_of_day", "productivity_patterns" },
                DateTime.UtcNow
            ),
            new TaskPrioritySuggestion(
                Guid.NewGuid(),
                "medium",
                0.7,
                "Focus on tasks with approaching deadlines",
                new List<string> { "due_dates", "urgency" },
                DateTime.UtcNow
            ),
            new TaskPrioritySuggestion(
                Guid.NewGuid(),
                "medium",
                0.6,
                "Review and update project tasks",
                new List<string> { "project_management", "progress_tracking" },
                DateTime.UtcNow
            ),
            new TaskPrioritySuggestion(
                Guid.NewGuid(),
                "low",
                0.5,
                "Clear small administrative tasks",
                new List<string> { "administrative", "quick_wins" },
                DateTime.UtcNow
            )
        };

        return fallbackSuggestions.Take(Math.Min(maxSuggestions, fallbackSuggestions.Count)).ToList();
    }
}

// Supporting types for task suggestions

/// <summary>
/// Context for generating task suggestions
/// </summary>
public record TaskSuggestionContext(
    Guid UserId,
    string ContextType,
    TimeSpan CurrentTime,
    DayOfWeek CurrentDay,
    List<UserTaskHistory> RecentTasks,
    Dictionary<DayOfWeek, TimeSpan> ProductivityPatterns,
    List<string> IncludeCategories,
    Dictionary<string, object> UserContext
);

/// <summary>
/// User task history for pattern analysis
/// </summary>
public record UserTaskHistory(
    Guid TaskId,
    string Title,
    string Category,
    string Priority,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    List<string> Tags
);