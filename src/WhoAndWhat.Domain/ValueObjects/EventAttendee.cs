namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing an event attendee
/// </summary>
public sealed record EventAttendee
{
    private EventAttendee(
        string email,
        string? name,
        AttendeeStatus status,
        AttendeeRole role,
        bool isOrganizer,
        bool isResource,
        bool isOptional,
        string? responseMessage,
        DateTime? responseTime,
        Dictionary<string, object> metadata)
    {
        Email = email;
        Name = name;
        Status = status;
        Role = role;
        IsOrganizer = isOrganizer;
        IsResource = isResource;
        IsOptional = isOptional;
        ResponseMessage = responseMessage;
        ResponseTime = responseTime;
        Metadata = metadata;
    }

    /// <summary>
    /// Email address of the attendee (required)
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Display name of the attendee
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Response status of the attendee
    /// </summary>
    public AttendeeStatus Status { get; }

    /// <summary>
    /// Role of the attendee in the event
    /// </summary>
    public AttendeeRole Role { get; }

    /// <summary>
    /// Whether this attendee is the event organizer
    /// </summary>
    public bool IsOrganizer { get; }

    /// <summary>
    /// Whether this attendee represents a resource (room, equipment, etc.)
    /// </summary>
    public bool IsResource { get; }

    /// <summary>
    /// Whether attendance is optional for this attendee
    /// </summary>
    public bool IsOptional { get; }

    /// <summary>
    /// Message provided with the response
    /// </summary>
    public string? ResponseMessage { get; }

    /// <summary>
    /// When the response was given
    /// </summary>
    public DateTime? ResponseTime { get; }

    /// <summary>
    /// Additional metadata for the attendee
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the display name or email if name is not available
    /// </summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Email;

    /// <summary>
    /// Gets whether the attendee has responded to the invitation
    /// </summary>
    public bool HasResponded => Status != AttendeeStatus.NeedsAction;

    /// <summary>
    /// Gets whether the attendee has accepted the invitation
    /// </summary>
    public bool HasAccepted => Status == AttendeeStatus.Accepted;

    /// <summary>
    /// Gets whether the attendee has declined the invitation
    /// </summary>
    public bool HasDeclined => Status == AttendeeStatus.Declined;

    /// <summary>
    /// Gets whether the attendee response is tentative
    /// </summary>
    public bool IsTentative => Status == AttendeeStatus.Tentative;

    /// <summary>
    /// Gets whether this is a required attendee
    /// </summary>
    public bool IsRequired => !IsOptional;

    /// <summary>
    /// Gets whether this attendee can modify the event
    /// </summary>
    public bool CanModifyEvent => IsOrganizer || Role == AttendeeRole.Chair;

    /// <summary>
    /// Creates a new attendee
    /// </summary>
    public static EventAttendee Create(string email, string? name = null, 
        AttendeeStatus status = AttendeeStatus.NeedsAction, AttendeeRole role = AttendeeRole.RequiredParticipant)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required for attendee", nameof(email));

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email address", nameof(email));

