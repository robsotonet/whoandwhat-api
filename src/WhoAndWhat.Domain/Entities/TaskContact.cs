using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Join entity representing the many-to-many relationship between AppTasks and Contacts
/// </summary>
public class TaskContact : BaseEntity
{
    public Guid TaskId { get; set; }
    public AppTask Task { get; set; } = null!;

    public Guid ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    /// <summary>
    /// Role of the contact in this task (e.g., "Participant", "Collaborator", "Reviewer", "Stakeholder")
    /// </summary>
    public string Role { get; set; } = "Participant";

    /// <summary>
    /// When the contact was linked to this task
    /// </summary>
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional notes about the contact's involvement in the task
    /// </summary>
    public string? Notes { get; set; }
}
