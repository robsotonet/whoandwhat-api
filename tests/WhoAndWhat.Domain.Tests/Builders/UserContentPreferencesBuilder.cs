using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Tests.Builders;

/// <summary>
/// Test builder for UserContentPreferences entity
/// </summary>
public class UserContentPreferencesBuilder
{
    private Guid _userId = Guid.NewGuid();
    private bool _isContentEnabled = true;
    private ContentFrequency _preferredFrequency = ContentFrequency.Moderate;
    private HashSet<MotivationalContentType> _preferredContentTypes = new();
    private HashSet<ContentCategory> _preferredCategories = new();
    private HashSet<ContentDeliveryChannel> _preferredChannels = new();
    private Dictionary<string, TimeSpan> _preferredDeliveryTimes = new();
    private Dictionary<string, object> _personalizationSettings = new();
    private DateTime? _lastContentDelivery = null;
    private DateTime? _contentPausedUntil = null;
    private int _maxDailyContent = 3;
    private int _maxWeeklyContent = 15;
    private bool _allowWeekends = true;
    private bool _allowAfterHours = false;
    private string _timeZone = "UTC";
    private Dictionary<string, object> _engagementHistory = new();

    /// <summary>
    /// Creates a new UserContentPreferencesBuilder with default values
    /// </summary>
    public static UserContentPreferencesBuilder New() => new();

