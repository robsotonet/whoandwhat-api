using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Validators;

/// <summary>
/// Validator for CalendarEvent entity
/// </summary>
public static class CalendarEventValidator
{
    /// <summary>
    /// Validates a calendar event for creation
    /// </summary>
    public static ValidationResult ValidateForCreation(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null)
            return ValidationResult.Fail("Calendar event is required");

        var errors = new List<string>();

        // Validate required fields
        ValidateRequiredFields(calendarEvent, errors);

        // Validate business rules
        ValidateBusinessRules(calendarEvent, errors);

        // Validate time constraints
        ValidateTimeConstraints(calendarEvent, errors);

        // Validate field lengths and formats
        ValidateFieldFormats(calendarEvent, errors);

        // Validate external integration fields
        ValidateExternalIntegrationFields(calendarEvent, errors);

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates a calendar event for update
    /// </summary>
    public static ValidationResult ValidateForUpdate(CalendarEvent calendarEvent, CalendarEvent? existingEvent = null)
    {
        var result = ValidateForCreation(calendarEvent);
        if (!result.IsSuccess)
            return result;

        var errors = new List<string>();

        // Additional validation for updates
        if (existingEvent != null)
        {
            ValidateUpdateConstraints(calendarEvent, existingEvent, errors);
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates calendar event for deletion
    /// </summary>
    public static ValidationResult ValidateForDeletion(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null)
            return ValidationResult.Fail("Calendar event is required");

        var errors = new List<string>();

        // Check if event can be deleted
        if (calendarEvent.IsActive)
        {
            errors.Add("Cannot delete an active (in-progress) event");
        }

        // Check for recurring events
        if (calendarEvent.IsMasterRecurringEvent)
        {
            // This might require special handling or user confirmation
            // For now, we'll allow it but could add business rule restrictions
        }

        // Check for external events with sync restrictions
        if (calendarEvent.IsExternal && calendarEvent.CalendarProvider == (int)CalendarProvider.ICloud)
        {
            // Some providers might have restrictions
            // Add provider-specific validation here
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates event scheduling constraints
    /// </summary>
    public static ValidationResult ValidateScheduling(CalendarEvent calendarEvent, List<CalendarEvent>? existingEvents = null)
    {
        if (calendarEvent == null)
            return ValidationResult.Fail("Calendar event is required");

        var errors = new List<string>();

        // Validate against existing events for conflicts
        if (existingEvents != null)
        {
            foreach (var existing in existingEvents.Where(e => e.Id != calendarEvent.Id))
            {
                if (calendarEvent.ConflictsWith(existing))
                {
                    var overlapMinutes = (int)calendarEvent.GetOverlapDuration(existing).TotalMinutes;
                    if (overlapMinutes > 0)
                    {
                        errors.Add($"Event conflicts with '{existing.Title}' by {overlapMinutes} minutes");
                    }
                }
            }
        }

        // Validate scheduling windows
        if (calendarEvent.StartTime < DateTime.UtcNow.AddMinutes(-15))
        {
            errors.Add("Cannot schedule events more than 15 minutes in the past");
        }

        if (calendarEvent.StartTime > DateTime.UtcNow.AddYears(5))
        {
            errors.Add("Cannot schedule events more than 5 years in the future");
        }

        // Validate reasonable duration
        var duration = calendarEvent.ScheduledDuration;
        if (duration > TimeSpan.FromDays(7))
        {
            errors.Add("Event duration cannot exceed 7 days");
        }

        if (duration < TimeSpan.FromMinutes(1) && !calendarEvent.IsAllDay)
        {
            errors.Add("Non-all-day events must be at least 1 minute long");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates recurrence settings
    /// </summary>
    public static ValidationResult ValidateRecurrence(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null || !calendarEvent.IsRecurring)
            return ValidationResult.Success();

        var errors = new List<string>();

        if (string.IsNullOrEmpty(calendarEvent.RecurrenceRule))
        {
            errors.Add("Recurring events must have recurrence rule defined");
        }
        else
        {
            try
            {
                // Attempt to deserialize recurrence rule to validate format
                var recurrenceData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(calendarEvent.RecurrenceRule);
                
                if (recurrenceData == null || !recurrenceData.Any())
                {
                    errors.Add("Invalid recurrence rule format");
                }
            }
            catch
            {
                errors.Add("Recurrence rule is not valid JSON");
            }
        }

        // Validate recurrence constraints
        if (calendarEvent.IsRecurrenceException && calendarEvent.OriginalStartTime == null)
        {
            errors.Add("Recurrence exceptions must have original start time");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates attendee information
    /// </summary>
    public static ValidationResult ValidateAttendees(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null || !calendarEvent.HasAttendees)
            return ValidationResult.Success();

        var errors = new List<string>();

        try
        {
            var attendeeCollection = EventAttendeeCollection.FromJson(calendarEvent.Attendees!);
            
            if (!attendeeCollection.IsValid(out var attendeeErrors))
            {
                errors.AddRange(attendeeErrors);
            }

            // Additional business rules
            if (attendeeCollection.TotalCount > 500)
            {
                errors.Add("Events cannot have more than 500 attendees");
            }

            if (attendeeCollection.Organizer == null)
            {
                errors.Add("Events with attendees must have an organizer");
            }
        }
        catch
        {
            errors.Add("Invalid attendee data format");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates reminder settings
    /// </summary>
    public static ValidationResult ValidateReminders(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null || !calendarEvent.HasReminders)
            return ValidationResult.Success();

        var errors = new List<string>();

        try
        {
            var reminderCollection = EventReminderCollection.FromJson(calendarEvent.Reminders!);
            
            if (!reminderCollection.IsValid(out var reminderErrors))
            {
                errors.AddRange(reminderErrors);
            }

            // Additional business rules
            if (reminderCollection.Reminders.Count > 10)
            {
                errors.Add("Events cannot have more than 10 reminders");
            }

            // Validate reminder timing against event time
            foreach (var reminder in reminderCollection.EnabledReminders)
            {
                var reminderTime = reminder.GetReminderTime(calendarEvent.StartTime);
                if (reminderTime < DateTime.UtcNow.AddMinutes(-5))
                {
                    errors.Add($"Reminder set for {reminder.OffsetMinutes} minutes before is in the past");
                }
            }
        }
        catch
        {
            errors.Add("Invalid reminder data format");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    private static void ValidateRequiredFields(CalendarEvent calendarEvent, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
        {
            errors.Add("Event title is required");
        }

        if (calendarEvent.UserId == Guid.Empty)
        {
            errors.Add("User ID is required");
        }

        if (calendarEvent.StartTime == default)
        {
            errors.Add("Start time is required");
        }

        if (calendarEvent.EndTime == default)
        {
            errors.Add("End time is required");
        }
    }

    private static void ValidateBusinessRules(CalendarEvent calendarEvent, List<string> errors)
    {
        // Validate event type
        if (!Enum.IsDefined(typeof(EventType), calendarEvent.EventType))
        {
            errors.Add("Invalid event type");
        }

        // Validate status
        if (!Enum.IsDefined(typeof(EventStatus), calendarEvent.Status))
        {
            errors.Add("Invalid event status");
        }

        // Validate visibility
        if (!Enum.IsDefined(typeof(EventVisibility), calendarEvent.Visibility))
        {
            errors.Add("Invalid event visibility");
        }

        // Validate priority
        if (!Enum.IsDefined(typeof(Priority), calendarEvent.Priority))
        {
            errors.Add("Invalid event priority");
        }

        // Validate calendar provider
        if (!Enum.IsDefined(typeof(CalendarProvider), calendarEvent.CalendarProvider))
        {
            errors.Add("Invalid calendar provider");
        }
    }

    private static void ValidateTimeConstraints(CalendarEvent calendarEvent, List<string> errors)
    {
        if (calendarEvent.StartTime >= calendarEvent.EndTime)
        {
            errors.Add("Start time must be before end time");
        }

        // For all-day events, validate date consistency
        if (calendarEvent.IsAllDay)
        {
            if (calendarEvent.StartTime.TimeOfDay != TimeSpan.Zero || 
                calendarEvent.EndTime.TimeOfDay != TimeSpan.Zero)
            {
                errors.Add("All-day events must have times set to midnight");
            }

            if (calendarEvent.StartTime.Date == calendarEvent.EndTime.Date && 
                calendarEvent.EndTime != calendarEvent.StartTime.Date.AddDays(1))
            {
                errors.Add("Single-day all-day events must end at start of next day");
            }
        }

        // Validate reasonable time bounds
        if (calendarEvent.StartTime.Year < 1900 || calendarEvent.StartTime.Year > 2100)
        {
            errors.Add("Event start time must be between years 1900 and 2100");
        }

        if (calendarEvent.EndTime.Year < 1900 || calendarEvent.EndTime.Year > 2100)
        {
            errors.Add("Event end time must be between years 1900 and 2100");
        }
    }

    private static void ValidateFieldFormats(CalendarEvent calendarEvent, List<string> errors)
    {
        // Title length validation
        if (calendarEvent.Title?.Length > CalendarEvent.MaxTitleLength)
        {
            errors.Add($"Event title cannot exceed {CalendarEvent.MaxTitleLength} characters");
        }

        // Description length validation
        if (calendarEvent.Description?.Length > CalendarEvent.MaxDescriptionLength)
        {
            errors.Add($"Event description cannot exceed {CalendarEvent.MaxDescriptionLength} characters");
        }

        // Location validation
        if (!string.IsNullOrEmpty(calendarEvent.Location) && calendarEvent.Location.Length > 1000)
        {
            errors.Add("Event location cannot exceed 1000 characters");
        }

        // Time zone validation
        if (!string.IsNullOrEmpty(calendarEvent.TimeZone))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(calendarEvent.TimeZone);
            }
            catch
            {
                // Try as UTC offset format
                if (!calendarEvent.TimeZone.StartsWith("UTC") && calendarEvent.TimeZone != "UTC")
                {
                    errors.Add("Invalid time zone identifier");
                }
            }
        }

        // Validate JSON fields if not empty
        ValidateJsonField(calendarEvent.RecurrenceRule, "recurrence rule", errors);
        ValidateJsonField(calendarEvent.Attendees, "attendees", errors);
        ValidateJsonField(calendarEvent.Reminders, "reminders", errors);
        ValidateJsonField(calendarEvent.ProviderMetadata, "provider metadata", errors);
    }

    private static void ValidateExternalIntegrationFields(CalendarEvent calendarEvent, List<string> errors)
    {
        // If external event, validate required external fields
        if (calendarEvent.IsExternal)
        {
            if (string.IsNullOrEmpty(calendarEvent.ExternalEventId))
            {
                errors.Add("External events must have external event ID");
            }

            if (calendarEvent.CalendarProvider == (int)CalendarProvider.None)
            {
                errors.Add("External events must specify calendar provider");
            }
        }

        // Validate external ID format if present
        if (!string.IsNullOrEmpty(calendarEvent.ExternalEventId) && calendarEvent.ExternalEventId.Length > 500)
        {
            errors.Add("External event ID cannot exceed 500 characters");
        }

        // Validate external calendar ID format if present
        if (!string.IsNullOrEmpty(calendarEvent.ExternalCalendarId) && calendarEvent.ExternalCalendarId.Length > 500)
        {
            errors.Add("External calendar ID cannot exceed 500 characters");
        }
    }

    private static void ValidateUpdateConstraints(CalendarEvent calendarEvent, CalendarEvent existingEvent, List<string> errors)
    {
        // Validate ID consistency
        if (calendarEvent.Id != existingEvent.Id)
        {
            errors.Add("Cannot change event ID during update");
        }

        // Validate user ownership
        if (calendarEvent.UserId != existingEvent.UserId)
        {
            errors.Add("Cannot change event ownership during update");
        }

        // Validate external integration constraints
        if (existingEvent.IsExternal && !calendarEvent.IsExternal)
        {
            errors.Add("Cannot convert external event to internal event");
        }

        if (existingEvent.CalendarProvider != calendarEvent.CalendarProvider)
        {
            errors.Add("Cannot change calendar provider during update");
        }

        // Validate completed event constraints
        if (existingEvent.IsCompleted && !calendarEvent.IsCompleted && 
            existingEvent.CompletedAt < DateTime.UtcNow.AddHours(-24))
        {
            errors.Add("Cannot reopen events that were completed more than 24 hours ago");
        }

        // Validate recurring event constraints
        if (existingEvent.IsRecurring != calendarEvent.IsRecurring)
        {
            errors.Add("Cannot change recurrence type during update. Create new event instead.");
        }
    }

    private static void ValidateJsonField(string? jsonField, string fieldName, List<string> errors)
    {
        if (string.IsNullOrEmpty(jsonField))
            return;

        try
        {
            System.Text.Json.JsonSerializer.Deserialize<object>(jsonField);
        }
        catch
        {
            errors.Add($"Invalid JSON format in {fieldName}");
        }
    }
}