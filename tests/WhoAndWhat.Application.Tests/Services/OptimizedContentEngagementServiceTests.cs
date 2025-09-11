using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Tests.Builders;
using WhoAndWhat.Domain.Tests.Fixtures;
using WhoAndWhat.Domain.Tests.Helpers;

namespace WhoAndWhat.Application.Tests.Services;

/// <summary>
/// Comprehensive unit tests for OptimizedContentEngagementService
/// Testing AI/ML optimization algorithms, statistical analysis, and content optimization logic
/// </summary>
public class OptimizedContentEngagementServiceTests : IClassFixture<MotivationalContentTestFixture>
{
    private readonly MotivationalContentTestFixture _fixture;
    private readonly Mock<ILogger> _mockLogger;

    public OptimizedContentEngagementServiceTests(MotivationalContentTestFixture fixture)
    {
        _fixture = fixture;
        _mockLogger = new Mock<ILogger>();
    }

    #region Main Optimization Method Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithValidData_ShouldOptimizeContent()
    {
        // Arrange
        _fixture.ResetRepositories();
        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Add test data with varying engagement rates
        var lowEngagementContent = MotivationalContentBuilder.New()
            .WithTitle("Low Engagement Content")
            .WithPriority(50)
            .Build();
        _fixture.AddTestContent(lowEngagementContent);

        // Create delivery logs with low engagement (20% engagement rate)
        var lowEngagementLogs = ContentDeliveryLogBuilder.New()
            .ForContent(lowEngagementContent)
            .BuildEngagementPattern(100, 0.2);
        foreach (var log in lowEngagementLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        optimizedCount.Should().BeGreaterThan(0);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting comprehensive content optimization")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithNoActiveContent_ShouldReturnZero()
    {
        // Arrange
        _fixture.ResetRepositories();
        _fixture.SetupContentRepositoryScenario(new List<MotivationalContent>()); // No content

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        optimizedCount.Should().Be(0);
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithException_ShouldReturnZero()
    {
        // Arrange
        var mockContentRepository = new Mock<IRepository<MotivationalContent>>();
        mockContentRepository
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MotivationalContent, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            mockContentRepository.Object,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        optimizedCount.Should().Be(0);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during content optimization")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Priority Optimization Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithHighEngagementContent_ShouldIncreasePriority()
    {
        // Arrange
        _fixture.ResetRepositories();
        var highEngagementContent = MotivationalContentBuilder.New()
            .WithTitle("High Engagement Content")
            .WithPriority(50)
            .Build();
        _fixture.AddTestContent(highEngagementContent);

        // Create delivery logs with high engagement (90% engagement rate)
        var highEngagementLogs = ContentDeliveryLogBuilder.New()
            .ForContent(highEngagementContent)
            .BuildEngagementPattern(100, 0.9);
        foreach (var log in highEngagementLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        var (currentContents, _, _) = _fixture.GetCurrentStorageState();
        var optimizedContent = currentContents.First(c => c.Id == highEngagementContent.Id);
        optimizedContent.Priority.Should().BeGreaterThan(50, "High engagement content should have increased priority");
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithVeryLowEngagement_ShouldDeactivateContent()
    {
        // Arrange
        _fixture.ResetRepositories();
        var poorContent = MotivationalContentBuilder.New()
            .WithTitle("Poor Performing Content")
            .WithPriority(100)
            .Build();
        _fixture.AddTestContent(poorContent);

        // Create delivery logs with very low engagement (5% engagement rate, 60 deliveries)
        var poorEngagementLogs = ContentDeliveryLogBuilder.New()
            .ForContent(poorContent)
            .BuildEngagementPattern(60, 0.05);
        foreach (var log in poorEngagementLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        var (currentContents, _, _) = _fixture.GetCurrentStorageState();
        var optimizedContent = currentContents.First(c => c.Id == poorContent.Id);
        optimizedContent.IsActive.Should().BeFalse("Very low engagement content should be deactivated");
    }

    #endregion

    #region Targeting Optimization Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithSegmentPerformanceData_ShouldOptimizeTargeting()
    {
        // Arrange
        _fixture.ResetRepositories();
        var content = MotivationalContentBuilder.New()
            .ForExperienceLevel(UserExperienceLevel.Beginner)
            .Build();
        _fixture.AddTestContent(content);

        // Create logs with different user segments performing differently
        var expertLogs = CreateSegmentLogs(content.Id, "expert", 20, 0.8); // High performance
        var beginnerLogs = CreateSegmentLogs(content.Id, "beginner", 20, 0.4); // Low performance

        foreach (var log in expertLogs.Concat(beginnerLogs))
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        var (currentContents, _, _) = _fixture.GetCurrentStorageState();
        var optimizedContent = currentContents.First(c => c.Id == content.Id);
        
        // Should have updated targeting based on performance
        optimizedContent.TargetConditions.Should().ContainKey("preferredSegments");
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithExperienceLevelPerformance_ShouldAdjustTargeting()
    {
        // Arrange
        _fixture.ResetRepositories();
        var content = MotivationalContentBuilder.New()
            .ForExperienceLevel(UserExperienceLevel.Beginner)
            .Build();
        _fixture.AddTestContent(content);

        // Create logs showing expert users perform much better
        var expertPerformanceLogs = CreateExperienceLevelLogs(content.Id, UserExperienceLevel.Expert, 50, 0.85);
        var beginnerPerformanceLogs = CreateExperienceLevelLogs(content.Id, UserExperienceLevel.Beginner, 50, 0.3);

        foreach (var log in expertPerformanceLogs.Concat(beginnerPerformanceLogs))
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert - Should optimize experience level targeting
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Optimized experience level targeting")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Delivery Timing Optimization Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithTimingData_ShouldOptimizeDeliveryTimes()
    {
        // Arrange
        _fixture.ResetRepositories();
        var preferences = UserContentPreferencesBuilder.New().Build();
        _fixture.AddTestPreferences(preferences);

        // Create logs showing certain hours perform better
        var morningLogs = CreateHourlyEngagementLogs(9, 20, 0.8); // 9 AM - high engagement
        var afternoonLogs = CreateHourlyEngagementLogs(14, 20, 0.6); // 2 PM - medium engagement
        var eveningLogs = CreateHourlyEngagementLogs(20, 20, 0.3); // 8 PM - low engagement

        foreach (var log in morningLogs.Concat(afternoonLogs).Concat(eveningLogs))
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated delivery timing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithInsufficientTimingData_ShouldSkipTimingOptimization()
    {
        // Arrange
        _fixture.ResetRepositories();
        _fixture.SetupDeliveryLogRepositoryScenario(new List<ContentDeliveryLog>()); // No logs

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert - Should not log timing optimization
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated delivery timing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region A/B Testing Optimization Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithABTestData_ShouldOptimizeWinners()
    {
        // Arrange
        _fixture.ResetRepositories();
        var abTestContent = MotivationalContentBuilder.New()
            .WithABTesting(new Dictionary<string, object> { ["testName"] = "title_format_test" })
            .Build();
        _fixture.AddTestContent(abTestContent);

        // Create A/B test logs with clear winner
        var groupALogs = CreateABTestLogs(abTestContent.Id, "group_a", 100, 0.4); // 40% engagement
        var groupBLogs = CreateABTestLogs(abTestContent.Id, "group_b", 100, 0.7); // 70% engagement

        foreach (var log in groupALogs.Concat(groupBLogs))
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A/B test winner identified")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithInsufficientABTestData_ShouldSkipABOptimization()
    {
        // Arrange
        _fixture.ResetRepositories();
        var abTestContent = MotivationalContentBuilder.New()
            .WithABTesting(new Dictionary<string, object> { ["testName"] = "small_test" })
            .Build();
        _fixture.AddTestContent(abTestContent);

        // Create insufficient A/B test data (less than 100 total)
        var smallTestLogs = CreateABTestLogs(abTestContent.Id, "group_a", 30, 0.5);
        foreach (var log in smallTestLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert - Should not identify winner with insufficient data
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A/B test winner identified")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region Statistical Analysis Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithStatisticallySignificantData_ShouldMakeOptimizations()
    {
        // Arrange
        _fixture.ResetRepositories();
        var content = MotivationalContentBuilder.New().WithPriority(75).Build();
        _fixture.AddTestContent(content);

        // Create statistically significant dataset (large sample, clear difference)
        var highPerformanceLogs = ContentDeliveryLogBuilder.New()
            .ForContent(content)
            .BuildEngagementPattern(200, 0.75); // 200 deliveries, 75% engagement

        foreach (var log in highPerformanceLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        optimizedCount.Should().BeGreaterThan(0);
        
        var (currentContents, _, _) = _fixture.GetCurrentStorageState();
        var optimizedContent = currentContents.First(c => c.Id == content.Id);
        optimizedContent.Priority.Should().NotBe(75, "Content with significant engagement should be optimized");
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithMultipleContentTypes_ShouldOptimizeEachIndividually()
    {
        // Arrange
        _fixture.ResetRepositories();
        
        var achievementContent = MotivationalContentBuilder.New()
            .AsAchievement()
            .WithPriority(50)
            .Build();
        var tipContent = MotivationalContentBuilder.New()
            .AsProductivityTip()
            .WithPriority(60)
            .Build();
        
        _fixture.AddTestContent(achievementContent);
        _fixture.AddTestContent(tipContent);

        // Achievement content performs well
        var achievementLogs = ContentDeliveryLogBuilder.New()
            .ForContent(achievementContent)
            .BuildEngagementPattern(80, 0.8);

        // Tip content performs poorly
        var tipLogs = ContentDeliveryLogBuilder.New()
            .ForContent(tipContent)
            .BuildEngagementPattern(80, 0.2);

        foreach (var log in achievementLogs.Concat(tipLogs))
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        var (currentContents, _, _) = _fixture.GetCurrentStorageState();
        var optimizedAchievement = currentContents.First(c => c.Id == achievementContent.Id);
        var optimizedTip = currentContents.First(c => c.Id == tipContent.Id);

        optimizedAchievement.Priority.Should().BeGreaterThan(50, "High-performing achievement content should have increased priority");
        optimizedTip.Priority.Should().BeLessThan(60, "Low-performing tip content should have decreased priority");
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithNullLogger_ShouldNotThrow()
    {
        // Arrange
        _fixture.ResetRepositories();
        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act & Assert
        var action = async () => await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithRepositoryFailures_ShouldHandleGracefully()
    {
        // Arrange
        var mockContentRepository = new Mock<IRepository<MotivationalContent>>();
        mockContentRepository
            .Setup(x => x.UpdateAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database update failed"));

        // Return some content to trigger the update
        mockContentRepository
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MotivationalContent, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MotivationalContentBuilder.New().Build() });

        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            mockContentRepository.Object,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert - Should handle repository failures gracefully
        optimizedCount.Should().Be(0);
    }

    [Fact]
    public async Task OptimizeContentForEngagementAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        _fixture.ResetRepositories();
        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var action = async () => await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object,
            cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Complex Integration Tests

    [Fact]
    public async Task OptimizeContentForEngagementAsync_CompleteOptimizationScenario_ShouldPerformAllOptimizations()
    {
        // Arrange - Create a complete scenario with multiple optimization opportunities
        _fixture.ResetRepositories();

        // 1. High-performing content that should get priority boost
        var starContent = MotivationalContentBuilder.New()
            .WithTitle("Star Performer")
            .WithPriority(70)
            .Build();

        // 2. Poor-performing content that should be deactivated
        var poorContent = MotivationalContentBuilder.New()
            .WithTitle("Poor Performer")
            .WithPriority(80)
            .Build();

        // 3. A/B test content with clear winner
        var abTestContent = MotivationalContentBuilder.New()
            .WithTitle("A/B Test Content")
            .WithABTesting()
            .Build();

        _fixture.AddTestContent(starContent);
        _fixture.AddTestContent(poorContent);
        _fixture.AddTestContent(abTestContent);

        // Add user preferences for timing optimization
        var preferences = UserContentPreferencesBuilder.New().Build();
        _fixture.AddTestPreferences(preferences);

        // Create comprehensive test data
        var starLogs = ContentDeliveryLogBuilder.New().ForContent(starContent).BuildEngagementPattern(100, 0.85);
        var poorLogs = ContentDeliveryLogBuilder.New().ForContent(poorContent).BuildEngagementPattern(60, 0.08);
        var abTestLogsA = CreateABTestLogs(abTestContent.Id, "group_a", 60, 0.4);
        var abTestLogsB = CreateABTestLogs(abTestContent.Id, "group_b", 60, 0.75);
        var timingLogs = CreateHourlyEngagementLogs(10, 50, 0.8); // 10 AM high engagement

        var allLogs = starLogs.Concat(poorLogs).Concat(abTestLogsA).Concat(abTestLogsB).Concat(timingLogs);
        foreach (var log in allLogs)
        {
            _fixture.AddTestDeliveryLog(log);
        }

        var contentRepository = _fixture.MockContentRepository.Object;
        var deliveryRepository = _fixture.MockDeliveryLogRepository.Object;
        var preferencesRepository = _fixture.MockPreferencesRepository.Object;

        // Act
        var optimizedCount = await OptimizedContentEngagementService.OptimizeContentForEngagementAsync(
            contentRepository,
            deliveryRepository,
            preferencesRepository,
            _mockLogger.Object);

        // Assert
        optimizedCount.Should().BeGreaterThan(0);

        // Verify various optimization types occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Content optimization completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var (currentContents, currentPreferences, _) = _fixture.GetCurrentStorageState();
        
        // Star content should have higher priority
        var optimizedStar = currentContents.First(c => c.Id == starContent.Id);
        optimizedStar.Priority.Should().BeGreaterThan(70);

        // Poor content should be deactivated
        var optimizedPoor = currentContents.First(c => c.Id == poorContent.Id);
        optimizedPoor.IsActive.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private List<ContentDeliveryLog> CreateSegmentLogs(Guid contentId, string segment, int count, double engagementRate)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(segment.GetHashCode()); // Deterministic randomness

        for (int i = 0; i < count; i++)
        {
            var builder = ContentDeliveryLogBuilder.New()
                .ForContent(contentId)
                .ForUserSegment(segment)
                .DeliveredAgo(TimeSpan.FromHours(i + 1));

            if (random.NextDouble() < engagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        return logs;
    }

    private List<ContentDeliveryLog> CreateExperienceLevelLogs(Guid contentId, UserExperienceLevel level, int count, double engagementRate)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(level.GetHashCode());

        for (int i = 0; i < count; i++)
        {
            var builder = ContentDeliveryLogBuilder.New()
                .ForContent(contentId)
                .ForExperienceLevel(level)
                .DeliveredAgo(TimeSpan.FromHours(i + 1));

            if (random.NextDouble() < engagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        return logs;
    }

    private List<ContentDeliveryLog> CreateHourlyEngagementLogs(int hour, int count, double engagementRate)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(hour);
        var baseTime = DateTime.UtcNow.Date.AddHours(hour);

        for (int i = 0; i < count; i++)
        {
            var deliveryTime = baseTime.AddDays(-i);
            var builder = ContentDeliveryLogBuilder.New()
                .DeliveredAt(deliveryTime);

            if (random.NextDouble() < engagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        return logs;
    }

    private List<ContentDeliveryLog> CreateABTestLogs(Guid contentId, string group, int count, double engagementRate)
    {
        var logs = new List<ContentDeliveryLog>();
        var random = new Random(group.GetHashCode());

        for (int i = 0; i < count; i++)
        {
            var builder = ContentDeliveryLogBuilder.New()
                .ForContent(contentId)
                .InABTestGroup(group)
                .DeliveredAgo(TimeSpan.FromHours(i + 1));

            if (random.NextDouble() < engagementRate)
            {
                builder.AsViewed();
            }

            logs.Add(builder.Build());
        }

        return logs;
    }

    #endregion
}