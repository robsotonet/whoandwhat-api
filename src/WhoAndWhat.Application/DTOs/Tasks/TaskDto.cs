using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Tasks;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ParentTaskId { get; set; }
    public List<TaskDto> Subtasks { get; set; } = new();
    public List<TaskContactDto> TaskContacts { get; set; } = new();
    public TaskMetadataDto? Metadata { get; set; }
}

public class TaskMetadataDto
{
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? Amount { get; set; }
    public string? RecurrencePattern { get; set; }
    public DateTime? NextRecurrence { get; set; }
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

public class TaskContactDto
{
    public Guid TaskId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}