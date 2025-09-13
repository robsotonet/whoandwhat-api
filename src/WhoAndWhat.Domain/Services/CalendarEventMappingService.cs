using System.Text.Json;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for mapping between internal and external calendar events
/// </summary>
public class CalendarEventMappingService
{
    /// <summary>
    /// Maps an external event to an internal CalendarEvent
    /// </summary>
    public CalendarEvent MapToInternal(ExternalEventData externalEvent, CalendarProvider provider, Guid userId)
    {
        if (externalEvent == null)
            throw new ArgumentNullException(nameof(externalEvent));

        var calendarEvent = new CalendarEvent
        {
            UserId = userId,
            Title = SanitizeTitle(externalEvent.Title),
            Description = SanitizeDescription(externalEvent.Description),
            StartTime = externalEvent.StartTime.ToUniversalTime(),
            EndTime = externalEvent.EndTime.ToUniversalTime(),
            Location = externalEvent.Location?.Trim(),
            IsAllDay = externalEvent.IsAllDay,
            TimeZone = externalEvent.TimeZone,
            
            // External integration properties
            ExternalEventId = externalEvent.Id,
            ExternalCalendarId = externalEvent.CalendarId,
            CalendarProvider = (int)provider,
            LastSyncTime = DateTime.UtcNow,
            SyncVersion = externalEvent.ETag,
            
            // Map status and visibility
            Status = MapEventStatus(externalEvent.Status),
            Visibility = MapEventVisibility(externalEvent.Visibility),
            EventType = (int)EventType.External,
            Priority = DetermineEventPriority(externalEvent)
        };

        // Map recurrence if present
        if (externalEvent.Recurrence != null)
        {
            calendarEvent.IsRecurring = true;
            calendarEvent.RecurrenceRule = JsonSerializer.Serialize(MapRecurrence(externalEvent.Recurrence));
        }

        // Map attendees if present
        if (externalEvent.Attendees?.Any() == true)
        {
            var attendeeCollection = MapAttendees(externalEvent.Attendees);
            calendarEvent.Attendees = attendeeCollection.ToJson();
        }

        // Map reminders if present
        if (externalEvent.Reminders?.Any() == true)
        {
            var reminderCollection = MapReminders(externalEvent.Reminders);
            calendarEvent.Reminders = reminderCollection.ToJson();
        }

        // Store provider-specific metadata
        if (externalEvent.ProviderData?.Any() == true)
        {
            calendarEvent.ProviderMetadata = JsonSerializer.Serialize(externalEvent.ProviderData);
        }

        return calendarEvent;
    }

    /// <summary>
    /// Maps an internal CalendarEvent to external format
    /// </summary>
    public ExternalEventData MapToExternal(CalendarEvent calendarEvent, CalendarProvider provider)
    {
        if (calendarEvent == null)
            throw new ArgumentNullException(nameof(calendarEvent));

        var externalEvent = new ExternalEventData
        {
            Id = calendarEvent.ExternalEventId,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            StartTime = calendarEvent.StartTime,
            EndTime = calendarEvent.EndTime,
            IsAllDay = calendarEvent.IsAllDay,
            Location = calendarEvent.Location,
            TimeZone = calendarEvent.TimeZone ?? "UTC",
            Status = MapToExternalStatus((EventStatus)calendarEvent.Status),
            Visibility = MapToExternalVisibility((EventVisibility)calendarEvent.Visibility),
            ETag = calendarEvent.SyncVersion
        };

        // Map recurrence if present
        if (calendarEvent.IsRecurring && !string.IsNullOrEmpty(calendarEvent.RecurrenceRule))
        {
            try
            {
                var recurrenceData = JsonSerializer.Deserialize<Dictionary<string, object>>(calendarEvent.RecurrenceRule);
                externalEvent.Recurrence = MapToExternalRecurrence(recurrenceData);
            }
            catch
            {
                // Skip recurrence if deserialization fails
            }
        }

        // Map attendees if present
        if (!string.IsNullOrEmpty(calendarEvent.Attendees))
        {
            var attendeeCollection = EventAttendeeCollection.FromJson(calendarEvent.Attendees);
            externalEvent.Attendees = MapToExternalAttendees(attendeeCollection.Attendees.ToList());
        }

        // Map reminders if present
        if (!string.IsNullOrEmpty(calendarEvent.Reminders))
        {
            var reminderCollection = EventReminderCollection.FromJson(calendarEvent.Reminders);
            externalEvent.Reminders = MapToExternalReminders(reminderCollection.Reminders.ToList(), provider);
        }

        // Include provider-specific data
        if (!string.IsNullOrEmpty(calendarEvent.ProviderMetadata))
        {
            try
            {
                externalEvent.ProviderData = JsonSerializer.Deserialize<Dictionary<string, object>>(calendarEvent.ProviderMetadata);
            }
            catch
            {
                // Skip provider data if deserialization fails
            }
        }

        return externalEvent;
    }

