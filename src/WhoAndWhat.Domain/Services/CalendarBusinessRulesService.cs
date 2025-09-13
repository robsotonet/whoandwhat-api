using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Centralized service for calendar business rules and validation logic
/// </summary>
public class CalendarBusinessRulesService
{
    private readonly CalendarSyncDomainService _syncService;
    private readonly CalendarConflictResolutionService _conflictService;
    private readonly CalendarEventMappingService _mappingService;

    public CalendarBusinessRulesService(
        CalendarSyncDomainService syncService,
        CalendarConflictResolutionService conflictService,
        CalendarEventMappingService mappingService)
    {
        _syncService = syncService;
        _conflictService = conflictService;
        _mappingService = mappingService;
    }

    /// <summary>
    /// Validates and prepares a calendar event for creation
    /// </summary>
    public BusinessRuleResult<CalendarEvent> ValidateEventCreation(CalendarEvent calendarEvent, 
        List<CalendarEvent>? existingEvents = null)
    {
        // Basic validation
        var validationResult = CalendarEventValidator.ValidateForCreation(calendarEvent);
        if (!validationResult.IsValid)
        {
            return BusinessRuleResult<CalendarEvent>.Fail(validationResult.ErrorMessages);
        }

        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply business rules
        rules.AddRange(ApplyEventCreationRules(calendarEvent, existingEvents, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<CalendarEvent>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Apply any automatic adjustments
        var adjustedEvent = ApplyEventCreationAdjustments(calendarEvent, rules);

        return BusinessRuleResult<CalendarEvent>.Success(adjustedEvent, warnings);
    }

    /// <summary>
    /// Validates and prepares a calendar event for update
    /// </summary>
    public BusinessRuleResult<CalendarEvent> ValidateEventUpdate(CalendarEvent updatedEvent, 
        CalendarEvent existingEvent, List<CalendarEvent>? otherEvents = null)
    {
        // Basic validation
        var validationResult = CalendarEventValidator.ValidateForUpdate(updatedEvent, existingEvent);
        if (!validationResult.IsValid)
        {
            return BusinessRuleResult<CalendarEvent>.Fail(validationResult.ErrorMessages);
        }

        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply update-specific business rules
        rules.AddRange(ApplyEventUpdateRules(updatedEvent, existingEvent, otherEvents, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<CalendarEvent>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Apply automatic adjustments
        var adjustedEvent = ApplyEventUpdateAdjustments(updatedEvent, existingEvent, rules);

        return BusinessRuleResult<CalendarEvent>.Success(adjustedEvent, warnings);
    }

    /// <summary>
    /// Validates calendar integration setup
    /// </summary>
    public BusinessRuleResult<CalendarIntegration> ValidateIntegrationSetup(CalendarIntegration integration,
        List<CalendarIntegration>? existingIntegrations = null)
    {
        // Basic validation
        var validationResult = CalendarIntegrationValidator.ValidateForCreation(integration);
        if (!validationResult.IsValid)
        {
            return BusinessRuleResult<CalendarIntegration>.Fail(validationResult.ErrorMessages);
        }

        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply integration setup rules
        rules.AddRange(ApplyIntegrationSetupRules(integration, existingIntegrations, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<CalendarIntegration>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Apply setup adjustments
        var adjustedIntegration = ApplyIntegrationSetupAdjustments(integration, rules);

        return BusinessRuleResult<CalendarIntegration>.Success(adjustedIntegration, warnings);
    }

    /// <summary>
    /// Validates sync operation readiness
    /// </summary>
    public BusinessRuleResult<SyncOperationPlan> ValidateSyncOperation(CalendarIntegration integration,
        List<CalendarEvent> eventsToSync)
    {
        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply sync operation rules
        rules.AddRange(ApplySyncOperationRules(integration, eventsToSync, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<SyncOperationPlan>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Create sync plan
        var syncPlan = CreateSyncPlan(integration, eventsToSync, rules);

        return BusinessRuleResult<SyncOperationPlan>.Success(syncPlan, warnings);
    }

    /// <summary>
    /// Validates conflict resolution action
    /// </summary>
    public BusinessRuleResult<ConflictResolutionPlan> ValidateConflictResolution(CalendarConflict conflict,
        ConflictResolutionAction proposedAction, Dictionary<string, object>? parameters = null)
    {
        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply conflict resolution rules
        rules.AddRange(ApplyConflictResolutionRules(conflict, proposedAction, parameters, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<ConflictResolutionPlan>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Create resolution plan
        var resolutionPlan = CreateConflictResolutionPlan(conflict, proposedAction, parameters, rules);

        return BusinessRuleResult<ConflictResolutionPlan>.Success(resolutionPlan, warnings);
    }

    /// <summary>
    /// Validates recurring event creation
    /// </summary>
    public BusinessRuleResult<List<CalendarEvent>> ValidateRecurringEventCreation(CalendarEvent masterEvent,
        EventRecurrence recurrence, DateTime rangeStart, DateTime rangeEnd)
    {
        var warnings = new List<string>();
        var rules = new List<BusinessRule>();

        // Apply recurring event rules
        rules.AddRange(ApplyRecurringEventRules(masterEvent, recurrence, rangeStart, rangeEnd, warnings));

        // Check for rule violations
        var violations = rules.Where(r => !r.IsSatisfied).ToList();
        if (violations.Any())
        {
            return BusinessRuleResult<List<CalendarEvent>>.Fail(violations.Select(v => v.ErrorMessage).ToList());
        }

        // Generate recurring event instances
        var recurringEvents = GenerateRecurringEventInstances(masterEvent, recurrence, rangeStart, rangeEnd);

        return BusinessRuleResult<List<CalendarEvent>>.Success(recurringEvents, warnings);
    }

    #region Private Helper Methods

    private List<BusinessRule> ApplyEventCreationRules(CalendarEvent calendarEvent, 
        List<CalendarEvent>? existingEvents, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: No more than 50 events per day per user
        if (existingEvents != null)
        {
            var eventsOnSameDay = existingEvents.Count(e => 
                e.StartTime.Date == calendarEvent.StartTime.Date && 
                e.UserId == calendarEvent.UserId);

            rules.Add(new BusinessRule(
                eventsOnSameDay < 50,
                "Cannot create more than 50 events per day"
            ));

            if (eventsOnSameDay > 30)
            {
                warnings.Add($"You have {eventsOnSameDay} events on this day");
            }
        }

        // Rule: Events cannot be scheduled more than 2 years in advance
        rules.Add(new BusinessRule(
            calendarEvent.StartTime <= DateTime.UtcNow.AddYears(2),
            "Events cannot be scheduled more than 2 years in advance"
        ));

        // Rule: All-day events must be at least 1 day long
        if (calendarEvent.IsAllDay)
        {
            rules.Add(new BusinessRule(
                calendarEvent.EndTime >= calendarEvent.StartTime.Date.AddDays(1),
                "All-day events must be at least 1 day long"
            ));
        }

        // Rule: External events must have integration
        if (calendarEvent.IsExternal)
        {
            rules.Add(new BusinessRule(
                !string.IsNullOrEmpty(calendarEvent.ExternalEventId),
                "External events must have external event ID"
            ));
        }

        // Rule: Events with attendees should have reasonable duration
        if (calendarEvent.HasAttendees)
        {
            var duration = calendarEvent.ScheduledDuration;
            if (duration.TotalMinutes < 15)
            {
                warnings.Add("Meetings with attendees are typically at least 15 minutes long");
            }
            if (duration.TotalHours > 8)
            {
                warnings.Add("Very long meetings may impact attendee availability");
            }
        }

        return rules;
    }

    private List<BusinessRule> ApplyEventUpdateRules(CalendarEvent updatedEvent, CalendarEvent existingEvent,
        List<CalendarEvent>? otherEvents, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: Cannot change completed events older than 24 hours
        if (existingEvent.IsCompleted && existingEvent.CompletedAt.HasValue)
        {
            rules.Add(new BusinessRule(
                DateTime.UtcNow - existingEvent.CompletedAt.Value <= TimeSpan.FromDays(1) || !updatedEvent.IsCompleted,
                "Cannot modify events completed more than 24 hours ago"
            ));
        }

        // Rule: Cannot reschedule events that started more than 1 hour ago
        if (existingEvent.StartTime < DateTime.UtcNow.AddHours(-1))
        {
            rules.Add(new BusinessRule(
                updatedEvent.StartTime == existingEvent.StartTime && updatedEvent.EndTime == existingEvent.EndTime,
                "Cannot reschedule events that started more than 1 hour ago"
            ));
        }

        // Rule: External events need sync validation
        if (existingEvent.IsExternal && _syncService.WouldCreateSyncLoop(updatedEvent, null))
        {
            warnings.Add("This update may conflict with external calendar sync");
        }

        // Rule: Attendee changes in active events require notification
        if (existingEvent.HasAttendees && updatedEvent.HasAttendees && 
            existingEvent.Attendees != updatedEvent.Attendees)
        {
            if (existingEvent.StartTime <= DateTime.UtcNow.AddHours(2))
            {
                warnings.Add("Attendee changes close to event time should be communicated manually");
            }
        }

        return rules;
    }

    private List<BusinessRule> ApplyIntegrationSetupRules(CalendarIntegration integration,
        List<CalendarIntegration>? existingIntegrations, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: No more than 5 integrations per user
        if (existingIntegrations != null)
        {
            rules.Add(new BusinessRule(
                existingIntegrations.Count < 5,
                "Cannot have more than 5 calendar integrations per user"
            ));

            // Rule: No duplicate providers (unless provider supports multiple accounts)
            var duplicateProviders = existingIntegrations.Where(ei => 
                ei.CalendarProvider == integration.CalendarProvider &&
                ei.ProviderAccountId != integration.ProviderAccountId).ToList();

            if (duplicateProviders.Any())
            {
                if (integration.CalendarProvider != (int)CalendarProvider.Google)
                {
                    rules.Add(new BusinessRule(
                        false,
                        $"Only one {(CalendarProvider)integration.CalendarProvider} integration allowed per user"
                    ));
                }
                else
                {
                    warnings.Add("Multiple Google accounts detected - ensure correct account is selected");
                }
            }
        }

        // Rule: Sync interval should be reasonable
        if (integration.SyncInterval < TimeSpan.FromMinutes(5))
        {
            warnings.Add("Very frequent sync intervals may impact performance and hit rate limits");
        }

        if (integration.SyncInterval > TimeSpan.FromDays(1))
        {
            warnings.Add("Long sync intervals may cause delays in conflict detection");
        }

        return rules;
    }

    private List<BusinessRule> ApplySyncOperationRules(CalendarIntegration integration,
        List<CalendarEvent> eventsToSync, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: Integration must be healthy for sync
        rules.Add(new BusinessRule(
            _syncService.CanSync(integration),
            "Integration must be healthy and enabled for sync operations"
        ));

        // Rule: Cannot sync too many events at once
        rules.Add(new BusinessRule(
            eventsToSync.Count <= 1000,
            "Cannot sync more than 1000 events in a single operation"
        ));

        // Rule: Events must belong to integration user
        rules.Add(new BusinessRule(
            eventsToSync.All(e => e.UserId == integration.UserId),
            "All events must belong to the integration user"
        ));

        // Rule: Validate provider-specific constraints
        var provider = (CalendarProvider)integration.CalendarProvider;
        if (provider == CalendarProvider.ICloud && eventsToSync.Any(e => e.HasAttendees))
        {
            warnings.Add("iCloud may have limited support for events with attendees");
        }

        if (provider == CalendarProvider.Outlook && eventsToSync.Any(e => e.IsRecurring))
        {
            warnings.Add("Complex recurring patterns may not sync perfectly with Outlook");
        }

        return rules;
    }

    private List<BusinessRule> ApplyConflictResolutionRules(CalendarConflict conflict,
        ConflictResolutionAction proposedAction, Dictionary<string, object>? parameters, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: Conflict must be active for resolution
        rules.Add(new BusinessRule(
            conflict.IsActive && !conflict.IsIgnored,
            "Only active conflicts can be resolved"
        ));

        // Rule: High severity conflicts require manual resolution
        if (conflict.Severity >= (int)ConflictSeverity.High && proposedAction != ConflictResolutionAction.UserDecision)
        {
            rules.Add(new BusinessRule(
                false,
                "High severity conflicts require manual resolution"
            ));
        }

        // Rule: Validate resolution action appropriateness
        var validationResult = _conflictService.ValidateResolution(conflict, proposedAction, parameters);
        rules.Add(new BusinessRule(
            validationResult.IsValid,
            string.Join("; ", validationResult.Errors)
        ));

        if (validationResult.HasWarnings)
        {
            warnings.AddRange(validationResult.Warnings);
        }

        return rules;
    }

    private List<BusinessRule> ApplyRecurringEventRules(CalendarEvent masterEvent, EventRecurrence recurrence,
        DateTime rangeStart, DateTime rangeEnd, List<string> warnings)
    {
        var rules = new List<BusinessRule>();

        // Rule: Recurring events cannot be too frequent
        if (recurrence.Frequency == RecurrenceFrequency.Daily && recurrence.Interval == 1)
        {
            var totalDays = (rangeEnd - rangeStart).TotalDays;
            if (totalDays > 365)
            {
                rules.Add(new BusinessRule(
                    false,
                    "Daily recurring events cannot span more than 1 year"
                ));
            }
        }

        // Rule: Cannot generate more than 500 instances at once
        var potentialInstances = recurrence.GetOccurrencesInRange(rangeStart, rangeEnd, masterEvent.StartTime);
        rules.Add(new BusinessRule(
            potentialInstances.Count <= 500,
            "Cannot generate more than 500 recurring event instances at once"
        ));

        if (potentialInstances.Count > 100)
        {
            warnings.Add($"Generating {potentialInstances.Count} recurring instances");
        }

        // Rule: Recurring events should have reasonable end conditions
        if (recurrence.IsInfinite)
        {
            warnings.Add("Infinite recurring events may impact performance over time");
        }

        return rules;
    }

    private CalendarEvent ApplyEventCreationAdjustments(CalendarEvent calendarEvent, List<BusinessRule> rules)
    {
        // Apply any automatic adjustments based on business rules
        var adjustedEvent = calendarEvent;

        // Automatically set buffer times for important events
        if (adjustedEvent.Priority >= (int)Priority.High && adjustedEvent.BufferTimeBefore == null)
        {
            adjustedEvent.BufferTimeBefore = TimeSpan.FromMinutes(15);
        }

        // Set default reminders if none specified and event has attendees
        if (adjustedEvent.HasAttendees && !adjustedEvent.HasReminders)
        {
            var defaultReminders = EventReminder.Presets.DefaultMeetingReminders;
            adjustedEvent.Reminders = EventReminderCollection.Create(defaultReminders).ToJson();
        }

        return adjustedEvent;
    }

    private CalendarEvent ApplyEventUpdateAdjustments(CalendarEvent updatedEvent, CalendarEvent existingEvent, List<BusinessRule> rules)
    {
        // Apply update-specific adjustments
        var adjustedEvent = updatedEvent;

        // Preserve sync information for external events
        if (existingEvent.IsExternal)
        {
            adjustedEvent.LastSyncTime = existingEvent.LastSyncTime;
            adjustedEvent.SyncVersion = existingEvent.SyncVersion;
        }

        return adjustedEvent;
    }

    private CalendarIntegration ApplyIntegrationSetupAdjustments(CalendarIntegration integration, List<BusinessRule> rules)
    {
        // Apply setup adjustments
        var adjustedIntegration = integration;

        // Set recommended sync interval based on provider
        var provider = (CalendarProvider)integration.CalendarProvider;
        if (provider == CalendarProvider.ICloud && integration.SyncInterval < TimeSpan.FromMinutes(30))
        {
            adjustedIntegration.SyncInterval = TimeSpan.FromMinutes(30);
        }

        return adjustedIntegration;
    }

    private SyncOperationPlan CreateSyncPlan(CalendarIntegration integration, List<CalendarEvent> eventsToSync, List<BusinessRule> rules)
    {
        var strategy = _syncService.DetermineSyncStrategy(integration);
        var batchSize = _syncService.GetRecommendedBatchSize(integration);
        var priority = SyncPriority.Medium;

        // Group events by priority for batching
        var prioritizedEvents = eventsToSync
            .Select(e => new { Event = e, Priority = _syncService.CalculateSyncPriority(e, integration) })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Event.StartTime)
            .Select(x => x.Event)
            .ToList();

        return new SyncOperationPlan(
            integration.Id,
            strategy,
            prioritizedEvents,
            batchSize,
            priority,
            DateTime.UtcNow.Add(TimeSpan.FromSeconds(30)) // Start in 30 seconds
        );
    }

    private ConflictResolutionPlan CreateConflictResolutionPlan(CalendarConflict conflict,
        ConflictResolutionAction action, Dictionary<string, object>? parameters, List<BusinessRule> rules)
    {
        var analysis = _conflictService.AnalyzeConflict(conflict);
        var canAutoResolve = _conflictService.CanAutoResolve(conflict);

        return new ConflictResolutionPlan(
            conflict.Id,
            action,
            parameters ?? new Dictionary<string, object>(),
            canAutoResolve,
            analysis.AutoResolutionConfidence,
            DateTime.UtcNow
        );
    }

    private List<CalendarEvent> GenerateRecurringEventInstances(CalendarEvent masterEvent,
        EventRecurrence recurrence, DateTime rangeStart, DateTime rangeEnd)
    {
        var instances = new List<CalendarEvent>();
        var occurrences = recurrence.GetOccurrencesInRange(rangeStart, rangeEnd, masterEvent.StartTime, 500);

        foreach (var occurrence in occurrences)
        {
            var instance = new CalendarEvent
            {
                Title = masterEvent.Title,
                Description = masterEvent.Description,
                StartTime = occurrence,
                EndTime = occurrence.Add(masterEvent.ScheduledDuration),
                Location = masterEvent.Location,
                IsAllDay = masterEvent.IsAllDay,
                TimeZone = masterEvent.TimeZone,
                EventType = masterEvent.EventType,
                Status = masterEvent.Status,
                Visibility = masterEvent.Visibility,
                Priority = masterEvent.Priority,
                UserId = masterEvent.UserId,
                RecurrenceGroupId = masterEvent.Id,
                IsRecurring = false,
                IsRecurrenceException = false,
                Attendees = masterEvent.Attendees,
                Reminders = masterEvent.Reminders,
                CalendarProvider = masterEvent.CalendarProvider
            };

            instances.Add(instance);
        }

        return instances;
    }

    #endregion
}

/// <summary>
/// Business rule representation
/// </summary>
public class BusinessRule
{
    public BusinessRule(bool isSatisfied, string errorMessage)
    {
        IsSatisfied = isSatisfied;
        ErrorMessage = errorMessage;
    }

    public bool IsSatisfied { get; }
    public string ErrorMessage { get; }
}

/// <summary>
/// Result of business rule validation
/// </summary>
public class BusinessRuleResult<T>
{
    private BusinessRuleResult(bool isSuccess, T? result, List<string> errors, List<string> warnings)
    {
        IsSuccess = isSuccess;
        Result = result;
        Errors = errors;
        Warnings = warnings;
    }

    public bool IsSuccess { get; }
    public T? Result { get; }
    public List<string> Errors { get; }
    public List<string> Warnings { get; }
    public bool HasWarnings => Warnings.Any();

    public static BusinessRuleResult<T> Success(T result, List<string>? warnings = null) =>
        new(true, result, new List<string>(), warnings ?? new List<string>());

    public static BusinessRuleResult<T> Fail(List<string> errors) =>
        new(false, default, errors, new List<string>());

    public static BusinessRuleResult<T> Fail(string error) =>
        new(false, default, new List<string> { error }, new List<string>());
}

/// <summary>
/// Plan for sync operation
/// </summary>
public record SyncOperationPlan(
    Guid IntegrationId,
    SyncStrategy Strategy,
    List<CalendarEvent> EventsToSync,
    int BatchSize,
    SyncPriority Priority,
    DateTime ScheduledStartTime);

/// <summary>
/// Plan for conflict resolution
/// </summary>
public record ConflictResolutionPlan(
    Guid ConflictId,
    ConflictResolutionAction Action,
    Dictionary<string, object> Parameters,
    bool CanAutoResolve,
    double Confidence,
    DateTime CreatedAt);