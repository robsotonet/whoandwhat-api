using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing an event reminder
/// </summary>
public sealed record EventReminder
{
    private EventReminder(
        ReminderMethod method,
        TimeSpan offsetBefore,
        bool isEnabled,
        string? customMessage,
        ReminderPriority priority,
        bool isDefault,
        Dictionary<string, object> metadata)
    {
        Method = method;
        OffsetBefore = offsetBefore;
        IsEnabled = isEnabled;
        CustomMessage = customMessage;
        Priority = priority;
        IsDefault = isDefault;
        Metadata = metadata;
    }

    /// <summary>
    /// Method of reminder delivery
    /// </summary>
    public ReminderMethod Method { get; }

    /// <summary>
    /// Time before the event to trigger the reminder
    /// </summary>
    public TimeSpan OffsetBefore { get; }

    /// <summary>
    /// Whether this reminder is currently enabled
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Custom message for the reminder (if not using default)
    /// </summary>
    public string? CustomMessage { get; }

    /// <summary>
    /// Priority of the reminder
    /// </summary>
    public ReminderPriority Priority { get; }

    /// <summary>
    /// Whether this is a default reminder for the user
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Additional metadata for the reminder
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the reminder time relative to an event start time
    /// </summary>
    public DateTime GetReminderTime(DateTime eventStartTime)
    {
        return eventStartTime.Subtract(OffsetBefore);
    }

    /// <summary>
    /// Gets whether the reminder should be triggered at the given time for an event
    /// </summary>
    public bool ShouldTrigger(DateTime currentTime, DateTime eventStartTime)
    {
        if (!IsEnabled) return false;
        
        var reminderTime = GetReminderTime(eventStartTime);
        return currentTime >= reminderTime && currentTime < eventStartTime;
    }

    /// <summary>
    /// Gets a human-readable description of the reminder
    /// </summary>
    public string Description => GenerateDescription();

    /// <summary>
    /// Gets the reminder offset in minutes
    /// </summary>
    public int OffsetMinutes => (int)OffsetBefore.TotalMinutes;

    /// <summary>
    /// Gets whether this reminder is immediate (at event time)
    /// </summary>
    public bool IsImmediate => OffsetBefore <= TimeSpan.Zero;

    /// <summary>
    /// Gets whether this reminder requires internet connectivity
    /// </summary>
    public bool RequiresConnectivity => Method == ReminderMethod.Email || Method == ReminderMethod.SMS;

