using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;
using Asp.Versioning;
using WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;
using WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;
using WhoAndWhat.Application.Features.Tasks.Commands.ConvertTask;
using WhoAndWhat.Application.Features.Tasks.Commands.ExecuteTaskAction;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTask;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskStatistics;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskWorkflow;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskScheduling;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Task management controller handling core task operations
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tasks")]
[Tags("Task Management")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TasksController> _logger;

    /// <summary>
    /// Initializes a new instance of the Tasks controller
    /// </summary>
    /// <param name="mediator">MediatR mediator for command handling</param>
    /// <param name="logger">Logger for Tasks controller</param>
    public TasksController(IMediator mediator, ILogger<TasksController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get user's tasks with filtering, sorting, and pagination
    /// </summary>
    /// <param name="search">Search term for tasks</param>
    /// <param name="category">Filter by category</param>
    /// <param name="status">Filter by status</param>
    /// <param name="priority">Filter by priority</param>
    /// <param name="dueDateFrom">Filter by due date from</param>
    /// <param name="dueDateTo">Filter by due date to</param>
    /// <param name="isOverdue">Filter overdue tasks</param>
    /// <param name="includeArchived">Include archived tasks</param>
    /// <param name="sortBy">Sort field</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of tasks</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetTasks(
        [FromQuery] string? search = null,
        [FromQuery] int? category = null,
        [FromQuery] int? status = null,
        [FromQuery] int? priority = null,
        [FromQuery] DateTime? dueDateFrom = null,
        [FromQuery] DateTime? dueDateTo = null,
        [FromQuery] bool? isOverdue = null,
        [FromQuery] bool includeArchived = false,
        [FromQuery] string sortBy = "UpdatedAt",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new GetTasksQuery(
                UserId: userId.Value,
                Search: search,
                Categories: category.HasValue ? new List<int> { category.Value } : null,
                Statuses: status.HasValue ? new List<int> { status.Value } : null,
                Priorities: priority.HasValue ? new List<int> { priority.Value } : null,
                DueDateFrom: dueDateFrom,
                DueDateTo: dueDateTo,
                IsOverdue: isOverdue,
                SortBy: sortBy,
                SortDescending: sortDescending,
                PageSize: pageSize,
                PageNumber: pageNumber,
                IncludeArchived: includeArchived
            );

            _logger.LogInformation("Getting tasks for user {UserId} with search '{Search}', category {Category}, status {Status}", 
                userId.Value, search, category, status);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get tasks for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve tasks",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving tasks",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get a specific task by ID
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskDto>> GetTask(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new GetTaskQuery(id, userId.Value, true);

            _logger.LogInformation("Getting task {TaskId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get task {TaskId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve task",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {TaskId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving the task",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get task workflow information
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task workflow state information</returns>
    [HttpGet("{id:guid}/workflow")]
    [ProducesResponseType(typeof(TaskWorkflowStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskWorkflowStateDto>> GetTaskWorkflow(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetTaskWorkflowQuery(id, userId.Value);

            _logger.LogInformation("Getting workflow information for task {TaskId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get workflow for task {TaskId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve task workflow",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow for task {TaskId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving task workflow information",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    /// <param name="request">Task creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created task</returns>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> CreateTask(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new CreateTaskCommand(
                Title: request.Title,
                Description: request.Description,
                Category: request.Category,
                Priority: request.Priority,
                DueDate: request.DueDate,
                ParentTaskId: request.ParentTaskId,
                ContactIds: new List<Guid>(),
                Metadata: null,
                UserId: userId.Value
            );

            _logger.LogInformation("Creating task for user {UserId}: {Title}", userId.Value, request.Title);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create task for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to create task",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task created successfully: {TaskId}", result.Value.Id);

            return CreatedAtAction(
                nameof(GetTask),
                new { id = result.Value.Id },
                result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while creating the task",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update an existing task
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="request">Task update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated task</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> UpdateTask(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new UpdateTaskCommand(
                TaskId: id,
                Title: request.Title,
                Description: request.Description,
                Category: null,
                Status: request.Status,
                Priority: request.Priority,
                DueDate: request.DueDate,
                ClearDueDate: null,
                Metadata: null,
                ContactIds: null,
                UserId: userId.Value
            );

            _logger.LogInformation("Updating task {TaskId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to update task {TaskId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to update task",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task updated successfully: {TaskId}", id);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while updating the task",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Delete a task (soft delete)
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new DeleteTaskCommand(id, userId.Value);

            _logger.LogInformation("Deleting task {TaskId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to delete task {TaskId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to delete task",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task deleted successfully: {TaskId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while deleting the task",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get task statistics for the current user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(TaskStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskStatisticsResponse>> GetTaskStatistics(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new GetTaskStatisticsQuery(userId.Value);

            _logger.LogInformation("Getting task statistics for user {UserId}", userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get task statistics for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve task statistics",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task statistics");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving task statistics",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Convert a task to a project
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Converted project information</returns>
    [HttpPost("{id:guid}/convert-to-project")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> ConvertToProject(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new ConvertTaskCommand(
                TaskId: id,
                ToCategory: 4, // Project category
                Reason: "Convert to project",
                CreateSubtasks: false,
                UserId: userId.Value
            );

            _logger.LogInformation("Converting task {TaskId} to project for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to convert task {TaskId} to project for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to convert task to project",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task converted to project successfully: {TaskId}", id);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting task {TaskId} to project", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while converting the task to project",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Execute a task action (complete, pause, resume, archive, etc.)
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="request">Task action request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated task after action execution</returns>
    [HttpPost("{id:guid}/actions")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> ExecuteTaskAction(
        Guid id,
        [FromBody] TaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new ExecuteTaskActionCommand(
                TaskId: id,
                ActionId: request.ActionId,
                Parameters: request.Parameters ?? new Dictionary<string, object>(),
                UserId: userId.Value
            );

            _logger.LogInformation("Executing action '{ActionId}' for task {TaskId} for user {UserId}", 
                request.ActionId, id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to execute action '{ActionId}' for task {TaskId} for user {UserId}: {Error}", 
                    request.ActionId, id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to execute task action",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task action '{ActionId}' executed successfully for task: {TaskId}", request.ActionId, id);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action '{ActionId}' for task {TaskId}", request.ActionId, id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while executing the task action",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update task status
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated task</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> UpdateTaskStatus(
        Guid id,
        [FromBody] TaskStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var actionId = request.Status switch
            {
                1 => "MarkInProgress", // InProgress
                2 => "MarkCompleted", // Completed
                3 => "MarkArchived", // Archived
                _ => "MarkPending" // Pending
            };

            var command = new ExecuteTaskActionCommand(
                TaskId: id,
                ActionId: actionId,
                Parameters: new Dictionary<string, object>
                {
                    ["NewStatus"] = request.Status,
                    ["Reason"] = request.Reason ?? ""
                },
                UserId: userId.Value
            );

            _logger.LogInformation("Updating status for task {TaskId} to {Status} for user {UserId}", 
                id, request.Status, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to update task {TaskId} status for user {UserId}: {Error}", 
                    id, userId.Value, result.Error);
                
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task not found",
                        Detail = $"Task with ID {id} was not found or you don't have access to it",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to update task status",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Task status updated successfully: {TaskId}", id);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for task {TaskId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while updating the task status",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get task categories information
    /// </summary>
    /// <returns>List of available task categories</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<TaskCategoryInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TaskCategoryInfo>> GetCategories()
    {
        try
        {
            _logger.LogInformation("Getting task categories");

            var categories = new[]
            {
                new TaskCategoryInfo { Id = 0, Name = "ToDo", Description = "General tasks and to-do items" },
                new TaskCategoryInfo { Id = 1, Name = "Idea", Description = "Ideas and creative thoughts" },
                new TaskCategoryInfo { Id = 2, Name = "Appointment", Description = "Scheduled appointments and meetings" },
                new TaskCategoryInfo { Id = 3, Name = "BillReminder", Description = "Bills and payment reminders" },
                new TaskCategoryInfo { Id = 4, Name = "Project", Description = "Complex projects with subtasks" }
            };

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task categories");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving task categories",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get task status options information
    /// </summary>
    /// <returns>List of available task status options</returns>
    [HttpGet("status-options")]
    [ProducesResponseType(typeof(IEnumerable<TaskStatusInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TaskStatusInfo>> GetStatusOptions()
    {
        try
        {
            _logger.LogInformation("Getting task status options");

            var statusOptions = new[]
            {
                new TaskStatusInfo { Id = 0, Name = "Pending", Description = "Task is pending and not yet started" },
                new TaskStatusInfo { Id = 1, Name = "InProgress", Description = "Task is currently being worked on" },
                new TaskStatusInfo { Id = 2, Name = "Completed", Description = "Task has been completed successfully" },
                new TaskStatusInfo { Id = 3, Name = "Archived", Description = "Task has been archived and is no longer active" }
            };

            return Ok(statusOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task status options");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving task status options",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get overdue tasks for the current user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of overdue tasks</returns>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(IEnumerable<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetOverdueTasks(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new GetTasksQuery(
                UserId: userId.Value,
                IsOverdue: true,
                IncludeArchived: false,
                PageSize: 100,
                SortBy: "DueDate",
                SortDescending: false
            );

            _logger.LogInformation("Getting overdue tasks for user {UserId}", userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get overdue tasks for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve overdue tasks",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue tasks");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving overdue tasks",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get tasks due today for the current user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tasks due today</returns>
    [HttpGet("due-today")]
    [ProducesResponseType(typeof(IEnumerable<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasksDueToday(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var today = DateTime.UtcNow.Date;
            var command = new GetTasksQuery(
                UserId: userId.Value,
                DueDateFrom: today,
                DueDateTo: today.AddDays(1).AddTicks(-1),
                IncludeArchived: false,
                PageSize: 100,
                SortBy: "DueDate",
                SortDescending: false
            );

            _logger.LogInformation("Getting tasks due today for user {UserId}", userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get tasks due today for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve tasks due today",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks due today");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving tasks due today",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Perform batch operations on multiple tasks
    /// </summary>
    /// <param name="request">Batch operation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation results</returns>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(TaskBatchOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskBatchOperationResult>> BatchOperation(
        [FromBody] TaskBatchOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            _logger.LogInformation("Performing batch operation {Operation} on {TaskCount} tasks for user {UserId}", 
                request.Operation, request.TaskIds.Count(), userId.Value);

            var results = new List<TaskBatchItemResult>();

            foreach (var taskId in request.TaskIds)
            {
                try
                {
                    IRequest<Result> command = request.Operation switch
                    {
                        "delete" => new DeleteTaskCommand(taskId, userId.Value),
                        "complete" => new ExecuteTaskActionCommand(taskId, "MarkCompleted", new Dictionary<string, object>(), userId.Value),
                        "archive" => new ExecuteTaskActionCommand(taskId, "MarkArchived", new Dictionary<string, object>(), userId.Value),
                        _ => throw new ArgumentException($"Unknown operation: {request.Operation}")
                    };

                    var result = await _mediator.Send(command, cancellationToken);
                    
                    results.Add(new TaskBatchItemResult
                    {
                        TaskId = taskId,
                        Success = result.IsSuccess,
                        Error = result.IsSuccess ? null : result.Error
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch operation for task {TaskId}", taskId);
                    results.Add(new TaskBatchItemResult
                    {
                        TaskId = taskId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var batchResult = new TaskBatchOperationResult
            {
                Operation = request.Operation,
                TotalTasks = request.TaskIds.Count(),
                SuccessfulTasks = results.Count(r => r.Success),
                FailedTasks = results.Count(r => !r.Success),
                Results = results
            };

            _logger.LogInformation("Batch operation {Operation} completed: {Successful}/{Total} successful", 
                request.Operation, batchResult.SuccessfulTasks, batchResult.TotalTasks);

            return Ok(batchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch operation");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while performing the batch operation",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Perform bulk status updates on multiple tasks
    /// </summary>
    /// <param name="request">Bulk status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bulk status update results</returns>
    [HttpPost("batch/update-status")]
    [ProducesResponseType(typeof(TaskBatchOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskBatchOperationResult>> BatchUpdateStatus(
        [FromBody] TaskBatchStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            if (request.NewStatus < 0 || request.NewStatus > 3)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid status",
                    Detail = "Status must be between 0 (Pending) and 3 (Archived)",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Performing bulk status update to {NewStatus} on {TaskCount} tasks for user {UserId}", 
                request.NewStatus, request.TaskIds.Count(), userId.Value);

            var results = new List<TaskBatchItemResult>();

            foreach (var taskId in request.TaskIds)
            {
                try
                {
                    var actionId = request.NewStatus switch
                    {
                        1 => "MarkInProgress", // InProgress
                        2 => "MarkCompleted", // Completed
                        3 => "MarkArchived", // Archived
                        _ => "MarkPending" // Pending
                    };

                    var command = new ExecuteTaskActionCommand(
                        TaskId: taskId,
                        ActionId: actionId,
                        Parameters: new Dictionary<string, object>
                        {
                            ["NewStatus"] = request.NewStatus,
                            ["Reason"] = request.Reason ?? ""
                        },
                        UserId: userId.Value
                    );

                    var result = await _mediator.Send(command, cancellationToken);
                    
                    results.Add(new TaskBatchItemResult
                    {
                        TaskId = taskId,
                        Success = result.IsSuccess,
                        Error = result.IsSuccess ? null : result.Error
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing bulk status update for task {TaskId}", taskId);
                    results.Add(new TaskBatchItemResult
                    {
                        TaskId = taskId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var batchResult = new TaskBatchOperationResult
            {
                Operation = $"update-status-{request.NewStatus}",
                TotalTasks = request.TaskIds.Count(),
                SuccessfulTasks = results.Count(r => r.Success),
                FailedTasks = results.Count(r => !r.Success),
                Results = results
            };

            _logger.LogInformation("Bulk status update to {NewStatus} completed: {Successful}/{Total} successful", 
                request.NewStatus, batchResult.SuccessfulTasks, batchResult.TotalTasks);

            return Ok(batchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing bulk status update");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while performing the bulk status update",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Search tasks with full-text search capabilities
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="category">Filter by category</param>
    /// <param name="status">Filter by status</param>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(TaskSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskSearchResult>> SearchTasks(
        [FromQuery] string query,
        [FromQuery] int? category = null,
        [FromQuery] int? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid search query",
                    Detail = "Search query cannot be empty",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Create search criteria using existing infrastructure
            var searchCriteria = new AppTaskSearchCriteria
            {
                UserId = userId.Value,
                Query = query,
                Category = category.HasValue ? (AppTaskCategory)category.Value : null,
                Status = status.HasValue ? (AppTaskStatus)status.Value : null,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            _logger.LogInformation("Searching tasks for user {UserId} with query '{Query}'", userId.Value, query);

            // Use the existing task search service through a query handler
            var getTasksCommand = new GetTasksQuery(
                UserId: userId.Value,
                Search: query,
                Categories: category.HasValue ? new List<int> { category.Value } : null,
                Statuses: status.HasValue ? new List<int> { status.Value } : null,
                PageNumber: pageNumber,
                PageSize: pageSize,
                SortBy: "Relevance"
            );

            var result = await _mediator.Send(getTasksCommand, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to search tasks for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to search tasks",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Convert TaskDto items to TaskSearchItem
            var searchItems = result.Value.Items.Select(dto => new TaskSearchItem
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                Priority = dto.Priority,
                Category = dto.Category,
                Status = dto.Status,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                UserId = userId.Value,
                ProjectId = null, // Not included in TaskDto
                RelevanceScore = 1.0, // Default relevance
                MatchedTerms = new[] { query },
                MatchInfo = new SearchMatchInfo
                {
                    TitleMatch = dto.Title.Contains(query, StringComparison.OrdinalIgnoreCase),
                    DescriptionMatch = dto.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false
                }
            }).ToList();

            var searchResult = TaskSearchResult.Create(
                tasks: searchItems,
                totalCount: result.Value.TotalCount,
                pageNumber: pageNumber,
                pageSize: pageSize,
                searchDuration: TimeSpan.FromMilliseconds(100),
                searchQuery: query,
                metadata: new SearchResultMetadata { FromCache = false }
            );

            return Ok(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks with query '{Query}'", query);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while searching tasks",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get task scheduling suggestions and recommendations
    /// </summary>
    /// <param name="targetDate">Target date for scheduling (optional)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task scheduling recommendations</returns>
    [HttpGet("scheduling")]
    [ProducesResponseType(typeof(TaskSchedulingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskSchedulingResponse>> GetTaskScheduling(
        [FromQuery] DateTime? targetDate = null,
        [FromQuery] int maxSuggestions = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetTaskSchedulingQuery(userId.Value, targetDate, maxSuggestions);

            _logger.LogInformation("Getting task scheduling suggestions for user {UserId} for date {TargetDate}", 
                userId.Value, targetDate?.ToString("yyyy-MM-dd") ?? "today");

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get task scheduling for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve task scheduling",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task scheduling suggestions");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An error occurred while retrieving task scheduling suggestions",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Helper method to get current user ID from JWT claims
    /// </summary>
    /// <returns>User ID if found, null otherwise</returns>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>
/// Task action execution request
/// </summary>
public class TaskActionRequest
{
    /// <summary>
    /// Action ID to execute (MarkCompleted, MarkInProgress, MarkArchived, etc.)
    /// </summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters for the action
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Optional reason for the action
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Task status update request
/// </summary>
public class TaskStatusUpdateRequest
{
    /// <summary>
    /// New status for the task (0=Pending, 1=InProgress, 2=Completed, 3=Archived)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Optional reason for the status change
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Task category information
/// </summary>
public class TaskCategoryInfo
{
    /// <summary>
    /// Category ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Category name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category description
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Task status information
/// </summary>
public class TaskStatusInfo
{
    /// <summary>
    /// Status ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Status name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Status description
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Task batch operation request
/// </summary>
public class TaskBatchOperationRequest
{
    /// <summary>
    /// Operation to perform (delete, complete, archive)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Task IDs to operate on
    /// </summary>
    public IEnumerable<Guid> TaskIds { get; set; } = new List<Guid>();
}

/// <summary>
/// Batch status update request
/// </summary>
public class TaskBatchStatusUpdateRequest
{
    /// <summary>
    /// New status to set for all tasks (0=Pending, 1=InProgress, 2=Completed, 3=Archived)
    /// </summary>
    public int NewStatus { get; set; }

    /// <summary>
    /// Task IDs to update
    /// </summary>
    public IEnumerable<Guid> TaskIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Optional reason for the status change
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Task batch operation result
/// </summary>
public class TaskBatchOperationResult
{
    /// <summary>
    /// Operation that was performed
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Total number of tasks processed
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Number of tasks processed successfully
    /// </summary>
    public int SuccessfulTasks { get; set; }

    /// <summary>
    /// Number of tasks that failed to process
    /// </summary>
    public int FailedTasks { get; set; }

    /// <summary>
    /// Individual task results
    /// </summary>
    public IEnumerable<TaskBatchItemResult> Results { get; set; } = new List<TaskBatchItemResult>();
}

/// <summary>
/// Individual task batch operation result
/// </summary>
public class TaskBatchItemResult
{
    /// <summary>
    /// Task ID
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? Error { get; set; }
}