using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result<TaskDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;
    private readonly ILogger<UpdateTaskCommandHandler> _logger;

    public UpdateTaskCommandHandler(
        ITaskRepository taskRepository,
        CategoryBusinessRuleService categoryBusinessRuleService,
        ILogger<UpdateTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryBusinessRuleService = categoryBusinessRuleService;
        _logger = logger;
    }

    public async Task<Result<TaskDto>> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.UserId != request.UserId)
            {
                return Result<TaskDto>.Failure("Task not found");
            }

            // Create update request for validation
            var updateRequest = new TaskUpdateRequest
            {
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                Status = request.Status,
                Priority = request.Priority,
                DueDate = request.DueDate,
                ClearDueDate = request.ClearDueDate
            };

            // Validate update using category-specific business rules
            var validationResult = _categoryBusinessRuleService.ValidateTaskUpdate(task, updateRequest);
            if (!validationResult.IsValid)
            {
                return Result<TaskDto>.Failure($"Task update validation failed: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            // Log warnings if any
            if (validationResult.HasWarnings)
            {
                _logger.LogWarning("Task update warnings for task {TaskId}: {Warnings}", 
                    task.Id, string.Join(", ", validationResult.WarningMessages));
            }

            // Apply updates
            var hasChanges = false;

            if (!string.IsNullOrEmpty(request.Title) && request.Title != task.Title)
            {
                task.Title = request.Title;
                hasChanges = true;
            }

            if (request.Description != null && request.Description != task.Description)
            {
                task.Description = request.Description;
                hasChanges = true;
            }

            if (request.Category.HasValue && request.Category.Value != task.Category)
            {
                task.Category = request.Category.Value;
                hasChanges = true;
            }

            if (request.Status.HasValue && request.Status.Value != task.Status)
            {
                task.Status = request.Status.Value;
                hasChanges = true;
            }

            if (request.Priority.HasValue && request.Priority.Value != task.Priority)
            {
                task.Priority = request.Priority.Value;
                hasChanges = true;
            }

            if (request.ClearDueDate == true)
            {
                task.DueDate = null;
                hasChanges = true;
            }
            else if (request.DueDate.HasValue && request.DueDate != task.DueDate)
            {
                task.DueDate = request.DueDate;
                hasChanges = true;
            }

            // Update task contacts if provided
            if (request.ContactIds != null)
            {
                // Remove existing contacts
                task.TaskContacts?.Clear();

                // Add new contacts
                foreach (var contactId in request.ContactIds)
                {
                    task.TaskContacts?.Add(new TaskContact
                    {
                        TaskId = task.Id,
                        ContactId = contactId,
                        Role = "Participant",
                        CreatedAt = DateTime.UtcNow
                    });
                }
                hasChanges = true;
            }

            if (hasChanges)
            {
                task.UpdatedAt = DateTime.UtcNow;
                await _taskRepository.UpdateAsync(task);
                await _taskRepository.SaveChangesAsync();

                _logger.LogInformation("Updated task {TaskId} for user {UserId}", task.Id, request.UserId);
            }

            // Map to DTO
            var taskDto = MapToDto(task);
            return Result<TaskDto>.Success(taskDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            return Result<TaskDto>.Failure("An error occurred while updating the task");
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