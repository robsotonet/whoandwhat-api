using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Commands.ExecuteTaskAction;

public class ExecuteTaskActionCommandHandler : IRequestHandler<ExecuteTaskActionCommand, Result<TaskDto>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly CategoryWorkflowService _categoryWorkflowService;
    private readonly ILogger<ExecuteTaskActionCommandHandler> _logger;

    public ExecuteTaskActionCommandHandler(
        IAppTaskRepository taskRepository,
        CategoryWorkflowService categoryWorkflowService,
        ILogger<ExecuteTaskActionCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryWorkflowService = categoryWorkflowService;
        _logger = logger;
    }

    public async Task<Result<TaskDto>> Handle(ExecuteTaskActionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.UserId != request.UserId)
            {
                return Result<TaskDto>.Failure("Task not found");
            }

            // Create workflow action
            var workflowAction = new WorkflowAction
            {
                ActionId = request.ActionId,
                Parameters = request.Parameters,
                ExecutedAt = DateTime.UtcNow,
                ExecutedBy = request.UserId
            };

            // Process the action using workflow service
            var workflowResult = _categoryWorkflowService.ProcessTaskAction(task, workflowAction);

            if (!workflowResult.IsSuccess)
            {
                return Result<TaskDto>.Failure($"Action execution failed: {string.Join(", ", workflowResult.ErrorMessages)}");
            }

            // Log warnings if any
            if (workflowResult.HasWarnings)
            {
                _logger.LogWarning("Task action warnings for task {TaskId}: {Warnings}",
                    task.Id, string.Join(", ", workflowResult.WarningMessages));
            }

            // Update task if changes were made
            if (workflowResult.HasChanges)
            {
                task.UpdatedAt = DateTime.UtcNow;
                await _taskRepository.UpdateAsync(task);
                await _taskRepository.SaveChangesAsync();
            }

            // Handle any created tasks (e.g., recurring tasks)
            if (workflowResult.CreatedTasks?.Any() == true)
            {
                foreach (var createdTask in workflowResult.CreatedTasks)
                {
                    await _taskRepository.AddAsync(createdTask);
                }
                await _taskRepository.SaveChangesAsync();
            }

            _logger.LogInformation("Executed action {ActionId} on task {TaskId} for user {UserId}",
                request.ActionId, task.Id, request.UserId);

            // Map to DTO
            var taskDto = MapToDto(task);
            return Result<TaskDto>.Success(taskDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {ActionId} on task {TaskId} for user {UserId}",
                request.ActionId, request.TaskId, request.UserId);
            return Result<TaskDto>.Failure("An error occurred while executing the task action");
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
                Role = tc.Role
            }).ToList() ?? new List<TaskContactDto>()
        };
    }
}
