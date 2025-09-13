using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Calendar.Commands.ResolveConflict;

public class ResolveCalendarConflictCommandHandler : IRequestHandler<ResolveCalendarConflictCommand, Result<ConflictResolutionResult>>
{
    private readonly ICalendarConflictDetector _conflictDetector;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ILogger<ResolveCalendarConflictCommandHandler> _logger;

    public ResolveCalendarConflictCommandHandler(
        ICalendarConflictDetector conflictDetector,
        ICalendarSyncService calendarSyncService,
        ILogger<ResolveCalendarConflictCommandHandler> logger)
    {
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ConflictResolutionResult>> Handle(ResolveCalendarConflictCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Resolving calendar conflict {ConflictId} for user {UserId} with action {Action}",
                request.ConflictId, request.UserId, request.Resolution.Action);

            // First, validate that the conflict exists and is not already resolved
            var isAlreadyResolved = await _conflictDetector.IsConflictResolvedAsync(
                request.ConflictId, 
                cancellationToken);

            if (isAlreadyResolved)
            {
                return Result<ConflictResolutionResult>.Failure(
                    $"Conflict {request.ConflictId} not found or does not belong to user {request.UserId}");
            }

            // Check if the conflict has already been resolved
            if (!conflict.RequiresUserAction)
            {
                return Result<ConflictResolutionResult>.Failure(
                    $"Conflict {request.ConflictId} has already been resolved");
            }

            // Validate the resolution action is appropriate for this conflict type
            var validationResult = ValidateResolutionAction(conflict, request.Resolution);
            if (!validationResult.IsValid)
            {
                return Result<ConflictResolutionResult>.Failure(validationResult.ErrorMessage);
            }

            // Execute the resolution
            var resolutionResult = await ExecuteResolution(conflict, request.Resolution, cancellationToken);

            if (resolutionResult.Success)
            {
                _logger.LogInformation("Successfully resolved conflict {ConflictId} with action {Action}",
                    request.ConflictId, request.Resolution.Action);
            }
            else
            {
                _logger.LogWarning("Failed to resolve conflict {ConflictId}: {Error}",
                    request.ConflictId, resolutionResult.ErrorMessage);
            }

            return Result<ConflictResolutionResult>.Success(resolutionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving calendar conflict {ConflictId} for user {UserId}",
                request.ConflictId, request.UserId);
            return Result<ConflictResolutionResult>.Failure(
                "An error occurred while resolving the calendar conflict");
        }
    }

    private static (bool IsValid, string ErrorMessage) ValidateResolutionAction(
        CalendarSyncConflict conflict,
        ConflictResolution resolution)
    {
        // Basic validation
        if (resolution == null)
        {
            return (false, "Resolution cannot be null");
        }

        if (resolution.ConflictId != conflict.ConflictId)
        {
            return (false, "Resolution conflict ID does not match the conflict being resolved");
        }

        // Action-specific validation
        switch (resolution.Action)
        {
            case ConflictResolutionAction.KeepInternal:
                // Always valid - keeps the internal version
                break;

            case ConflictResolutionAction.KeepExternal:
                // Valid only if there's an external event
                if (conflict.ExternalEvent == null)
                {
                    return (false, "Cannot keep external version - no external event available");
                }
                break;

            case ConflictResolutionAction.Merge:
                // Valid only for data consistency conflicts with both events
                if (conflict.Type != ConflictType.DataInconsistency ||
                    conflict.InternalEvent == null ||
                    conflict.ExternalEvent == null)
                {
                    return (false, "Merge action is only valid for data consistency conflicts with both internal and external events");
                }
                break;

            case ConflictResolutionAction.CreateBoth:
                // Valid for duplicate event conflicts
                if (conflict.Type != ConflictType.DuplicateEvent)
                {
                    return (false, "Create both action is only valid for duplicate event conflicts");
                }
                break;

            case ConflictResolutionAction.Skip:
                // Always valid - ignores the conflict
                break;

            case ConflictResolutionAction.UserDecision:
                // Requires additional data in the resolution
                if (resolution.ResolutionData == null || !resolution.ResolutionData.Any())
                {
                    return (false, "User decision action requires additional resolution data");
                }
                break;

            default:
                return (false, $"Unknown resolution action: {resolution.Action}");
        }

        return (true, string.Empty);
    }

    private async Task<ConflictResolutionResult> ExecuteResolution(
        CalendarSyncConflict conflict,
        ConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        var affectedEvents = new List<EventSyncResult>();

        try
        {
            switch (resolution.Action)
            {
                case ConflictResolutionAction.KeepInternal:
                    affectedEvents = await ExecuteKeepInternal(conflict, cancellationToken);
                    break;

                case ConflictResolutionAction.KeepExternal:
                    affectedEvents = await ExecuteKeepExternal(conflict, cancellationToken);
                    break;

                case ConflictResolutionAction.Merge:
                    affectedEvents = await ExecuteMerge(conflict, resolution, cancellationToken);
                    break;

                case ConflictResolutionAction.CreateBoth:
                    affectedEvents = await ExecuteCreateBoth(conflict, cancellationToken);
                    break;

                case ConflictResolutionAction.Skip:
                    // No action needed - just mark as resolved
                    break;

                case ConflictResolutionAction.UserDecision:
                    affectedEvents = await ExecuteUserDecision(conflict, resolution, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported resolution action: {resolution.Action}");
            }

            // Mark the conflict as resolved
            await MarkConflictAsResolved(conflict.ConflictId, resolution, cancellationToken);

            return new ConflictResolutionResult(
                conflict.ConflictId,
                true, // Success
                resolution.Action,
                null, // ErrorMessage
                affectedEvents,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing resolution for conflict {ConflictId}", conflict.ConflictId);
            
            return new ConflictResolutionResult(
                conflict.ConflictId,
                false, // Success
                resolution.Action,
                ex.Message,
                affectedEvents,
                DateTime.UtcNow
            );
        }
    }

    private async Task<List<EventSyncResult>> ExecuteKeepInternal(CalendarSyncConflict conflict, CancellationToken cancellationToken)
    {
        var results = new List<EventSyncResult>();

        if (conflict.InternalEvent != null)
        {
            // Keep the internal event - might need to update external calendar
            results.Add(new EventSyncResult(
                conflict.InternalEvent.Id,
                conflict.ExternalEvent?.Id,
                true,
                SyncOperation.Update,
                null,
                DateTime.UtcNow,
                new Dictionary<string, object> { { "action", "keep_internal" } }
            ));
        }

        return results;
    }

    private async Task<List<EventSyncResult>> ExecuteKeepExternal(CalendarSyncConflict conflict, CancellationToken cancellationToken)
    {
        var results = new List<EventSyncResult>();

        if (conflict.ExternalEvent != null)
        {
            // Keep the external event - update internal event
            results.Add(new EventSyncResult(
                conflict.InternalEvent?.Id ?? Guid.NewGuid(),
                conflict.ExternalEvent.Id,
                true,
                SyncOperation.Update,
                null,
                DateTime.UtcNow,
                new Dictionary<string, object> { { "action", "keep_external" } }
            ));
        }

        return results;
    }

    private async Task<List<EventSyncResult>> ExecuteMerge(CalendarSyncConflict conflict, ConflictResolution resolution, CancellationToken cancellationToken)
    {
        var results = new List<EventSyncResult>();

        // Merge logic would go here - for now, create a simple merge result
        if (conflict.InternalEvent != null && conflict.ExternalEvent != null)
        {
            results.Add(new EventSyncResult(
                conflict.InternalEvent.Id,
                conflict.ExternalEvent.Id,
                true,
                SyncOperation.Update,
                null,
                DateTime.UtcNow,
                new Dictionary<string, object> { { "action", "merge" } }
            ));
        }

        return results;
    }

    private async Task<List<EventSyncResult>> ExecuteCreateBoth(CalendarSyncConflict conflict, CancellationToken cancellationToken)
    {
        var results = new List<EventSyncResult>();

        // Create both events as separate items
        if (conflict.InternalEvent != null)
        {
            results.Add(new EventSyncResult(
                conflict.InternalEvent.Id,
                null,
                true,
                SyncOperation.Create,
                null,
                DateTime.UtcNow,
                new Dictionary<string, object> { { "action", "create_internal" } }
            ));
        }

        if (conflict.ExternalEvent != null)
        {
            results.Add(new EventSyncResult(
                Guid.NewGuid(),
                conflict.ExternalEvent.Id,
                true,
                SyncOperation.Create,
                null,
                DateTime.UtcNow,
                new Dictionary<string, object> { { "action", "create_external" } }
            ));
        }

        return results;
    }

    private async Task<List<EventSyncResult>> ExecuteUserDecision(CalendarSyncConflict conflict, ConflictResolution resolution, CancellationToken cancellationToken)
    {
        var results = new List<EventSyncResult>();

        // Execute based on user's specific decision data
        var userChoice = resolution.ResolutionData.GetValueOrDefault("choice", "keep_internal").ToString();

        switch (userChoice?.ToLower())
        {
            case "keep_internal":
                results = await ExecuteKeepInternal(conflict, cancellationToken);
                break;
            case "keep_external":
                results = await ExecuteKeepExternal(conflict, cancellationToken);
                break;
            case "merge":
                results = await ExecuteMerge(conflict, resolution, cancellationToken);
                break;
            default:
                results = await ExecuteKeepInternal(conflict, cancellationToken);
                break;
        }

        return results;
    }

    private async Task MarkConflictAsResolved(Guid conflictId, ConflictResolution resolution, CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would update the conflict in the database
            _logger.LogDebug("Marking conflict {ConflictId} as resolved", conflictId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conflict {ConflictId} as resolved", conflictId);
            // Don't throw - this is not critical for the resolution process
        }
    }
}