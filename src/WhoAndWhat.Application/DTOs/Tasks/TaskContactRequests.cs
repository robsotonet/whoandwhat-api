using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Tasks;

/// <summary>
/// Request model for linking a contact to a task
/// </summary>
public class LinkContactToTaskRequest
{
    /// <summary>
    /// ID of the contact to link
    /// </summary>
    [Required]
    public Guid ContactId { get; set; }

    /// <summary>
    /// Role for the contact in the task (Owner, Collaborator, Reviewer, Observer)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the contact's role in this task
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for updating a contact's role in a task
/// </summary>
public class UpdateContactRoleRequest
{
    /// <summary>
    /// New role for the contact (Owner, Collaborator, Reviewer, Observer)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role change
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
