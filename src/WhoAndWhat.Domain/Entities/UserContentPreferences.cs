using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing user preferences for motivational content delivery
/// </summary>
public class UserContentPreferences : BaseEntity
{
    public Guid UserId { get; private set; }
    public bool IsContentEnabled { get; private set; }
    public ContentFrequency PreferredFrequency { get; private set; }
    public HashSet<MotivationalContentType> PreferredContentTypes { get; private set; }
    public HashSet<ContentCategory> PreferredCategories { get; private set; }
    public HashSet<ContentDeliveryChannel> PreferredChannels { get; private set; }
    public Dictionary<string, TimeSpan> PreferredDeliveryTimes { get; private set; }
    public Dictionary<string, object> PersonalizationSettings { get; private set; }
    public DateTime? LastContentDelivery { get; private set; }
    public DateTime? ContentPausedUntil { get; private set; }
    public int MaxDailyContent { get; private set; }
    public int MaxWeeklyContent { get; private set; }
    public bool AllowWeekends { get; private set; }
    public bool AllowAfterHours { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public Dictionary<string, object> EngagementHistory { get; private set; }

    // Protected constructor for EF Core
    protected UserContentPreferences()
    {
        PreferredContentTypes = new HashSet<MotivationalContentType>();
        PreferredCategories = new HashSet<ContentCategory>();
        PreferredChannels = new HashSet<ContentDeliveryChannel>();
        PreferredDeliveryTimes = new Dictionary<string, TimeSpan>();
        PersonalizationSettings = new Dictionary<string, object>();
        EngagementHistory = new Dictionary<string, object>();
        PreferredFrequency = ContentFrequency.Moderate;
        MaxDailyContent = 3;
        MaxWeeklyContent = 15;
        AllowWeekends = true;
        AllowAfterHours = false;
    }

    private UserContentPreferences(Guid userId) : this()
    {
        UserId = userId;
        IsContentEnabled = true;

        // Default preferred content types
        PreferredContentTypes.Add(MotivationalContentType.Insight);
        PreferredContentTypes.Add(MotivationalContentType.Achievement);
        PreferredContentTypes.Add(MotivationalContentType.Encouragement);

        // Default preferred categories
        PreferredCategories.Add(ContentCategory.Productivity);
        PreferredCategories.Add(ContentCategory.Motivation);

        // Default preferred channels
        PreferredChannels.Add(ContentDeliveryChannel.Dashboard);
        PreferredChannels.Add(ContentDeliveryChannel.InApp);

        // Default delivery times (morning and afternoon)
        PreferredDeliveryTimes["morning"] = new TimeSpan(9, 0, 0);   // 9:00 AM
        PreferredDeliveryTimes["afternoon"] = new TimeSpan(14, 0, 0); // 2:00 PM
    }

    /// <summary>
    /// Creates default content preferences for a user
    /// </summary>
    public static UserContentPreferences CreateDefault(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        var preferences = new UserContentPreferences(userId);
        preferences.AddDomainEvent(new UserContentPreferencesCreatedEvent(preferences));

        return preferences;
    }

    /// <summary>
    /// Enables or disables content delivery for the user
    /// </summary>
    public void SetContentEnabled(bool enabled)
    {
        if (IsContentEnabled != enabled)
        {
            IsContentEnabled = enabled;
            MarkAsModified();

            if (enabled)
            {
                ContentPausedUntil = null;
                AddDomainEvent(new UserContentEnabledEvent(this));
            }
            else
            {
                AddDomainEvent(new UserContentDisabledEvent(this));
            }
        }
    }

    /// <summary>
    /// Sets the preferred content delivery frequency
    /// </summary>
    public void SetPreferredFrequency(ContentFrequency frequency)
    {
        if (PreferredFrequency != frequency)
        {
            PreferredFrequency = frequency;

            // Adjust max daily/weekly content based on frequency
            (MaxDailyContent, MaxWeeklyContent) = frequency switch
            {
                ContentFrequency.Low => (1, 5),
                ContentFrequency.Moderate => (3, 15),
                ContentFrequency.High => (5, 25),
                ContentFrequency.VeryHigh => (8, 40),
                _ => (3, 15)
            };

            MarkAsModified();
            AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
        }
    }

    /// <summary>
    /// Sets preferred content types
    /// </summary>
    public void SetPreferredContentTypes(IEnumerable<MotivationalContentType> contentTypes)
    {
        PreferredContentTypes.Clear();
        foreach (var contentType in contentTypes)
        {
            PreferredContentTypes.Add(contentType);
        }
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets preferred content categories
    /// </summary>
    public void SetPreferredCategories(IEnumerable<ContentCategory> categories)
    {
        PreferredCategories.Clear();
        foreach (var category in categories)
        {
            PreferredCategories.Add(category);
        }
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets preferred delivery channels
    /// </summary>
    public void SetPreferredChannels(IEnumerable<ContentDeliveryChannel> channels)
    {
        PreferredChannels.Clear();
        foreach (var channel in channels)
        {
            PreferredChannels.Add(channel);
        }
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets preferred delivery times
    /// </summary>
    public void SetPreferredDeliveryTimes(Dictionary<string, TimeSpan> deliveryTimes)
    {
        PreferredDeliveryTimes = deliveryTimes ?? new Dictionary<string, TimeSpan>();
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets maximum content limits
    /// </summary>
    public void SetContentLimits(int maxDaily, int maxWeekly)
    {
        if (maxDaily <= 0 || maxWeekly <= 0)
        {
            throw new ArgumentException("Content limits must be positive");
        }

        if (maxDaily > maxWeekly)
        {
            throw new ArgumentException("Daily limit cannot exceed weekly limit");
        }

        MaxDailyContent = maxDaily;
        MaxWeeklyContent = maxWeekly;
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets weekend and after-hours preferences
    /// </summary>
    public void SetSchedulingPreferences(bool allowWeekends, bool allowAfterHours)
    {
        AllowWeekends = allowWeekends;
        AllowAfterHours = allowAfterHours;
        MarkAsModified();
        AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
    }

    /// <summary>
    /// Sets the user's timezone
    /// </summary>
    public void SetTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("Timezone cannot be empty", nameof(timeZone));
        }

        // Validate timezone (basic validation)
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            TimeZone = timeZone;
            MarkAsModified();
            AddDomainEvent(new UserContentPreferencesUpdatedEvent(this));
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Invalid timezone: {timeZone}", nameof(timeZone));
        }
    }

    /// <summary>
    /// Temporarily pauses content delivery until a specific date
    /// </summary>
    public void PauseContentUntil(DateTime pauseUntil)
    {
        if (pauseUntil <= DateTime.UtcNow)
        {
            throw new ArgumentException("Pause date must be in the future");
        }

        ContentPausedUntil = pauseUntil;
        MarkAsModified();
        AddDomainEvent(new UserContentPausedEvent(this, pauseUntil));
    }

    /// <summary>
    /// Resumes content delivery (removes pause)
    /// </summary>
    public void ResumeContent()
    {
        if (ContentPausedUntil.HasValue)
        {
            ContentPausedUntil = null;
            MarkAsModified();
            AddDomainEvent(new UserContentResumedEvent(this));
        }
    }

    /// <summary>
    /// Records the last content delivery time
    /// </summary>
    public void RecordContentDelivery(DateTime deliveryTime)
    {
        LastContentDelivery = deliveryTime;
        MarkAsModified();
    }

    /// <summary>
    /// Updates personalization settings
    /// </summary>
    public void UpdatePersonalizationSetting(string key, object value)
    {
        PersonalizationSettings[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets a personalization setting value
    /// </summary>
    public T? GetPersonalizationSetting<T>(string key)
    {
        if (PersonalizationSettings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Updates engagement history for learning user preferences
    /// </summary>
    public void UpdateEngagementHistory(string key, object value)
    {
        EngagementHistory[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets engagement history value for the specified key
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve</typeparam>
    /// <param name="key">The key to search for in engagement history</param>
    /// <returns>The value if found and can be cast to T, otherwise null for nullable types or default for non-nullable types</returns>
    /// <remarks>
    /// This method returns a nullable version of the requested type (T?). 
    /// For value types like double, int, etc., it returns null when the key is not found or the value cannot be cast to the specified type.
    /// For reference types like string, it returns null when the key is not found.
    /// Used extensively in content scoring calculations where historical engagement data may or may not be available.
    /// </remarks>
    public T? GetEngagementHistory<T>(string key)
    {
        if (EngagementHistory.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T?);
    }

    /// <summary>
    /// Checks if content can be delivered now based on preferences
    /// </summary>
    public bool CanDeliverContentNow(ContentDeliveryChannel channel, MotivationalContentType contentType)
    {
        // Check if content is enabled
        if (!IsContentEnabled)
        {
            return false;
        }

        // Check if content is paused
        if (ContentPausedUntil.HasValue && DateTime.UtcNow < ContentPausedUntil.Value)
        {
            return false;
        }

        // Check if channel is preferred
        if (!PreferredChannels.Contains(channel))
        {
            return false;
        }

        // Check if content type is preferred
        if (!PreferredContentTypes.Contains(contentType))
        {
            return false;
        }

        // Check time constraints
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZone));

        // Check weekend constraint
        if (!AllowWeekends && (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday))
        {
            return false;
        }

        // Check after-hours constraint (assuming work hours are 8 AM to 6 PM)
        if (!AllowAfterHours && (now.Hour < 8 || now.Hour >= 18))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the next preferred delivery time
    /// </summary>
    public DateTime? GetNextPreferredDeliveryTime()
    {
        if (!IsContentEnabled || !PreferredDeliveryTimes.Any())
        {
            return null;
        }

        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);
        var today = now.Date;

        // Find next delivery time today
        foreach (var deliveryTime in PreferredDeliveryTimes.Values.OrderBy(t => t))
        {
            var deliveryDateTime = today.Add(deliveryTime);
            if (deliveryDateTime > now)
            {
                return TimeZoneInfo.ConvertTimeToUtc(deliveryDateTime, userTimeZone);
            }
        }

        // No more delivery times today, use first delivery time tomorrow
        var tomorrow = today.AddDays(1);
        var firstDeliveryTime = PreferredDeliveryTimes.Values.Min();
        var nextDeliveryDateTime = tomorrow.Add(firstDeliveryTime);

        return TimeZoneInfo.ConvertTimeToUtc(nextDeliveryDateTime, userTimeZone);
    }

    /// <summary>
    /// Calculates a personalized preference score for motivational content based on user preferences and historical engagement
    /// </summary>
    /// <param name="contentType">The type of motivational content (e.g., Achievement, Tip, Encouragement)</param>
    /// <param name="category">The content category (e.g., Productivity, General, Wellness)</param>
    /// <returns>A normalized score between 0.0 and 1.0, where higher values indicate better personalization match</returns>
    /// <remarks>
    /// <para>The scoring algorithm works as follows:</para>
    /// <list type="number">
    /// <item><description>Base score: 0.5 (neutral starting point)</description></item>
    /// <item><description>Content type preference: +0.3 if the content type is in user's preferred types</description></item>
    /// <item><description>Category preference: +0.2 if the category is in user's preferred categories</description></item>
    /// <item><description>Historical engagement: If engagement history exists for this content type, the score is averaged with the historical performance</description></item>
    /// <item><description>Normalization: Final score is clamped between 0.0 and 1.0</description></item>
    /// </list>
    /// <para>This method integrates with the engagement history system to provide adaptive personalization that improves over time based on user interactions.</para>
    /// </remarks>
    public double CalculateContentScore(MotivationalContentType contentType, ContentCategory category)
    {
        double score = 0.5; // Base score

        // Boost score if content type is preferred
        if (PreferredContentTypes.Contains(contentType))
        {
            score += 0.3;
        }

        // Boost score if category is preferred
        if (PreferredCategories.Contains(category))
        {
            score += 0.2;
        }

        // Apply engagement history if available
        double? contentTypeHistory = GetEngagementHistory<double>($"score_{contentType}");
        if (contentTypeHistory.HasValue)
        {
            score = (score + contentTypeHistory.Value) / 2; // Average with historical performance
        }

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    public override bool CanSoftDelete()
    {
        // User preferences should generally not be deleted to maintain user experience
        return false;
    }
}

/// <summary>
/// Enumeration of content delivery frequencies
/// </summary>
public enum ContentFrequency
{
    Low = 0,        // 1 per day, 5 per week max
    Moderate = 1,   // 3 per day, 15 per week max  
    High = 2,       // 5 per day, 25 per week max
    VeryHigh = 3    // 8 per day, 40 per week max
}

/// <summary>
/// Domain events for user content preferences
/// </summary>
public record UserContentPreferencesCreatedEvent(UserContentPreferences Preferences) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record UserContentPreferencesUpdatedEvent(UserContentPreferences Preferences) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record UserContentEnabledEvent(UserContentPreferences Preferences) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record UserContentDisabledEvent(UserContentPreferences Preferences) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record UserContentPausedEvent(UserContentPreferences Preferences, DateTime PausedUntil) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

public record UserContentResumedEvent(UserContentPreferences Preferences) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
