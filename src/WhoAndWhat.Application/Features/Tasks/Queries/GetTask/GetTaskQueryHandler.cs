using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTask;

public class GetTaskQueryHandler : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetTaskQueryHandler> _logger;

    public GetTaskQueryHandler(
        IAppTaskRepository taskRepository,
        ILogger<GetTaskQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<TaskDto>> Handle(GetTaskQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var task = request.IncludeSubtasks
                ? await _taskRepository.GetTaskWithSubtasksAndContactsAsync(request.TaskId, request.UserId)
                : await _taskRepository.GetTaskWithContactsAsync(request.TaskId, request.UserId);

            if (task == null)
            {
                return Result<TaskDto>.Failure("Task not found");
            }

            var taskDto = MapToDto(task);
            return Result<TaskDto>.Success(taskDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            return Result<TaskDto>.Failure("An error occurred while retrieving the task");
        }
    }

    private static TaskDto MapToDto(DomainTask task)
    {
        var category = AppTaskCategory.FromValue(task.Category);
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
            StatusName = status.GetDisplayName(),
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
                ContactName = tc.Contact?.Name ?? string.Empty,
                ContactEmail = tc.Contact?.Email ?? string.Empty,
                Role = tc.Role
            }).ToList() ?? new List<TaskContactDto>()
        };
    }
}
