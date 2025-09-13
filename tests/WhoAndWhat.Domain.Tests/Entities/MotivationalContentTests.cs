using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Tests.Builders;
using WhoAndWhat.Domain.Tests.Helpers;

namespace WhoAndWhat.Domain.Tests.Entities;

/// <summary>
/// Comprehensive unit tests for MotivationalContent entity
/// Testing business logic, validation, state management, and domain rules
/// </summary>
public class MotivationalContentTests
{
    #region Creation and Validation Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateMotivationalContent()
    {
        // Arrange
        var title = "Test Motivational Content";
        var message = "This is a test motivational message.";
        var contentType = MotivationalContentType.Insight;
        var category = ContentCategory.Productivity;
        var targetConditions = new Dictionary<string, object> { ["experienceLevel"] = UserExperienceLevel.Beginner };
        var priority = 85;

        // Act
        var content = MotivationalContent.Create(title, message, contentType, category, targetConditions, priority);

        // Assert
        content.Should().NotBeNull();
        content.Title.Should().Be(title);
        content.Message.Should().Be(message);
        content.ContentType.Should().Be(contentType);
        content.Category.Should().Be(category);
        content.TargetConditions.Should().BeEquivalentTo(targetConditions);
        content.Priority.Should().Be(priority);
        content.IsActive.Should().BeTrue();
        content.IsDeleted.Should().BeFalse();
        content.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        content.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        content.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithNullTitle_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => MotivationalContent.Create(
            null!,
            "Valid message",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => MotivationalContent.Create(
            "",
            "Valid message",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceTitle_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => MotivationalContent.Create(
            "   ",
            "Valid message",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WithNullMessage_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => MotivationalContent.Create(
            "Valid title",
            null!,
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Message cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyMessage_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        var action = () => MotivationalContent.Create(
            "Valid title",
            "",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Message cannot be empty*");
    }

    [Fact]
    public void Create_WithDefaultPriority_ShouldSetPriorityToZero()
    {
        // Arrange & Act
        var content = MotivationalContent.Create(
            "Test Title",
            "Test Message",
            MotivationalContentType.Tip,
            ContentCategory.Learning);

        // Assert
        content.Priority.Should().Be(0);
    }

    [Fact]
    public void Create_WithNullTargetConditions_ShouldCreateWithEmptyDictionary()
    {
        // Arrange & Act
        var content = MotivationalContent.Create(
            "Test Title",
            "Test Message",
            MotivationalContentType.Tip,
            ContentCategory.Learning,
            null);

        // Assert
        content.TargetConditions.Should().NotBeNull();
        content.TargetConditions.Should().BeEmpty();
    }

    #endregion

    #region Property Update Tests

    [Fact]
    public void SetPriority_WithValidValue_ShouldUpdatePriority()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var newPriority = 150;

        // Act
        content.SetPriority(newPriority);

        // Assert
        content.Priority.Should().Be(newPriority);
    }

    [Fact]
    public void SetPriority_WithNegativeValue_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act & Assert
        var action = () => content.SetPriority(-10);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Priority cannot be negative*");
    }

    [Fact]
    public void SetPriority_WithExcessiveValue_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act & Assert
        var action = () => content.SetPriority(1000);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Priority cannot exceed 200*");
    }

    [Fact]
    public void SetTargetConditions_WithValidConditions_ShouldUpdateConditions()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var newConditions = new Dictionary<string, object>
        {
            ["experienceLevel"] = UserExperienceLevel.Expert,
            ["minCompletionRate"] = 0.8,
            ["categories"] = new[] { "Productivity", "Learning" }
        };

        // Act
        content.SetTargetConditions(newConditions);

        // Assert
        content.TargetConditions.Should().BeEquivalentTo(newConditions);
    }

    [Fact]
    public void SetTargetConditions_WithNullConditions_ShouldSetEmptyDictionary()
    {
        // Arrange
        var content = MotivationalContentBuilder.New()
            .WithTargetConditions(new Dictionary<string, object> { ["test"] = "value" })
            .Build();

        // Act
        content.SetTargetConditions(null);

        // Assert
        content.TargetConditions.Should().NotBeNull();
        content.TargetConditions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateContent_WithNewTitleAndMessage_ShouldUpdateContent()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var newTitle = "Updated Motivational Title";
        var newMessage = "Updated motivational message content.";

        // Act
        content.UpdateContent(newTitle, newMessage);

        // Assert
        content.Title.Should().Be(newTitle);
        content.Message.Should().Be(newMessage);
    }

    [Fact]
    public void UpdateContent_WithInvalidTitle_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act & Assert
        var action = () => content.UpdateContent("", "Valid message");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void UpdateContent_WithInvalidMessage_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act & Assert
        var action = () => content.UpdateContent("Valid title", null!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Message cannot be empty*");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Activate_WhenInactive_ShouldSetActiveToTrue()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsInactive().Build();
        content.IsActive.Should().BeFalse(); // Verify initial state

        // Act
        content.Activate();

        // Assert
        content.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldRemainActive()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build(); // Active by default
        content.IsActive.Should().BeTrue(); // Verify initial state

        // Act
        content.Activate();

        // Assert
        content.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldSetActiveToFalse()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build(); // Active by default
        content.IsActive.Should().BeTrue(); // Verify initial state

        // Act
        content.Deactivate();

        // Assert
        content.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldRemainInactive()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsInactive().Build();
        content.IsActive.Should().BeFalse(); // Verify initial state

        // Act
        content.Deactivate();

        // Assert
        content.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_WhenNotDeleted_ShouldMarkAsDeleted()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        content.IsDeleted.Should().BeFalse(); // Verify initial state

        // Act
        content.SoftDelete();

        // Assert
        content.IsDeleted.Should().BeTrue();
        content.IsActive.Should().BeFalse(); // Should also deactivate
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_ShouldRemainDeleted()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsDeleted().Build();
        content.IsDeleted.Should().BeTrue(); // Verify initial state

        // Act
        content.SoftDelete();

        // Assert
        content.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void CanSoftDelete_ForMotivationalContent_ShouldReturnTrue()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act
        var canDelete = content.CanSoftDelete();

        // Assert
        canDelete.Should().BeTrue();
    }

    #endregion

    #region A/B Testing Configuration Tests

    [Fact]
    public void ConfigureABTest_WithValidParameters_ShouldEnableABTesting()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var testGroup = "group_a";
        var configuration = new Dictionary<string, object>
        {
            ["testName"] = "title_format_test",
            ["variant"] = "emoji_heavy",
            ["splitRatio"] = 0.5
        };

        // Act
        content.ConfigureABTest(testGroup, configuration);

        // Assert
        content.IsABTestEnabled.Should().BeTrue();
        content.ABTestConfiguration.Should().NotBeNull();
        content.ABTestConfiguration.Should().ContainKey("testGroup");
        content.ABTestConfiguration["testGroup"].Should().Be(testGroup);
        content.ABTestConfiguration.Should().ContainKey("testName");
        content.ABTestConfiguration["testName"].Should().Be("title_format_test");
    }

