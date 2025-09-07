using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for managing soft delete operations across entities
/// </summary>
public class SoftDeleteService
{
    /// <summary>
    /// Attempts to soft delete a task with validation and cascading behavior
    /// </summary>
    /// <param name="task">The task to delete</param>
    /// <param name="deleteSubtasks">Whether to cascade delete to subtasks</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult SoftDeleteTask(DomainTask task, bool deleteSubtasks = true)
    {
        if (task == null)
            return SoftDeleteResult.Failed("Task not found");

        if (!task.CanSoftDelete())
            return SoftDeleteResult.Failed("Task cannot be deleted in its current state");

        // Validate business rules
        var validationResult = ValidateTaskDeletion(task);
        if (!validationResult.IsSuccess)
            return validationResult;

        try
        {
            if (deleteSubtasks)
            {
                task.SoftDelete(); // This will cascade to subtasks
            }
            else
            {
                // Delete only the task without subtasks
                task.SoftDelete();
            }

            return SoftDeleteResult.Success($"Task '{task.Title}' and its dependencies have been deleted");
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to delete task: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to soft delete a project with validation and cascading behavior
    /// </summary>
    /// <param name="project">The project to delete</param>
    /// <param name="deleteTasks">Whether to cascade delete to tasks</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult SoftDeleteProject(Project project, bool deleteTasks = true)
    {
        if (project == null)
            return SoftDeleteResult.Failed("Project not found");

        if (!project.CanSoftDelete())
            return SoftDeleteResult.Failed("Project cannot be deleted - it contains active tasks");

        try
        {
            if (deleteTasks)
            {
                project.SoftDelete(); // This will cascade to tasks
            }
            else
            {
                // Check if there are any active tasks that would prevent deletion
                var activeTasks = project.Tasks?.Where(t => !t.IsDeleted &&
                    t.Status != (int)DomainTaskStatus.Completed &&
                    t.Status != (int)DomainTaskStatus.Archived).ToList();

                if (activeTasks?.Count > 0)
                {
                    return SoftDeleteResult.Failed($"Cannot delete project with {activeTasks.Count} active tasks. Complete or archive tasks first.");
                }

                project.SoftDelete();
            }

            return SoftDeleteResult.Success($"Project '{project.Name}' has been deleted");
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to delete project: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to soft delete a contact with validation
    /// </summary>
    /// <param name="contact">The contact to delete</param>
    /// <param name="removeFromTasks">Whether to remove contact from associated tasks</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult SoftDeleteContact(Contact contact, bool removeFromTasks = true)
    {
        if (contact == null)
            return SoftDeleteResult.Failed("Contact not found");

        if (!contact.CanSoftDelete())
        {
            var activeTaskCount = contact.Tasks?.Count(t => !t.IsDeleted) ?? 0;
            return SoftDeleteResult.Failed($"Contact cannot be deleted - it is associated with {activeTaskCount} active tasks");
        }

        try
        {
            if (removeFromTasks)
            {
                contact.SoftDelete(); // This will remove from task associations
            }
            else
            {
                // Simple delete without removing associations (may cause referential issues)
                if (contact.HasActiveTaskAssociations())
                {
                    return SoftDeleteResult.Failed("Contact has active task associations. Use removeFromTasks=true to handle associations.");
                }
                
                contact.SoftDelete();
            }

            return SoftDeleteResult.Success($"Contact '{contact.Name}' has been deleted");
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to delete contact: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores a soft deleted task with validation
    /// </summary>
    /// <param name="task">The task to restore</param>
    /// <param name="restoreSubtasks">Whether to restore subtasks</param>
    /// <param name="restoreProject">Whether to restore the parent project if deleted</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult RestoreTask(DomainTask task, bool restoreSubtasks = false, bool restoreProject = false)
    {
        if (task == null)
            return SoftDeleteResult.Failed("Task not found");

        if (!task.CanRestore())
            return SoftDeleteResult.Failed("Task is not deleted or cannot be restored");

        try
        {
            // Restore parent project first if requested and if it's deleted
            if (restoreProject && task.Project != null && task.Project.IsDeleted)
            {
                task.Project.Restore(false); // Don't restore all project tasks, just the project
            }

            task.Restore(restoreSubtasks);

            var message = $"Task '{task.Title}' has been restored";
            if (restoreSubtasks)
                message += " with its subtasks";
            if (restoreProject && task.Project != null)
                message += $" and its parent project '{task.Project.Name}'";

            return SoftDeleteResult.Success(message);
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to restore task: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores a soft deleted project with validation
    /// </summary>
    /// <param name="project">The project to restore</param>
    /// <param name="restoreTasks">Whether to restore associated tasks</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult RestoreProject(Project project, bool restoreTasks = false)
    {
        if (project == null)
            return SoftDeleteResult.Failed("Project not found");

        if (!project.CanRestore())
            return SoftDeleteResult.Failed("Project is not deleted or cannot be restored");

        try
        {
            project.Restore(restoreTasks);

            var message = $"Project '{project.Name}' has been restored";
            if (restoreTasks)
            {
                var restoredTaskCount = project.Tasks?.Count(t => !t.IsDeleted) ?? 0;
                message += $" with {restoredTaskCount} tasks";
            }

            return SoftDeleteResult.Success(message);
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to restore project: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores a soft deleted contact
    /// </summary>
    /// <param name="contact">The contact to restore</param>
    /// <returns>Result indicating success and any validation messages</returns>
    public SoftDeleteResult RestoreContact(Contact contact)
    {
        if (contact == null)
            return SoftDeleteResult.Failed("Contact not found");

        if (!contact.CanRestore())
            return SoftDeleteResult.Failed("Contact is not deleted or cannot be restored");

        try
        {
            contact.Restore();
            return SoftDeleteResult.Success($"Contact '{contact.Name}' has been restored");
        }
        catch (Exception ex)
        {
            return SoftDeleteResult.Failed($"Failed to restore contact: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates business rules for task deletion
    /// </summary>
    private SoftDeleteResult ValidateTaskDeletion(DomainTask task)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Business rule validations
        var currentStatus = (DomainTaskStatus)task.Status;

        // Warning for deleting incomplete tasks
        if (currentStatus == DomainTaskStatus.InProgress)
        {
            warnings.Add("Deleting a task that is currently in progress");
        }

        // Warning for deleting tasks with future due dates
        if (task.DueDate.HasValue && task.DueDate.Value > DateTime.UtcNow)
        {
            warnings.Add($"Deleting a task with future due date: {task.DueDate.Value:yyyy-MM-dd}");
        }

        // Warning for deleting tasks with subtasks
        if (task.Subtasks?.Any(st => !st.IsDeleted) == true)
        {
            var activeSubtaskCount = task.Subtasks.Count(st => !st.IsDeleted);
            warnings.Add($"Deleting a task with {activeSubtaskCount} active subtasks");
        }

        // Warning for deleting high priority tasks
        if (task.Priority == (int)Priority.High)
        {
            warnings.Add("Deleting a high priority task");
        }

        if (errors.Count > 0)
        {
            return SoftDeleteResult.Failed(string.Join("; ", errors));
        }

        var result = SoftDeleteResult.Success("Validation passed");
        result.Warnings = warnings;
        return result;
    }
}

/// <summary>
/// Result of a soft delete operation
/// </summary>
public class SoftDeleteResult
{
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public List<string> Warnings { get; set; } = new();
    public Exception? Exception { get; private set; }

    private SoftDeleteResult(bool isSuccess, string message, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        Exception = exception;
    }

    public static SoftDeleteResult Success(string message)
    {
        return new SoftDeleteResult(true, message);
    }

    public static SoftDeleteResult Failed(string message, Exception? exception = null)
    {
        return new SoftDeleteResult(false, message, exception);
    }

    /// <summary>
    /// Gets all messages including warnings
    /// </summary>
    public string GetFullMessage()
    {
        var fullMessage = Message;
        if (Warnings.Count > 0)
        {
            fullMessage += $" (Warnings: {string.Join(", ", Warnings)})";
        }
        return fullMessage;
    }
}