    /// <summary>
    /// Creates a popup reminder
    /// </summary>
    public static EventReminder CreatePopup(TimeSpan offsetBefore, bool isDefault = false, string? customMessage = null)
    {
        return new EventReminder(
            ReminderMethod.Popup,
            offsetBefore,
            true,
            customMessage,
            ReminderPriority.Normal,
            isDefault,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates an email reminder
    /// </summary>
    public static EventReminder CreateEmail(TimeSpan offsetBefore, string? customMessage = null, 
        bool isDefault = false, ReminderPriority priority = ReminderPriority.Normal)
    {
        return new EventReminder(
            ReminderMethod.Email,
            offsetBefore,
            true,
            customMessage,
            priority,
            isDefault,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates an SMS reminder
    /// </summary>
    public static EventReminder CreateSMS(TimeSpan offsetBefore, string? customMessage = null, 
        bool isDefault = false, ReminderPriority priority = ReminderPriority.High)
    {
        return new EventReminder(
            ReminderMethod.SMS,
            offsetBefore,
            true,
            customMessage,
            priority,
            isDefault,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates a push notification reminder
    /// </summary>
    public static EventReminder CreatePushNotification(TimeSpan offsetBefore, string? customMessage = null, 
        bool isDefault = false, ReminderPriority priority = ReminderPriority.Normal)
    {
        return new EventReminder(
            ReminderMethod.Push,
            offsetBefore,
            true,
            customMessage,
            priority,
            isDefault,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates an in-app notification reminder
    /// </summary>
    public static EventReminder CreateInAppNotification(TimeSpan offsetBefore, string? customMessage = null, 
        bool isDefault = false)
    {
        return new EventReminder(
            ReminderMethod.InApp,
            offsetBefore,
            true,
            customMessage,
            ReminderPriority.Normal,
            isDefault,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates a sound/audio reminder
    /// </summary>
    public static EventReminder CreateSound(TimeSpan offsetBefore, string? soundFile = null, 
        bool isDefault = false)
    {
        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(soundFile))
            metadata["soundFile"] = soundFile;

        return new EventReminder(
            ReminderMethod.Sound,
            offsetBefore,
            true,
            null,
            ReminderPriority.Normal,
            isDefault,
            metadata
        );
    }

    /// <summary>
    /// Creates a reminder with custom metadata
    /// </summary>
    public static EventReminder Create(ReminderMethod method, TimeSpan offsetBefore, 
        string? customMessage = null, ReminderPriority priority = ReminderPriority.Normal,
        bool isDefault = false, Dictionary<string, object>? metadata = null)
    {
        return new EventReminder(
            method,
            offsetBefore,
            true,
            customMessage,
            priority,
            isDefault,
            metadata ?? new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Enables the reminder
    /// </summary>
    public EventReminder Enable()
    {
        if (IsEnabled) return this;
        
        return new EventReminder(Method, OffsetBefore, true, CustomMessage, Priority, IsDefault, Metadata);
    }

    /// <summary>
    /// Disables the reminder
    /// </summary>
    public EventReminder Disable()
    {
        if (!IsEnabled) return this;
        
        return new EventReminder(Method, OffsetBefore, false, CustomMessage, Priority, IsDefault, Metadata);
    }

    /// <summary>
    /// Updates the reminder offset
    /// </summary>
    public EventReminder WithOffset(TimeSpan newOffset)
    {
        if (newOffset < TimeSpan.Zero)
            throw new ArgumentException("Reminder offset cannot be negative", nameof(newOffset));
            
        return new EventReminder(Method, newOffset, IsEnabled, CustomMessage, Priority, IsDefault, Metadata);
    }

    /// <summary>
    /// Updates the reminder message
    /// </summary>
    public EventReminder WithMessage(string? message)
    {
        return new EventReminder(Method, OffsetBefore, IsEnabled, message, Priority, IsDefault, Metadata);
    }

    /// <summary>
    /// Updates the reminder priority
    /// </summary>
    public EventReminder WithPriority(ReminderPriority newPriority)
    {
        return new EventReminder(Method, OffsetBefore, IsEnabled, CustomMessage, newPriority, IsDefault, Metadata);
    }

    /// <summary>
    /// Sets or unsets this reminder as default
    /// </summary>
    public EventReminder AsDefault(bool isDefault = true)
    {
        return new EventReminder(Method, OffsetBefore, IsEnabled, CustomMessage, Priority, isDefault, Metadata);
    }

    /// <summary>
    /// Adds metadata to the reminder
    /// </summary>
    public EventReminder WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return new EventReminder(Method, OffsetBefore, IsEnabled, CustomMessage, Priority, IsDefault, newMetadata);
    }

    /// <summary>
    /// Gets a metadata value by key
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Generates common reminder presets
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// 5 minutes before - popup
        /// </summary>
        public static EventReminder FiveMinutePopup => CreatePopup(TimeSpan.FromMinutes(5));

        /// <summary>
        /// 15 minutes before - popup
        /// </summary>
        public static EventReminder FifteenMinutePopup => CreatePopup(TimeSpan.FromMinutes(15));

        /// <summary>
        /// 30 minutes before - popup
        /// </summary>
        public static EventReminder ThirtyMinutePopup => CreatePopup(TimeSpan.FromMinutes(30));

        /// <summary>
        /// 1 hour before - email
        /// </summary>
        public static EventReminder OneHourEmail => CreateEmail(TimeSpan.FromHours(1));

        /// <summary>
        /// 1 day before - email
        /// </summary>
        public static EventReminder OneDayEmail => CreateEmail(TimeSpan.FromDays(1));

        /// <summary>
        /// 1 week before - email
        /// </summary>
        public static EventReminder OneWeekEmail => CreateEmail(TimeSpan.FromDays(7));

        /// <summary>
        /// 10 minutes before - push notification
        /// </summary>
        public static EventReminder TenMinutePush => CreatePushNotification(TimeSpan.FromMinutes(10));

        /// <summary>
        /// At event start - popup
        /// </summary>
        public static EventReminder AtStartPopup => CreatePopup(TimeSpan.Zero);

        /// <summary>
        /// Default reminder set for meetings
        /// </summary>
        public static List<EventReminder> DefaultMeetingReminders => new()
        {
            FifteenMinutePopup.AsDefault(),
            OneHourEmail
        };

        /// <summary>
        /// Default reminder set for appointments
        /// </summary>
        public static List<EventReminder> DefaultAppointmentReminders => new()
        {
            ThirtyMinutePopup.AsDefault(),
            OneDayEmail
        };

        /// <summary>
        /// Default reminder set for tasks
        /// </summary>
        public static List<EventReminder> DefaultTaskReminders => new()
        {
            TenMinutePush.AsDefault()
        };

        /// <summary>
        /// High priority reminders
        /// </summary>
        public static List<EventReminder> HighPriorityReminders => new()
        {
            CreateSMS(TimeSpan.FromHours(1), priority: ReminderPriority.Critical),
            CreatePushNotification(TimeSpan.FromMinutes(30), priority: ReminderPriority.High),
            FifteenMinutePopup.WithPriority(ReminderPriority.High)
        };
    }

    private string GenerateDescription()
    {
        var timeDescription = OffsetBefore.TotalMinutes switch
        {
            0 => "at event start",
            < 60 => $"{(int)OffsetBefore.TotalMinutes} minutes before",
            < 1440 => $"{(int)OffsetBefore.TotalHours} hours before",
            _ => $"{(int)OffsetBefore.TotalDays} days before"
        };

        var methodDescription = Method switch
        {
            ReminderMethod.Popup => "popup reminder",
            ReminderMethod.Email => "email reminder",
            ReminderMethod.SMS => "SMS reminder",
            ReminderMethod.Push => "push notification",
            ReminderMethod.InApp => "in-app notification",
            ReminderMethod.Sound => "sound alert",
            _ => "reminder"
        };

        var result = $"{methodDescription} {timeDescription}";
        
        if (Priority != ReminderPriority.Normal)
        {
            result = $"{Priority.ToString().ToLower()} priority {result}";
        }

        if (!IsEnabled)
        {
            result = $"disabled {result}";
        }

        return result;
    }

    /// <summary>
    /// Creates a collection of reminders from time offsets
    /// </summary>
    public static List<EventReminder> CreateMultiple(ReminderMethod method, params TimeSpan[] offsets)
    {
        return offsets.Select(offset => Create(method, offset)).ToList();
    }

    /// <summary>
    /// Creates default reminders based on event type
    /// </summary>
    public static List<EventReminder> CreateForEventType(EventType eventType)
    {
        return eventType switch
        {
            EventType.Meeting => Presets.DefaultMeetingReminders,
            EventType.Appointment => Presets.DefaultAppointmentReminders,
            EventType.Task => Presets.DefaultTaskReminders,
            EventType.Personal => new List<EventReminder> { Presets.ThirtyMinutePopup.AsDefault() },
            EventType.Work => new List<EventReminder> { Presets.FifteenMinutePopup.AsDefault(), Presets.OneHourEmail },
            _ => new List<EventReminder> { Presets.FifteenMinutePopup.AsDefault() }
        };
    }

    /// <summary>
    /// Validates reminder configuration
    /// </summary>
    public bool IsValid(out string? validationError)
    {
        validationError = null;

        if (OffsetBefore < TimeSpan.Zero)
        {
            validationError = "Reminder offset cannot be negative";
            return false;
        }

        if (OffsetBefore > TimeSpan.FromDays(365))
        {
            validationError = "Reminder offset cannot be more than 1 year";
            return false;
        }

        if (Method == ReminderMethod.SMS && string.IsNullOrWhiteSpace(CustomMessage))
        {
            // SMS reminders might need validation for message content
            if (CustomMessage?.Length > 160)
            {
                validationError = "SMS reminder message cannot exceed 160 characters";
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Collection of event reminders with helper methods
/// </summary>
public sealed record EventReminderCollection
{
    private readonly List<EventReminder> _reminders;

    private EventReminderCollection(List<EventReminder> reminders)
    {
        _reminders = reminders?.ToList() ?? new List<EventReminder>();
    }

    /// <summary>
    /// Gets all reminders in the collection
    /// </summary>
    public IReadOnlyList<EventReminder> Reminders => _reminders.AsReadOnly();

    /// <summary>
    /// Gets only enabled reminders
    /// </summary>
    public IEnumerable<EventReminder> EnabledReminders => _reminders.Where(r => r.IsEnabled);

    /// <summary>
    /// Gets reminders by method
    /// </summary>
    public IEnumerable<EventReminder> GetByMethod(ReminderMethod method) => 
        _reminders.Where(r => r.Method == method);

    /// <summary>
    /// Gets the default reminder
    /// </summary>
    public EventReminder? DefaultReminder => _reminders.FirstOrDefault(r => r.IsDefault);

    /// <summary>
    /// Gets reminders that should trigger at a specific time
    /// </summary>
    public IEnumerable<EventReminder> GetTriggeredReminders(DateTime currentTime, DateTime eventStartTime) =>
        EnabledReminders.Where(r => r.ShouldTrigger(currentTime, eventStartTime));

    /// <summary>
    /// Gets the next reminder to trigger
    /// </summary>
    public EventReminder? GetNextReminder(DateTime currentTime, DateTime eventStartTime)
    {
        return EnabledReminders
            .Where(r => r.GetReminderTime(eventStartTime) > currentTime)
            .OrderBy(r => r.GetReminderTime(eventStartTime))
            .FirstOrDefault();
    }

    /// <summary>
    /// Creates an empty collection
    /// </summary>
    public static EventReminderCollection Empty => new(new List<EventReminder>());

    /// <summary>
    /// Creates a collection from a list of reminders
    /// </summary>
    public static EventReminderCollection Create(List<EventReminder> reminders) => new(reminders);

    /// <summary>
    /// Creates a collection from individual reminders
    /// </summary>
    public static EventReminderCollection Create(params EventReminder[] reminders) => new(reminders.ToList());

    /// <summary>
    /// Adds a reminder to the collection
    /// </summary>
    public EventReminderCollection Add(EventReminder reminder)
    {
        var newReminders = new List<EventReminder>(_reminders) { reminder };
        return new EventReminderCollection(newReminders);
    }

    /// <summary>
    /// Removes a reminder from the collection
    /// </summary>
    public EventReminderCollection Remove(EventReminder reminder)
    {
        var newReminders = _reminders.Where(r => !r.Equals(reminder)).ToList();
        return new EventReminderCollection(newReminders);
    }

    /// <summary>
    /// Updates a reminder in the collection
    /// </summary>
    public EventReminderCollection Update(EventReminder oldReminder, EventReminder newReminder)
    {
        var newReminders = _reminders.Select(r => r.Equals(oldReminder) ? newReminder : r).ToList();
        return new EventReminderCollection(newReminders);
    }

    /// <summary>
    /// Validates all reminders in the collection
    /// </summary>
    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        
        foreach (var reminder in _reminders)
        {
            if (!reminder.IsValid(out var error) && !string.IsNullOrEmpty(error))
            {
                validationErrors.Add(error);
            }
        }

        return !validationErrors.Any();
    }

    /// <summary>
    /// Converts to JSON for storage
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(_reminders);
    }

    /// <summary>
    /// Creates from JSON
    /// </summary>
    public static EventReminderCollection FromJson(string json)
    {
        try
        {
            var reminders = System.Text.Json.JsonSerializer.Deserialize<List<EventReminder>>(json);
            return new EventReminderCollection(reminders ?? new List<EventReminder>());
        }
        catch
        {
            return Empty;
        }
    }
}

/// <summary>
/// Reminder delivery method
/// </summary>
public enum ReminderMethod
{
    Popup = 0,      // Desktop/browser popup
    Email = 1,      // Email notification
    SMS = 2,        // SMS text message
    Push = 3,       // Push notification
    InApp = 4,      // In-app notification
    Sound = 5       // Sound/audio alert
}

/// <summary>
/// Reminder priority level
/// </summary>
public enum ReminderPriority
{
    Low = 0,        // Low priority reminder
    Normal = 1,     // Normal priority reminder
    High = 2,       // High priority reminder
    Critical = 3    // Critical priority reminder
}