    [Fact]
    public void ConfigureABTest_WithNullTestGroup_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var configuration = new Dictionary<string, object>();

        // Act & Assert
        var action = () => content.ConfigureABTest(null!, configuration);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Test group cannot be empty*");
    }

    [Fact]
    public void ConfigureABTest_WithEmptyTestGroup_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var configuration = new Dictionary<string, object>();

        // Act & Assert
        var action = () => content.ConfigureABTest("", configuration);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Test group cannot be empty*");
    }

    [Fact]
    public void ConfigureABTest_WithNullConfiguration_ShouldUseEmptyConfiguration()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var testGroup = "group_b";

        // Act
        content.ConfigureABTest(testGroup, null);

        // Assert
        content.IsABTestEnabled.Should().BeTrue();
        content.ABTestConfiguration.Should().NotBeNull();
        content.ABTestConfiguration["testGroup"].Should().Be(testGroup);
    }

    [Fact]
    public void DisableABTest_WhenABTestEnabled_ShouldDisableABTesting()
    {
        // Arrange
        var content = MotivationalContentBuilder.New()
            .WithABTesting(new Dictionary<string, object> { ["testGroup"] = "test" })
            .Build();
        content.IsABTestEnabled.Should().BeTrue(); // Verify initial state

        // Act
        content.DisableABTest();

        // Assert
        content.IsABTestEnabled.Should().BeFalse();
        content.ABTestConfiguration.Should().BeNull();
    }

    #endregion

    #region Active Period and Scheduling Tests

    [Fact]
    public void SetActivePeriod_WithValidDates_ShouldSetStartAndEndDate()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = DateTime.UtcNow.AddDays(7);

        // Act
        content.SetActivePeriod(startDate, endDate);

        // Assert
        content.StartDate.Should().Be(startDate);
        content.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void SetActivePeriod_WithStartDateAfterEndDate_ShouldThrowArgumentException()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var startDate = DateTime.UtcNow.AddDays(7);
        var endDate = DateTime.UtcNow.AddHours(1);

        // Act & Assert
        var action = () => content.SetActivePeriod(startDate, endDate);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Start date must be before end date*");
    }

    [Fact]
    public void SetActivePeriod_WithNullDates_ShouldClearActivePeriod()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        // Set initial period
        content.SetActivePeriod(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        // Act
        content.SetActivePeriod(null, null);

        // Assert
        content.StartDate.Should().BeNull();
        content.EndDate.Should().BeNull();
    }

    [Fact]
    public void IsCurrentlyActive_WhenActiveAndWithinPeriod_ShouldReturnTrue()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        content.SetActivePeriod(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));

        // Act
        var isActive = content.IsCurrentlyActive();

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyActive_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsInactive().Build();

        // Act
        var isActive = content.IsCurrentlyActive();

        // Assert
        isActive.Should().BeFalse();
    }

    [Fact]
    public void SetSchedulingRules_WithValidRules_ShouldSetRules()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var rules = new Dictionary<string, object>
        {
            ["frequency"] = "daily",
            ["maxDeliveries"] = 3,
            ["preferredTimes"] = new[] { "09:00", "14:00" }
        };

        // Act
        content.SetSchedulingRules(rules);

        // Assert
        content.SchedulingRules.Should().BeEquivalentTo(rules);
    }

    #endregion

    #region Targeting and Personalization Tests

    [Fact]
    public void MatchesUserConditions_WithMatchingExperienceLevel_ShouldReturnTrue()
    {
        // Arrange
        var targetLevel = UserExperienceLevel.Intermediate;
        var content = MotivationalContentBuilder.New()
            .ForExperienceLevel(targetLevel)
            .Build();

        var userConditions = new Dictionary<string, object>
        {
            ["experienceLevel"] = targetLevel
        };

        // Act
        var matches = content.MatchesUserConditions(userConditions);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void MatchesUserConditions_WithNonMatchingExperienceLevel_ShouldReturnFalse()
    {
        // Arrange
        var content = MotivationalContentBuilder.New()
            .ForExperienceLevel(UserExperienceLevel.Beginner)
            .Build();

        var userConditions = new Dictionary<string, object>
        {
            ["experienceLevel"] = UserExperienceLevel.Expert
        };

        // Act
        var matches = content.MatchesUserConditions(userConditions);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact]
    public void MatchesUserConditions_WithEmptyTargetConditions_ShouldReturnTrue()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        // Ensure no target conditions
        content.SetTargetConditions(new Dictionary<string, object>());

        var userConditions = new Dictionary<string, object>
        {
            ["experienceLevel"] = UserExperienceLevel.Expert
        };

        // Act
        var matches = content.MatchesUserConditions(userConditions);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void SetTargetCondition_WithNewCondition_ShouldAddCondition()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var key = "minCompletionRate";
        var value = 0.75;

        // Act
        content.SetTargetCondition(key, value);

        // Assert
        content.TargetConditions.Should().ContainKey(key);
        content.TargetConditions[key].Should().Be(value);
    }

    [Fact]
    public void SetTargetCondition_WithExistingCondition_ShouldUpdateCondition()
    {
        // Arrange
        var key = "experienceLevel";
        var originalValue = UserExperienceLevel.Beginner;
        var newValue = UserExperienceLevel.Expert;
        var content = MotivationalContentBuilder.New()
            .ForExperienceLevel(originalValue)
            .Build();

        // Act
        content.SetTargetCondition(key, newValue);

        // Assert
        content.TargetConditions[key].Should().Be(newValue);
    }

    [Fact]
    public void SetAction_WithValidUrl_ShouldSetActionUrl()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var actionUrl = "/dashboard/tasks";
        var actionText = "View Tasks";

        // Act
        content.SetAction(actionUrl, actionText);

        // Assert
        content.ActionUrl.Should().Be(actionUrl);
        // Note: ActionText property might not exist, check entity
    }

    [Fact]
    public void SetImageUrl_WithValidUrl_ShouldSetImageUrl()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var imageUrl = "https://example.com/image.jpg";

        // Act
        content.SetImageUrl(imageUrl);

        // Assert
        content.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public void UpdateMetadata_WithNewMetadata_ShouldAddMetadata()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        var key = "source";
        var value = "ai_generated";

        // Act
        content.UpdateMetadata(key, value);

        // Assert
        content.Metadata.Should().ContainKey(key);
        content.Metadata[key].Should().Be(value);
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Arrange & Act
        var content = MotivationalContent.Create(
            "Test Title",
            "Test Message",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        // Assert
        content.DomainEvents.Should().NotBeEmpty();
        content.DomainEvents.Should().ContainSingle();
        // Note: The actual event type would depend on the domain event implementation
    }

    [Fact]
    public void Activate_WhenStateChanges_ShouldRaiseDomainEvent()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsInactive().Build();
        content.ClearDomainEvents(); // Clear creation events

        // Act
        content.Activate();

        // Assert
        content.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void SoftDelete_ShouldRaiseDomainEvent()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        content.ClearDomainEvents(); // Clear creation events

        // Act
        content.SoftDelete();

        // Assert
        content.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void ConfigureABTest_ShouldRaiseDomainEvent()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();
        content.ClearDomainEvents(); // Clear creation events

        // Act
        content.ConfigureABTest("testGroup", new Dictionary<string, object>());

        // Assert
        content.DomainEvents.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public void Create_WithVeryLongTitle_ShouldNotThrow()
    {
        // Arrange
        var longTitle = new string('A', 500);
        var message = "Valid message";

        // Act
        var action = () => MotivationalContent.Create(
            longTitle,
            message,
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Create_WithVeryLongMessage_ShouldNotThrow()
    {
        // Arrange
        var title = "Valid title";
        var longMessage = new string('A', 2000);

        // Act
        var action = () => MotivationalContent.Create(
            title,
            longMessage,
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Create_WithComplexTargetConditions_ShouldPreserveConditions()
    {
        // Arrange
        var complexConditions = new Dictionary<string, object>
        {
            ["experienceLevel"] = UserExperienceLevel.Expert,
            ["minCompletionRate"] = 0.85,
            ["preferredCategories"] = new[] { "Productivity", "Learning", "Achievement" },
            ["timeOfDay"] = new[] { "morning", "afternoon" },
            ["maxDeliveries"] = 5,
            ["excludeWeekends"] = true,
            ["customMetrics"] = new Dictionary<string, object>
            {
                ["streakDays"] = 30,
                ["averageTaskTime"] = 25.5,
                ["preferredDifficulty"] = "high"
            }
        };

        // Act
        var content = MotivationalContent.Create(
            "Complex Targeting Test",
            "Test message with complex targeting",
            MotivationalContentType.Challenge,
            ContentCategory.Gamification,
            complexConditions);

        // Assert
        content.TargetConditions.Should().BeEquivalentTo(complexConditions);
        content.TargetConditions["customMetrics"].Should().BeOfType<Dictionary<string, object>>();
    }

    [Fact]
    public void OperationsOnDeletedContent_ShouldStillWork()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().AsDeleted().Build();

        // Act & Assert - These operations should not throw even on deleted content
        var updateAction = () => content.SetPriority(150);
        updateAction.Should().NotThrow();

        var targetAction = () => content.AddTargetCondition("test", "value");
        targetAction.Should().NotThrow();

        var activateAction = () => content.Activate();
        activateAction.Should().NotThrow();
    }

    [Fact]
    public void ConcurrentModification_ShouldHandleGracefully()
    {
        // Arrange
        var content = MotivationalContentBuilder.New().Build();

        // Act - Simulate concurrent modifications
        var tasks = new[]
        {
            System.Threading.Tasks.Task.Run(() => content.SetPriority(100)),
            System.Threading.Tasks.Task.Run(() => content.Activate()),
            System.Threading.Tasks.Task.Run(() => content.AddTargetCondition("test1", "value1")),
            System.Threading.Tasks.Task.Run(() => content.AddTargetCondition("test2", "value2"))
        };

        // Assert
        var action = () => System.Threading.Tasks.Task.WaitAll(tasks);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(MotivationalContentType.Insight)]
    [InlineData(MotivationalContentType.Streak)]
    [InlineData(MotivationalContentType.Achievement)]
    [InlineData(MotivationalContentType.Encouragement)]
    [InlineData(MotivationalContentType.Tip)]
    [InlineData(MotivationalContentType.Challenge)]
    [InlineData(MotivationalContentType.Celebration)]
    [InlineData(MotivationalContentType.Reminder)]
    public void Create_WithAllContentTypes_ShouldCreateSuccessfully(MotivationalContentType contentType)
    {
        // Arrange & Act
        var content = MotivationalContent.Create(
            $"Test {contentType}",
            $"Test message for {contentType}",
            contentType,
            ContentCategory.Productivity);

        // Assert
        content.Should().NotBeNull();
        content.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData(ContentCategory.Productivity)]
    [InlineData(ContentCategory.Motivation)]
    [InlineData(ContentCategory.Achievement)]
    [InlineData(ContentCategory.Learning)]
    [InlineData(ContentCategory.Wellness)]
    [InlineData(ContentCategory.Social)]
    [InlineData(ContentCategory.Gamification)]
    public void Create_WithAllCategories_ShouldCreateSuccessfully(ContentCategory category)
    {
        // Arrange & Act
        var content = MotivationalContent.Create(
            $"Test {category}",
            $"Test message for {category}",
            MotivationalContentType.Insight,
            category);

        // Assert
        content.Should().NotBeNull();
        content.Category.Should().Be(category);
    }

    #endregion
}
