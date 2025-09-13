using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Tests.Builders;

namespace WhoAndWhat.Domain.Tests.Helpers;

/// <summary>
/// Helper class for motivational content testing operations
/// </summary>
public static class MotivationalContentTestHelper
{
    /// <summary>
    /// Creates a default test user ID
    /// </summary>
    public static Guid CreateTestUserId() => Guid.NewGuid();

    /// <summary>
    /// Creates multiple test user IDs
    /// </summary>
    public static List<Guid> CreateTestUserIds(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }

    /// <summary>
    /// Creates a comprehensive set of motivational content for testing
    /// </summary>
    public static List<MotivationalContent> CreateDiverseContentSet()
    {
        var contents = new List<MotivationalContent>();

        // Achievement content for different experience levels
        contents.Add(MotivationalContentBuilder.New().AsAchievement().ForExperienceLevel(UserExperienceLevel.Beginner).Build());
        contents.Add(MotivationalContentBuilder.New().AsAchievement().ForExperienceLevel(UserExperienceLevel.Intermediate).Build());
        contents.Add(MotivationalContentBuilder.New().AsAchievement().ForExperienceLevel(UserExperienceLevel.Expert).Build());

        // Productivity tips
        contents.Add(MotivationalContentBuilder.New().AsProductivityTip().WithPriority(85).Build());
        contents.Add(MotivationalContentBuilder.New().AsProductivityTip().WithPriority(75).Build());

        // Encouragement messages
        contents.Add(MotivationalContentBuilder.New().AsEncouragement().WithCategory(ContentCategory.Wellness).Build());
        contents.Add(MotivationalContentBuilder.New().AsEncouragement().WithCategory(ContentCategory.Motivation).Build());

        // Streak celebrations
        contents.Add(MotivationalContentBuilder.New().AsStreakCelebration(3).Build());
        contents.Add(MotivationalContentBuilder.New().AsStreakCelebration(7).Build());
        contents.Add(MotivationalContentBuilder.New().AsStreakCelebration(30).Build());

        // Wellness reminders
        contents.Add(MotivationalContentBuilder.New().AsWellnessReminder().Build());

        // A/B test enabled content
        contents.Add(MotivationalContentBuilder.New()
            .WithTitle("A/B Test Content")
            .WithABTesting(new Dictionary<string, object> { ["testGroup"] = "A" })
            .Build());

        // Scheduled content
        contents.Add(MotivationalContentBuilder.New()
            .WithTitle("Scheduled Content")
            .ScheduledFor(DateTime.UtcNow.AddHours(2))
            .Build());

        // Inactive and deleted content for edge case testing
        contents.Add(MotivationalContentBuilder.New().WithTitle("Inactive Content").AsInactive().Build());
        contents.Add(MotivationalContentBuilder.New().WithTitle("Deleted Content").AsDeleted().Build());

        return contents;
    }

    /// <summary>
    /// Creates user preferences representing different user archetypes
    /// </summary>
    public static List<UserContentPreferences> CreateUserArchetypes()
    {
        var userIds = CreateTestUserIds(6);
        var preferences = new List<UserContentPreferences>();

        // Beginner user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[0])
            .AsBeginner()
            .WithStandardDeliveryTimes()
            .Build());