    /// <summary>
    /// Sets the user ID
    /// </summary>
    public UserContentPreferencesBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Disables content delivery
    /// </summary>
    public UserContentPreferencesBuilder WithContentDisabled()
    {
        _isContentEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the preferred frequency
    /// </summary>
    public UserContentPreferencesBuilder WithFrequency(ContentFrequency frequency)
    {
        _preferredFrequency = frequency;

        // Automatically adjust limits based on frequency
        (_maxDailyContent, _maxWeeklyContent) = frequency switch
        {
            ContentFrequency.Low => (1, 5),
            ContentFrequency.Moderate => (3, 15),
            ContentFrequency.High => (5, 25),
            ContentFrequency.VeryHigh => (8, 40),
            _ => (3, 15)
        };

        return this;
    }

    /// <summary>
    /// Sets preferred content types
    /// </summary>
    public UserContentPreferencesBuilder WithContentTypes(params MotivationalContentType[] contentTypes)
    {
        _preferredContentTypes = new HashSet<MotivationalContentType>(contentTypes);
        return this;
    }

    /// <summary>
    /// Adds a single content type to preferences
    /// </summary>
    public UserContentPreferencesBuilder AddContentType(MotivationalContentType contentType)
    {
        _preferredContentTypes.Add(contentType);
        return this;
    }

    /// <summary>
    /// Sets preferred categories
    /// </summary>
    public UserContentPreferencesBuilder WithCategories(params ContentCategory[] categories)
    {
        _preferredCategories = new HashSet<ContentCategory>(categories);
        return this;
    }

    /// <summary>
    /// Adds a single category to preferences
    /// </summary>
    public UserContentPreferencesBuilder AddCategory(ContentCategory category)
    {
        _preferredCategories.Add(category);
        return this;
    }

    /// <summary>
    /// Sets preferred delivery channels
    /// </summary>
    public UserContentPreferencesBuilder WithChannels(params ContentDeliveryChannel[] channels)
    {
        _preferredChannels = new HashSet<ContentDeliveryChannel>(channels);
        return this;
    }

    /// <summary>
    /// Adds a single delivery channel to preferences
    /// </summary>
    public UserContentPreferencesBuilder AddChannel(ContentDeliveryChannel channel)
    {
        _preferredChannels.Add(channel);
        return this;
    }

    /// <summary>
    /// Sets preferred delivery times
    /// </summary>
    public UserContentPreferencesBuilder WithDeliveryTimes(Dictionary<string, TimeSpan> deliveryTimes)
    {
        _preferredDeliveryTimes = new Dictionary<string, TimeSpan>(deliveryTimes);
        return this;
    }

    /// <summary>
    /// Adds a delivery time preference
    /// </summary>
    public UserContentPreferencesBuilder AddDeliveryTime(string name, TimeSpan time)
    {
        _preferredDeliveryTimes[name] = time;
        return this;
    }

    /// <summary>
    /// Sets standard morning and afternoon delivery times
    /// </summary>
    public UserContentPreferencesBuilder WithStandardDeliveryTimes()
    {
        _preferredDeliveryTimes = new Dictionary<string, TimeSpan>
        {
            ["morning"] = new TimeSpan(9, 0, 0),     // 9:00 AM
            ["afternoon"] = new TimeSpan(14, 0, 0)   // 2:00 PM
        };
        return this;
    }

    /// <summary>
    /// Sets content limits
    /// </summary>
    public UserContentPreferencesBuilder WithContentLimits(int maxDaily, int maxWeekly)
    {
        _maxDailyContent = maxDaily;
        _maxWeeklyContent = maxWeekly;
        return this;
    }

    /// <summary>
    /// Allows weekend delivery
    /// </summary>
    public UserContentPreferencesBuilder AllowingWeekends()
    {
        _allowWeekends = true;
        return this;
    }

    /// <summary>
    /// Disallows weekend delivery
    /// </summary>
    public UserContentPreferencesBuilder DisallowingWeekends()
    {
        _allowWeekends = false;
        return this;
    }

    /// <summary>
    /// Allows after-hours delivery
    /// </summary>
    public UserContentPreferencesBuilder AllowingAfterHours()
    {
        _allowAfterHours = true;
        return this;
    }

    /// <summary>
    /// Disallows after-hours delivery
    /// </summary>
    public UserContentPreferencesBuilder DisallowingAfterHours()
    {
        _allowAfterHours = false;
        return this;
    }

    /// <summary>
    /// Sets the timezone
    /// </summary>
    public UserContentPreferencesBuilder InTimeZone(string timeZone)
    {
        _timeZone = timeZone;
        return this;
    }

    /// <summary>
    /// Sets last content delivery time
    /// </summary>
    public UserContentPreferencesBuilder LastDeliveredAt(DateTime lastDelivery)
    {
        _lastContentDelivery = lastDelivery;
        return this;
    }

    /// <summary>
    /// Pauses content until specified date
    /// </summary>
    public UserContentPreferencesBuilder PausedUntil(DateTime pauseUntil)
    {
        _contentPausedUntil = pauseUntil;
        return this;
    }

    /// <summary>
    /// Adds a personalization setting
    /// </summary>
    public UserContentPreferencesBuilder WithPersonalizationSetting(string key, object value)
    {
        _personalizationSettings[key] = value;
        return this;
    }

    /// <summary>
    /// Adds engagement history
    /// </summary>
    public UserContentPreferencesBuilder WithEngagementHistory(string key, object value)
    {
        _engagementHistory[key] = value;
        return this;
    }

    /// <summary>
    /// Creates beginner user preferences
    /// </summary>
    public UserContentPreferencesBuilder AsBeginner()
    {
        _preferredFrequency = ContentFrequency.Low;
        _maxDailyContent = 1;
        _maxWeeklyContent = 5;
        _preferredContentTypes = new HashSet<MotivationalContentType>
        {
            MotivationalContentType.Encouragement,
            MotivationalContentType.Tip
        };
        _preferredCategories = new HashSet<ContentCategory>
        {
            ContentCategory.Motivation,
            ContentCategory.Productivity
        };
        _preferredChannels = new HashSet<ContentDeliveryChannel>
        {
            ContentDeliveryChannel.Dashboard,
            ContentDeliveryChannel.InApp
        };
        return this;
    }

    /// <summary>
    /// Creates intermediate user preferences
    /// </summary>
    public UserContentPreferencesBuilder AsIntermediate()
    {
        _preferredFrequency = ContentFrequency.Moderate;
        _maxDailyContent = 3;
        _maxWeeklyContent = 15;
        _preferredContentTypes = new HashSet<MotivationalContentType>
        {
            MotivationalContentType.Insight,
            MotivationalContentType.Achievement,
            MotivationalContentType.Tip
        };
        _preferredCategories = new HashSet<ContentCategory>
        {
            ContentCategory.Productivity,
            ContentCategory.Learning,
            ContentCategory.Achievement
        };
        _preferredChannels = new HashSet<ContentDeliveryChannel>
        {
            ContentDeliveryChannel.Dashboard,
            ContentDeliveryChannel.InApp,
            ContentDeliveryChannel.Push
        };
        return this;
    }

    /// <summary>
    /// Creates expert user preferences
    /// </summary>
    public UserContentPreferencesBuilder AsExpert()
    {
        _preferredFrequency = ContentFrequency.High;
        _maxDailyContent = 5;
        _maxWeeklyContent = 25;
        _preferredContentTypes = new HashSet<MotivationalContentType>
        {
            MotivationalContentType.Insight,
            MotivationalContentType.Achievement,
            MotivationalContentType.Challenge,
            MotivationalContentType.Streak
        };
        _preferredCategories = new HashSet<ContentCategory>
        {
            ContentCategory.Productivity,
            ContentCategory.Learning,
            ContentCategory.Achievement,
            ContentCategory.Gamification
        };
        _preferredChannels = new HashSet<ContentDeliveryChannel>
        {
            ContentDeliveryChannel.Dashboard,
            ContentDeliveryChannel.InApp,
            ContentDeliveryChannel.Push,
            ContentDeliveryChannel.Email
        };
        _allowAfterHours = true;
        return this;
    }

    /// <summary>
    /// Creates wellness-focused preferences
    /// </summary>
    public UserContentPreferencesBuilder AsWellnessFocused()
    {
        _preferredFrequency = ContentFrequency.Low;
        _maxDailyContent = 2;
        _maxWeeklyContent = 10;
        _preferredContentTypes = new HashSet<MotivationalContentType>
        {
            MotivationalContentType.Encouragement,
            MotivationalContentType.Reminder
        };
        _preferredCategories = new HashSet<ContentCategory>
        {
            ContentCategory.Wellness,
            ContentCategory.Motivation
        };
        _preferredChannels = new HashSet<ContentDeliveryChannel>
        {
            ContentDeliveryChannel.Dashboard,
            ContentDeliveryChannel.InApp
        };
        _allowWeekends = false;
        _allowAfterHours = false;
        return this;
    }

    /// <summary>
    /// Creates high-engagement preferences with extensive history
    /// </summary>
    public UserContentPreferencesBuilder AsHighEngagement()
    {
        _engagementHistory = new Dictionary<string, object>
        {
            ["score_Insight"] = 0.85,
            ["score_Achievement"] = 0.92,
            ["score_Tip"] = 0.78,
            ["totalEngagements"] = 150,
            ["averageEngagementRate"] = 0.83,
            ["preferredTimeOfDay"] = "morning",
            ["lastHighEngagement"] = DateTime.UtcNow.AddDays(-1)
        };
        return this;
    }

    /// <summary>
    /// Builds the UserContentPreferences instance
    /// </summary>
    public UserContentPreferences Build()
    {
        var preferences = UserContentPreferences.CreateDefault(_userId);

        // Apply all configurations
        preferences.SetContentEnabled(_isContentEnabled);
        preferences.SetPreferredFrequency(_preferredFrequency);

        if (_preferredContentTypes.Any())
        {
            preferences.SetPreferredContentTypes(_preferredContentTypes);
        }

        if (_preferredCategories.Any())
        {
            preferences.SetPreferredCategories(_preferredCategories);
        }

        if (_preferredChannels.Any())
        {
            preferences.SetPreferredChannels(_preferredChannels);
        }

        if (_preferredDeliveryTimes.Any())
        {
            preferences.SetPreferredDeliveryTimes(_preferredDeliveryTimes);
        }

        preferences.SetContentLimits(_maxDailyContent, _maxWeeklyContent);
        preferences.SetSchedulingPreferences(_allowWeekends, _allowAfterHours);
        preferences.SetTimeZone(_timeZone);

        if (_lastContentDelivery.HasValue)
        {
            preferences.RecordContentDelivery(_lastContentDelivery.Value);
        }

        if (_contentPausedUntil.HasValue)
        {
            preferences.PauseContentUntil(_contentPausedUntil.Value);
        }

        foreach (var setting in _personalizationSettings)
        {
            preferences.UpdatePersonalizationSetting(setting.Key, setting.Value);
        }

        foreach (var history in _engagementHistory)
        {
            preferences.UpdateEngagementHistory(history.Key, history.Value);
        }

        return preferences;
    }

    /// <summary>
    /// Builds multiple instances with different user IDs
    /// </summary>
    public List<UserContentPreferences> BuildMany(int count)
    {
        var preferences = new List<UserContentPreferences>();

        for (int i = 0; i < count; i++)
        {
            var builder = New()
                .ForUser(Guid.NewGuid())
                .WithFrequency(_preferredFrequency);

            // Add some variation
            if (i % 3 == 0)
            {
                builder.AsExpert();
            }
            else if (i % 3 == 1)
            {
                builder.AsIntermediate();
            }
            else
            {
                builder.AsBeginner();
            }

            preferences.Add(builder.Build());
        }

        return preferences;
    }
}
