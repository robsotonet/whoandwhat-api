using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Common;
using TaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Task entity with rich domain behavior and business rules
/// </summary>
public class Task
{
    /// <summary>
    /// Maximum allowed title length
    /// </summary>
    public const int MaxTitleLength = 200;
    
    /// <summary>
    /// Maximum allowed description length
    /// </summary>
    public const int MaxDescriptionLength = 5000;

    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; } // Mapped from Priority value object
    public int Category { get; set; } // Mapped from TaskCategory value object
    public int Status { get; set; } // Mapped from TaskStatus value object
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false; // Soft delete flag

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Task> Subtasks { get; set; } = new List<Task>();

    // Calculated Properties
    
    /// <summary>
    /// Gets whether the task is overdue (past due date and not completed)
    /// </summary>
    public bool IsOverdue
    {
        get
        {
            if (!DueDate.HasValue || Status == (int)TaskStatus.Completed || Status == (int)TaskStatus.Archived)
            {
                return false;
            }
            return DateTime.UtcNow > DueDate.Value;
        }
    }

    /// <summary>
    /// Gets the number of days until the due date (negative if overdue)
    /// </summary>
    public int? DaysUntilDue
    {
        get
        {
            if (!DueDate.HasValue)
            {
                return null;
            }
            return (int)(DueDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
        }
    }

    /// <summary>
    /// Gets the completion percentage based on completed subtasks
    /// </summary>
    public decimal CompletionPercentage
    {
        get
        {
            if (Status == (int)TaskStatus.Completed)
            {
                return 100m;
            }
            
            if (!Subtasks.Any())
            {
                return Status == (int)TaskStatus.InProgress ? 50m : 0m;
            }

            var completedSubtasks = Subtasks.Count(s => s.Status == (int)TaskStatus.Completed);
            return (decimal)completedSubtasks / Subtasks.Count * 100m;
        }
    }

    /// <summary>
    /// Gets whether the task has active (non-completed) subtasks
    /// </summary>
    public bool HasActiveSubtasks => Subtasks.Any(s => s.Status != (int)TaskStatus.Completed && s.Status != (int)TaskStatus.Archived);

    /// <summary>
    /// Gets whether the task is a standalone task (not part of a project)
    /// </summary>
    public bool IsStandalone => !ProjectId.HasValue;

    // Validation Methods

    /// <summary>
    /// Validates the task title
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    public ValidationResult ValidateTitle()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Title))
        {
            errors.Add("Title is required");
        }
        else if (Title.Length > MaxTitleLength)
        {
            errors.Add($"Title cannot exceed {MaxTitleLength} characters");
        }
        else if (Title.Trim() != Title)
        {
            errors.Add("Title cannot start or end with whitespace");
        }

        return new ValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    /// <summary>
    /// Validates the task description
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    public ValidationResult ValidateDescription()
    {
        var errors = new List<string>();

        if (!string.IsNullOrEmpty(Description) && Description.Length > MaxDescriptionLength)
        {
            errors.Add($"Description cannot exceed {MaxDescriptionLength} characters");
        }

        return new ValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    /// <summary>
    /// Validates the due date
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    public ValidationResult ValidateDueDate()
    {
        var errors = new List<string>();
        var category = (TaskCategory)Category;

        // Appointments must have a due date
        if (category == TaskCategory.Appointment && !DueDate.HasValue)
        {
            errors.Add("Appointments must have a due date");
        }

        // Bill reminders must have a due date
        if (category == TaskCategory.BillReminder && !DueDate.HasValue)
        {
            errors.Add("Bill reminders must have a due date");
        }

        // Due date cannot be in the past for new tasks
        if (DueDate.HasValue && CreatedAt == DateTime.MinValue && DueDate.Value < DateTime.UtcNow.Date)
        {
            errors.Add("Due date cannot be in the past");
        }

        return new ValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    /// <summary>
    /// Validates the entire task
    /// </summary>
    /// <returns>Validation result combining all validation checks</returns>
    public ValidationResult Validate()
    {
        var titleValidation = ValidateTitle();
        var descriptionValidation = ValidateDescription();
        var dueDateValidation = ValidateDueDate();

        var allErrors = titleValidation.Errors
            .Concat(descriptionValidation.Errors)
            .Concat(dueDateValidation.Errors)
            .ToList();

        return new ValidationResult { IsValid = !allErrors.Any(), Errors = allErrors };
    }

    // Business Methods

    /// <summary>
    /// Determines if the task can be marked as completed
    /// </summary>
    /// <returns>True if the task can be completed</returns>
    public bool CanBeCompleted()
    {
        var currentStatus = (TaskStatus)Status;
        
        // Cannot complete archived or deleted tasks
        if (currentStatus == TaskStatus.Archived || IsDeleted)
        {
            return false;
        }

        // Task is already completed
        if (currentStatus == TaskStatus.Completed)
        {
            return false;
        }

        // Cannot complete parent task if it has active subtasks (except for Ideas and Projects)
        var category = (TaskCategory)Category;
        if (HasActiveSubtasks && category != TaskCategory.Idea && category != TaskCategory.Project)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if the task can be archived
    /// </summary>
    /// <returns>True if the task can be archived</returns>
    public bool CanBeArchived()
    {
        var currentStatus = (TaskStatus)Status;
        
        // Can only archive completed tasks or very old pending tasks
        if (currentStatus != TaskStatus.Completed && 
            !(currentStatus == TaskStatus.Pending && CreatedAt < DateTime.UtcNow.AddMonths(-6)))
        {
            return false;
        }

        // Cannot archive if already archived or deleted
        if (currentStatus == TaskStatus.Archived || IsDeleted)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if the task can be converted to a project
    /// </summary>
    /// <returns>True if the task can be converted to a project</returns>
    public bool CanConvertToProject()
    {
        var currentStatus = (TaskStatus)Status;
        var currentCategory = (TaskCategory)Category;
        
        // Cannot convert completed, archived, or deleted tasks
        if (currentStatus == TaskStatus.Completed || currentStatus == TaskStatus.Archived || IsDeleted)
        {
            return false;
        }

        // Cannot convert if already a project
        if (currentCategory == TaskCategory.Project)
        {
            return false;
        }

        // Cannot convert if already part of a project
        if (ProjectId.HasValue)
        {
            return false;
        }

        // Only convert tasks that have subtasks or are complex enough
        return Subtasks.Any() || (Description?.Length ?? 0) > 100 || currentCategory == TaskCategory.Idea;
    }

    // State Transition Methods

    /// <summary>
    /// Marks the task as in progress
    /// </summary>
    /// <returns>True if the status was changed successfully</returns>
    public bool MarkInProgress()
    {
        var currentStatus = (TaskStatus)Status;
        
        if (currentStatus == TaskStatus.Pending || currentStatus == TaskStatus.InProgress)
        {
            Status = (int)TaskStatus.InProgress;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Marks the task as completed
    /// </summary>
    /// <returns>True if the status was changed successfully</returns>
    public bool MarkCompleted()
    {
        if (CanBeCompleted())
        {
            Status = (int)TaskStatus.Completed;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Marks the task as archived
    /// </summary>
    /// <returns>True if the status was changed successfully</returns>
    public bool MarkArchived()
    {
        if (CanBeArchived())
        {
            Status = (int)TaskStatus.Archived;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Soft deletes the task
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted task
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the task title with validation
    /// </summary>
    /// <param name="newTitle">The new title</param>
    /// <returns>True if updated successfully</returns>
    public bool UpdateTitle(string newTitle)
    {
        var originalTitle = Title;
        Title = newTitle?.Trim() ?? string.Empty;
        
        var validation = ValidateTitle();
        if (!validation.IsValid)
        {
            Title = originalTitle; // Rollback
            return false;
        }

        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Updates the task description with validation
    /// </summary>
    /// <param name="newDescription">The new description</param>
    /// <returns>True if updated successfully</returns>
    public bool UpdateDescription(string? newDescription)
    {
        var originalDescription = Description;
        Description = newDescription?.Trim();
        
        var validation = ValidateDescription();
        if (!validation.IsValid)
        {
            Description = originalDescription; // Rollback
            return false;
        }

        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Updates the due date with validation
    /// </summary>
    /// <param name="newDueDate">The new due date</param>
    /// <returns>True if updated successfully</returns>
    public bool UpdateDueDate(DateTime? newDueDate)
    {
        var originalDueDate = DueDate;
        DueDate = newDueDate;
        
        var validation = ValidateDueDate();
        if (!validation.IsValid)
        {
            DueDate = originalDueDate; // Rollback
            return false;
        }

        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}