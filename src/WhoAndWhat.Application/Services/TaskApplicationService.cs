using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Commands.ConvertTask;
using WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;
using WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;
using WhoAndWhat.Application.Features.Tasks.Commands.ExecuteTaskAction;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTask;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskScheduling;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskStatistics;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskWorkflow;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;

namespace WhoAndWhat.Application.Services;

public class TaskApplicationService : ITaskApplicationService
{
    private readonly IMediator _mediator;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;

    public TaskApplicationService(
        IMediator mediator,
        CategoryBusinessRuleService categoryBusinessRuleService)
    {
        _mediator = mediator;
        _categoryBusinessRuleService = categoryBusinessRuleService;
    }

    public async Task<Result<TaskDto>> CreateTaskAsync(CreateTaskRequest request, Guid userId)
    {
        var command = new CreateTaskCommand(
            request.Title,
            request.Description,
            request.Category,
            request.Priority,
            request.DueDate,
            request.ParentTaskId,
            request.ContactIds,
            request.Metadata,
            userId);

        return await _mediator.Send(command);
    }

    public async Task<Result<TaskDto>> GetTaskAsync(Guid taskId, Guid userId, bool includeSubtasks = true)
    {
        var query = new GetTaskQuery(taskId, userId, includeSubtasks);
        return await _mediator.Send(query);
    }

    public async Task<Result<PagedResult<TaskDto>>> GetTasksAsync(TaskQueryRequest request, Guid userId)
    {
        var query = new GetTasksQuery(
            userId,
            request.Search,
            request.Categories,
            request.Statuses,
            request.Priorities,
            request.DueDateFrom,
            request.DueDateTo,
            request.CreatedFrom,
            request.CreatedTo,
            request.ContactIds,
            request.HasDueDate,
            request.IsOverdue,
            request.HasSubtasks,
            request.ParentTaskId,
            request.SortBy,
            request.SortDescending,
            request.PageSize,
            request.PageNumber,
            request.IncludeArchived);

        return await _mediator.Send(query);
    }

    public async Task<Result<TaskDto>> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, Guid userId)
    {
        var command = new UpdateTaskCommand(
            taskId,
            request.Title,
            request.Description,
            request.Category,
            request.Status,
            request.Priority,
            request.DueDate,
            request.ClearDueDate,
            request.Metadata,
            request.ContactIds,
            userId);

        return await _mediator.Send(command);
    }

    public async Task<Result> DeleteTaskAsync(Guid taskId, Guid userId, bool hardDelete = false)
    {
        var command = new DeleteTaskCommand(taskId, userId, hardDelete);
        return await _mediator.Send(command);
    }

    public async Task<Result<TaskDto>> ConvertTaskAsync(Guid taskId, ConvertTaskRequest request, Guid userId)
    {
        var command = new ConvertTaskCommand(
            taskId,
            request.ToCategory,
            request.Reason,
            request.CreateSubtasks,
            userId);

        return await _mediator.Send(command);
    }

    public async Task<Result<TaskDto>> ExecuteTaskActionAsync(Guid taskId, TaskActionRequest request, Guid userId)
    {
        var command = new ExecuteTaskActionCommand(
            taskId,
            request.ActionId,
            request.Parameters,
            userId);

        return await _mediator.Send(command);
    }

    public async Task<Result<TaskWorkflowStateDto>> GetTaskWorkflowAsync(Guid taskId, Guid userId)
    {
        var query = new GetTaskWorkflowQuery(taskId, userId);
        return await _mediator.Send(query);
    }

    public async Task<Result<TaskSchedulingResponse>> GetTaskSchedulingSuggestionsAsync(Guid userId, DateTime? targetDate = null, int maxSuggestions = 20)
    {
        var query = new GetTaskSchedulingQuery(userId, targetDate, maxSuggestions);
        return await _mediator.Send(query);
    }

    public async Task<Result<TaskStatisticsResponse>> GetTaskStatisticsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var query = new GetTaskStatisticsQuery(userId, from, to);
        return await _mediator.Send(query);
    }

    public async Task<Result<TaskMetricsDto>> GetTaskMetricsAsync(Guid userId)
    {
        // This would typically get tasks and calculate metrics using the category business rule service
        // For now, return a simple implementation

        // Get user's tasks (last 90 days)
        var from = DateTime.UtcNow.AddDays(-90);
        var tasksQuery = new GetTasksQuery(userId, IncludeArchived: true);
        var tasksResult = await _mediator.Send(tasksQuery);

        if (!tasksResult.IsSuccess)
        {
            return Result<TaskMetricsDto>.Failure(tasksResult.Error);
        }

        // Convert DTOs back to domain objects for metrics calculation
        // In a real implementation, this should be optimized
        var tasks = new List<Domain.Entities.AppTask>(); // This would need proper mapping

        var metrics = _categoryBusinessRuleService.CalculateCategoryMetrics((IEnumerable<Domain.Entities.AppTask>)tasks);

        var metricsDto = new TaskMetricsDto
        {
            CategoryMetrics = metrics.Metrics.Select(cm => new CategoryMetricsDto
            {
                Category = new TaskCategoryDto
                {
                    Value = cm.Category.Value,
                    Name = cm.Category.Name,
                    DisplayName = cm.Category.GetDisplayName(),
                    Description = cm.Category.Description,
                    RequiresDueDate = cm.Category.RequiresDueDate,
                    AllowsSubtasks = cm.Category.AllowsSubtasks
                },
                TotalTasks = cm.TotalTasks,
                CompletedTasks = cm.CompletedTasks,
                OverdueTasks = cm.OverdueTasks,
                CompletionPercentage = cm.CompletionPercentage,
                AverageCompletionTime = TimeSpan.FromHours(cm.AverageCompletionTime),
                CommonPatterns = cm.CommonPatterns
            }).ToList(),
            CalculatedAt = metrics.CalculatedAt,
            TotalTasksAnalyzed = metrics.TotalTasksAnalyzed
        };

        return Result<TaskMetricsDto>.Success(metricsDto);
    }
}
