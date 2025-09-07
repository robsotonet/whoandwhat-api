using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Repositories;

/// <summary>
/// Repository interface for Contact entities with soft delete support
/// </summary>
public interface IContactRepository
{
    /// <summary>
    /// Gets a contact by ID including soft deleted contacts
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The contact including if soft deleted, or null if not found</returns>
    Task<Contact?> GetContactIncludingDeletedAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all soft deleted contacts for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of soft deleted contacts</returns>
    Task<IEnumerable<Contact>> GetDeletedContactsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a contact with all its task associations (including soft deleted if specified)
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="includeDeletedTasks">Whether to include soft deleted tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The contact with tasks, or null if not found</returns>
    Task<Contact?> GetContactWithTasksAsync(Guid contactId, Guid userId, bool includeDeletedTasks = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a contact and removes it from all task associations
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the contact was soft deleted, false if not found or already deleted</returns>
    Task<bool> SoftDeleteContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft deleted contact
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the contact was restored, false if not found or not deleted</returns>
    Task<bool> RestoreContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a soft deleted contact
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the contact was permanently deleted, false if not found or not soft deleted</returns>
    Task<bool> PermanentlyDeleteContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets contacts that can be safely deleted (no active task associations)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of contacts that can be safely deleted</returns>
    Task<IEnumerable<Contact>> GetContactsSafeToDeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active task associations for a contact
    /// </summary>
    /// <param name="contactId">The contact ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of active tasks associated with the contact</returns>
    Task<int> CountActiveTaskAssociationsAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds contacts by email or name (including soft deleted if specified)
    /// </summary>
    /// <param name="query">Email or name search term</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="includeDeleted">Whether to include soft deleted contacts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching contacts</returns>
    Task<IEnumerable<Contact>> FindContactsAsync(string query, Guid userId, bool includeDeleted = false, CancellationToken cancellationToken = default);
}