    /// <summary>
    /// Merges external event data into an existing internal event
    /// </summary>
    public CalendarEvent MergeFromExternal(CalendarEvent existingEvent, ExternalEventData externalEvent, 
        MergeStrategy strategy = MergeStrategy.PreserveInternal)
    {
        if (existingEvent == null)
            throw new ArgumentNullException(nameof(existingEvent));
        if (externalEvent == null)
            throw new ArgumentNullException(nameof(externalEvent));

        var mergedEvent = new CalendarEvent
        {
            Id = existingEvent.Id,
            UserId = existingEvent.UserId,
            ExternalEventId = externalEvent.Id,
            ExternalCalendarId = externalEvent.CalendarId,
            CalendarProvider = existingEvent.CalendarProvider,
            LastSyncTime = DateTime.UtcNow,
            SyncVersion = externalEvent.ETag
        };

        // Apply merge strategy for each field
        mergedEvent.Title = ApplyMergeStrategy(strategy, existingEvent.Title, externalEvent.Title);
        mergedEvent.Description = ApplyMergeStrategy(strategy, existingEvent.Description, externalEvent.Description);
        mergedEvent.Location = ApplyMergeStrategy(strategy, existingEvent.Location, externalEvent.Location);
        
        // Time fields usually follow external (they're more likely to be authoritative)
        if (strategy == MergeStrategy.PreferExternal || 
            (strategy == MergeStrategy.PreferMostRecent && WasExternalModifiedLater(existingEvent, externalEvent)))
        {
            mergedEvent.StartTime = externalEvent.StartTime.ToUniversalTime();
            mergedEvent.EndTime = externalEvent.EndTime.ToUniversalTime();
            mergedEvent.IsAllDay = externalEvent.IsAllDay;
            mergedEvent.TimeZone = externalEvent.TimeZone;
        }
        else
        {
            mergedEvent.StartTime = existingEvent.StartTime;
            mergedEvent.EndTime = existingEvent.EndTime;
            mergedEvent.IsAllDay = existingEvent.IsAllDay;
            mergedEvent.TimeZone = existingEvent.TimeZone;
        }

        // Merge complex objects
        mergedEvent.Status = strategy == MergeStrategy.PreferExternal ? 
            MapEventStatus(externalEvent.Status) : existingEvent.Status;
        
        mergedEvent.Visibility = strategy == MergeStrategy.PreferExternal ?
            MapEventVisibility(externalEvent.Visibility) : existingEvent.Visibility;

        // Preserve internal relationships and metadata
        mergedEvent.RelatedTaskId = existingEvent.RelatedTaskId;
        mergedEvent.RelatedProjectId = existingEvent.RelatedProjectId;
        mergedEvent.EventType = existingEvent.EventType;
        mergedEvent.Priority = existingEvent.Priority;

        return mergedEvent;
    }

