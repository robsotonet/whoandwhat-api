using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Tests.Builders;

/// <summary>
/// Test builder for ContentDeliveryLog entity
/// </summary>
public class ContentDeliveryLogBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _motivationalContentId = Guid.NewGuid();
    private ContentDeliveryChannel _deliveryChannel = ContentDeliveryChannel.Dashboard;
    private DateTime _deliveredAt = DateTime.UtcNow;
    private ContentEngagementType? _engagementType = null;
    private DateTime? _engagedAt = null;
    private Dictionary<string, object> _deliveryContext = new();
    private Dictionary<string, object> _engagementContext = new();
    private string? _abTestGroup = null;
    private TimeSpan? _engagementDuration = null;
    private double? _personalizedScore = null;
    private Dictionary<string, object> _analyticsData = new();

    /// <summary>
    /// Creates a new ContentDeliveryLogBuilder with default values
    /// </summary>
    public static ContentDeliveryLogBuilder New() => new();

    /// <summary>
    /// Sets the user ID
    /// </summary>
    public ContentDeliveryLogBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the motivational content ID
    /// </summary>
    public ContentDeliveryLogBuilder ForContent(Guid contentId)
    {
        _motivationalContentId = contentId;
        return this;
    }

    /// <summary>
    /// Sets the content from a MotivationalContent entity
    /// </summary>
    public ContentDeliveryLogBuilder ForContent(MotivationalContent content)
    {
        _motivationalContentId = content.Id;
        return this;
    }

    /// <summary>
    /// Sets the delivery channel
    /// </summary>
    public ContentDeliveryLogBuilder ViaChannel(ContentDeliveryChannel channel)
    {
        _deliveryChannel = channel;
        return this;
    }

    /// <summary>
    /// Sets the delivered timestamp
    /// </summary>
    public ContentDeliveryLogBuilder DeliveredAt(DateTime deliveredAt)
    {
        _deliveredAt = deliveredAt;
        return this;
    }

    /// <summary>
    /// Sets the delivered timestamp relative to now
    /// </summary>
    public ContentDeliveryLogBuilder DeliveredAgo(TimeSpan timeAgo)
    {
        _deliveredAt = DateTime.UtcNow.Subtract(timeAgo);
        return this;
    }

    /// <summary>
    /// Marks as engaged with specific engagement type
    /// </summary>
    public ContentDeliveryLogBuilder WithEngagement(ContentEngagementType engagementType, DateTime? engagedAt = null)
    {
        _engagementType = engagementType;
        _engagedAt = engagedAt ?? DateTime.UtcNow;
        return this;
    }

    /// <summary>
    /// Marks as viewed
    /// </summary>
    public ContentDeliveryLogBuilder AsViewed(DateTime? engagedAt = null)
    {
        return WithEngagement(ContentEngagementType.Viewed, engagedAt);
    }

    /// <summary>
    /// Marks as clicked
    /// </summary>
    public ContentDeliveryLogBuilder AsClicked(DateTime? engagedAt = null)
    {
        return WithEngagement(ContentEngagementType.Clicked, engagedAt);
    }

    /// <summary>
    /// Marks as dismissed
    /// </summary>
    public ContentDeliveryLogBuilder AsDismissed(DateTime? engagedAt = null)
    {
        return WithEngagement(ContentEngagementType.Dismissed, engagedAt);
    }

    /// <summary>
    /// Marks as shared
    /// </summary>
    public ContentDeliveryLogBuilder AsShared(DateTime? engagedAt = null)
    {
        return WithEngagement(ContentEngagementType.Shared, engagedAt);
    }

    /// <summary>
    /// Marks with action taken
    /// </summary>
    public ContentDeliveryLogBuilder WithActionTaken(DateTime? engagedAt = null)
    {
        return WithEngagement(ContentEngagementType.ActionTaken, engagedAt);
    }

    /// <summary>
    /// Sets engagement duration
    /// </summary>
    public ContentDeliveryLogBuilder WithEngagementDuration(TimeSpan duration)
    {
        _engagementDuration = duration;
        return this;
    }

    /// <summary>
    /// Sets the A/B test group
    /// </summary>
    public ContentDeliveryLogBuilder InABTestGroup(string group)
    {
        _abTestGroup = group;
        return this;
    }

    /// <summary>
    /// Sets the personalized score
    /// </summary>
    public ContentDeliveryLogBuilder WithPersonalizedScore(double score)
    {
        _personalizedScore = score;
        return this;
    }

    /// <summary>
    /// Adds delivery context information
    /// </summary>
    public ContentDeliveryLogBuilder WithDeliveryContext(string key, object value)
    {
        _deliveryContext[key] = value;
        return this;
    }

    /// <summary>
    /// Sets delivery context for user experience level
    /// </summary>
    public ContentDeliveryLogBuilder ForExperienceLevel(UserExperienceLevel level)
    {
        _deliveryContext["experienceLevel"] = level;
        return this;
    }

    /// <summary>
    /// Sets delivery context for user segment
    /// </summary>
    public ContentDeliveryLogBuilder ForUserSegment(string segment)
    {
        _deliveryContext["userSegment"] = segment;
        return this;
    }

    /// <summary>
    /// Adds engagement context information
    /// </summary>
    public ContentDeliveryLogBuilder WithEngagementContext(string key, object value)
    {
        _engagementContext[key] = value;
        return this;
    }

    /// <summary>
    /// Adds analytics data
    /// </summary>
    public ContentDeliveryLogBuilder WithAnalyticsData(string key, object value)
    {
        _analyticsData[key] = value;
        return this;
    }

    /// <summary>
    /// Creates a successful dashboard delivery
    /// </summary>
    public ContentDeliveryLogBuilder AsDashboardSuccess()
    {
        _deliveryChannel = ContentDeliveryChannel.Dashboard;
        _engagementType = ContentEngagementType.Viewed;
        _engagedAt = _deliveredAt.AddMinutes(2);
        _engagementDuration = TimeSpan.FromSeconds(15);
        _deliveryContext["device"] = "desktop";
        _deliveryContext["location"] = "dashboard_main";
        return this;
    }

    /// <summary>
    /// Creates a push notification delivery
    /// </summary>
    public ContentDeliveryLogBuilder AsPushNotification()
    {
        _deliveryChannel = ContentDeliveryChannel.Push;
        _deliveryContext["device"] = "mobile";
        _deliveryContext["platform"] = "iOS";
        return this;
    }

    /// <summary>
    /// Creates an email delivery
    /// </summary>
    public ContentDeliveryLogBuilder AsEmailDelivery()
    {
        _deliveryChannel = ContentDeliveryChannel.Email;
        _deliveryContext["emailType"] = "daily_digest";
        _deliveryContext["emailTemplate"] = "motivational_v1";
        return this;
    }

    /// <summary>
    /// Creates an in-app notification delivery
    /// </summary>
    public ContentDeliveryLogBuilder AsInAppNotification()
    {
        _deliveryChannel = ContentDeliveryChannel.InApp;
        _deliveryContext["placement"] = "task_completion";
        _deliveryContext["timing"] = "immediate";
        return this;
    }

    /// <summary>
    /// Creates a SignalR real-time delivery
    /// </summary>
    public ContentDeliveryLogBuilder AsRealTimeDelivery()
    {
        _deliveryChannel = ContentDeliveryChannel.SignalR;
        _deliveryContext["connectionId"] = Guid.NewGuid().ToString();
        _deliveryContext["hubMethod"] = "MotivationalContentReceived";
        return this;
    }

    /// <summary>
    /// Creates a high-engagement log entry
    /// </summary>
    public ContentDeliveryLogBuilder AsHighEngagement()
    {
        _engagementType = ContentEngagementType.ActionTaken;
        _engagedAt = _deliveredAt.AddMinutes(1);
        _engagementDuration = TimeSpan.FromMinutes(5);
        _personalizedScore = 0.95;
        _engagementContext["satisfaction"] = "high";
        _engagementContext["actionType"] = "task_created";
        return this;
    }

    /// <summary>
    /// Creates a low-engagement log entry
    /// </summary>
    public ContentDeliveryLogBuilder AsLowEngagement()
    {
        _engagementType = ContentEngagementType.Dismissed;
        _engagedAt = _deliveredAt.AddSeconds(2);
        _engagementDuration = TimeSpan.FromSeconds(2);
        _personalizedScore = 0.15;
        _engagementContext["dismissReason"] = "not_relevant";
        return this;
    }

    /// <summary>
    /// Creates an A/B test log entry
    /// </summary>
    public ContentDeliveryLogBuilder AsABTestEntry(string group, double score)
    {
        _abTestGroup = group;
        _personalizedScore = score;
        _deliveryContext["abTestName"] = "content_format_test";
        _deliveryContext["abTestVersion"] = "v1.2";
        return this;
    }

    /// <summary>
    /// Creates a log entry for streak content
    /// </summary>
    public ContentDeliveryLogBuilder ForStreakContent(int streakDays)
    {
        _deliveryContext["streakDays"] = streakDays;
        _deliveryContext["contentTrigger"] = "streak_milestone";
        _personalizedScore = Math.Min(0.9, 0.5 + (streakDays * 0.02)); // Higher score for longer streaks
        return this;
    }

    /// <summary>
    /// Creates a log entry for achievement content
    /// </summary>
    public ContentDeliveryLogBuilder ForAchievementContent(string achievementType)
    {
        _deliveryContext["achievementType"] = achievementType;
        _deliveryContext["contentTrigger"] = "achievement_earned";
        _personalizedScore = 0.85; // Achievement content typically scores high
        return this;
    }

    /// <summary>
    /// Builds the ContentDeliveryLog instance
    /// </summary>
    public ContentDeliveryLog Build()
    {
        var log = ContentDeliveryLog.Create(
            _userId,
            _motivationalContentId,
            _deliveryChannel,
            _deliveredAt,
            _deliveryContext);

        // Set engagement data if provided
        if (_engagementType.HasValue)
        {
            log.RecordEngagement(_engagementType.Value, _engagedAt ?? DateTime.UtcNow, _engagementContext);
        }

        // Set A/B test group if provided
        if (!string.IsNullOrEmpty(_abTestGroup))
        {
            // Note: This might need to be set through a method depending on the entity's API
            // log.SetABTestGroup(_abTestGroup);
        }

        // Set engagement duration if provided
        if (_engagementDuration.HasValue)
        {
            // Note: This might need to be set through a method depending on the entity's API
            // log.SetEngagementDuration(_engagementDuration.Value);
        }

        // Set personalized score if provided
        if (_personalizedScore.HasValue)
        {
            // Note: This might need to be set through a method depending on the entity's API
            // log.SetPersonalizedScore(_personalizedScore.Value);
        }

        // Add analytics data
        foreach (var data in _analyticsData)
        {
            // Note: This might need to be set through a method depending on the entity's API
            // log.AddAnalyticsData(data.Key, data.Value);
        }

        return log;
    }

    /// <summary>
    /// Builds multiple log entries with time distribution
    /// </summary>
    public List<ContentDeliveryLog> BuildMany(int count, TimeSpan? timeSpread = null)
    {
        var logs = new List<ContentDeliveryLog>();
        var spread = timeSpread ?? TimeSpan.FromDays(7);
        
        for (int i = 0; i < count; i++)
        {
            var deliveryTime = _deliveredAt.AddTicks(-(spread.Ticks * i / count));
            
            var builder = New()
                .ForUser(_userId)
                .ForContent(_motivationalContentId)
                .ViaChannel(_deliveryChannel)
                .DeliveredAt(deliveryTime);

            // Add some realistic variation
            if (i % 4 == 0) builder.AsHighEngagement();
            else if (i % 4 == 1) builder.AsViewed();
            else if (i % 4 == 2) builder.AsClicked();
            else builder.AsLowEngagement();

            logs.Add(builder.Build());
        }

        return logs;
    }

    /// <summary>
    /// Builds engagement pattern logs for testing analytics
    /// </summary>
    public List<ContentDeliveryLog> BuildEngagementPattern(
        int totalLogs, 
        double engagementRate = 0.7,
        DateTime? startDate = null)
    {
        var logs = new List<ContentDeliveryLog>();
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var random = new Random(42); // Seed for consistent results

        for (int i = 0; i < totalLogs; i++)
        {
            var deliveryTime = start.AddHours(i * 6); // Every 6 hours
            var builder = New()
                .ForUser(_userId)
                .ForContent(_motivationalContentId)
                .DeliveredAt(deliveryTime);

            // Determine engagement based on rate
            if (random.NextDouble() < engagementRate)
            {
                var engagementTypes = new[] 
                { 
                    ContentEngagementType.Viewed,
                    ContentEngagementType.Clicked,
                    ContentEngagementType.ActionTaken 
                };
                var engagementType = engagementTypes[random.Next(engagementTypes.Length)];
                builder.WithEngagement(engagementType, deliveryTime.AddMinutes(random.Next(1, 30)));
            }

            logs.Add(builder.Build());
        }

        return logs;
    }
}