        // Intermediate user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[1])
            .AsIntermediate()
            .WithStandardDeliveryTimes()
            .AllowingWeekends()
            .Build());

        // Expert user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[2])
            .AsExpert()
            .WithStandardDeliveryTimes()
            .AllowingAfterHours()
            .AllowingWeekends()
            .Build());

        // Wellness-focused user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[3])
            .AsWellnessFocused()
            .DisallowingWeekends()
            .DisallowingAfterHours()
            .Build());

        // High-engagement user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[4])
            .AsHighEngagement()
            .WithFrequency(ContentFrequency.High)
            .Build());

        // Paused user
        preferences.Add(UserContentPreferencesBuilder.New()
            .ForUser(userIds[5])
            .AsIntermediate()
            .PausedUntil(DateTime.UtcNow.AddDays(7))
            .Build());

        return preferences;
    }

    /// <summary>
    /// Creates delivery logs with realistic engagement patterns
    /// </summary>
    public static List<ContentDeliveryLog> CreateRealisticDeliveryLogs(
        Guid userId,
        List<MotivationalContent> contents,
        int logsPerContent = 10,
        double overallEngagementRate = 0.65)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(42); // Seed for consistent results

        foreach (var content in contents)
        {
            // Adjust engagement rate based on content type
            var contentEngagementRate = overallEngagementRate;
            switch (content.ContentType)
            {
                case MotivationalContentType.Achievement:
                    contentEngagementRate *= 1.3; // Achievements get higher engagement
                    break;
                case MotivationalContentType.Streak:
                    contentEngagementRate *= 1.4; // Streaks get highest engagement
                    break;
                case MotivationalContentType.Reminder:
                    contentEngagementRate *= 0.7; // Reminders get lower engagement
                    break;
            }

            var contentLogs = ContentDeliveryLogBuilder.New()
                .ForUser(userId)
                .ForContent(content)
                .BuildEngagementPattern(logsPerContent, Math.Min(1.0, contentEngagementRate));

            logs.AddRange(contentLogs);
        }

        return logs;
    }

    /// <summary>
    /// Creates A/B testing data for statistical analysis
    /// </summary>
    public static List<ContentDeliveryLog> CreateABTestingData(
        Guid userId,
        Guid contentId,
        string groupA = "A",
        string groupB = "B",
        double groupAEngagementRate = 0.6,
        double groupBEngagementRate = 0.8,
        int logsPerGroup = 100)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(42);

        // Group A logs
        for (int i = 0; i < logsPerGroup; i++)
        {
            var builder = ContentDeliveryLogBuilder.New()
                .ForUser(userId)
                .ForContent(contentId)
                .InABTestGroup(groupA)
                .DeliveredAgo(TimeSpan.FromHours(i + 1));

            if (random.NextDouble() < groupAEngagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        // Group B logs
        for (int i = 0; i < logsPerGroup; i++)
        {
            var builder = ContentDeliveryLogBuilder.New()
                .ForUser(userId)
                .ForContent(contentId)
                .InABTestGroup(groupB)
                .DeliveredAgo(TimeSpan.FromHours(i + logsPerGroup + 1));

            if (random.NextDouble() < groupBEngagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        return logs;
    }

    /// <summary>
    /// Creates time-series delivery data for trend analysis
    /// </summary>
    public static List<ContentDeliveryLog> CreateTimeSeriesData(
        Guid userId,
        Guid contentId,
        int days = 30,
        int deliveriesPerDay = 3)
    {
        var logs = new List<ContentDeliveryLog>();
        var startDate = DateTime.UtcNow.AddDays(-days);

        for (int day = 0; day < days; day++)
        {
            for (int delivery = 0; delivery < deliveriesPerDay; delivery++)
            {
                var deliveryTime = startDate.AddDays(day).AddHours(8 + (delivery * 4)); // 8 AM, 12 PM, 4 PM

                var builder = ContentDeliveryLogBuilder.New()
                    .ForUser(userId)
                    .ForContent(contentId)
                    .DeliveredAt(deliveryTime);

                // Add realistic engagement patterns (higher in mornings, lower in evenings)
                var engagementProbability = delivery switch
                {
                    0 => 0.8, // Morning - high engagement
                    1 => 0.6, // Afternoon - medium engagement
                    2 => 0.4, // Evening - lower engagement
                    _ => 0.5
                };

                var random = new Random(day * 10 + delivery); // Deterministic randomness
                if (random.NextDouble() < engagementProbability)
                {
                    builder.AsViewed();
                }

                logs.Add(builder.Build());
            }
        }

        return logs;
    }

    /// <summary>
    /// Asserts that content matches expected targeting conditions
    /// </summary>
    public static void AssertContentTargeting(
        MotivationalContent content,
        UserContentPreferences preferences)
    {
        // Check if content type is preferred
        if (preferences.PreferredContentTypes.Any())
        {
            preferences.PreferredContentTypes.Should().Contain(content.ContentType,
                $"Content type {content.ContentType} should be in user's preferred types");
        }

        // Check if category is preferred
        if (preferences.PreferredCategories.Any())
        {
            preferences.PreferredCategories.Should().Contain(content.Category,
                $"Content category {content.Category} should be in user's preferred categories");
        }
    }

    /// <summary>
    /// Asserts that delivery respects user's time preferences
    /// </summary>
    public static void AssertDeliveryTiming(
        ContentDeliveryLog deliveryLog,
        UserContentPreferences preferences)
    {
        var deliveryTime = deliveryLog.DeliveredAt;
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(deliveryTime, userTimeZone);

        // Check weekend preferences
        if (!preferences.AllowWeekends)
        {
            localTime.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            localTime.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }

        // Check after-hours preferences
        if (!preferences.AllowAfterHours)
        {
            localTime.Hour.Should().BeInRange(8, 17, "Delivery should be within work hours (8 AM - 6 PM)");
        }
    }

    /// <summary>
    /// Calculates expected engagement rate for content type
    /// </summary>
    public static double GetExpectedEngagementRate(MotivationalContentType contentType)
    {
        return contentType switch
        {
            MotivationalContentType.Achievement => 0.85,
            MotivationalContentType.Streak => 0.90,
            MotivationalContentType.Celebration => 0.80,
            MotivationalContentType.Insight => 0.70,
            MotivationalContentType.Tip => 0.65,
            MotivationalContentType.Encouragement => 0.60,
            MotivationalContentType.Challenge => 0.75,
            MotivationalContentType.Reminder => 0.45,
            _ => 0.60
        };
    }

    /// <summary>
    /// Validates that engagement history is properly formatted
    /// </summary>
    public static void ValidateEngagementHistory(Dictionary<string, object> engagementHistory)
    {
        engagementHistory.Should().NotBeNull();

        // Check for expected keys
        if (engagementHistory.ContainsKey("totalEngagements"))
        {
            engagementHistory["totalEngagements"].Should().BeOfType<int>();
            ((int)engagementHistory["totalEngagements"]).Should().BeGreaterOrEqualTo(0);
        }

        if (engagementHistory.ContainsKey("averageEngagementRate"))
        {
            engagementHistory["averageEngagementRate"].Should().BeOfType<double>();
            var rate = (double)engagementHistory["averageEngagementRate"];
            rate.Should().BeInRange(0.0, 1.0);
        }

        // Check score entries
        var scoreKeys = engagementHistory.Keys.Where(k => k.StartsWith("score_")).ToList();
        foreach (var scoreKey in scoreKeys)
        {
            engagementHistory[scoreKey].Should().BeOfType<double>();
            var score = (double)engagementHistory[scoreKey];
            score.Should().BeInRange(0.0, 1.0, $"Score {scoreKey} should be between 0 and 1");
        }
    }

    /// <summary>
    /// Creates test data for personalization algorithm testing
    /// </summary>
    public static (List<MotivationalContent> contents, List<UserContentPreferences> preferences, List<ContentDeliveryLog> logs)
        CreatePersonalizationTestData()
    {
        var contents = CreateDiverseContentSet();
        var preferences = CreateUserArchetypes();
        var logs = new List<ContentDeliveryLog>();

        // Create logs for each user with their preferences
        foreach (var userPref in preferences.Take(3)) // Limit to first 3 for manageable test data
        {
            var userLogs = CreateRealisticDeliveryLogs(userPref.UserId, contents, 5, 0.7);
            logs.AddRange(userLogs);
        }

        return (contents, preferences, logs);
    }

    /// <summary>
    /// Generates test timezone identifiers for testing
    /// </summary>
    public static List<string> GetTestTimeZones()
    {
        return new List<string>
        {
            "UTC",
            "America/New_York",
            "America/Los_Angeles",
            "Europe/London",
            "Europe/Paris",
            "Asia/Tokyo",
            "Australia/Sydney"
        };
    }

    /// <summary>
    /// Creates edge case test scenarios
    /// </summary>
    public static class EdgeCases
    {
        public static MotivationalContent EmptyTargetConditions()
        {
            return MotivationalContentBuilder.New()
                .WithTitle("No Target Conditions")
                .WithTargetConditions(new Dictionary<string, object>())
                .Build();
        }

        public static UserContentPreferences NoPreferences()
        {
            return UserContentPreferencesBuilder.New()
                .ForUser(Guid.NewGuid())
                .WithContentTypes() // Empty
                .WithCategories() // Empty
                .WithChannels() // Empty
                .Build();
        }

        public static UserContentPreferences DisabledUser()
        {
            return UserContentPreferencesBuilder.New()
                .ForUser(Guid.NewGuid())
                .WithContentDisabled()
                .Build();
        }

        public static UserContentPreferences PausedUser()
        {
            return UserContentPreferencesBuilder.New()
                .ForUser(Guid.NewGuid())
                .PausedUntil(DateTime.UtcNow.AddDays(30))
                .Build();
        }

        public static UserContentPreferences VeryHighFrequencyUser()
        {
            return UserContentPreferencesBuilder.New()
                .ForUser(Guid.NewGuid())
                .WithFrequency(ContentFrequency.VeryHigh)
                .WithContentLimits(10, 50)
                .Build();
        }
    }
}
