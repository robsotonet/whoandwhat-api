using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Contacts;

public class ContactDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? QRCode { get; set; }

    public string? InviteCode { get; set; }

    public int RelationshipType { get; set; }

    public string RelationshipTypeName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int ActiveTaskCount { get; set; }

    public List<ContactTaskDto> AssociatedTasks { get; set; } = new();
}

public class ContactTaskDto
{
    public Guid TaskId { get; set; }

    public string TaskTitle { get; set; } = string.Empty;

    public int TaskStatus { get; set; }

    public string TaskStatusName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime LinkedAt { get; set; }

    public string? Notes { get; set; }
}
