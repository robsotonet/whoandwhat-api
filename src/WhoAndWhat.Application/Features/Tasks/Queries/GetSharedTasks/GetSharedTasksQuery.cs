using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetSharedTasks;

/// <summary>
/// Query to get tasks shared with the user through contact relationships
/// </summary>
public record GetSharedTasksQuery(
    Guid UserId,
    string? Role = null,           // Filter by contact role (Owner, Collaborator, Reviewer, Observer)
    Guid? ContactId = null,        // Filter by specific contact
    int? Status = null,            // Filter by task status
    int? Category = null,          // Filter by task category
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null      // Search in task title and description
) : IRequest<Result<PagedResult<SharedTaskDto>>>;

/// <summary>
/// DTO representing a task shared with the user
/// </summary>
public class SharedTaskDto
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Contact relationship information
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactRole { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
    public string? ContactNotes { get; set; }
    
    // Task owner information
    public Guid TaskOwnerId { get; set; }
    public string TaskOwnerName { get; set; } = string.Empty;
    
    // Authorization flags based on role
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanComment { get; set; }
    public bool CanViewDetails { get; set; }
}