        return new EventAttendee(
            email.Trim().ToLowerInvariant(),
            name?.Trim(),
            status,
            role,
            false,
            false,
            role == AttendeeRole.OptionalParticipant,
            null,
            null,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates an organizer attendee
    /// </summary>
    public static EventAttendee CreateOrganizer(string email, string? name = null)
    {
        var attendee = Create(email, name, AttendeeStatus.Accepted, AttendeeRole.Chair);
        return attendee with { IsOrganizer = true };
    }

    /// <summary>
    /// Creates a required participant
    /// </summary>
    public static EventAttendee CreateRequired(string email, string? name = null, 
        AttendeeStatus status = AttendeeStatus.NeedsAction)
    {
        return Create(email, name, status, AttendeeRole.RequiredParticipant);
    }

    /// <summary>
    /// Creates an optional participant
    /// </summary>
    public static EventAttendee CreateOptional(string email, string? name = null, 
        AttendeeStatus status = AttendeeStatus.NeedsAction)
    {
        return Create(email, name, status, AttendeeRole.OptionalParticipant);
    }

    /// <summary>
    /// Creates a resource attendee (room, equipment, etc.)
    /// </summary>
    public static EventAttendee CreateResource(string email, string name, 
        AttendeeStatus status = AttendeeStatus.Accepted)
    {
        var attendee = Create(email, name, status, AttendeeRole.NonParticipant);
        return attendee with { IsResource = true };
    }

    /// <summary>
    /// Updates the attendee's response status
    /// </summary>
    public EventAttendee WithResponse(AttendeeStatus newStatus, string? message = null)
    {
        if (Status == newStatus && ResponseMessage == message)
            return this;

        return this with 
        { 
            Status = newStatus, 
            ResponseMessage = message,
            ResponseTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Accepts the invitation
    /// </summary>
    public EventAttendee Accept(string? message = null) => WithResponse(AttendeeStatus.Accepted, message);

    /// <summary>
    /// Declines the invitation
    /// </summary>
    public EventAttendee Decline(string? message = null) => WithResponse(AttendeeStatus.Declined, message);

    /// <summary>
    /// Marks as tentative
    /// </summary>
    public EventAttendee MarkTentative(string? message = null) => WithResponse(AttendeeStatus.Tentative, message);

    /// <summary>
    /// Updates the attendee's name
    /// </summary>
    public EventAttendee WithName(string? newName)
    {
        return this with { Name = newName?.Trim() };
    }

    /// <summary>
    /// Updates the attendee's role
    /// </summary>
    public EventAttendee WithRole(AttendeeRole newRole)
    {
        var isOptional = newRole == AttendeeRole.OptionalParticipant;
        return this with { Role = newRole, IsOptional = isOptional };
    }

    /// <summary>
    /// Adds metadata to the attendee
    /// </summary>
    public EventAttendee WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Gets a description of the attendee's status
    /// </summary>
    public string StatusDescription => Status switch
    {
        AttendeeStatus.NeedsAction => "Invitation pending",
        AttendeeStatus.Accepted => HasResponded ? $"Accepted{GetResponseTimeText()}" : "Accepted",
        AttendeeStatus.Declined => HasResponded ? $"Declined{GetResponseTimeText()}" : "Declined", 
        AttendeeStatus.Tentative => HasResponded ? $"Maybe{GetResponseTimeText()}" : "Maybe",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the role description
    /// </summary>
    public string RoleDescription => Role switch
    {
        AttendeeRole.Chair => IsOrganizer ? "Organizer" : "Chair",
        AttendeeRole.RequiredParticipant => "Required",
        AttendeeRole.OptionalParticipant => "Optional",
        AttendeeRole.NonParticipant => IsResource ? "Resource" : "Non-Participant",
        _ => "Participant"
    };

    private string GetResponseTimeText()
    {
        if (!ResponseTime.HasValue) return string.Empty;
        
        var timeSpan = DateTime.UtcNow - ResponseTime.Value;
        if (timeSpan.TotalDays >= 1)
            return $" {(int)timeSpan.TotalDays}d ago";
        if (timeSpan.TotalHours >= 1)
            return $" {(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalMinutes >= 1)
            return $" {(int)timeSpan.TotalMinutes}m ago";
        return " just now";
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the attendee data
    /// </summary>
    public bool IsValid(out string? validationError)
    {
        validationError = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            validationError = "Email is required";
            return false;
        }

        if (!IsValidEmail(Email))
        {
            validationError = "Invalid email address";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Name) && Name.Length > 255)
        {
            validationError = "Name cannot exceed 255 characters";
            return false;
        }

        return true;
    }
}

/// <summary>
/// Collection of event attendees with helper methods
/// </summary>
public sealed record EventAttendeeCollection
{
    private readonly List<EventAttendee> _attendees;

    private EventAttendeeCollection(List<EventAttendee> attendees)
    {
        _attendees = attendees?.ToList() ?? new List<EventAttendee>();
    }

    /// <summary>
    /// Gets all attendees in the collection
    /// </summary>
    public IReadOnlyList<EventAttendee> Attendees => _attendees.AsReadOnly();

    /// <summary>
    /// Gets the event organizer
    /// </summary>
    public EventAttendee? Organizer => _attendees.FirstOrDefault(a => a.IsOrganizer);

    /// <summary>
    /// Gets required attendees
    /// </summary>
    public IEnumerable<EventAttendee> RequiredAttendees => 
        _attendees.Where(a => a.IsRequired && !a.IsOrganizer);

    /// <summary>
    /// Gets optional attendees
    /// </summary>
    public IEnumerable<EventAttendee> OptionalAttendees => 
        _attendees.Where(a => a.IsOptional);

    /// <summary>
    /// Gets resource attendees
    /// </summary>
    public IEnumerable<EventAttendee> Resources => 
        _attendees.Where(a => a.IsResource);

    /// <summary>
    /// Gets attendees who have accepted
    /// </summary>
    public IEnumerable<EventAttendee> AcceptedAttendees => 
        _attendees.Where(a => a.HasAccepted);

    /// <summary>
    /// Gets attendees who have declined
    /// </summary>
    public IEnumerable<EventAttendee> DeclinedAttendees => 
        _attendees.Where(a => a.HasDeclined);

    /// <summary>
    /// Gets attendees who haven't responded
    /// </summary>
    public IEnumerable<EventAttendee> PendingAttendees => 
        _attendees.Where(a => !a.HasResponded);

    /// <summary>
    /// Gets total attendee count
    /// </summary>
    public int TotalCount => _attendees.Count;

    /// <summary>
    /// Gets response statistics
    /// </summary>
    public AttendeeResponseStats GetResponseStats()
    {
        var total = _attendees.Count(a => !a.IsResource); // Exclude resources from stats
        var accepted = _attendees.Count(a => a.HasAccepted && !a.IsResource);
        var declined = _attendees.Count(a => a.HasDeclined && !a.IsResource);
        var tentative = _attendees.Count(a => a.IsTentative && !a.IsResource);
        var pending = _attendees.Count(a => !a.HasResponded && !a.IsResource);

        return new AttendeeResponseStats(total, accepted, declined, tentative, pending);
    }

    /// <summary>
    /// Creates an empty collection
    /// </summary>
    public static EventAttendeeCollection Empty => new(new List<EventAttendee>());

    /// <summary>
    /// Creates a collection from a list of attendees
    /// </summary>
    public static EventAttendeeCollection Create(List<EventAttendee> attendees) => new(attendees);

    /// <summary>
    /// Creates a collection with an organizer
    /// </summary>
    public static EventAttendeeCollection CreateWithOrganizer(string organizerEmail, string? organizerName = null)
    {
        var organizer = EventAttendee.CreateOrganizer(organizerEmail, organizerName);
        return new EventAttendeeCollection(new List<EventAttendee> { organizer });
    }

    /// <summary>
    /// Adds an attendee to the collection
    /// </summary>
    public EventAttendeeCollection Add(EventAttendee attendee)
    {
        if (_attendees.Any(a => a.Email.Equals(attendee.Email, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Attendee with email {attendee.Email} already exists");

        var newAttendees = new List<EventAttendee>(_attendees) { attendee };
        return new EventAttendeeCollection(newAttendees);
    }

    /// <summary>
    /// Removes an attendee from the collection
    /// </summary>
    public EventAttendeeCollection Remove(string email)
    {
        var newAttendees = _attendees.Where(a => !a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();
        return new EventAttendeeCollection(newAttendees);
    }

    /// <summary>
    /// Updates an attendee in the collection
    /// </summary>
    public EventAttendeeCollection Update(string email, EventAttendee updatedAttendee)
    {
        var newAttendees = _attendees.Select(a => 
            a.Email.Equals(email, StringComparison.OrdinalIgnoreCase) ? updatedAttendee : a).ToList();
        return new EventAttendeeCollection(newAttendees);
    }

    /// <summary>
    /// Updates an attendee's response
    /// </summary>
    public EventAttendeeCollection UpdateResponse(string email, AttendeeStatus status, string? message = null)
    {
        var attendee = _attendees.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (attendee == null)
            throw new ArgumentException($"Attendee with email {email} not found");

        var updatedAttendee = attendee.WithResponse(status, message);
        return Update(email, updatedAttendee);
    }

    /// <summary>
    /// Gets attendee by email
    /// </summary>
    public EventAttendee? GetByEmail(string email)
    {
        return _attendees.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if an email is in the attendee list
    /// </summary>
    public bool Contains(string email)
    {
        return _attendees.Any(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates all attendees in the collection
    /// </summary>
    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        if (!_attendees.Any(a => a.IsOrganizer))
        {
            validationErrors.Add("Event must have an organizer");
        }

        foreach (var attendee in _attendees)
        {
            if (!attendee.IsValid(out var error) && !string.IsNullOrEmpty(error))
            {
                validationErrors.Add($"{attendee.Email}: {error}");
            }
        }

        var duplicateEmails = _attendees.GroupBy(a => a.Email.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var email in duplicateEmails)
        {
            validationErrors.Add($"Duplicate email address: {email}");
        }

        return !validationErrors.Any();
    }

    /// <summary>
    /// Converts to JSON for storage
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(_attendees);
    }

    /// <summary>
    /// Creates from JSON
    /// </summary>
    public static EventAttendeeCollection FromJson(string json)
    {
        try
        {
            var attendees = System.Text.Json.JsonSerializer.Deserialize<List<EventAttendee>>(json);
            return new EventAttendeeCollection(attendees ?? new List<EventAttendee>());
        }
        catch
        {
            return Empty;
        }
    }
}

/// <summary>
/// Statistics about attendee responses
/// </summary>
public sealed record AttendeeResponseStats(
    int Total,
    int Accepted,
    int Declined,
    int Tentative,
    int Pending)
{
    /// <summary>
    /// Response rate (percentage of attendees who have responded)
    /// </summary>
    public double ResponseRate => Total > 0 ? (double)(Accepted + Declined + Tentative) / Total * 100 : 0;

    /// <summary>
    /// Acceptance rate (percentage of total attendees who accepted)
    /// </summary>
    public double AcceptanceRate => Total > 0 ? (double)Accepted / Total * 100 : 0;

    /// <summary>
    /// Whether all attendees have responded
    /// </summary>
    public bool AllResponded => Pending == 0;

    /// <summary>
    /// Whether majority has accepted
    /// </summary>
    public bool MajorityAccepted => Accepted > Total / 2;
}

/// <summary>
/// Attendee response status
/// </summary>
public enum AttendeeStatus
{
    NeedsAction = 0,    // Hasn't responded yet
    Accepted = 1,       // Accepted the invitation
    Declined = 2,       // Declined the invitation
    Tentative = 3       // Maybe attending
}

/// <summary>
/// Attendee role in the event
/// </summary>
public enum AttendeeRole
{
    Chair = 0,                  // Event chair/leader
    RequiredParticipant = 1,    // Required participant
    OptionalParticipant = 2,    // Optional participant
    NonParticipant = 3          // Information only (or resource)
}