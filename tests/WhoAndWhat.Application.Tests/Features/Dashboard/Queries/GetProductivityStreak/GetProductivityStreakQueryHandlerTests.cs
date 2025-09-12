using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetProductivityStreak;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GetProductivityStreak;

/// <summary>
/// Comprehensive unit tests for GetProductivityStreakQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class GetProductivityStreakQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetProductivityStreakQueryHandler>> _mockLogger;
    private readonly GetProductivityStreakQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetProductivityStreakQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetProductivityStreakQueryHandler>>();
        
        _handler = new GetProductivityStreakQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithNoCompletedTasks_ShouldReturnEmptyResponse()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var emptyTasks = new List<AppTask>();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((emptyTasks, 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CurrentStreak.Should().Be(0);
        result.Value.LongestStreak.Should().Be(0);
        result.Value.BestMonthlyStreak.Should().Be(0);
        result.Value.LastCompletionDate.Should().BeNull();
        result.Value.StreakStartDate.Should().BeNull();
        result.Value.Milestones.Should().NotBeEmpty();
        result.Value.WeeklyStats.Should().NotBeNull();
        result.Value.MonthlyStats.Should().NotBeNull();
        result.Value.Last30Days.Should().HaveCount(30);
        
        // All milestones should be unachieved
        result.Value.Milestones.Should().AllSatisfy(m => m.IsAchieved.Should().BeFalse());
        
        _mockTaskRepository.Verify(x => x.GetTasksByUserIdAsync(
            _testUserId, 
            It.Is<TaskFilter>(f => f.PageSize == 10000), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCurrentStreak_ShouldCalculateCorrectly()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = CreateTasksForStreak(today, 5); // 5-day current streak

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentStreak.Should().Be(5);
        result.Value.LongestStreak.Should().Be(5);
        result.Value.LastCompletionDate.Should().NotBeNull();
        result.Value.StreakStartDate.Should().Be(today.AddDays(-4)); // Start of 5-day streak
        
        // Check milestones
        var threedayMilestone = result.Value.Milestones.First(m => m.Days == 3);
        threedayMilestone.IsAchieved.Should().BeTrue();
        threedayMilestone.Title.Should().Be("Getting Started");
        
        var sevenDayMilestone = result.Value.Milestones.First(m => m.Days == 7);
        sevenDayMilestone.IsAchieved.Should().BeFalse();

        // Check last 30 days - last 5 days should be part of current streak
        var last5Days = result.Value.Last30Days.TakeLast(5);
        last5Days.Should().AllSatisfy(day => day.IsPartOfCurrentStreak.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_WithBrokenStreak_ShouldNotCountCurrentStreak()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = new List<AppTask>
        {
            // Old streak - 3 days ago to 5 days ago
            CreateCompletedTask(today.AddDays(-3)),
            CreateCompletedTask(today.AddDays(-4)),
            CreateCompletedTask(today.AddDays(-5))
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentStreak.Should().Be(0); // No recent activity
        result.Value.LongestStreak.Should().Be(3); // Historical longest
        result.Value.StreakStartDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithMultipleStreaks_ShouldCalculateLongestCorrectly()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = new List<AppTask>();

        // Add current 3-day streak
        completedTasks.AddRange(CreateTasksForStreak(today, 3));
        
        // Add gap
        // Add historical 7-day streak (10-16 days ago)
        for (int i = 10; i <= 16; i++)
        {
            completedTasks.Add(CreateCompletedTask(today.AddDays(-i)));
        }

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentStreak.Should().Be(3);
        result.Value.LongestStreak.Should().Be(7); // Historical longest streak
        
        // 7-day milestone should be achieved
        var sevenDayMilestone = result.Value.Milestones.First(m => m.Days == 7);
        sevenDayMilestone.IsAchieved.Should().BeTrue();
        sevenDayMilestone.Title.Should().Be("Week Warrior");
    }

    [Fact]
    public async Task Handle_WithMonthlyStreaks_ShouldCalculateBestMonthlyStreak()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = new DateTime(2024, 6, 15); // Mid June for predictable testing
        var completedTasks = new List<AppTask>();

        // May: 5-day streak
        for (int i = 10; i <= 14; i++)
        {
            completedTasks.Add(CreateCompletedTask(new DateTime(2024, 5, i)));
        }

        // June: 8-day streak  
        for (int i = 1; i <= 8; i++)
        {
            completedTasks.Add(CreateCompletedTask(new DateTime(2024, 6, i)));
        }

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BestMonthlyStreak.Should().Be(8); // Best monthly streak from June
    }

    [Fact]
    public async Task Handle_ShouldCalculateWeeklyStatsCorrectly()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = new List<AppTask>();

        // Add tasks for 3 days this week (today, yesterday, 2 days ago)
        // 2 tasks today, 1 task yesterday, 3 tasks 2 days ago
        completedTasks.Add(CreateCompletedTask(today));
        completedTasks.Add(CreateCompletedTask(today));
        completedTasks.Add(CreateCompletedTask(today.AddDays(-1)));
        completedTasks.Add(CreateCompletedTask(today.AddDays(-2)));
        completedTasks.Add(CreateCompletedTask(today.AddDays(-2)));
        completedTasks.Add(CreateCompletedTask(today.AddDays(-2)));

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var weeklyStats = result.Value.WeeklyStats;
        weeklyStats.TotalDays.Should().Be(7);
        weeklyStats.ActiveDays.Should().Be(3); // 3 days with activity
        weeklyStats.CompletedTasks.Should().Be(6); // Total tasks this week
        weeklyStats.ConsistencyRate.Should().BeApproximately(42.9, 0.1); // 3/7 * 100
        weeklyStats.AverageTasksPerDay.Should().BeApproximately(0.9, 0.1); // 6/7
    }

    [Fact]
    public async Task Handle_ShouldCalculateMonthlyStatsCorrectly()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = new DateTime(2024, 6, 15); // Mid June for predictable testing
        var completedTasks = new List<AppTask>();

        // Add tasks for 5 days this month
        for (int day = 1; day <= 5; day++)
        {
            completedTasks.Add(CreateCompletedTask(new DateTime(2024, 6, day)));
            completedTasks.Add(CreateCompletedTask(new DateTime(2024, 6, day))); // 2 tasks per day
        }

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var monthlyStats = result.Value.MonthlyStats;
        monthlyStats.TotalDays.Should().Be(30); // June has 30 days
        monthlyStats.ActiveDays.Should().Be(5); // 5 days with activity
        monthlyStats.CompletedTasks.Should().Be(10); // Total tasks this month
        monthlyStats.ConsistencyRate.Should().BeApproximately(16.7, 0.1); // 5/30 * 100
        monthlyStats.AverageTasksPerDay.Should().BeApproximately(0.3, 0.1); // 10/30
    }

    [Fact]
    public async Task Handle_ShouldGenerateLast30DaysDataCorrectly()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = CreateTasksForStreak(today, 3); // 3-day current streak

        // Add some tasks from 10 days ago
        completedTasks.AddRange(CreateTasksForStreak(today.AddDays(-10), 2));

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var last30Days = result.Value.Last30Days;
        
        last30Days.Should().HaveCount(30);
        last30Days.Should().BeInAscendingOrder(x => x.Date);
        
        // First day should be 29 days ago
        last30Days.First().Date.Should().Be(today.AddDays(-29));
        
        // Last day should be today
        last30Days.Last().Date.Should().Be(today);
        
        // Check that current streak days are marked correctly
        var todayPoint = last30Days.Last();
        todayPoint.HasActivity.Should().BeTrue();
        todayPoint.IsPartOfCurrentStreak.Should().BeTrue();
        
        // Check historical activity
        var tenDaysAgoPoint = last30Days.Single(d => d.Date == today.AddDays(-10));
        tenDaysAgoPoint.HasActivity.Should().BeTrue();
        tenDaysAgoPoint.IsPartOfCurrentStreak.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve productivity streak");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting productivity streak")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(3, "Getting Started", "Complete tasks for 3 consecutive days")]
    [InlineData(7, "Week Warrior", "Maintain productivity for a full week")]
    [InlineData(30, "Monthly Master", "A full month of daily productivity")]
    [InlineData(100, "Century Achiever", "Reached the prestigious 100-day milestone")]
    [InlineData(365, "Year-Long Legend", "An entire year of daily achievement")]
    public async Task Handle_ShouldGenerateCorrectMilestoneTexts(int days, string expectedTitle, string expectedDescription)
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var completedTasks = CreateTasksForStreak(today, days); // Create streak of required length

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var milestone = result.Value.Milestones.First(m => m.Days == days);
        milestone.IsAchieved.Should().BeTrue();
        milestone.Title.Should().Be(expectedTitle);
        milestone.Description.Should().Be(expectedDescription);
        milestone.AchievedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var query = new GetProductivityStreakQuery(_testUserId);
        var completedTasks = CreateTasksForStreak(DateTime.UtcNow.Date, 1);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((completedTasks, completedTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting productivity streak for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully retrieved productivity streak for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #region Helper Methods

    private List<AppTask> CreateTasksForStreak(DateTime endDate, int days)
    {
        var tasks = new List<AppTask>();
        for (int i = 0; i < days; i++)
        {
            tasks.Add(CreateCompletedTask(endDate.AddDays(-i)));
        }
        return tasks;
    }

    private AppTask CreateCompletedTask(DateTime completionDate)
    {
        var task = AppTask.Create(
            title: $"Test Task {Guid.NewGuid()}",
            category: AppTaskCategory.ToDo,
            userId: _testUserId);
            
        // Use reflection to set private fields since we need to simulate completed tasks
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Completed);
        
        var updatedAtField = typeof(AppTask).GetField("_updatedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        updatedAtField?.SetValue(task, completionDate);

        return task;
    }

    #endregion
}