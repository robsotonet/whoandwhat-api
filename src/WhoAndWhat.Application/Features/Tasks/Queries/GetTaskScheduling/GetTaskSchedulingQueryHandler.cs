using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskScheduling;

public class GetTaskSchedulingQueryHandler : IRequestHandler<GetTaskSchedulingQuery, Result<TaskSchedulingResponse>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;
    private readonly ILogger<GetTaskSchedulingQueryHandler> _logger;

    public GetTaskSchedulingQueryHandler(
        ITaskRepository taskRepository,
        CategoryBusinessRuleService categoryBusinessRuleService,
        ILogger<GetTaskSchedulingQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryBusinessRuleService = categoryBusinessRuleService;
        _logger = logger;
    }

    public async Task<Result<TaskSchedulingResponse>> Handle(GetTaskSchedulingQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get incomplete tasks for the user
            var searchCriteria = new TaskSearchCriteria
            {
                UserId = request.UserId,
                Statuses = new List<int> 
                { 
                    (int)TaskStatus.Pending, 
                    (int)TaskStatus.InProgress, 
                    (int)TaskStatus.Confirmed 
                },
                IncludeArchived = false
            };

            var pagedTasks = await _taskRepository.SearchAsync(searchCriteria, 1, request.MaxSuggestions * 2);
            var tasks = pagedTasks.Items.ToList();

            if (!tasks.Any())
            {
                return Result<TaskSchedulingResponse>.Success(new TaskSchedulingResponse
                {
                    Suggestions = new List<TaskSchedulingSuggestionDto>(),
                    GeneratedAt = DateTime.UtcNow,
                    TotalEstimatedTime = TimeSpan.Zero
                });
            }

            // Get scheduling suggestions from business rule service
            var schedulingSuggestions = _categoryBusinessRuleService.GetSchedulingSuggestions(tasks);

            // Convert to DTOs and limit results
            var suggestionDtos = schedulingSuggestions.Suggestions
                .Take(request.MaxSuggestions)
                .Select(suggestion => new TaskSchedulingSuggestionDto
                {
                    TaskId = suggestion.Task.Id,
                    TaskTitle = suggestion.Task.Title,
                    RecommendedDate = suggestion.RecommendedDate,
                    EstimatedDuration = suggestion.EstimatedDuration,
                    Reason = suggestion.Reason,
                    Priority = suggestion.Task.Priority
                })
                .ToList();

            var totalEstimatedTime = suggestionDtos
                .Aggregate(TimeSpan.Zero, (total, suggestion) => total.Add(suggestion.EstimatedDuration));

            var response = new TaskSchedulingResponse
            {
                Suggestions = suggestionDtos,
                GeneratedAt = DateTime.UtcNow,
                TotalEstimatedTime = totalEstimatedTime
            };

            return Result<TaskSchedulingResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating task scheduling suggestions for user {UserId}", request.UserId);
            return Result<TaskSchedulingResponse>.Failure("An error occurred while generating scheduling suggestions");
        }
    }
}