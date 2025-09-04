namespace WhoAndWhat.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? QRCode { get; set; }
    public string? InviteCode { get; set; }
    public int RelationshipType { get; set; } // Mapped from ContactRelationType value object

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
