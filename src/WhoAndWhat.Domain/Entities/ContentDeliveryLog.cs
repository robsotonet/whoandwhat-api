using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity for tracking motivational content delivery and user engagement
/// </summary>
public class ContentDeliveryLog : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid MotivationalContentId { get; private set; }
    public DateTime DeliveredAt { get; private set; }
    public ContentDeliveryChannel DeliveryChannel { get; private set; }
    public string? ABTestGroup { get; private set; }
    public ContentEngagementType? EngagementType { get; private set; }
    public DateTime? EngagementAt { get; private set; }
    public Dictionary<string, object> DeliveryContext { get; private set; }
    public Dictionary<string, object> EngagementMetadata { get; private set; }
    public bool WasPersonalized { get; private set; }
    public double? PersonalizationScore { get; private set; }
    public string? DeliveryMethod { get; private set; }
    public TimeSpan? ViewDuration { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceType { get; private set; }

    // Navigation properties
    public MotivationalContent? MotivationalContent { get; private set; }

    // Protected constructor for EF Core
    protected ContentDeliveryLog()
    {
        DeliveryContext = new Dictionary<string, object>();
        EngagementMetadata = new Dictionary<string, object>();
        AnalyticsData = new Dictionary<string, object>();
    }

    private ContentDeliveryLog(
        Guid userId,
        Guid motivationalContentId,
        ContentDeliveryChannel deliveryChannel,
        string? abTestGroup = null) : this()
    {
        UserId = userId;
        MotivationalContentId = motivationalContentId;
        DeliveredAt = DateTime.UtcNow;
        DeliveryChannel = deliveryChannel;
        ABTestGroup = abTestGroup;
        WasPersonalized = false;
    }

    /// <summary>
    /// Creates a new content delivery log entry
    /// </summary>
    public static ContentDeliveryLog Create(
        Guid userId,
        Guid motivationalContentId,
        ContentDeliveryChannel deliveryChannel,
        string? abTestGroup = null,
        Dictionary<string, object>? deliveryContext = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (motivationalContentId == Guid.Empty)
        {
            throw new ArgumentException("Motivational content ID cannot be empty", nameof(motivationalContentId));
        }

        var log = new ContentDeliveryLog(userId, motivationalContentId, deliveryChannel, abTestGroup);

        if (deliveryContext != null)
        {
            log.DeliveryContext = deliveryContext;
        }

        log.AddDomainEvent(new ContentDeliveredEvent(log));
        return log;
    }

    /// <summary>
    /// Records user engagement with the delivered content
    /// </summary>
    public void RecordEngagement(
        ContentEngagementType engagementType,
        Dictionary<string, object>? engagementMetadata = null,
        TimeSpan? viewDuration = null)
    {
        if (EngagementType.HasValue)
        {
            // Already has engagement recorded, update if it's a more significant engagement
            if (engagementType > EngagementType.Value)
            {
                EngagementType = engagementType;
                EngagementAt = DateTime.UtcNow;
            }
        }
        else
        {
            EngagementType = engagementType;
            EngagementAt = DateTime.UtcNow;
        }

        if (engagementMetadata != null)
        {
            // Merge new metadata with existing
            foreach (var kvp in engagementMetadata)
            {
                EngagementMetadata[kvp.Key] = kvp.Value;
            }
        }

        if (viewDuration.HasValue)
        {
            ViewDuration = viewDuration.Value;
        }

        MarkAsModified();
        AddDomainEvent(new ContentEngagedEvent(this, engagementType));
    }

    /// <summary>
    /// Marks the content as personalized with a score
    /// </summary>
    public void MarkAsPersonalized(double personalizationScore, string? deliveryMethod = null)
    {
        WasPersonalized = true;
        PersonalizationScore = Math.Max(0.0, Math.Min(1.0, personalizationScore));
        DeliveryMethod = deliveryMethod;
        MarkAsModified();
    }

    /// <summary>
    /// Sets device and user agent information
    /// </summary>
    public void SetDeviceInfo(string? userAgent, string? deviceType)
    {
        UserAgent = userAgent;
        DeviceType = deviceType;
        MarkAsModified();
    }

    /// <summary>
    /// Adds delivery context information
    /// </summary>
    public void AddDeliveryContext(string key, object value)
    {
        DeliveryContext[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets delivery context value
    /// </summary>
    public T? GetDeliveryContext<T>(string key)
    {
        if (DeliveryContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Adds engagement metadata
    /// </summary>
    public void AddEngagementMetadata(string key, object value)
    {
        EngagementMetadata[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets engagement metadata value
    /// </summary>
    public T? GetEngagementMetadata<T>(string key)
    {
        if (EngagementMetadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Calculates the time since content was delivered
    /// </summary>
    public TimeSpan TimeSinceDelivery()
    {
        return DateTime.UtcNow - DeliveredAt;
    }

    /// <summary>
    /// Gets the engagement latency (time from delivery to engagement)
    /// </summary>
    public TimeSpan? GetEngagementLatency()
    {
        if (!EngagementAt.HasValue)
        {
            return null;
        }

        return EngagementAt.Value - DeliveredAt;
    }

    /// <summary>
    /// Determines if the content delivery was successful based on engagement
    /// </summary>
    public bool IsSuccessfulDelivery()
    {
        return EngagementType.HasValue && 
               EngagementType.Value >= ContentEngagementType.Viewed;
    }

    /// <summary>
    /// Gets the engagement score for analytics (0-1 scale)
    /// </summary>
    public double GetEngagementScore()
    {
        return EngagementType switch
        {
            ContentEngagementType.Dismissed => 0.1,
            ContentEngagementType.Viewed => 0.3,
            ContentEngagementType.Clicked => 0.6,
            ContentEngagementType.Shared => 0.8,
            ContentEngagementType.ActionTaken => 1.0,
            null => 0.0,
            _ => 0.0
        };
    }

    /// <summary>
    /// Creates a summary for analytics
    /// </summary>
    public ContentDeliveryAnalytics ToAnalytics()
    {
        return new ContentDeliveryAnalytics
        {
            Id = Id,
            UserId = UserId,
            MotivationalContentId = MotivationalContentId,
            DeliveredAt = DeliveredAt,
            DeliveryChannel = DeliveryChannel.ToString(),
            ABTestGroup = ABTestGroup,
            EngagementType = EngagementType?.ToString(),
            EngagementAt = EngagementAt,
            EngagementLatency = GetEngagementLatency(),
            EngagementScore = GetEngagementScore(),
            WasPersonalized = WasPersonalized,
            PersonalizationScore = PersonalizationScore,
            ViewDuration = ViewDuration,
            DeviceType = DeviceType,
            IsSuccessful = IsSuccessfulDelivery()
        };
    }

    /// <summary>
    /// Convenience property for accessing engagement time
    /// </summary>
    public DateTime? EngagedAt => EngagementAt;

    /// <summary>
    /// Convenience property for accessing personalization score
    /// </summary>
    public double? PersonalizedScore => PersonalizationScore;

    /// <summary>
    /// Convenience property for accessing engagement metadata  
    /// </summary>
    public Dictionary<string, object> EngagementContext => EngagementMetadata;

    /// <summary>
    /// Analytics data collection for advanced tracking
    /// </summary>
    public Dictionary<string, object> AnalyticsData { get; private set; } = new();

    /// <summary>
    /// Gets the engagement duration if available
    /// </summary>
    public TimeSpan? GetEngagementDuration() => ViewDuration;

    /// <summary>
    /// Checks if content has been engaged with
    /// </summary>
    public bool IsEngaged() => EngagementType.HasValue;

    /// <summary>
    /// Sets the A/B test group for this delivery
    /// </summary>
    public void SetABTestGroup(string abTestGroup)
    {
        ABTestGroup = abTestGroup;
        MarkAsModified();
    }

    /// <summary>
    /// Sets the personalization score for this delivery
    /// </summary>
    public void SetPersonalizedScore(double score)
    {
        PersonalizationScore = Math.Max(0.0, Math.Min(1.0, score));
        WasPersonalized = true;
        MarkAsModified();
    }

    /// <summary>
    /// Adds analytics data
    /// </summary>
    public void AddAnalyticsData(string key, object value)
    {
        AnalyticsData[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Updates delivery context information
    /// </summary>
    public void UpdateDeliveryContext(Dictionary<string, object> context)
    {
        foreach (var kvp in context)
        {
            DeliveryContext[kvp.Key] = kvp.Value;
        }
        MarkAsModified();
    }

    /// <summary>
    /// Updates delivery context information with a single key-value pair
    /// </summary>
    public void UpdateDeliveryContext(string key, object value)
    {
        DeliveryContext[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets delivery context value by key
    /// </summary>
    public T? GetDeliveryContextValue<T>(string key)
    {
        if (DeliveryContext.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Gets delivery context value by key (non-generic)
    /// </summary>
    public object? GetDeliveryContextValue(string key)
    {
        return DeliveryContext.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Checks if content was delivered within a specified time window
    /// </summary>
    public bool IsDeliveredWithinTime(TimeSpan timeWindow)
    {
        return DateTime.UtcNow - DeliveredAt <= timeWindow;
    }

    /// <summary>
    /// Gets the time since content was delivered
    /// </summary>
    public TimeSpan GetTimeSinceDelivery()
    {
        return DateTime.UtcNow - DeliveredAt;
    }

    /// <summary>
    /// Calculates engagement rate based on content type and engagement
    /// </summary>
    public double CalculateEngagementRate()
    {
        if (!EngagementType.HasValue)
        {
            return 0.0;
        }

        return GetEngagementScore();
    }

    /// <summary>
    /// Determines if this represents high engagement
    /// </summary>
    public bool IsHighEngagement()
    {
        return EngagementType.HasValue && 
               EngagementType.Value >= ContentEngagementType.Clicked &&
               GetEngagementScore() >= 0.6;
    }

    public override bool CanSoftDelete()
    {
        // Delivery logs should generally not be deleted to maintain analytics history
        return false;
    }
}

/// <summary>
/// Enumeration of content delivery channels
/// </summary>
public enum ContentDeliveryChannel
{
    Dashboard = 0,      // Delivered via dashboard
    Push = 1,           // Push notification
    Email = 2,          // Email notification
    InApp = 3,          // In-app notification
    SignalR = 4,        // Real-time via SignalR
    API = 5,            // Direct API request
    Background = 6      // Background scheduled delivery
}

/// <summary>
/// Enumeration of user engagement types (ordered by significance)
/// </summary>
public enum ContentEngagementType
{
    Dismissed = 0,      // User dismissed the content
    Viewed = 1,         // User viewed the content
    Clicked = 2,        // User clicked on the content
    Shared = 3,         // User shared the content
    ActionTaken = 4     // User took the suggested action
}

/// <summary>
/// Analytics model for content delivery
/// </summary>
public class ContentDeliveryAnalytics
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MotivationalContentId { get; set; }
    public DateTime DeliveredAt { get; set; }
    public string DeliveryChannel { get; set; } = string.Empty;
    public string? ABTestGroup { get; set; }
    public string? EngagementType { get; set; }
    public DateTime? EngagementAt { get; set; }
    public TimeSpan? EngagementLatency { get; set; }
    public double EngagementScore { get; set; }
    public bool WasPersonalized { get; set; }
    public double? PersonalizationScore { get; set; }
    public TimeSpan? ViewDuration { get; set; }
    public string? DeviceType { get; set; }
    public bool IsSuccessful { get; set; }
}

/// <summary>
/// Domain events for content delivery
/// </summary>
public record ContentDeliveredEvent(ContentDeliveryLog DeliveryLog) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record ContentEngagedEvent(ContentDeliveryLog DeliveryLog, ContentEngagementType EngagementType) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}