    /// <summary>
    /// Detects changes between internal and external events
    /// </summary>
    public EventChangeDetectionResult DetectChanges(CalendarEvent internalEvent, ExternalEventData externalEvent)
    {
        if (internalEvent == null || externalEvent == null)
            return EventChangeDetectionResult.NoChanges();

        var changes = new List<EventFieldChange>();

        // Check basic fields
        CheckFieldChange(changes, "Title", internalEvent.Title, externalEvent.Title);
        CheckFieldChange(changes, "Description", internalEvent.Description, externalEvent.Description);
        CheckFieldChange(changes, "Location", internalEvent.Location, externalEvent.Location);
        CheckFieldChange(changes, "StartTime", internalEvent.StartTime, externalEvent.StartTime.ToUniversalTime());
        CheckFieldChange(changes, "EndTime", internalEvent.EndTime, externalEvent.EndTime.ToUniversalTime());
        CheckFieldChange(changes, "IsAllDay", internalEvent.IsAllDay, externalEvent.IsAllDay);

        // Check status and visibility
        var externalStatus = MapEventStatus(externalEvent.Status);
        var externalVisibility = MapEventVisibility(externalEvent.Visibility);
        CheckFieldChange(changes, "Status", internalEvent.Status, externalStatus);
        CheckFieldChange(changes, "Visibility", internalEvent.Visibility, externalVisibility);

        // Check sync version/etag
        CheckFieldChange(changes, "SyncVersion", internalEvent.SyncVersion, externalEvent.ETag);

        return new EventChangeDetectionResult(
            internalEvent.Id,
            externalEvent.Id,
            changes,
            changes.Any(),
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Calculates similarity score between internal and external events
    /// </summary>
    public double CalculateSimilarity(CalendarEvent internalEvent, ExternalEventData externalEvent)
    {
        if (internalEvent == null || externalEvent == null)
            return 0.0;

        var similarityFactors = new List<(double weight, double similarity)>();

        // Title similarity (high weight)
        similarityFactors.Add((0.3, CalculateStringSimilarity(internalEvent.Title, externalEvent.Title)));

        // Time similarity (high weight)
        var timesSimilar = Math.Abs((internalEvent.StartTime - externalEvent.StartTime.ToUniversalTime()).TotalMinutes) <= 5 &&
                          Math.Abs((internalEvent.EndTime - externalEvent.EndTime.ToUniversalTime()).TotalMinutes) <= 5;
        similarityFactors.Add((0.25, timesSimilar ? 1.0 : 0.0));

        // Description similarity (medium weight)
        similarityFactors.Add((0.2, CalculateStringSimilarity(internalEvent.Description, externalEvent.Description)));

        // Location similarity (medium weight)
        similarityFactors.Add((0.15, CalculateStringSimilarity(internalEvent.Location, externalEvent.Location)));

        // Duration similarity (low weight)
        var internalDuration = internalEvent.EndTime - internalEvent.StartTime;
        var externalDuration = externalEvent.EndTime - externalEvent.StartTime;
        var durationSimilar = Math.Abs((internalDuration - externalDuration).TotalMinutes) <= 15;
        similarityFactors.Add((0.1, durationSimilar ? 1.0 : 0.0));

        // Calculate weighted average
        var totalWeight = similarityFactors.Sum(f => f.weight);
        var weightedSum = similarityFactors.Sum(f => f.weight * f.similarity);

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }

    /// <summary>
    /// Validates that an event mapping is compatible
    /// </summary>
    public MappingValidationResult ValidateMapping(CalendarEvent internalEvent, CalendarProvider provider)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (internalEvent == null)
        {
            errors.Add("Internal event is required");
            return new MappingValidationResult(false, errors, warnings);
        }

        // Check required fields
        if (string.IsNullOrWhiteSpace(internalEvent.Title))
            errors.Add("Event title is required");

        if (internalEvent.StartTime >= internalEvent.EndTime)
            errors.Add("Start time must be before end time");

        // Check provider-specific limitations
        switch (provider)
        {
            case CalendarProvider.Google:
                ValidateForGoogle(internalEvent, errors, warnings);
                break;
            case CalendarProvider.Outlook:
                ValidateForOutlook(internalEvent, errors, warnings);
                break;
            case CalendarProvider.ICloud:
                ValidateForICloud(internalEvent, errors, warnings);
                break;
        }

        // Check for unsupported features
        if (internalEvent.IsRecurring && provider == CalendarProvider.Custom)
            warnings.Add("Recurring events may not be fully supported by custom providers");

        if (!string.IsNullOrEmpty(internalEvent.Attendees) && provider == CalendarProvider.ICloud)
            warnings.Add("Attendee management may be limited with iCloud");

        return new MappingValidationResult(errors.Count == 0, errors, warnings);
    }

    private int MapEventStatus(string externalStatus)
    {
        return externalStatus?.ToLower() switch
        {
            "confirmed" => (int)EventStatus.Confirmed,
            "tentative" => (int)EventStatus.Tentative,
            "cancelled" => (int)EventStatus.Cancelled,
            _ => (int)EventStatus.Confirmed
        };
    }

    private int MapEventVisibility(string externalVisibility)
    {
        return externalVisibility?.ToLower() switch
        {
            "public" => (int)EventVisibility.Public,
            "private" => (int)EventVisibility.Private,
            "confidential" => (int)EventVisibility.Confidential,
            _ => (int)EventVisibility.Private
        };
    }

    private string MapToExternalStatus(EventStatus status)
    {
        return status switch
        {
            EventStatus.Confirmed => "confirmed",
            EventStatus.Tentative => "tentative",
            EventStatus.Cancelled => "cancelled",
            EventStatus.Completed => "confirmed",
            _ => "confirmed"
        };
    }

    private string MapToExternalVisibility(EventVisibility visibility)
    {
        return visibility switch
        {
            EventVisibility.Public => "public",
            EventVisibility.Private => "private",
            EventVisibility.Confidential => "confidential",
            _ => "private"
        };
    }

    private int DetermineEventPriority(ExternalEventData externalEvent)
    {
        // Simple heuristics for determining priority from external event
        var priorityScore = 0;

        if (externalEvent.Attendees?.Count() > 5)
            priorityScore += 10;

        if (externalEvent.Title?.ToLower().Contains("urgent") == true ||
            externalEvent.Title?.ToLower().Contains("important") == true)
            priorityScore += 20;

        if (externalEvent.Reminders?.Count() > 2)
            priorityScore += 10;

        return priorityScore switch
        {
            >= 30 => (int)Priority.High,
            >= 15 => (int)Priority.Medium,
            _ => (int)Priority.Low
        };
    }

    private EventAttendeeCollection MapAttendees(IEnumerable<ExternalAttendeeData> externalAttendees)
    {
        var attendees = externalAttendees.Select(ea => EventAttendee.Create(
            ea.Email,
            ea.Name,
            MapAttendeeStatus(ea.Status),
            MapAttendeeRole(ea.Role)
        )).ToList();

        return EventAttendeeCollection.Create(attendees);
    }

    private List<ExternalAttendeeData> MapToExternalAttendees(List<EventAttendee> internalAttendees)
    {
        return internalAttendees.Select(ia => new ExternalAttendeeData
        {
            Email = ia.Email,
            Name = ia.Name,
            Status = MapToExternalAttendeeStatus(ia.Status),
            Role = MapToExternalAttendeeRole(ia.Role),
            IsOrganizer = ia.IsOrganizer,
            IsOptional = ia.IsOptional
        }).ToList();
    }

    private AttendeeStatus MapAttendeeStatus(string externalStatus)
    {
        return externalStatus?.ToLower() switch
        {
            "accepted" => AttendeeStatus.Accepted,
            "declined" => AttendeeStatus.Declined,
            "tentative" => AttendeeStatus.Tentative,
            _ => AttendeeStatus.NeedsAction
        };
    }

    private AttendeeRole MapAttendeeRole(string externalRole)
    {
        return externalRole?.ToLower() switch
        {
            "chair" => AttendeeRole.Chair,
            "required-participant" => AttendeeRole.RequiredParticipant,
            "optional-participant" => AttendeeRole.OptionalParticipant,
            "non-participant" => AttendeeRole.NonParticipant,
            _ => AttendeeRole.RequiredParticipant
        };
    }

    private string MapToExternalAttendeeStatus(AttendeeStatus status)
    {
        return status switch
        {
            AttendeeStatus.Accepted => "accepted",
            AttendeeStatus.Declined => "declined", 
            AttendeeStatus.Tentative => "tentative",
            _ => "needsAction"
        };
    }

    private string MapToExternalAttendeeRole(AttendeeRole role)
    {
        return role switch
        {
            AttendeeRole.Chair => "chair",
            AttendeeRole.RequiredParticipant => "required-participant",
            AttendeeRole.OptionalParticipant => "optional-participant",
            AttendeeRole.NonParticipant => "non-participant",
            _ => "required-participant"
        };
    }

    private EventReminderCollection MapReminders(IEnumerable<ExternalReminderData> externalReminders)
    {
        var reminders = externalReminders.Select(er => EventReminder.Create(
            MapReminderMethod(er.Method),
            TimeSpan.FromMinutes(er.MinutesBefore),
            er.CustomMessage,
            ReminderPriority.Normal
        )).ToList();

        return EventReminderCollection.Create(reminders);
    }

    private List<ExternalReminderData> MapToExternalReminders(List<EventReminder> internalReminders, CalendarProvider provider)
    {
        return internalReminders.Where(r => r.IsEnabled).Select(ir => new ExternalReminderData
        {
            Method = MapToExternalReminderMethod(ir.Method, provider),
            MinutesBefore = ir.OffsetMinutes,
            CustomMessage = ir.CustomMessage
        }).ToList();
    }

    private ReminderMethod MapReminderMethod(string externalMethod)
    {
        return externalMethod?.ToLower() switch
        {
            "email" => ReminderMethod.Email,
            "popup" => ReminderMethod.Popup,
            "sms" => ReminderMethod.SMS,
            "display" => ReminderMethod.Popup,
            "sound" => ReminderMethod.Sound,
            _ => ReminderMethod.Popup
        };
    }

    private string MapToExternalReminderMethod(ReminderMethod method, CalendarProvider provider)
    {
        return method switch
        {
            ReminderMethod.Email => "email",
            ReminderMethod.Popup => provider == CalendarProvider.Google ? "popup" : "display",
            ReminderMethod.SMS => "sms",
            ReminderMethod.Sound => "sound",
            ReminderMethod.Push => "display",
            ReminderMethod.InApp => "display",
            _ => "display"
        };
    }

    private Dictionary<string, object> MapRecurrence(ExternalRecurrenceData externalRecurrence)
    {
        return new Dictionary<string, object>
        {
            ["frequency"] = externalRecurrence.Frequency,
            ["interval"] = externalRecurrence.Interval,
            ["daysOfWeek"] = externalRecurrence.DaysOfWeek ?? new List<string>(),
            ["endDate"] = externalRecurrence.EndDate,
            ["count"] = externalRecurrence.Count
        };
    }

    private ExternalRecurrenceData MapToExternalRecurrence(Dictionary<string, object> recurrenceData)
    {
        return new ExternalRecurrenceData
        {
            Frequency = recurrenceData.TryGetValue("frequency", out var freq) ? freq.ToString() : "DAILY",
            Interval = recurrenceData.TryGetValue("interval", out var interval) ? Convert.ToInt32(interval) : 1,
            DaysOfWeek = recurrenceData.TryGetValue("daysOfWeek", out var days) 
                ? JsonSerializer.Deserialize<List<string>>(days.ToString() ?? "[]") 
                : new List<string>(),
            EndDate = recurrenceData.TryGetValue("endDate", out var endDate) && DateTime.TryParse(endDate.ToString(), out var ed) ? ed : null,
            Count = recurrenceData.TryGetValue("count", out var count) && int.TryParse(count.ToString(), out var c) ? c : null
        };
    }

    private string SanitizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Untitled Event";

        return title.Trim().Length > CalendarEvent.MaxTitleLength 
            ? title.Trim().Substring(0, CalendarEvent.MaxTitleLength - 3) + "..."
            : title.Trim();
    }

