namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Data transfer object for archived task information
/// </summary>
public record ArchivedAppTaskDto
{
    /// <summary>
    /// Archive record ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Original task ID before archiving
    /// </summary>
    public Guid OriginalTaskId { get; init; }

    /// <summary>
    /// ID of the user who owned the task
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Task title
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Task description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Task category
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Task priority level
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Task status at time of archiving
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Original due date
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// When the task was originally created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the task was last updated
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// When the task was completed (if applicable)
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// When the task was archived
    /// </summary>
    public DateTime ArchivedAt { get; init; }

    /// <summary>
    /// Reason for archiving
    /// </summary>
    public string ArchiveReason { get; init; } = string.Empty;

    /// <summary>
    /// ID of user who initiated the archiving (if manual)
    /// </summary>
    public Guid? ArchivedByUserId { get; init; }

    /// <summary>
    /// Original project ID if task was part of a project
    /// </summary>
    public Guid? ProjectId { get; init; }

    /// <summary>
    /// Project name at time of archiving
    /// </summary>
    public string? ProjectName { get; init; }

    /// <summary>
    /// Parent task ID if this was a subtask
    /// </summary>
    public Guid? ParentTaskId { get; init; }

    /// <summary>
    /// Parent task title
    /// </summary>
    public string? ParentTaskTitle { get; init; }

    /// <summary>
    /// Number of subtasks this task had
    /// </summary>
    public int SubtaskCount { get; init; }

    /// <summary>
    /// Number of contacts linked to this task
    /// </summary>
    public int ContactCount { get; init; }

    /// <summary>
    /// Number of attachments this task had
    /// </summary>
    public int AttachmentCount { get; init; }

    /// <summary>
    /// Whether this archived task can be restored
    /// </summary>
    public bool CanBeRestored { get; init; }

    /// <summary>
    /// Whether this task was archived recently (within 30 days)
    /// </summary>
    public bool IsRecentlyArchived { get; init; }

    /// <summary>
    /// Creates DTO from ArchivedAppTask entity
    /// </summary>
    public static ArchivedAppTaskDto FromEntity(Domain.Entities.ArchivedAppTask entity)
    {
        return new ArchivedAppTaskDto
        {
            Id = entity.Id,
            OriginalTaskId = entity.OriginalAppTaskId,
            UserId = entity.UserId,
            Title = entity.Title,
            Description = entity.Description,
            Category = ((Domain.ValueObjects.AppTaskCategory)entity.Category).ToString(),
            Priority = ((Domain.ValueObjects.Priority)entity.Priority).ToString(),
            Status = ((Domain.ValueObjects.AppTaskStatus)entity.Status).ToString(),
            DueDate = entity.DueDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CompletedAt = entity.CompletedAt,
            ArchivedAt = entity.ArchivedAt,
            ArchiveReason = entity.ArchiveReason,
            ArchivedByUserId = entity.ArchivedByUserId,
            ProjectId = entity.ProjectId,
            ProjectName = entity.ProjectName,
            ParentTaskId = entity.ParentAppTaskId,
            ParentTaskTitle = entity.ParentAppTaskTitle,
            SubtaskCount = GetCountFromJson(entity.SubtasksJson),
            ContactCount = GetCountFromJson(entity.ContactsJson),
            AttachmentCount = GetCountFromJson(entity.AttachmentsJson),
            CanBeRestored = entity.CanBeRestored,
            IsRecentlyArchived = entity.IsRecentlyArchived
        };
    }

    private static int GetCountFromJson(string? jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            return 0;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(jsonData);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return document.RootElement.GetArrayLength();
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Filter criteria for querying archived tasks
/// </summary>
public record ArchivedAppTaskFilter
{
    /// <summary>
    /// Filter by task category
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Filter by task status
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Filter by priority
    /// </summary>
    public string? Priority { get; init; }

    /// <summary>
    /// Filter by archive reason
    /// </summary>
    public string? ArchiveReason { get; init; }

    /// <summary>
    /// Filter by archived date range (from)
    /// </summary>
    public DateTime? ArchivedAfter { get; init; }

    /// <summary>
    /// Filter by archived date range (to)
    /// </summary>
    public DateTime? ArchivedBefore { get; init; }

    /// <summary>
    /// Filter by original creation date range (from)
    /// </summary>
    public DateTime? CreatedAfter { get; init; }

    /// <summary>
    /// Filter by original creation date range (to)
    /// </summary>
    public DateTime? CreatedBefore { get; init; }

    /// <summary>
    /// Filter by project ID
    /// </summary>
    public Guid? ProjectId { get; init; }

    /// <summary>
    /// Search in title and description
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Only show tasks that can be restored
    /// </summary>
    public bool? OnlyRestorable { get; init; }

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Sort field
    /// </summary>
    public ArchivedAppTaskSortBy SortBy { get; init; } = ArchivedAppTaskSortBy.ArchivedAt;

    /// <summary>
    /// Sort direction
    /// </summary>
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;
}

/// <summary>
/// Available sort options for archived tasks
/// </summary>
public enum ArchivedAppTaskSortBy
{
    Title,
    CreatedAt,
    UpdatedAt,
    ArchivedAt,
    DueDate,
    Priority,
    Category
}

/// <summary>
/// Sort direction
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Archive statistics for dashboard and reporting
/// </summary>
public record ArchiveStatistics
{
    /// <summary>
    /// Total number of archived tasks
    /// </summary>
    public int TotalArchivedAppTasks { get; init; }

    /// <summary>
    /// Total number of archived projects
    /// </summary>
    public int TotalArchivedProjects { get; init; }

    /// <summary>
    /// Total storage space used by archives (in bytes)
    /// </summary>
    public long TotalStorageUsed { get; init; }

    /// <summary>
    /// Number of tasks archived in the last 30 days
    /// </summary>
    public int RecentlyArchivedAppTasks { get; init; }

    /// <summary>
    /// Breakdown by archive reason
    /// </summary>
    public Dictionary<string, int> ArchiveReasonBreakdown { get; init; } = new();

    /// <summary>
    /// Breakdown by task category
    /// </summary>
    public Dictionary<string, int> CategoryBreakdown { get; init; } = new();

    /// <summary>
    /// Breakdown by month for the last year
    /// </summary>
    public Dictionary<string, int> MonthlyArchiveCount { get; init; } = new();

    /// <summary>
    /// Date of the oldest archived item
    /// </summary>
    public DateTime? OldestArchivedDate { get; init; }

    /// <summary>
    /// Date of the most recent archive operation
    /// </summary>
    public DateTime? LatestArchivedDate { get; init; }

    /// <summary>
    /// Average tasks archived per operation
    /// </summary>
    public double AverageTasksPerOperation { get; init; }

    /// <summary>
    /// Estimated space savings from archiving
    /// </summary>
    public long EstimatedSpaceSavings { get; init; }
}
