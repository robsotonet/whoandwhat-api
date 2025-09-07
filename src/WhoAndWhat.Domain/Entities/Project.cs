using WhoAndWhat.Domain.ValueObjects;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Entities;

public class Project : BaseEntity
{
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

    /// <summary>
    /// Checks if the project can be soft deleted
    /// Projects with active tasks cannot be deleted
    /// </summary>
    /// <returns>True if the project can be soft deleted</returns>
    public override bool CanSoftDelete()
    {
        if (!base.CanSoftDelete())
            return false;

        // Cannot delete project if it has active tasks
        var activeTasks = Tasks?.Where(t => !t.IsDeleted && 
            t.Status != (int)DomainTaskStatus.Completed && 
            t.Status != (int)DomainTaskStatus.Archived).ToList();
            
        return activeTasks?.Count == 0;
    }

    /// <summary>
    /// Soft deletes the project and all its tasks
    /// </summary>
    public override void SoftDelete()
    {
        if (!CanSoftDelete())
            return;

        base.SoftDelete();

        // Soft delete all associated tasks
        if (Tasks != null)
        {
            foreach (var task in Tasks.Where(t => !t.IsDeleted))
            {
                task.SoftDelete();
            }
        }
    }

    /// <summary>
    /// Restores the project and optionally its tasks
    /// </summary>
    /// <param name="restoreTasks">Whether to restore associated tasks</param>
    public void Restore(bool restoreTasks = false)
    {
        if (!CanRestore())
            return;

        base.Restore();

        if (restoreTasks && Tasks != null)
        {
            foreach (var task in Tasks.Where(t => t.IsDeleted))
            {
                task.Restore();
            }
        }
    }
}
