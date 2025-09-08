using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, Result<PagedResult<TaskDto>>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<GetTasksQueryHandler> _logger;

    public GetTasksQueryHandler(
        ITaskRepository taskRepository,
        ILogger<GetTasksQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<TaskDto>>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var searchCriteria = new TaskSearchCriteria
            {
                UserId = request.UserId,
                SearchText = request.Search,
                Categories = request.Categories,
                Statuses = request.Statuses,
                Priorities = request.Priorities,
                DueDateFrom = request.DueDateFrom,
                DueDateTo = request.DueDateTo,
                CreatedFrom = request.CreatedFrom,
                CreatedTo = request.CreatedTo,
                ContactIds = request.ContactIds,
                HasDueDate = request.HasDueDate,
                IsOverdue = request.IsOverdue,
                HasSubtasks = request.HasSubtasks,
                ParentTaskId = request.ParentTaskId,
                IncludeArchived = request.IncludeArchived
            };

            var pagedTasks = await _taskRepository.SearchAsync(
                searchCriteria, 
                request.PageNumber, 
                request.PageSize, 
                request.SortBy ?? "UpdatedAt", 
                request.SortDescending);

            var taskDtos = pagedTasks.Items.Select(MapToDto).ToList();

            var result = PagedResult<TaskDto>.Create(
                taskDtos,
                pagedTasks.TotalCount,
                pagedTasks.PageNumber,
                pagedTasks.PageSize);

            return Result<PagedResult<TaskDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks for user {UserId}", request.UserId);
            return Result<PagedResult<TaskDto>>.Failure("An error occurred while retrieving tasks");
        }
    }

    private static TaskDto MapToDto(DomainTask task)
    {
        var category = TaskCategory.FromValue(task.Category);
        var status = DomainTaskStatus.FromValue(task.Status);
        var priority = Priority.FromValue(task.Priority);

        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Category = task.Category,
            CategoryName = category.GetDisplayName(),
            Status = task.Status,
            StatusName = status.Name,
            Priority = task.Priority,
            PriorityName = priority.Name,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            IsArchived = task.IsArchived,
            ArchivedAt = task.ArchivedAt,
            ParentTaskId = task.ParentTaskId,
            Subtasks = task.Subtasks?.Select(MapToDto).ToList() ?? new List<TaskDto>(),
            TaskContacts = task.TaskContacts?.Select(tc => new TaskContactDto
            {
                TaskId = tc.TaskId,
                ContactId = tc.ContactId,
                Role = tc.Role
            }).ToList() ?? new List<TaskContactDto>()
        };
    }
}