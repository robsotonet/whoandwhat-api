namespace WhoAndWhat.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; } // Mapped from a status enum/value object
    public int Progress { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
