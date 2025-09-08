using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;
    private readonly ILogger<CreateTaskCommandHandler> _logger;

    public CreateTaskCommandHandler(
        IAppTaskRepository taskRepository,
        CategoryBusinessRuleService categoryBusinessRuleService,
        ILogger<CreateTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryBusinessRuleService = categoryBusinessRuleService;
        _logger = logger;
    }

    public async Task<Result<TaskDto>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var category = AppTaskCategory.FromValue(request.Category);
            var priority = Priority.FromValue(request.Priority);

            var task = new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                Priority = request.Priority,
                Status = (int)DomainTaskStatus.Pending,
                DueDate = request.DueDate,
                ParentTaskId = request.ParentTaskId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Subtasks = new List<DomainTask>(),
                TaskContacts = new List<TaskContact>()
            };

            // Validate task creation using category-specific business rules
            var validationResult = _categoryBusinessRuleService.ValidateTaskCreation(task);
            if (!validationResult.IsValid)
            {
                return Result<TaskDto>.Failure($"Task creation validation failed: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            // Log warnings if any
            if (validationResult.HasWarnings)
            {
                _logger.LogWarning("Task creation warnings for task {TaskId}: {Warnings}",
                    task.Id, string.Join(", ", validationResult.WarningMessages));
            }

            // Add task contacts
            if (request.ContactIds.Any())
            {
                foreach (var contactId in request.ContactIds)
                {
                    task.TaskContacts.Add(new TaskContact
                    {
                        TaskId = task.Id,
                        ContactId = contactId,
                        Role = "Participant",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Save task
            await _taskRepository.AddAsync(task);
            await _taskRepository.SaveChangesAsync();

            _logger.LogInformation("Created task {TaskId} with title '{Title}' for user {UserId}",
                task.Id, task.Title, request.UserId);

            // Map to DTO
            var taskDto = MapToDto(task);
            return Result<TaskDto>.Success(taskDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task for user {UserId}", request.UserId);
            return Result<TaskDto>.Failure("An error occurred while creating the task");
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
