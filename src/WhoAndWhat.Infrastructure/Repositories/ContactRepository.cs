using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;

namespace WhoAndWhat.Infrastructure.Repositories;

/// <summary>
/// Implementation of IContactRepository with comprehensive contact management capabilities
/// </summary>
public class ContactRepository : Repository<Contact>, IContactRepository
{
    private readonly ILogger<ContactRepository> _logger;

    public ContactRepository(ApplicationDbContext context, ILogger<ContactRepository> logger)
        : base(context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Contact?> GetContactIncludingDeletedAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Contacts
                .IgnoreQueryFilters() // Include soft deleted
                .FirstOrDefaultAsync(c => c.Id == contactId && c.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contact {ContactId} (including deleted) for user {UserId}", contactId, userId);
            return null;
        }
    }

    public async Task<IEnumerable<Contact>> GetDeletedContactsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Contacts
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId && c.IsDeleted)
                .OrderBy(c => c.DeletedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deleted contacts for user {UserId}", userId);
            return Enumerable.Empty<Contact>();
        }
    }

    public async Task<Contact?> GetContactWithTasksAsync(Guid contactId, Guid userId, bool includeDeletedTasks = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet.Where(c => c.Id == contactId && c.UserId == userId);

            if (includeDeletedTasks)
            {
                query = query.Include(c => c.Tasks.Where(t => !t.IsDeleted || t.IsDeleted));
            }
            else
            {
                query = query.Include(c => c.Tasks.Where(t => !t.IsDeleted));
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contact {ContactId} with tasks for user {UserId}", contactId, userId);
            return null;
        }
    }

    public async Task<bool> SoftDeleteContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var contact = await GetContactWithTasksAsync(contactId, userId, false, cancellationToken);
            if (contact == null || contact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} not found or already deleted for user {UserId}", contactId, userId);
                return false;
            }

            if (!contact.CanSoftDelete())
            {
                _logger.LogWarning("Contact {ContactId} cannot be soft deleted due to active task associations for user {UserId}", contactId, userId);
                return false;
            }

            contact.SoftDelete();
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} soft deleted for user {UserId}", contactId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting contact {ContactId} for user {UserId}", contactId, userId);
            return false;
        }
    }

    public async Task<bool> RestoreContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var contact = await GetContactIncludingDeletedAsync(contactId, userId, cancellationToken);
            if (contact == null || !contact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} not found or not deleted for user {UserId}", contactId, userId);
                return false;
            }

            contact.Restore();
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} restored for user {UserId}", contactId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring contact {ContactId} for user {UserId}", contactId, userId);
            return false;
        }
    }

    public async Task<bool> PermanentlyDeleteContactAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var contact = await GetContactIncludingDeletedAsync(contactId, userId, cancellationToken);
            if (contact == null || !contact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} not found or not soft deleted for user {UserId}", contactId, userId);
                return false;
            }

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} permanently deleted for user {UserId}", contactId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error permanently deleting contact {ContactId} for user {UserId}", contactId, userId);
            return false;
        }
    }

    public async Task<IEnumerable<Contact>> GetContactsSafeToDeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Include(c => c.Tasks.Where(t => !t.IsDeleted))
                .Where(c => c.UserId == userId)
                .Where(c => !c.Tasks.Any(t => !t.IsDeleted))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contacts safe to delete for user {UserId}", userId);
            return Enumerable.Empty<Contact>();
        }
    }

    public async Task<int> CountActiveTaskAssociationsAsync(Guid contactId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(c => c.Id == contactId && c.UserId == userId)
                .SelectMany(c => c.Tasks.Where(t => !t.IsDeleted))
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting active task associations for contact {ContactId} and user {UserId}", contactId, userId);
            return 0;
        }
    }

    public async Task<IEnumerable<Contact>> FindContactsAsync(string query, Guid userId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<Contact>();
            }

            var queryable = includeDeleted
                ? _context.Contacts.IgnoreQueryFilters()
                : _dbSet.AsQueryable();

            var searchTerm = query.Trim().ToLower();

            return await queryable
                .Where(c => c.UserId == userId)
                .Where(c => includeDeleted || !c.IsDeleted)
                .Where(c =>
                    c.Name.ToLower().Contains(searchTerm) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchTerm)) ||
                    (c.Phone != null && c.Phone.Contains(searchTerm)))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching contacts with query '{Query}' for user {UserId}", query, userId);
            return Enumerable.Empty<Contact>();
        }
    }

    public async Task<Contact?> FindContactByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                return null;
            }

            return await _dbSet
                .FirstOrDefaultAsync(c => c.InviteCode == inviteCode && !c.IsDeleted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding contact by invite code '{InviteCode}'", inviteCode);
            return null;
        }
    }
}
