namespace WhoAndWhat.Domain.Entities;

public class Contact : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? QRCode { get; set; }
    public string? InviteCode { get; set; }
    public int RelationshipType { get; set; } // Mapped from ContactRelationType value object

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<AppTask> Tasks { get; set; } = new List<AppTask>();
    public ICollection<TaskContact> TaskContacts { get; set; } = new List<TaskContact>();

    /// <summary>
    /// Checks if the contact can be soft deleted
    /// Contacts linked to active tasks cannot be deleted
    /// </summary>
    /// <returns>True if the contact can be soft deleted</returns>
    public override bool CanSoftDelete()
    {
        if (!base.CanSoftDelete())
        {
            return false;
        }

        // Cannot delete contact if it has active task associations
        var activeTasks = Tasks?.Where(t => !t.IsDeleted).ToList();
        return activeTasks?.Count == 0;
    }

    /// <summary>
    /// Soft deletes the contact and removes it from all task associations
    /// </summary>
    public override void SoftDelete()
    {
        if (!CanSoftDelete())
        {
            return;
        }

        base.SoftDelete();

        // Remove this contact from all task associations
        if (Tasks != null)
        {
            var taskList = Tasks.ToList(); // Create copy to avoid collection modification issues
            foreach (var task in taskList)
            {
                if (!task.IsDeleted)
                {
                    task.Contacts.Remove(this);
                    task.MarkAsModified();
                }
            }
        }
    }

    /// <summary>
    /// Checks if this contact is associated with any active tasks
    /// </summary>
    /// <returns>True if the contact has active task associations</returns>
    public bool HasActiveTaskAssociations()
    {
        return Tasks?.Any(t => !t.IsDeleted) == true;
    }
}
