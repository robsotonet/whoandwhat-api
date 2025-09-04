namespace WhoAndWhat.Domain.Entities;

public class Task
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; } // Mapped from Priority value object
    public int Category { get; set; } // Mapped from TaskCategory value object
    public int Status { get; set; } // Mapped from TaskStatus value object
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Task> Subtasks { get; set; } = new List<Task>();
}
