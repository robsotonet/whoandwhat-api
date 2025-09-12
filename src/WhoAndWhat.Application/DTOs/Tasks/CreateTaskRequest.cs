using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Tasks;

public class CreateTaskRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Range(0, 4)] // TaskCategory enum values
    public int Category { get; set; }

    [Range(0, 3)] // Priority enum values
    public int Priority { get; set; } = 1; // Medium by default

    public DateTime? DueDate { get; set; }

    public Guid? ParentTaskId { get; set; }

    public List<Guid> ContactIds { get; set; } = new();

    public TaskMetadataRequest? Metadata { get; set; }
}

public class TaskMetadataRequest
{
    [StringLength(500)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? PaymentMethod { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? RecurrencePattern { get; set; }

    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}
