namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Archived AppTask entity that preserves all original AppTask data for historical records
/// AppTasks are archived when completed for extended periods to optimize active dataset performance
/// </summary>
public class ArchivedAppTask
{
    public Guid Id { get; set; }
    
    // Original task data preservation
    public Guid OriginalAppTaskId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; } // Mapped from Priority value object
    public int Category { get; set; } // Mapped from TaskCategory value object
    public int Status { get; set; } // Mapped from TaskStatus value object
    
    // Original timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Archive-specific metadata
    public DateTime ArchivedAt { get; set; }
    public string ArchiveReason { get; set; } = null!; // "Completed", "Manual", "Expired", etc.
    public Guid? ArchivedByUserId { get; set; } // For manual archiving tracking
    
    // Preserved relationship data (denormalized for performance)
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ParentAppTaskId { get; set; }
    public string? ParentAppTaskTitle { get; set; }
    
    // Serialized related data for complete preservation
    public string? SubtasksJson { get; set; } // JSON of subtasks that were archived together
    public string? ContactsJson { get; set; } // JSON of linked contacts
    public string? AttachmentsJson { get; set; } // JSON of attachments metadata
    
    // Navigation properties for archived user (soft reference)
    public User User { get; set; } = null!;
    
    // Archive validation and business rules
    public bool CanBeRestored => 
        Status == (int)ValueObjects.AppTaskStatus.Completed || 
        Status == (int)ValueObjects.AppTaskStatus.Archived;
    
    public bool IsRecentlyArchived => 
        ArchivedAt > DateTime.UtcNow.AddDays(-30);
}