    private string? SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        return description.Trim().Length > CalendarEvent.MaxDescriptionLength
            ? description.Trim().Substring(0, CalendarEvent.MaxDescriptionLength - 3) + "..."
            : description.Trim();
    }

    private string ApplyMergeStrategy(MergeStrategy strategy, string? internalValue, string? externalValue)
    {
        return strategy switch
        {
            MergeStrategy.PreferInternal => internalValue ?? externalValue ?? "",
            MergeStrategy.PreferExternal => externalValue ?? internalValue ?? "",
            MergeStrategy.PreferMostRecent => externalValue ?? internalValue ?? "", // Assuming external is more recent
            MergeStrategy.PreferDetailed => 
                (internalValue?.Length > externalValue?.Length ? internalValue : externalValue) ?? "",
            _ => internalValue ?? externalValue ?? ""
        };
    }

    private bool WasExternalModifiedLater(CalendarEvent internalEvent, ExternalEventData externalEvent)
    {
        // Simple heuristic - could be enhanced with actual modification timestamps
        return internalEvent.LastSyncTime.HasValue && 
               DateTime.UtcNow - internalEvent.LastSyncTime.Value > TimeSpan.FromMinutes(30);
    }

    private void CheckFieldChange<T>(List<EventFieldChange> changes, string fieldName, T? internalValue, T? externalValue)
    {
        if (!Equals(internalValue, externalValue))
        {
            changes.Add(new EventFieldChange(
                fieldName,
                internalValue?.ToString(),
                externalValue?.ToString(),
                CalculateFieldChangeType(fieldName)
            ));
        }
    }

    private EventFieldChangeType CalculateFieldChangeType(string fieldName)
    {
        return fieldName switch
        {
            "StartTime" or "EndTime" => EventFieldChangeType.Critical,
            "Title" or "Status" => EventFieldChangeType.Important,
            "Description" or "Location" => EventFieldChangeType.Minor,
            _ => EventFieldChangeType.Metadata
        };
    }

    private double CalculateStringSimilarity(string? str1, string? str2)
    {
        if (str1 == str2) return 1.0;
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2)) return 1.0;
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0.0;

        // Simple similarity calculation - could be enhanced with more sophisticated algorithms
        var maxLength = Math.Max(str1.Length, str2.Length);
        var distance = CalculateLevenshteinDistance(str1, str2);
        return 1.0 - (double)distance / maxLength;
    }

    private int CalculateLevenshteinDistance(string str1, string str2)
    {
        var matrix = new int[str1.Length + 1, str2.Length + 1];

        for (int i = 0; i <= str1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= str2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= str1.Length; i++)
        {
            for (int j = 1; j <= str2.Length; j++)
            {
                var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[str1.Length, str2.Length];
    }

    private void ValidateForGoogle(CalendarEvent internalEvent, List<string> errors, List<string> warnings)
    {
        if (internalEvent.Title.Length > 1024)
            errors.Add("Google Calendar title cannot exceed 1024 characters");

        if (!string.IsNullOrEmpty(internalEvent.Attendees))
        {
            var attendees = EventAttendeeCollection.FromJson(internalEvent.Attendees);
            if (attendees.TotalCount > 200)
                warnings.Add("Google Calendar supports up to 200 attendees");
        }
    }

    private void ValidateForOutlook(CalendarEvent internalEvent, List<string> errors, List<string> warnings)
    {
        if (internalEvent.Title.Length > 255)
            errors.Add("Outlook title cannot exceed 255 characters");

        if (internalEvent.StartTime.Year < 1753)
            errors.Add("Outlook does not support dates before year 1753");
    }

    private void ValidateForICloud(CalendarEvent internalEvent, List<string> errors, List<string> warnings)
    {
        if (internalEvent.IsRecurring)
            warnings.Add("iCloud recurrence support may be limited");

        if (!string.IsNullOrEmpty(internalEvent.Attendees))
            warnings.Add("iCloud attendee support may be limited");
    }
}

// Supporting classes and enums would go here...
public record ExternalEventData
{
    public string? Id { get; init; }
    public string? CalendarId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public bool IsAllDay { get; init; }
    public string? Location { get; init; }
    public string? TimeZone { get; init; }
    public string Status { get; init; } = "confirmed";
    public string Visibility { get; init; } = "private";
    public string? ETag { get; init; }
    public ExternalRecurrenceData? Recurrence { get; init; }
    public IEnumerable<ExternalAttendeeData>? Attendees { get; init; }
    public IEnumerable<ExternalReminderData>? Reminders { get; init; }
    public Dictionary<string, object>? ProviderData { get; init; }
}

public record ExternalAttendeeData
{
    public string Email { get; init; } = "";
    public string? Name { get; init; }
    public string Status { get; init; } = "needsAction";
    public string Role { get; init; } = "required-participant";
    public bool IsOrganizer { get; init; }
    public bool IsOptional { get; init; }
}

public record ExternalReminderData
{
    public string Method { get; init; } = "display";
    public int MinutesBefore { get; init; }
    public string? CustomMessage { get; init; }
}

public record ExternalRecurrenceData
{
    public string Frequency { get; init; } = "DAILY";
    public int Interval { get; init; } = 1;
    public List<string>? DaysOfWeek { get; init; }
    public DateTime? EndDate { get; init; }
    public int? Count { get; init; }
}

public enum MergeStrategy
{
    PreserveInternal,
    PreferExternal,
    PreferInternal,
    PreferMostRecent,
    PreferDetailed
}

public record EventChangeDetectionResult(
    Guid InternalEventId,
    string? ExternalEventId,
    List<EventFieldChange> Changes,
    bool HasChanges,
    DateTime DetectedAt)
{
    public static EventChangeDetectionResult NoChanges() => new(Guid.Empty, null, new(), false, DateTime.UtcNow);
}

public record EventFieldChange(
    string FieldName,
    string? InternalValue,
    string? ExternalValue,
    EventFieldChangeType ChangeType);

public enum EventFieldChangeType
{
    Metadata,
    Minor,
    Important,
    Critical
}

public record MappingValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings);