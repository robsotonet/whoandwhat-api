using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Tasks;

public class UpdateTaskRequest
{
    [StringLength(200, MinimumLength = 3)]
    public string? Title { get; set; }
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [Range(0, 4)]
    public int? Category { get; set; }
    
    [Range(0, 3)]
    public int? Status { get; set; }
    
    [Range(0, 3)]
    public int? Priority { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    public bool? ClearDueDate { get; set; }
    
    public TaskMetadataRequest? Metadata { get; set; }
    
    public List<Guid>? ContactIds { get; set; }
}

public class TaskActionRequest
{
    [Required]
    public string ActionId { get; set; } = string.Empty;
    
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class ConvertTaskRequest
{
    [Required]
    [Range(0, 4)]
    public int ToCategory { get; set; }
    
    [StringLength(500)]
    public string? Reason { get; set; }
    
    public bool CreateSubtasks { get; set; } = false;
}