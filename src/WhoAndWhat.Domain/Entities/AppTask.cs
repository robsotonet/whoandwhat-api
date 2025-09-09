using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// AppTask entity with rich domain behavior and business rules
/// </summary>
public class AppTask : BaseEntity
{
    /// <summary>
    /// Maximum allowed title length
    /// </summary>
    public const int MaxTitleLength = 200;

    /// <summary>
    /// Maximum allowed description length
    /// </summary>
    public const int MaxDescriptionLength = 5000;

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; } // Mapped from Priority value object
    public int Category { get; set; } // Mapped from AppTaskCategory value object
    public int Status { get; set; } // Mapped from AppTaskStatus value object

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? ParentTaskId { get; set; }
    public AppTask? ParentTask { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<TaskContact> TaskContacts { get; set; } = new List<TaskContact>();
    public ICollection<AppTask> Subtasks { get; set; } = new List<AppTask>();

    // Calculated Properties

    /// <summary>
    /// Gets whether the task is overdue (past due date and not completed)
    /// </summary>
    public bool IsOverdue
    {
        get
        {
            if (!DueDate.HasValue || Status == (int)AppTaskStatus.Completed || Status == (int)AppTaskStatus.Archived)
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
            if (Status == (int)AppTaskStatus.Completed)
            {
                return 100m;
            }

            if (!Subtasks.Any())
            {
                return Status == (int)AppTaskStatus.InProgress ? 50m : 0m;
            }

            var completedSubtasks = Subtasks.Count(s => s.Status == (int)AppTaskStatus.Completed);
            return (decimal)completedSubtasks / Subtasks.Count * 100m;
        }
    }

    /// <summary>
    /// Gets whether the task has active (non-completed) subtasks
    /// </summary>
    public bool HasActiveSubtasks => Subtasks.Any(s => s.Status != (int)AppTaskStatus.Completed && s.Status != (int)AppTaskStatus.Archived);

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
        var category = (AppTaskCategory)Category;

        // Appointments must have a due date
        if (category == AppTaskCategory.Appointment && !DueDate.HasValue)
        {
            errors.Add("Appointments must have a due date");
        }

        // Bill reminders must have a due date
        if (category == AppTaskCategory.BillReminder && !DueDate.HasValue)
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
        var currentStatus = (AppTaskStatus)Status;

        // Cannot complete archived or deleted tasks
        if (currentStatus == AppTaskStatus.Archived || IsDeleted)
        {
            return false;
        }

        // Task is already completed
        if (currentStatus == AppTaskStatus.Completed)
        {
            return false;
        }

        // Cannot complete parent task if it has active subtasks (except for Ideas and Projects)
        var category = (AppTaskCategory)Category;
        if (HasActiveSubtasks && category != AppTaskCategory.Idea && category != AppTaskCategory.Project)
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
        var currentStatus = (AppTaskStatus)Status;

        // Can only archive completed tasks or very old pending tasks
        if (currentStatus != AppTaskStatus.Completed &&
            !(currentStatus == AppTaskStatus.Pending && CreatedAt < DateTime.UtcNow.AddMonths(-6)))
        {
            return false;
        }

        // Cannot archive if already archived or deleted
        if (currentStatus == AppTaskStatus.Archived || IsDeleted)
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
        var currentStatus = (AppTaskStatus)Status;
        var currentCategory = (AppTaskCategory)Category;

        // Cannot convert completed, archived, or deleted tasks
        if (currentStatus == AppTaskStatus.Completed || currentStatus == AppTaskStatus.Archived || IsDeleted)
        {
            return false;
        }

        // Cannot convert if already a project
        if (currentCategory == AppTaskCategory.Project)
        {
            return false;
        }

        // Cannot convert if already part of a project
        if (ProjectId.HasValue)
        {
            return false;
        }

        // Only convert tasks that have subtasks or are complex enough
        return Subtasks.Any() || (Description?.Length ?? 0) > 100 || currentCategory == AppTaskCategory.Idea;
    }

    // State Transition Methods

    /// <summary>
    /// Marks the task as in progress
    /// </summary>
    /// <returns>True if the status was changed successfully</returns>
    public bool MarkInProgress()
    {
        var currentStatus = (AppTaskStatus)Status;

        if (currentStatus == AppTaskStatus.Pending || currentStatus == AppTaskStatus.InProgress)
        {
            Status = (int)AppTaskStatus.InProgress;
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
            Status = (int)AppTaskStatus.Completed;
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
            Status = (int)AppTaskStatus.Archived;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the task can be soft deleted
    /// Completed and archived tasks can be deleted
    /// </summary>
    /// <returns>True if the task can be soft deleted</returns>
    public override bool CanSoftDelete()
    {
        if (!base.CanSoftDelete())
        {
            return false;
        }

        var currentStatus = (AppTaskStatus)Status;

        // Any task can be soft deleted regardless of status
        // Business rules may restrict this in higher layers
        return true;
    }

    /// <summary>
    /// Soft deletes the task and all its subtasks
    /// </summary>
    public override void SoftDelete()
    {
        if (!CanSoftDelete())
        {
            return;
        }

        base.SoftDelete();

        // Soft delete all subtasks recursively
        if (Subtasks != null)
        {
            foreach (var subtask in Subtasks.Where(st => !st.IsDeleted))
            {
                subtask.SoftDelete();
            }
        }
    }

    /// <summary>
    /// Restores the task and optionally its subtasks
    /// </summary>
    /// <param name="restoreSubtasks">Whether to restore subtasks</param>
    public void Restore(bool restoreSubtasks = false)
    {
        if (!CanRestore())
        {
            return;
        }

        base.Restore();

        if (restoreSubtasks && Subtasks != null)
        {
            foreach (var subtask in Subtasks.Where(st => st.IsDeleted))
            {
                subtask.Restore(restoreSubtasks);
            }
        }
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
