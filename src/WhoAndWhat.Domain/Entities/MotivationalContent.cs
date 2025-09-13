using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing motivational content for user engagement and productivity
/// </summary>
public class MotivationalContent : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public MotivationalContentType ContentType { get; private set; }
    public ContentCategory Category { get; private set; }
    public Dictionary<string, object> TargetConditions { get; private set; }
    public Dictionary<string, object> ABTestConfiguration { get; private set; }
    public Dictionary<string, object> SchedulingRules { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsABTestEnabled { get; private set; }
    public string ABTestGroup { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? ActionUrl { get; private set; }
    public string? ActionText { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; }

    // Protected constructor for EF Core
    protected MotivationalContent()
    {
        TargetConditions = new Dictionary<string, object>();
        ABTestConfiguration = new Dictionary<string, object>();
        SchedulingRules = new Dictionary<string, object>();
        Metadata = new Dictionary<string, object>();
    }

    private MotivationalContent(string title, string message, MotivationalContentType contentType, ContentCategory category) : this()
    {
        Title = title;
        Message = message;
        ContentType = contentType;
        Category = category;
        IsActive = true;
        IsABTestEnabled = false;
        Priority = 0;
    }

    /// <summary>
    /// Creates new motivational content
    /// </summary>
    public static MotivationalContent Create(
        string title,
        string message,
        MotivationalContentType contentType,
        ContentCategory category,
        Dictionary<string, object>? targetConditions = null,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        var content = new MotivationalContent(title, message, contentType, category);

        if (targetConditions != null)
        {
            content.TargetConditions = targetConditions;
        }

        content.Priority = priority;
        content.AddDomainEvent(new MotivationalContentCreatedEvent(content));

        return content;
    }

    /// <summary>
    /// Updates the content message and title
    /// </summary>
    public void UpdateContent(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        Title = title;
        Message = message;
        MarkAsModified();

        AddDomainEvent(new MotivationalContentUpdatedEvent(this));
    }

    /// <summary>
    /// Configures target conditions for content delivery
    /// </summary>
    public void SetTargetConditions(Dictionary<string, object> conditions)
    {
        TargetConditions = conditions ?? new Dictionary<string, object>();
        MarkAsModified();
    }

    /// <summary>
    /// Adds or updates a specific target condition
    /// </summary>
    public void SetTargetCondition(string key, object value)
    {
        TargetConditions[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets a target condition value
    /// </summary>
    public T? GetTargetCondition<T>(string key)
    {
        if (TargetConditions.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Adds a target condition (alias for SetTargetCondition)
    /// </summary>
    public void AddTargetCondition(string key, object value)
    {
        SetTargetCondition(key, value);
    }

    /// <summary>
    /// Configures A/B testing for this content
    /// </summary>
    public void ConfigureABTest(string testGroup, Dictionary<string, object>? configuration = null)
    {
        if (string.IsNullOrWhiteSpace(testGroup))
        {
            throw new ArgumentException("Test group cannot be empty", nameof(testGroup));
        }

        IsABTestEnabled = true;
        ABTestGroup = testGroup;
        ABTestConfiguration = configuration ?? new Dictionary<string, object>();
        MarkAsModified();

        AddDomainEvent(new MotivationalContentABTestConfiguredEvent(this, testGroup));
    }

    /// <summary>
    /// Disables A/B testing for this content
    /// </summary>
    public void DisableABTest()
    {
        IsABTestEnabled = false;
        ABTestGroup = string.Empty;
        ABTestConfiguration.Clear();
        MarkAsModified();
    }

    /// <summary>
    /// Sets content scheduling rules
    /// </summary>
    public void SetSchedulingRules(Dictionary<string, object> rules)
    {
        SchedulingRules = rules ?? new Dictionary<string, object>();
        MarkAsModified();
    }

    /// <summary>
    /// Sets content active period
    /// </summary>
    public void SetActivePeriod(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && startDate.Value >= endDate.Value)
        {
            throw new ArgumentException("Start date must be before end date");
        }

        StartDate = startDate;
        EndDate = endDate;
        MarkAsModified();
    }

    /// <summary>
    /// Sets content action (URL and text for clickable content)
    /// </summary>
    public void SetAction(string? actionUrl, string? actionText)
    {
        ActionUrl = actionUrl;
        ActionText = actionText;
        MarkAsModified();
    }

    /// <summary>
    /// Sets content image URL
    /// </summary>
    public void SetImageUrl(string? imageUrl)
    {
        ImageUrl = imageUrl;
        MarkAsModified();
    }

    /// <summary>
    /// Updates content priority for display ordering
    /// </summary>
    public void SetPriority(int priority)
    {
        Priority = priority;
        MarkAsModified();
    }

    /// <summary>
    /// Activates the content
    /// </summary>
    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            MarkAsModified();
            AddDomainEvent(new MotivationalContentActivatedEvent(this));
        }
    }

    /// <summary>
    /// Deactivates the content
    /// </summary>
    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            MarkAsModified();
            AddDomainEvent(new MotivationalContentDeactivatedEvent(this));
        }
    }

    /// <summary>
    /// Updates metadata for the content
    /// </summary>
    public void UpdateMetadata(string key, object value)
    {
        Metadata[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets metadata value
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Checks if content is currently active based on date constraints
    /// </summary>
    public bool IsCurrentlyActive()
    {
        if (!IsActive)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        if (StartDate.HasValue && now < StartDate.Value)
        {
            return false;
        }

        if (EndDate.HasValue && now > EndDate.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if content matches the given user conditions
    /// </summary>
    public bool MatchesUserConditions(Dictionary<string, object> userConditions)
    {
        if (TargetConditions.Count == 0)
        {
            return true; // No conditions means it matches all users
        }

        foreach (var condition in TargetConditions)
        {
            if (!userConditions.TryGetValue(condition.Key, out var userValue))
            {
                return false; // Required condition not present in user data
            }

            // Handle different condition types
            switch (condition.Key.ToLower())
            {
                case "mincompletionrate":
                    if (userValue is double userRate && condition.Value is double minRate)
                    {
                        if (userRate < minRate)
                        {
                            return false;
                        }
                    }
                    break;

                case "minstreakdays":
                    if (userValue is int userStreak && condition.Value is int minStreak)
                    {
                        if (userStreak < minStreak)
                        {
                            return false;
                        }
                    }
                    break;

                case "experiencelevel":
                    if (userValue is UserExperienceLevel userLevel && condition.Value is UserExperienceLevel requiredLevel)
                    {
                        if (userLevel < requiredLevel)
                        {
                            return false;
                        }
                    }
                    break;

                case "categories":
                    if (condition.Value is string[] allowedCategories && userValue is string userCategory)
                    {
                        if (!allowedCategories.Contains(userCategory))
                        {
                            return false;
                        }
                    }
                    break;

                default:
                    // Generic equality check for other conditions
                    if (!condition.Value.Equals(userValue))
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a display-ready version of the content
    /// </summary>
    public MotivationalContentDisplay ToDisplay()
    {
        return new MotivationalContentDisplay
        {
            Id = Id,
            Title = Title,
            Message = Message,
            ContentType = ContentType.ToString(),
            Category = Category.ToString(),
            ImageUrl = ImageUrl,
            ActionUrl = ActionUrl,
            ActionText = ActionText,
            Priority = Priority,
            ABTestGroup = IsABTestEnabled ? ABTestGroup : null,
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }

    /// <summary>
    /// Schedules content for a specific delivery time
    /// </summary>
    public void ScheduleFor(DateTime scheduledTime)
    {
        if (scheduledTime < DateTime.UtcNow)
        {
            throw new ArgumentException("Scheduled time cannot be in the past");
        }

        var schedulingRule = new Dictionary<string, object>
        {
            ["scheduledDelivery"] = scheduledTime,
            ["schedulingType"] = "specific_time"
        };

        SchedulingRules["specific_schedule"] = schedulingRule;
        MarkAsModified();
    }

    public override bool CanSoftDelete()
    {
        // Motivational content can be soft deleted but should retain delivery history
        return true;
    }
}

/// <summary>
/// Enumeration of motivational content types
/// </summary>
public enum MotivationalContentType
{
    Insight = 0,        // Personalized insights based on user data
    Streak = 1,         // Streak-related motivation
    Achievement = 2,    // Achievement celebrations
    Encouragement = 3,  // General encouragement messages
    Tip = 4,           // Productivity tips
    Challenge = 5,     // Productivity challenges
    Celebration = 6,   // Success celebrations
    Reminder = 7       // Gentle reminders and nudges
}

/// <summary>
/// Enumeration of content categories
/// </summary>
public enum ContentCategory
{
    Productivity = 0,
    Motivation = 1,
    Achievement = 2,
    Learning = 3,
    Wellness = 4,
    Social = 5,
    Gamification = 6
}

/// <summary>
/// Display model for motivational content
/// </summary>
public class MotivationalContentDisplay
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public int Priority { get; set; }
    public string? ABTestGroup { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Domain events for motivational content
/// </summary>
public record MotivationalContentCreatedEvent(MotivationalContent Content) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record MotivationalContentUpdatedEvent(MotivationalContent Content) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record MotivationalContentActivatedEvent(MotivationalContent Content) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record MotivationalContentDeactivatedEvent(MotivationalContent Content) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record MotivationalContentABTestConfiguredEvent(MotivationalContent Content, string TestGroup) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
