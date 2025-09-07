namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Archived project entity that preserves project data when all associated tasks are completed and archived
/// Maintains historical record of project structure and metadata
/// </summary>
public class ArchivedProject
{
    public Guid Id { get; set; }
    
    // Original project data preservation
    public Guid OriginalProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; } // Mapped from a status enum/value object
    public int Progress { get; set; }
    
    // Original timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Archive-specific metadata
    public DateTime ArchivedAt { get; set; }
    public string ArchiveReason { get; set; } = null!; // "AllTasksCompleted", "Manual", "Expired", etc.
    public Guid? ArchivedByUserId { get; set; }
    
    // Project statistics at time of archiving
    public int TotalTasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public TimeSpan? TotalDuration { get; set; } // Time from start to completion
    
    // Serialized related data for complete preservation
    public string? TasksJson { get; set; } // JSON of all tasks that were in this project
    public string? ContactsJson { get; set; } // JSON of linked contacts
    public string? MetadataJson { get; set; } // JSON of additional project metadata
    
    // Navigation properties
    public User User { get; set; } = null!;
    
    // Archive validation
    public bool CanBeRestored => ArchivedAt > DateTime.UtcNow.AddDays(-90); // 90 day restore window
    
    public bool IsRecentlyArchived => ArchivedAt > DateTime.UtcNow.AddDays(-30);
}