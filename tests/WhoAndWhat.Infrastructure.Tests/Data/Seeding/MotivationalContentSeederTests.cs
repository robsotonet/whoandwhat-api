using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Data.Seeding;

namespace WhoAndWhat.Infrastructure.Tests.Data.Seeding;

/// <summary>
/// Comprehensive unit tests for MotivationalContentSeeder
/// Testing data seeding logic, content variety, and seeding behavior
/// </summary>
public class MotivationalContentSeederTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<MotivationalContentSeeder>> _mockLogger;
    private readonly MotivationalContentSeeder _seeder;

    public MotivationalContentSeederTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<MotivationalContentSeeder>>();
        _seeder = new MotivationalContentSeeder(_context, _mockLogger.Object);
    }

    #region Basic Seeding Tests

    [Fact]
    public async Task SeedAsync_WithEmptyDatabase_ShouldSeedContent()
    {
        // Arrange
        _context.MotivationalContents.Should().BeEmpty(); // Verify starting state

        // Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        seededContent.Should().NotBeEmpty();
        seededContent.Count.Should().BeGreaterThan(10, "Should seed a reasonable variety of content");
    }

    [Fact]
    public async Task SeedAsync_WithExistingContent_ShouldSkipSeeding()
    {
        // Arrange
        var existingContent = MotivationalContent.Create(
            "Existing Content",
            "This content already exists",
            MotivationalContentType.Insight,
            ContentCategory.Productivity);

        _context.MotivationalContents.Add(existingContent);
        await _context.SaveChangesAsync();

        var initialCount = await _context.MotivationalContents.CountAsync();

        // Act
        await _seeder.SeedAsync();

        // Assert
        var finalCount = await _context.MotivationalContents.CountAsync();
        finalCount.Should().Be(initialCount, "Should not seed when content already exists");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already exists")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SeedAsync_ShouldLogStartAndCompletion()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting motivational content seeding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully seeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var action = async () => await _seeder.SeedAsync(cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Content Variety Tests

    [Fact]
    public async Task SeedAsync_ShouldCreateAllContentTypes()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var contentTypes = seededContent.Select(c => c.ContentType).Distinct().ToList();

        contentTypes.Should().Contain(MotivationalContentType.Achievement);
        contentTypes.Should().Contain(MotivationalContentType.Insight);
        contentTypes.Should().Contain(MotivationalContentType.Encouragement);
        contentTypes.Should().Contain(MotivationalContentType.Reminder);
        contentTypes.Should().Contain(MotivationalContentType.Tip);

        contentTypes.Count.Should().BeGreaterThan(3, "Should include diverse content types");
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateAllContentCategories()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var categories = seededContent.Select(c => c.Category).Distinct().ToList();

        categories.Should().Contain(ContentCategory.Productivity);
        categories.Should().Contain(ContentCategory.Motivation);
        categories.Should().Contain(ContentCategory.Achievement);
        categories.Should().Contain(ContentCategory.Learning);
        categories.Should().Contain(ContentCategory.Wellness);

        categories.Count.Should().BeGreaterThan(3, "Should include diverse categories");
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateContentForDifferentExperienceLevels()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var experienceLevelContent = seededContent
            .Where(c => c.TargetConditions.ContainsKey("experienceLevel"))
            .ToList();

        experienceLevelContent.Should().NotBeEmpty("Should include experience-level targeted content");

        var experienceLevels = experienceLevelContent
            .Select(c => c.TargetConditions["experienceLevel"])
            .Distinct()
            .ToList();

        experienceLevels.Should().Contain(UserExperienceLevel.Beginner);
        experienceLevels.Should().Contain(UserExperienceLevel.Intermediate);
        experienceLevels.Should().Contain(UserExperienceLevel.Expert);
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateContentWithVariedPriorities()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var priorities = seededContent.Select(c => c.Priority).Distinct().ToList();

        priorities.Should().HaveCountGreaterThan(3, "Should have varied priority levels");
        priorities.Min().Should().BeGreaterOrEqualTo(0);
        priorities.Max().Should().BeLessOrEqualTo(200);
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateAchievementContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var achievementContent = await _context.MotivationalContents
            .Where(c => c.ContentType == MotivationalContentType.Achievement)
            .ToListAsync();

        achievementContent.Should().NotBeEmpty("Should create achievement content");
        achievementContent.Should().HaveCountGreaterThan(2, "Should create multiple achievement variations");

        foreach (var content in achievementContent)
        {
            content.Title.Should().NotBeNullOrEmpty();
            content.Message.Should().NotBeNullOrEmpty();
            content.Category.Should().BeOneOf(ContentCategory.Productivity, ContentCategory.Achievement);
            content.Priority.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateProductivityContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var productivityContent = await _context.MotivationalContents
            .Where(c => c.ContentType == MotivationalContentType.Tip || c.ContentType == MotivationalContentType.Insight)
            .Where(c => c.Category == ContentCategory.Productivity)
            .ToListAsync();

        productivityContent.Should().NotBeEmpty("Should create productivity content");
        productivityContent.Should().HaveCountGreaterThan(2, "Should create multiple productivity tips");

        foreach (var content in productivityContent)
        {
            content.Title.Should().NotBeNullOrEmpty();
            content.Message.Should().NotBeNullOrEmpty();
            content.Message.Length.Should().BeGreaterThan(20, "Tips should have meaningful content");
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateWellnessContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var wellnessContent = await _context.MotivationalContents
            .Where(c => c.Category == ContentCategory.Wellness)
            .ToListAsync();

        wellnessContent.Should().NotBeEmpty("Should create wellness content");

        foreach (var content in wellnessContent)
        {
            content.Title.Should().NotBeNullOrEmpty();
            content.Message.Should().NotBeNullOrEmpty();
            content.ContentType.Should().BeOneOf(
                MotivationalContentType.Reminder,
                MotivationalContentType.Encouragement,
                MotivationalContentType.Insight);
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateLearningContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var learningContent = await _context.MotivationalContents
            .Where(c => c.Category == ContentCategory.Learning)
            .ToListAsync();

        learningContent.Should().NotBeEmpty("Should create learning content");

        foreach (var content in learningContent)
        {
            content.Title.Should().NotBeNullOrEmpty();
            content.Message.Should().NotBeNullOrEmpty();
            content.ContentType.Should().BeOneOf(
                MotivationalContentType.Insight,
                MotivationalContentType.Tip,
                MotivationalContentType.Encouragement);
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateStreakContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var streakContent = await _context.MotivationalContents
            .Where(c => c.ContentType == MotivationalContentType.Streak)
            .ToListAsync();

        streakContent.Should().NotBeEmpty("Should create streak content");

        foreach (var content in streakContent)
        {
            content.Title.Should().Contain("Streak", "Streak content should mention streaks");
            content.TargetConditions.Should().ContainKey("minStreakDays");

            var minStreakDays = content.TargetConditions["minStreakDays"];
            minStreakDays.Should().BeOfType<int>();
            ((int)minStreakDays).Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateEncouragementContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var encouragementContent = await _context.MotivationalContents
            .Where(c => c.ContentType == MotivationalContentType.Encouragement)
            .ToListAsync();

        encouragementContent.Should().NotBeEmpty("Should create encouragement content");

        foreach (var content in encouragementContent)
        {
            content.Title.Should().NotBeNullOrEmpty();
            content.Message.Should().NotBeNullOrEmpty();
            content.Category.Should().BeOneOf(ContentCategory.Wellness, ContentCategory.Motivation, ContentCategory.Learning);
        }
    }

    #endregion

    #region Content Quality Tests

    [Fact]
    public async Task SeedAsync_AllContentShouldBeActive()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        seededContent.Should().OnlyContain(c => c.IsActive, "All seeded content should be active");
    }

    [Fact]
    public async Task SeedAsync_AllContentShouldNotBeDeleted()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        seededContent.Should().OnlyContain(c => !c.IsDeleted, "All seeded content should not be deleted");
    }

    [Fact]
    public async Task SeedAsync_AllContentShouldHaveValidTitles()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();

        seededContent.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Title), "All content should have titles");
        seededContent.Should().OnlyContain(c => c.Title.Length >= 5, "Titles should be meaningful length");
        seededContent.Should().OnlyContain(c => c.Title.Length <= 100, "Titles should not be too long");
    }

    [Fact]
    public async Task SeedAsync_AllContentShouldHaveValidMessages()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();

        seededContent.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Message), "All content should have messages");
        seededContent.Should().OnlyContain(c => c.Message.Length >= 20, "Messages should be substantial");
        seededContent.Should().OnlyContain(c => c.Message.Length <= 500, "Messages should be concise");
    }

    [Fact]
    public async Task SeedAsync_ContentShouldHaveUniqueIds()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var ids = seededContent.Select(c => c.Id).ToList();

        ids.Should().OnlyHaveUniqueItems("All content should have unique IDs");
        ids.Should().OnlyContain(id => id != Guid.Empty, "All IDs should be valid GUIDs");
    }

    [Fact]
    public async Task SeedAsync_ContentShouldHaveValidTimestamps()
    {
        // Arrange
        var beforeSeeding = DateTime.UtcNow;

        // Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var afterSeeding = DateTime.UtcNow;

        seededContent.Should().OnlyContain(c => c.CreatedAt >= beforeSeeding && c.CreatedAt <= afterSeeding,
            "All content should have recent creation timestamps");
        seededContent.Should().OnlyContain(c => c.UpdatedAt >= beforeSeeding && c.UpdatedAt <= afterSeeding,
            "All content should have recent update timestamps");
    }

    #endregion

    #region Targeting Conditions Tests

    [Fact]
    public async Task SeedAsync_TargetingConditionsShouldBeValid()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var contentWithTargeting = seededContent.Where(c => c.TargetConditions.Any()).ToList();

        contentWithTargeting.Should().NotBeEmpty("Some content should have targeting conditions");

        foreach (var content in contentWithTargeting)
        {
            content.TargetConditions.Should().NotBeNull();

            if (content.TargetConditions.ContainsKey("experienceLevel"))
            {
                var experienceLevel = content.TargetConditions["experienceLevel"];
                experienceLevel.Should().BeOfType<UserExperienceLevel>();
            }

            if (content.TargetConditions.ContainsKey("minCompletionRate"))
            {
                var minCompletionRate = content.TargetConditions["minCompletionRate"];
                minCompletionRate.Should().BeOfType<double>();
                ((double)minCompletionRate).Should().BeInRange(0.0, 1.0);
            }
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateAdvancedTargetingContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var advancedTargeting = seededContent
            .Where(c => c.TargetConditions.ContainsKey("minCompletionRate"))
            .ToList();

        advancedTargeting.Should().NotBeEmpty("Should create content with advanced targeting");

        foreach (var content in advancedTargeting)
        {
            var minCompletionRate = (double)content.TargetConditions["minCompletionRate"];
            minCompletionRate.Should().BeGreaterThan(0.5, "Advanced targeting should require higher completion rates");
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SeedAsync_WithDatabaseError_ShouldLogErrorAndThrow()
    {
        // Arrange
        await _context.Database.EnsureDeletedAsync(); // Delete the database to cause error
        await _context.DisposeAsync(); // Dispose the context

        // Act & Assert
        var action = async () => await _seeder.SeedAsync();
        await action.Should().ThrowAsync<Exception>();

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred while seeding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () => new MotivationalContentSeeder(null!, _mockLogger.Object);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () => new MotivationalContentSeeder(_context, null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Performance and Efficiency Tests

    [Fact]
    public async Task SeedAsync_ShouldBeEfficient()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _seeder.SeedAsync();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Seeding should complete within 5 seconds");
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateReasonableAmountOfContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();

        seededContent.Count.Should().BeInRange(15, 50,
            "Should create a reasonable amount of content - enough for variety but not excessive");
    }

    [Fact]
    public async Task SeedAsync_MultipleCalls_ShouldNotDuplicateContent()
    {
        // Arrange & Act
        await _seeder.SeedAsync();
        var firstCount = await _context.MotivationalContents.CountAsync();

        await _seeder.SeedAsync(); // Second call
        var secondCount = await _context.MotivationalContents.CountAsync();

        // Assert
        secondCount.Should().Be(firstCount, "Multiple seeding calls should not duplicate content");
    }

    #endregion

    #region Content Distribution Tests

    [Fact]
    public async Task SeedAsync_ShouldHaveBalancedContentDistribution()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();

        // Check content type distribution
        var contentTypeGroups = seededContent.GroupBy(c => c.ContentType).ToList();
        contentTypeGroups.Should().HaveCountGreaterThan(3, "Should have diverse content types");

        // No single content type should dominate (more than 60% of total)
        var maxTypeCount = contentTypeGroups.Max(g => g.Count());
        var totalCount = seededContent.Count;
        (maxTypeCount / (double)totalCount).Should().BeLessThan(0.6,
            "No single content type should dominate the seeded content");
    }

    [Fact]
    public async Task SeedAsync_ShouldHaveVariedPriorityDistribution()
    {
        // Arrange & Act
        await _seeder.SeedAsync();

        // Assert
        var seededContent = await _context.MotivationalContents.ToListAsync();
        var priorities = seededContent.Select(c => c.Priority).ToList();

        // Should have content across different priority ranges
        priorities.Should().Contain(p => p >= 0 && p < 50, "Should have low priority content");
        priorities.Should().Contain(p => p >= 50 && p < 100, "Should have medium priority content");
        priorities.Should().Contain(p => p >= 100, "Should have high priority content");
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
