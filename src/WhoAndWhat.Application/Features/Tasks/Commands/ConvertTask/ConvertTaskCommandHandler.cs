using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Commands.ConvertTask;

public class ConvertTaskCommandHandler : IRequestHandler<ConvertTaskCommand, Result<TaskDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;
    private readonly ILogger<ConvertTaskCommandHandler> _logger;

    public ConvertTaskCommandHandler(
        ITaskRepository taskRepository,
        CategoryBusinessRuleService categoryBusinessRuleService,
        ILogger<ConvertTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryBusinessRuleService = categoryBusinessRuleService;
        _logger = logger;
    }

    public async Task<Result<TaskDto>> Handle(ConvertTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.UserId != request.UserId)
            {
                return Result<TaskDto>.Failure("Task not found");
            }

            var fromCategory = TaskCategory.FromValue(task.Category);
            var toCategory = TaskCategory.FromValue(request.ToCategory);

            // Validate category transition using business rules
            var validationResult = _categoryBusinessRuleService.ValidateTaskUpdate(task, new TaskUpdateRequest
            {
                Category = request.ToCategory
            });

            if (!validationResult.IsValid)
            {
                return Result<TaskDto>.Failure($"Task conversion validation failed: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            // Log warnings if any
            if (validationResult.HasWarnings)
            {
                _logger.LogWarning("Task conversion warnings for task {TaskId}: {Warnings}", 
                    task.Id, string.Join(", ", validationResult.WarningMessages));
            }

            // Convert the task
            var oldCategory = task.Category;
            task.Category = request.ToCategory;
            task.UpdatedAt = DateTime.UtcNow;

            // Apply category-specific changes
            ApplyCategorySpecificChanges(task, fromCategory, toCategory, request.CreateSubtasks);

            await _taskRepository.UpdateAsync(task);

            // Create subtasks if requested and converting to Project
            if (request.CreateSubtasks && toCategory.Name == "Project" && !string.IsNullOrWhiteSpace(task.Description))
            {
                await CreateProjectSubtasks(task);
            }

            await _taskRepository.SaveChangesAsync();

            _logger.LogInformation("Converted task {TaskId} from {FromCategory} to {ToCategory} for user {UserId}",
                task.Id, fromCategory.Name, toCategory.Name, request.UserId);

            // Map to DTO
            var taskDto = MapToDto(task);
            return Result<TaskDto>.Success(taskDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            return Result<TaskDto>.Failure("An error occurred while converting the task");
        }
    }

    private void ApplyCategorySpecificChanges(DomainTask task, TaskCategory fromCategory, TaskCategory toCategory, bool createSubtasks)
    {
        // Reset status to Pending for most conversions
        if (task.Status == (int)DomainTaskStatus.Completed)
        {
            task.Status = (int)DomainTaskStatus.Pending;
        }

        // Category-specific adjustments
        switch (toCategory.Name)
        {
            case "Appointment":
                // Appointments need due dates
                if (!task.DueDate.HasValue)
                {
                    task.DueDate = DateTime.UtcNow.AddDays(7); // Default to next week
                }
                break;

            case "BillReminder":
                // Bill reminders need due dates and medium priority
                if (!task.DueDate.HasValue)
                {
                    task.DueDate = DateTime.UtcNow.AddDays(14); // Default to 2 weeks
                }
                if (task.Priority < (int)Priority.Medium)
                {
                    task.Priority = (int)Priority.Medium;
                }
                break;

            case "Project":
                // Projects can have lower priority if not urgent
                if (task.Priority == (int)Priority.High && fromCategory.Name == "Idea")
                {
                    task.Priority = (int)Priority.Medium;
                }
                break;

            case "Idea":
                // Ideas should have low priority and no urgent due dates
                if (task.Priority >= (int)Priority.High)
                {
                    task.Priority = (int)Priority.Low;
                }
                if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow.AddDays(1))
                {
                    task.DueDate = null;
                }
                break;
        }
    }

    private async Task CreateProjectSubtasks(DomainTask parentTask)
    {
        // Simple heuristic: create subtasks based on description content
        var description = parentTask.Description ?? "";
        var possibleTasks = new List<string>();

        // Look for bullet points or numbered lists
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("• ") || 
                char.IsDigit(trimmedLine.FirstOrDefault()) && trimmedLine.Contains('.'))
            {
                var taskTitle = trimmedLine
                    .TrimStart('-', '•', ' ')
                    .Trim();
                
                if (char.IsDigit(taskTitle.FirstOrDefault()))
                {
                    var dotIndex = taskTitle.IndexOf('.');
                    if (dotIndex > 0 && dotIndex < taskTitle.Length - 1)
                    {
                        taskTitle = taskTitle.Substring(dotIndex + 1).Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(taskTitle) && taskTitle.Length > 3)
                {
                    possibleTasks.Add(taskTitle);
                }
            }
        }

        // Create up to 5 subtasks
        foreach (var taskTitle in possibleTasks.Take(5))
        {
            var subtask = new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = parentTask.UserId,
                Title = taskTitle,
                Category = (int)TaskCategory.ToDo,
                Priority = (int)Priority.Medium,
                Status = (int)DomainTaskStatus.Pending,
                ParentTaskId = parentTask.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _taskRepository.AddAsync(subtask);
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