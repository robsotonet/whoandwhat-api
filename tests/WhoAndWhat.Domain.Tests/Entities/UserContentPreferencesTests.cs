using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Tests.Builders;
using WhoAndWhat.Domain.Tests.Helpers;

namespace WhoAndWhat.Domain.Tests.Entities;

/// <summary>
/// Comprehensive unit tests for UserContentPreferences entity
/// Testing business logic, validation, content delivery preferences, and user engagement features
/// </summary>
public class UserContentPreferencesTests
{
    #region Creation and Default Values Tests

    [Fact]
    public void CreateDefault_WithValidUserId_ShouldCreateWithDefaultSettings()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.Should().NotBeNull();
        preferences.UserId.Should().Be(userId);
        preferences.IsContentEnabled.Should().BeTrue();
        preferences.PreferredFrequency.Should().Be(ContentFrequency.Moderate);
        preferences.MaxDailyContent.Should().Be(3);
        preferences.MaxWeeklyContent.Should().Be(15);
        preferences.AllowWeekends.Should().BeTrue();
        preferences.AllowAfterHours.Should().BeFalse();
        preferences.TimeZone.Should().Be("UTC");
        preferences.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        preferences.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateDefault_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => UserContentPreferences.CreateDefault(Guid.Empty);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*User ID cannot be empty*");
    }

    [Fact]
    public void CreateDefault_ShouldSetDefaultContentTypes()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.PreferredContentTypes.Should().NotBeEmpty();
        preferences.PreferredContentTypes.Should().Contain(MotivationalContentType.Insight);
        preferences.PreferredContentTypes.Should().Contain(MotivationalContentType.Achievement);
        preferences.PreferredContentTypes.Should().Contain(MotivationalContentType.Encouragement);
    }

    [Fact]
    public void CreateDefault_ShouldSetDefaultCategories()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.PreferredCategories.Should().NotBeEmpty();
        preferences.PreferredCategories.Should().Contain(ContentCategory.Productivity);
        preferences.PreferredCategories.Should().Contain(ContentCategory.Motivation);
    }

    [Fact]
    public void CreateDefault_ShouldSetDefaultChannels()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.PreferredChannels.Should().NotBeEmpty();
        preferences.PreferredChannels.Should().Contain(ContentDeliveryChannel.Dashboard);
        preferences.PreferredChannels.Should().Contain(ContentDeliveryChannel.InApp);
    }

    [Fact]
    public void CreateDefault_ShouldSetDefaultDeliveryTimes()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.PreferredDeliveryTimes.Should().NotBeEmpty();
        preferences.PreferredDeliveryTimes.Should().ContainKey("morning");
        preferences.PreferredDeliveryTimes.Should().ContainKey("afternoon");
        preferences.PreferredDeliveryTimes["morning"].Should().Be(new TimeSpan(9, 0, 0));
        preferences.PreferredDeliveryTimes["afternoon"].Should().Be(new TimeSpan(14, 0, 0));
    }

    [Fact]
    public void CreateDefault_ShouldInitializeEmptyCollections()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var preferences = UserContentPreferences.CreateDefault(userId);

        // Assert
        preferences.PersonalizationSettings.Should().NotBeNull();
        preferences.EngagementHistory.Should().NotBeNull();
        preferences.PersonalizationSettings.Should().BeEmpty();
        preferences.EngagementHistory.Should().BeEmpty();
    }

    #endregion

    #region Content Settings Management Tests

    [Fact]
    public void SetContentEnabled_WithTrue_ShouldEnableContent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().WithContentDisabled().Build();
        preferences.IsContentEnabled.Should().BeFalse(); // Verify initial state

        // Act
        preferences.SetContentEnabled(true);

        // Assert
        preferences.IsContentEnabled.Should().BeTrue();
        preferences.ContentPausedUntil.Should().BeNull();
    }

    [Fact]
    public void SetContentEnabled_WithFalse_ShouldDisableContent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build(); // Enabled by default
        preferences.IsContentEnabled.Should().BeTrue(); // Verify initial state

        // Act
        preferences.SetContentEnabled(false);

        // Assert
        preferences.IsContentEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetContentEnabled_WithSameValue_ShouldNotTriggerChange()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var initialEnabledState = preferences.IsContentEnabled;

        // Act
        preferences.SetContentEnabled(initialEnabledState);

        // Assert
        preferences.IsContentEnabled.Should().Be(initialEnabledState);
        // Additional assertions would depend on domain event implementation
    }

    [Fact]
    public void SetPreferredFrequency_WithLowFrequency_ShouldUpdateLimits()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        preferences.SetPreferredFrequency(ContentFrequency.Low);

        // Assert
        preferences.PreferredFrequency.Should().Be(ContentFrequency.Low);
        preferences.MaxDailyContent.Should().Be(1);
        preferences.MaxWeeklyContent.Should().Be(5);
    }

    [Fact]
    public void SetPreferredFrequency_WithHighFrequency_ShouldUpdateLimits()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        preferences.SetPreferredFrequency(ContentFrequency.High);

        // Assert
        preferences.PreferredFrequency.Should().Be(ContentFrequency.High);
        preferences.MaxDailyContent.Should().Be(5);
        preferences.MaxWeeklyContent.Should().Be(25);
    }

    [Fact]
    public void SetPreferredFrequency_WithVeryHighFrequency_ShouldUpdateLimits()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        preferences.SetPreferredFrequency(ContentFrequency.VeryHigh);

        // Assert
        preferences.PreferredFrequency.Should().Be(ContentFrequency.VeryHigh);
        preferences.MaxDailyContent.Should().Be(8);
        preferences.MaxWeeklyContent.Should().Be(40);
    }

    [Fact]
    public void SetPreferredContentTypes_WithValidTypes_ShouldUpdateContentTypes()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var newTypes = new[] { MotivationalContentType.Achievement, MotivationalContentType.Streak };

        // Act
        preferences.SetPreferredContentTypes(newTypes);

        // Assert
        preferences.PreferredContentTypes.Should().BeEquivalentTo(newTypes);
    }

    [Fact]
    public void SetPreferredContentTypes_WithEmptyCollection_ShouldClearTypes()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentTypes(MotivationalContentType.Achievement, MotivationalContentType.Tip)
            .Build();

        // Act
        preferences.SetPreferredContentTypes(Array.Empty<MotivationalContentType>());

        // Assert
        preferences.PreferredContentTypes.Should().BeEmpty();
    }

    [Fact]
    public void SetPreferredCategories_WithValidCategories_ShouldUpdateCategories()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var newCategories = new[] { ContentCategory.Learning, ContentCategory.Wellness };

        // Act
        preferences.SetPreferredCategories(newCategories);

        // Assert
        preferences.PreferredCategories.Should().BeEquivalentTo(newCategories);
    }

    [Fact]
    public void SetPreferredChannels_WithValidChannels_ShouldUpdateChannels()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var newChannels = new[] { ContentDeliveryChannel.Email, ContentDeliveryChannel.Push };

        // Act
        preferences.SetPreferredChannels(newChannels);

        // Assert
        preferences.PreferredChannels.Should().BeEquivalentTo(newChannels);
    }

    #endregion

    #region Delivery Time Management Tests

    [Fact]
    public void SetPreferredDeliveryTimes_WithValidTimes_ShouldUpdateTimes()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var newTimes = new Dictionary<string, TimeSpan>
        {
            ["early_morning"] = new TimeSpan(7, 0, 0),
            ["lunch"] = new TimeSpan(12, 30, 0),
            ["evening"] = new TimeSpan(18, 0, 0)
        };

        // Act
        preferences.SetPreferredDeliveryTimes(newTimes);

        // Assert
        preferences.PreferredDeliveryTimes.Should().BeEquivalentTo(newTimes);
    }

    [Fact]
    public void SetPreferredDeliveryTimes_WithNullTimes_ShouldSetEmptyDictionary()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithStandardDeliveryTimes()
            .Build();

        // Act
        preferences.SetPreferredDeliveryTimes(null);

        // Assert
        preferences.PreferredDeliveryTimes.Should().NotBeNull();
        preferences.PreferredDeliveryTimes.Should().BeEmpty();
    }

    [Fact]
    public void GetNextPreferredDeliveryTime_WithValidTimes_ShouldReturnNextTime()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithStandardDeliveryTimes()
            .Build();

        // Act
        var nextTime = preferences.GetNextPreferredDeliveryTime();

        // Assert
        nextTime.Should().NotBeNull();
        nextTime.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GetNextPreferredDeliveryTime_WithDisabledContent_ShouldReturnNull()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentDisabled()
            .WithStandardDeliveryTimes()
            .Build();

        // Act
        var nextTime = preferences.GetNextPreferredDeliveryTime();

        // Assert
        nextTime.Should().BeNull();
    }

    [Fact]
    public void GetNextPreferredDeliveryTime_WithEmptyTimes_ShouldReturnNull()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithDeliveryTimes(new Dictionary<string, TimeSpan>())
            .Build();

        // Act
        var nextTime = preferences.GetNextPreferredDeliveryTime();

        // Assert
        nextTime.Should().BeNull();
    }

    #endregion

    #region Content Limits and Frequency Tests

    [Fact]
    public void SetContentLimits_WithValidLimits_ShouldUpdateLimits()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var maxDaily = 7;
        var maxWeekly = 30;

        // Act
        preferences.SetContentLimits(maxDaily, maxWeekly);

        // Assert
        preferences.MaxDailyContent.Should().Be(maxDaily);
        preferences.MaxWeeklyContent.Should().Be(maxWeekly);
    }

    [Fact]
    public void SetContentLimits_WithZeroDaily_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act & Assert
        var action = () => preferences.SetContentLimits(0, 10);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Content limits must be positive*");
    }

    [Fact]
    public void SetContentLimits_WithNegativeWeekly_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act & Assert
        var action = () => preferences.SetContentLimits(5, -1);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Content limits must be positive*");
    }

    [Fact]
    public void SetContentLimits_WithDailyExceedingWeekly_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act & Assert
        var action = () => preferences.SetContentLimits(10, 5);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Daily limit cannot exceed weekly limit*");
    }

    [Fact]
    public void SetSchedulingPreferences_WithValidPreferences_ShouldUpdatePreferences()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        preferences.SetSchedulingPreferences(allowWeekends: false, allowAfterHours: true);

        // Assert
        preferences.AllowWeekends.Should().BeFalse();
        preferences.AllowAfterHours.Should().BeTrue();
    }

    #endregion

    #region Pause and Resume Functionality Tests

    [Fact]
    public void PauseContentUntil_WithFutureDate_ShouldPauseContent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var pauseUntil = DateTime.UtcNow.AddDays(7);

        // Act
        preferences.PauseContentUntil(pauseUntil);

        // Assert
        preferences.ContentPausedUntil.Should().Be(pauseUntil);
    }

    [Fact]
    public void PauseContentUntil_WithPastDate_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var pastDate = DateTime.UtcNow.AddHours(-1);

        // Act & Assert
        var action = () => preferences.PauseContentUntil(pastDate);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Pause date must be in the future*");
    }

    [Fact]
    public void ResumeContent_WhenPaused_ShouldClearPauseDate()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .PausedUntil(DateTime.UtcNow.AddDays(7))
            .Build();
        preferences.ContentPausedUntil.Should().NotBeNull(); // Verify initial state

        // Act
        preferences.ResumeContent();

        // Assert
        preferences.ContentPausedUntil.Should().BeNull();
    }

    [Fact]
    public void ResumeContent_WhenNotPaused_ShouldNotThrow()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        preferences.ContentPausedUntil.Should().BeNull(); // Verify initial state

        // Act & Assert
        var action = () => preferences.ResumeContent();
        action.Should().NotThrow();
    }

    #endregion

    #region Timezone Handling Tests

    [Fact]
    public void SetTimeZone_WithValidTimeZone_ShouldUpdateTimeZone()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var timeZone = "America/New_York";

        // Act
        preferences.SetTimeZone(timeZone);

        // Assert
        preferences.TimeZone.Should().Be(timeZone);
    }

    [Fact]
    public void SetTimeZone_WithInvalidTimeZone_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var invalidTimeZone = "Invalid/TimeZone";

        // Act & Assert
        var action = () => preferences.SetTimeZone(invalidTimeZone);
        action.Should().Throw<ArgumentException>()
            .WithMessage($"*Invalid timezone: {invalidTimeZone}*");
    }

    [Fact]
    public void SetTimeZone_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act & Assert
        var action = () => preferences.SetTimeZone("");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Timezone cannot be empty*");
    }

    [Fact]
    public void SetTimeZone_WithWhitespace_ShouldThrowArgumentException()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act & Assert
        var action = () => preferences.SetTimeZone("   ");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Timezone cannot be empty*");
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("Australia/Sydney")]
    public void SetTimeZone_WithValidSystemTimeZones_ShouldSucceed(string timeZone)
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        var action = () => preferences.SetTimeZone(timeZone);

        // Assert
        action.Should().NotThrow();
        preferences.TimeZone.Should().Be(timeZone);
    }

    #endregion

    #region Personalization and Engagement Tests

    [Fact]
    public void UpdatePersonalizationSetting_WithNewSetting_ShouldAddSetting()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var key = "preferredStyle";
        var value = "motivational";

        // Act
        preferences.UpdatePersonalizationSetting(key, value);

        // Assert
        preferences.PersonalizationSettings.Should().ContainKey(key);
        preferences.PersonalizationSettings[key].Should().Be(value);
    }

    [Fact]
    public void UpdatePersonalizationSetting_WithExistingSetting_ShouldUpdateSetting()
    {
        // Arrange
        var key = "preferredStyle";
        var initialValue = "casual";
        var newValue = "professional";
        var preferences = UserContentPreferencesBuilder.New()
            .WithPersonalizationSetting(key, initialValue)
            .Build();

        // Act
        preferences.UpdatePersonalizationSetting(key, newValue);

        // Assert
        preferences.PersonalizationSettings[key].Should().Be(newValue);
    }

    [Fact]
    public void GetPersonalizationSetting_WithExistingSetting_ShouldReturnValue()
    {
        // Arrange
        var key = "difficulty";
        var value = "intermediate";
        var preferences = UserContentPreferencesBuilder.New()
            .WithPersonalizationSetting(key, value)
            .Build();

        // Act
        var result = preferences.GetPersonalizationSetting<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void GetPersonalizationSetting_WithNonExistentSetting_ShouldReturnDefault()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        var result = preferences.GetPersonalizationSetting<string>("nonExistentKey");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPersonalizationSetting_WithWrongType_ShouldReturnDefault()
    {
        // Arrange
        var key = "numberSetting";
        var value = 42;
        var preferences = UserContentPreferencesBuilder.New()
            .WithPersonalizationSetting(key, value)
            .Build();

        // Act
        var result = preferences.GetPersonalizationSetting<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UpdateEngagementHistory_WithNewEntry_ShouldAddEntry()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var key = "totalClicks";
        var value = 15;

        // Act
        preferences.UpdateEngagementHistory(key, value);

        // Assert
        preferences.EngagementHistory.Should().ContainKey(key);
        preferences.EngagementHistory[key].Should().Be(value);
    }

    [Fact]
    public void GetEngagementHistory_WithExistingEntry_ShouldReturnValue()
    {
        // Arrange
        var key = "averageEngagement";
        var value = 0.75;
        var preferences = UserContentPreferencesBuilder.New()
            .WithEngagementHistory(key, value)
            .Build();

        // Act
        double? result = preferences.GetEngagementHistory<double>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void GetEngagementHistory_WithNonExistentEntry_ShouldReturnNull()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        double? result = preferences.GetEngagementHistory<double>("nonExistentKey");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEngagementHistory_WithNullableTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var key = "testScore";
        var value = 0.85;
        var preferences = UserContentPreferencesBuilder.New()
            .WithEngagementHistory(key, value)
            .Build();

        // Act
        double? result = preferences.GetEngagementHistory<double>(key);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(value);
        result!.Value.Should().Be(value); // Test .Value access
    }

    [Fact]
    public void GetEngagementHistory_ForScoreCalculation_ShouldWorkWithNullableTypes()
    {
        // Arrange
        var contentType = MotivationalContentType.Achievement;
        var scoreKey = $"score_{contentType}";
        var historicalScore = 0.7;
        
        var preferences = UserContentPreferencesBuilder.New()
            .WithEngagementHistory(scoreKey, historicalScore)
            .Build();

        // Act - This simulates the actual usage in CalculateContentScore method
        double? contentTypeHistory = preferences.GetEngagementHistory<double>(scoreKey);

        // Assert
        contentTypeHistory.Should().NotBeNull();
        contentTypeHistory.HasValue.Should().BeTrue();
        contentTypeHistory.Value.Should().Be(historicalScore);

        // Test the score calculation logic
        var baseScore = 0.5;
        if (contentTypeHistory.HasValue)
        {
            var combinedScore = (baseScore + contentTypeHistory.Value) / 2;
            combinedScore.Should().Be(0.6); // (0.5 + 0.7) / 2 = 0.6
        }
    }

    [Fact]
    public void GetEngagementHistory_WithDifferentTypes_ShouldReturnCorrectNullableTypes()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithEngagementHistory("intValue", 42)
            .WithEngagementHistory("stringValue", "test")
            .Build();

        // Act & Assert
        int? intResult = preferences.GetEngagementHistory<int>("intValue");
        intResult.Should().Be(42);

        string? stringResult = preferences.GetEngagementHistory<string>("stringValue");
        stringResult.Should().Be("test");

        // Non-existent keys should return null
        int? missingInt = preferences.GetEngagementHistory<int>("missing");
        missingInt.Should().BeNull();

        string? missingString = preferences.GetEngagementHistory<string>("missing");
        missingString.Should().BeNull();
    }

    #endregion

    #region Content Delivery Validation Tests

    [Fact]
    public void CanDeliverContentNow_WithEnabledContentAndValidConditions_ShouldReturnTrue()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithChannels(ContentDeliveryChannel.Dashboard)
            .WithContentTypes(MotivationalContentType.Insight)
            .AllowingAfterHours()
            .AllowingWeekends()
            .Build();

        // Act
        var canDeliver = preferences.CanDeliverContentNow(
            ContentDeliveryChannel.Dashboard,
            MotivationalContentType.Insight);

        // Assert
        canDeliver.Should().BeTrue();
    }

    [Fact]
    public void CanDeliverContentNow_WithDisabledContent_ShouldReturnFalse()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentDisabled()
            .WithChannels(ContentDeliveryChannel.Dashboard)
            .WithContentTypes(MotivationalContentType.Insight)
            .Build();

        // Act
        var canDeliver = preferences.CanDeliverContentNow(
            ContentDeliveryChannel.Dashboard,
            MotivationalContentType.Insight);

        // Assert
        canDeliver.Should().BeFalse();
    }

    [Fact]
    public void CanDeliverContentNow_WithPausedContent_ShouldReturnFalse()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .PausedUntil(DateTime.UtcNow.AddDays(7))
            .WithChannels(ContentDeliveryChannel.Dashboard)
            .WithContentTypes(MotivationalContentType.Insight)
            .Build();

        // Act
        var canDeliver = preferences.CanDeliverContentNow(
            ContentDeliveryChannel.Dashboard,
            MotivationalContentType.Insight);

        // Assert
        canDeliver.Should().BeFalse();
    }

    [Fact]
    public void CanDeliverContentNow_WithNonPreferredChannel_ShouldReturnFalse()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithChannels(ContentDeliveryChannel.Dashboard)
            .WithContentTypes(MotivationalContentType.Insight)
            .Build();

        // Act
        var canDeliver = preferences.CanDeliverContentNow(
            ContentDeliveryChannel.Email, // Non-preferred channel
            MotivationalContentType.Insight);

        // Assert
        canDeliver.Should().BeFalse();
    }

    [Fact]
    public void CanDeliverContentNow_WithNonPreferredContentType_ShouldReturnFalse()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithChannels(ContentDeliveryChannel.Dashboard)
            .WithContentTypes(MotivationalContentType.Insight)
            .Build();

        // Act
        var canDeliver = preferences.CanDeliverContentNow(
            ContentDeliveryChannel.Dashboard,
            MotivationalContentType.Streak); // Non-preferred content type

        // Assert
        canDeliver.Should().BeFalse();
    }

    [Fact]
    public void RecordContentDelivery_WithValidTime_ShouldUpdateLastDelivery()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        var deliveryTime = DateTime.UtcNow;

        // Act
        preferences.RecordContentDelivery(deliveryTime);

        // Assert
        preferences.LastContentDelivery.Should().Be(deliveryTime);
    }

    #endregion

    #region Content Scoring and Personalization Tests

    [Fact]
    public void CalculateContentScore_WithPreferredTypeAndCategory_ShouldReturnHighScore()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentTypes(MotivationalContentType.Achievement)
            .WithCategories(ContentCategory.Productivity)
            .Build();

        // Act
        var score = preferences.CalculateContentScore(
            MotivationalContentType.Achievement,
            ContentCategory.Productivity);

        // Assert
        score.Should().BeGreaterThan(0.5);
        score.Should().BeLessOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateContentScore_WithNonPreferredTypeAndCategory_ShouldReturnBaseScore()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentTypes(MotivationalContentType.Achievement)
            .WithCategories(ContentCategory.Productivity)
            .Build();

        // Act
        var score = preferences.CalculateContentScore(
            MotivationalContentType.Reminder,    // Non-preferred
            ContentCategory.Wellness);            // Non-preferred

        // Assert
        score.Should().Be(0.5); // Base score
    }

    [Fact]
    public void CalculateContentScore_WithEngagementHistory_ShouldIncorporateHistory()
    {
        // Arrange
        var contentType = MotivationalContentType.Tip;
        var historyScore = 0.9;
        var preferences = UserContentPreferencesBuilder.New()
            .WithEngagementHistory($"score_{contentType}", historyScore)
            .Build();

        // Act
        var score = preferences.CalculateContentScore(contentType, ContentCategory.Learning);

        // Assert
        score.Should().BeGreaterThan(0.5);
        score.Should().BeLessOrEqualTo(1.0);
        // Score should be influenced by history
    }

    [Fact]
    public void CalculateContentScore_ShouldNeverExceedBounds()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .WithContentTypes(MotivationalContentType.Achievement)
            .WithCategories(ContentCategory.Achievement)
            .WithEngagementHistory("score_Achievement", 1.5) // Artificially high
            .Build();

        // Act
        var score = preferences.CalculateContentScore(
            MotivationalContentType.Achievement,
            ContentCategory.Achievement);

        // Assert
        score.Should().BeInRange(0.0, 1.0);
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public void CreateDefault_ShouldRaiseDomainEvent()
    {
        // Arrange & Act
        var preferences = UserContentPreferences.CreateDefault(Guid.NewGuid());

        // Assert
        preferences.DomainEvents.Should().NotBeEmpty();
        preferences.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void SetContentEnabled_WhenStateChanges_ShouldRaiseDomainEvent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().WithContentDisabled().Build();
        preferences.ClearDomainEvents(); // Clear creation events

        // Act
        preferences.SetContentEnabled(true);

        // Assert
        preferences.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void PauseContentUntil_ShouldRaiseDomainEvent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();
        preferences.ClearDomainEvents(); // Clear creation events

        // Act
        preferences.PauseContentUntil(DateTime.UtcNow.AddDays(7));

        // Assert
        preferences.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void ResumeContent_ShouldRaiseDomainEvent()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .PausedUntil(DateTime.UtcNow.AddDays(7))
            .Build();
        preferences.ClearDomainEvents(); // Clear creation events

        // Act
        preferences.ResumeContent();

        // Assert
        preferences.DomainEvents.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases and Business Rules Tests

    [Fact]
    public void CanSoftDelete_ForUserContentPreferences_ShouldReturnFalse()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        var canDelete = preferences.CanSoftDelete();

        // Assert
        canDelete.Should().BeFalse();
    }

    [Theory]
    [InlineData(ContentFrequency.Low)]
    [InlineData(ContentFrequency.Moderate)]
    [InlineData(ContentFrequency.High)]
    [InlineData(ContentFrequency.VeryHigh)]
    public void SetPreferredFrequency_WithAllFrequencies_ShouldSetAppropriateDefaults(ContentFrequency frequency)
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act
        preferences.SetPreferredFrequency(frequency);

        // Assert
        preferences.PreferredFrequency.Should().Be(frequency);
        preferences.MaxDailyContent.Should().BeGreaterThan(0);
        preferences.MaxWeeklyContent.Should().BeGreaterThan(0);
        preferences.MaxDailyContent.Should().BeLessOrEqualTo(preferences.MaxWeeklyContent);
    }

    [Fact]
    public void MultipleOperations_ShouldMaintainConsistentState()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New().Build();

        // Act - Perform multiple operations
        preferences.SetPreferredFrequency(ContentFrequency.High);
        preferences.SetContentLimits(6, 30);
        preferences.SetSchedulingPreferences(false, true);
        preferences.UpdatePersonalizationSetting("theme", "dark");
        preferences.UpdateEngagementHistory("totalViews", 100);

        // Assert
        preferences.PreferredFrequency.Should().Be(ContentFrequency.High);
        preferences.MaxDailyContent.Should().Be(6);
        preferences.MaxWeeklyContent.Should().Be(30);
        preferences.AllowWeekends.Should().BeFalse();
        preferences.AllowAfterHours.Should().BeTrue();
        preferences.GetPersonalizationSetting<string>("theme").Should().Be("dark");
        int? totalViews = preferences.GetEngagementHistory<int>("totalViews");
        totalViews.Should().Be(100);
    }

    [Fact]
    public void ComplexDeliveryTimeScenario_ShouldHandleCorrectly()
    {
        // Arrange
        var preferences = UserContentPreferencesBuilder.New()
            .InTimeZone("America/New_York")
            .DisallowingWeekends()
            .DisallowingAfterHours()
            .Build();

        var deliveryTimes = new Dictionary<string, TimeSpan>
        {
            ["morning"] = new TimeSpan(8, 0, 0),
            ["lunch"] = new TimeSpan(12, 0, 0),
            ["afternoon"] = new TimeSpan(15, 30, 0)
        };

        // Act
        preferences.SetPreferredDeliveryTimes(deliveryTimes);

        // Assert
        preferences.PreferredDeliveryTimes.Should().BeEquivalentTo(deliveryTimes);
        preferences.TimeZone.Should().Be("America/New_York");
        preferences.AllowWeekends.Should().BeFalse();
        preferences.AllowAfterHours.Should().BeFalse();
    }

    #endregion
}