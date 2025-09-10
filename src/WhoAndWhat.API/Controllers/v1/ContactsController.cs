using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;
using Asp.Versioning;
using WhoAndWhat.Application.Features.Contacts.Commands.CreateContact;
using WhoAndWhat.Application.Features.Contacts.Commands.UpdateContact;
using WhoAndWhat.Application.Features.Contacts.Commands.DeleteContact;
using WhoAndWhat.Application.Features.Contacts.Queries.GetContact;
using WhoAndWhat.Application.Features.Contacts.Queries.GetContacts;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Contact management controller handling core contact operations
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/contacts")]
[Tags("Contact Management")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ContactsController> _logger;

    /// <summary>
    /// Initializes a new instance of the Contacts controller
    /// </summary>
    /// <param name="mediator">MediatR mediator for command handling</param>
    /// <param name="logger">Logger for Contacts controller</param>
    public ContactsController(IMediator mediator, ILogger<ContactsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get user's contacts with filtering, sorting, and pagination
    /// </summary>
    /// <param name="search">Search term for contacts (name or email)</param>
    /// <param name="relationshipType">Filter by relationship type</param>
    /// <param name="includeDeleted">Include soft-deleted contacts</param>
    /// <param name="sortBy">Sort field (Name, Email, CreatedAt, UpdatedAt)</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of contacts</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ContactSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ContactSearchResult>> GetContacts(
        [FromQuery] string? search = null,
        [FromQuery] int? relationshipType = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] string sortBy = "Name",
        [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetContactsQuery(
                UserId: userId.Value,
                Search: search,
                RelationshipTypes: relationshipType.HasValue ? new List<int> { relationshipType.Value } : null,
                IncludeDeleted: includeDeleted,
                SortBy: sortBy,
                SortDescending: sortDescending,
                PageNumber: pageNumber,
                PageSize: pageSize
            );

            _logger.LogInformation("Getting contacts for user {UserId} with search '{Search}', relationshipType {RelationshipType}", 
                userId.Value, search, relationshipType);

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get contacts for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve contacts",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to retrieve contacts",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get a specific contact by ID
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="includeTasks">Include associated tasks in response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Contact details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> GetContact(
        Guid id,
        [FromQuery] bool includeTasks = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetContactQuery(id, userId.Value, includeTasks, false);

            _logger.LogInformation("Getting contact {ContactId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get contact {ContactId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error == "Contact not found")
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Contact not found",
                        Detail = $"Contact with ID {id} was not found",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve contact",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact {ContactId} for user", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to retrieve contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Create a new contact
    /// </summary>
    /// <param name="request">Contact creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created contact</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ContactDto>> CreateContact(
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new CreateContactCommand(
                Name: request.Name,
                Email: request.Email,
                Phone: request.Phone,
                RelationshipType: request.RelationshipType,
                UserId: userId.Value
            );

            _logger.LogInformation("Creating contact for user {UserId}: {Name}", userId.Value, request.Name);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create contact for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to create contact",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return CreatedAtAction(
                nameof(GetContact),
                new { id = result.Value.Id },
                result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to create contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update an existing contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="request">Contact update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated contact</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> UpdateContact(
        Guid id,
        [FromBody] UpdateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new UpdateContactCommand(
                ContactId: id,
                Name: request.Name,
                Email: request.Email,
                Phone: request.Phone,
                RelationshipType: request.RelationshipType,
                UserId: userId.Value
            );

            _logger.LogInformation("Updating contact {ContactId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to update contact {ContactId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error == "Contact not found")
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Contact not found",
                        Detail = $"Contact with ID {id} was not found",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to update contact",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} for user", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to update contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Soft delete a contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteContact(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var command = new DeleteContactCommand(id, userId.Value);

            _logger.LogInformation("Deleting contact {ContactId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to delete contact {ContactId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error == "Contact not found")
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Contact not found",
                        Detail = $"Contact with ID {id} was not found",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to delete contact",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact {ContactId} for user", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to delete contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get soft-deleted contacts for the user
    /// </summary>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of soft-deleted contacts</returns>
    [HttpGet("deleted")]
    [ProducesResponseType(typeof(ContactSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ContactSearchResult>> GetDeletedContacts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetContactsQuery(
                UserId: userId.Value,
                Search: null,
                RelationshipTypes: null,
                IncludeDeleted: true,
                SortBy: "DeletedAt",
                SortDescending: true,
                PageNumber: pageNumber,
                PageSize: pageSize
            );

            _logger.LogInformation("Getting deleted contacts for user {UserId}", userId.Value);

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get deleted contacts for user {UserId}: {Error}", userId.Value, result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve deleted contacts",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deleted contacts for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to retrieve deleted contacts",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get tasks associated with a specific contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="includeCompleted">Include completed tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Contact with associated tasks</returns>
    [HttpGet("{id:guid}/tasks")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> GetContactTasks(
        Guid id,
        [FromQuery] bool includeCompleted = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            var query = new GetContactQuery(id, userId.Value, true, false);

            _logger.LogInformation("Getting tasks for contact {ContactId} for user {UserId}", id, userId.Value);

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get tasks for contact {ContactId} for user {UserId}: {Error}", id, userId.Value, result.Error);
                
                if (result.Error == "Contact not found")
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Contact not found",
                        Detail = $"Contact with ID {id} was not found",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve contact tasks",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Filter out completed tasks if requested
            if (!includeCompleted && result.Value.AssociatedTasks != null)
            {
                result.Value.AssociatedTasks = result.Value.AssociatedTasks
                    .Where(task => task.TaskStatus != 2) // Assuming 2 is Completed status
                    .ToList();
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks for contact {ContactId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to retrieve contact tasks",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Restore a soft-deleted contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Restored contact</returns>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> RestoreContact(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            _logger.LogInformation("Attempting to restore contact {ContactId} for user {UserId}", id, userId.Value);

            // First, get the soft-deleted contact
            var getContactQuery = new GetContactQuery(id, userId.Value, false, true);
            var contactResult = await _mediator.Send(getContactQuery, cancellationToken);
            
            if (!contactResult.IsSuccess)
            {
                _logger.LogWarning("Failed to find soft-deleted contact {ContactId} for user {UserId}: {Error}", id, userId.Value, contactResult.Error);
                return NotFound(new ProblemDetails
                {
                    Title = "Contact not found",
                    Detail = $"Soft-deleted contact with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var contact = contactResult.Value;
            if (!contact.IsDeleted)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Contact is not deleted",
                    Detail = "Cannot restore a contact that is not soft-deleted",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Note: We would need a RestoreContactCommand for this operation
            // For now, we'll return a not implemented response
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Not Implemented",
                Detail = "Contact restoration will be implemented in a future version",
                Status = StatusCodes.Status501NotImplemented
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring contact {ContactId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to restore contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Permanently delete a soft-deleted contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> PermanentlyDeleteContact(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User identity not found");
            }

            _logger.LogInformation("Attempting to permanently delete contact {ContactId} for user {UserId}", id, userId.Value);

            // First, verify the contact exists and is soft-deleted
            var getContactQuery = new GetContactQuery(id, userId.Value, false, true);
            var contactResult = await _mediator.Send(getContactQuery, cancellationToken);
            
            if (!contactResult.IsSuccess)
            {
                _logger.LogWarning("Failed to find soft-deleted contact {ContactId} for user {UserId}: {Error}", id, userId.Value, contactResult.Error);
                return NotFound(new ProblemDetails
                {
                    Title = "Contact not found",
                    Detail = $"Soft-deleted contact with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var contact = contactResult.Value;
            if (!contact.IsDeleted)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Contact is not deleted",
                    Detail = "Cannot permanently delete a contact that is not soft-deleted",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Note: We would need a PermanentlyDeleteContactCommand for this operation
            // For now, we'll return a not implemented response
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Not Implemented",
                Detail = "Permanent contact deletion will be implemented in a future version",
                Status = StatusCodes.Status501NotImplemented
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error permanently deleting contact {ContactId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Unable to permanently delete contact",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Gets the current user ID from JWT claims
    /// </summary>
    /// <returns>User ID if found, null otherwise</returns>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}