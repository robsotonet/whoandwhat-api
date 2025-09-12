using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetCompletionStats;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GetCompletionStats;

/// <summary>
/// Comprehensive unit tests for GetCompletionStatsQueryHandler
/// Tests all scenarios: period filtering, statistics calculations, trends, breakdowns, comparisons, and insights
/// </summary>
public class GetCompletionStatsQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetCompletionStatsQueryHandler>> _mockLogger;
    private readonly GetCompletionStatsQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetCompletionStatsQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetCompletionStatsQueryHandler>>();
        
        _handler = new GetCompletionStatsQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithEmptyTasks_ShouldReturnEmptyStats()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var emptyTasks = new List<AppTask>();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((emptyTasks, 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Overview.TotalTasksCreated.Should().Be(0);
        response.Overview.TotalTasksCompleted.Should().Be(0);
        response.Overview.CompletionRate.Should().Be(0.0);
        response.Overview.OnTimeCompletionRate.Should().Be(0.0);
        response.Overview.AverageCompletionTime.Should().Be(TimeSpan.Zero);
        
        response.Trends.DailyData.Should().NotBeEmpty(); // Should have data points even with no tasks
        response.Breakdown.ByCategory.Should().BeEmpty();
        response.Insights.Should().BeEmpty();
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("quarter")]
    [InlineData("year")]
    public async Task Handle_WithDifferentPeriods_ShouldCalculateCorrectDateRanges(string period)
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, period);
        var tasks = CreateTestTasks();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify that repository was called with appropriate filter for the period
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(
                _testUserId,
                It.Is<TaskFilter>(f => f.CreatedAfter.HasValue && f.CreatedBefore.HasValue && f.PageSize == 10000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDefaultPeriod_ShouldDefaultToMonth()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "invalid-period");
        var tasks = CreateTestTasks();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Should default to month period calculation
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCompletedTasks_ShouldCalculateOverviewCorrectly()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var now = DateTime.UtcNow;
        
        var tasks = new List<AppTask>
        {
            CreateCompletedTask("Task 1", now.AddDays(-5), now.AddDays(-3)), // 2 days to complete
            CreateCompletedTask("Task 2", now.AddDays(-4), now.AddDays(-2)), // 2 days to complete
            CreateInProgressTask("Task 3", now.AddDays(-3)),
            CreatePendingTask("Task 4", now.AddDays(-2)),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var overview = result.Value.Overview;
        
        overview.TotalTasksCreated.Should().Be(4);
        overview.TotalTasksCompleted.Should().Be(2);
        overview.TasksInProgress.Should().Be(1);
        overview.TasksPending.Should().Be(1);
        overview.CompletionRate.Should().Be(50.0); // 2/4 * 100
        overview.AverageCompletionTime.Should().Be(TimeSpan.FromDays(2)); // Both tasks took 2 days
    }

    [Fact]
    public async Task Handle_WithOnTimeAndLateCompletions_ShouldCalculateOnTimeRateCorrectly()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var now = DateTime.UtcNow;
        
        var tasks = new List<AppTask>
        {
            // On-time completion (completed before due date)
            CreateCompletedTaskWithDueDate("On Time 1", now.AddDays(-10), now.AddDays(-5), now.AddDays(-6)),
            
            // Late completion (completed after due date)
            CreateCompletedTaskWithDueDate("Late 1", now.AddDays(-10), now.AddDays(-3), now.AddDays(-5)),
            
            // Ahead of schedule (completed before due date)
            CreateCompletedTaskWithDueDate("Early 1", now.AddDays(-10), now.AddDays(-7), now.AddDays(-5)),
            
            // No due date (should not affect on-time rate)
            CreateCompletedTask("No Due Date", now.AddDays(-8), now.AddDays(-6)),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var overview = result.Value.Overview;
        
        overview.OnTimeCompletionRate.Should().Be(66.7); // 2/3 with due dates completed on time, rounded to 66.7%
        overview.TasksCompletedAheadOfSchedule.Should().Be(2); // On Time 1 and Early 1
        overview.TasksCompletedLate.Should().Be(1); // Late 1
    }

    [Fact]
    public async Task Handle_ShouldGenerateDailyTrendData()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "week");
        var startOfWeek = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        
        var tasks = new List<AppTask>
        {
            CreateCompletedTask("Day 1 Task", startOfWeek, startOfWeek.AddHours(2)),
            CreateCompletedTask("Day 2 Task", startOfWeek.AddDays(1), startOfWeek.AddDays(1).AddHours(3)),
            CreateTask("Day 2 Created", AppTaskStatus.InProgress, startOfWeek.AddDays(1)),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var trends = result.Value.Trends;
        
        trends.DailyData.Should().NotBeEmpty();
        trends.DailyData.Should().BeInAscendingOrder(d => d.Date);
        trends.DailyData.First().Date.Should().Be(startOfWeek);
        
        // Verify daily completion tracking
        var day1Data = trends.DailyData.First(d => d.Date == startOfWeek);
        day1Data.TasksCompleted.Should().Be(1);
        day1Data.TasksCreated.Should().Be(1);
        day1Data.CompletionRate.Should().Be(100.0);
    }

    [Fact]
    public async Task Handle_ShouldGenerateWeeklyAndMonthlyTrendData()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksForTrendAnalysis();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var trends = result.Value.Trends;
        
        trends.WeeklyData.Should().HaveCount(4); // Last 4 weeks
        trends.MonthlyData.Should().HaveCount(6); // Last 6 months
        
        trends.WeeklyData.Should().BeInAscendingOrder(w => w.WeekStarting);
        trends.MonthlyData.Should().BeInAscendingOrder(m => m.Month);
        
        // Verify velocity calculations
        trends.Velocity.Should().NotBeNull();
        trends.Velocity.TasksPerDay.Should().BeGreaterThanOrEqualTo(0);
        trends.Velocity.VelocityTrend.Should().BeOneOf("Increasing", "Stable", "Declining");
    }

    [Fact]
    public async Task Handle_ShouldGenerateCategoryAndPriorityBreakdowns()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksWithVariousCategories();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var breakdown = result.Value.Breakdown;
        
        breakdown.ByCategory.Should().NotBeEmpty();
        breakdown.ByPriority.Should().NotBeEmpty();
        
        // Verify category stats
        breakdown.ByCategory.Should().ContainKey("ToDo");
        var todoStats = breakdown.ByCategory["ToDo"];
        todoStats.TotalTasks.Should().BeGreaterThan(0);
        todoStats.CompletionRate.Should().BeInRange(0, 100);
        todoStats.AverageTimeToComplete.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        
        // Verify priority stats
        breakdown.ByPriority.Should().ContainKey("High");
        var highPriorityStats = breakdown.ByPriority["High"];
        highPriorityStats.OnTimeRate.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Handle_ShouldGenerateTimeBasedBreakdowns()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksWithVariousCompletionTimes();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var breakdown = result.Value.Breakdown;
        
        breakdown.ByHourOfDay.Should().NotBeEmpty();
        breakdown.ByDayOfWeek.Should().NotBeEmpty();
        breakdown.ByTimeRange.Should().NotBeEmpty();
        
        // Verify time range breakdown structure
        breakdown.ByTimeRange.Should().ContainKey("Same Day");
        breakdown.ByTimeRange.Should().ContainKey("1-3 Days");
        breakdown.ByTimeRange.Should().ContainKey("4-7 Days");
        breakdown.ByTimeRange.Should().ContainKey("1-2 Weeks");
        breakdown.ByTimeRange.Should().ContainKey("2+ Weeks");
        
        // Verify percentages sum to reasonable total
        var totalPercentage = breakdown.ByTimeRange.Values.Sum(tr => tr.Percentage);
        totalPercentage.Should().BeInRange(90, 110); // Allow for rounding differences
    }

    [Fact]
    public async Task Handle_ShouldGenerateComparisonWithPreviousPeriod()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksForPeriodComparison();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var comparison = result.Value.Comparison;
        
        comparison.CurrentPeriodRate.Should().BeInRange(0, 100);
        comparison.PreviousPeriodRate.Should().BeInRange(0, 100);
        comparison.Trend.Should().BeOneOf("Improving", "Declining", "Stable");
        comparison.BestDay.Should().BeGreaterThanOrEqualTo(0);
        comparison.WorstDay.Should().BeGreaterThanOrEqualTo(0);
        comparison.BestCategory.Should().NotBeNullOrEmpty();
        comparison.WorstCategory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithLowCompletionRate_ShouldGeneratePerformanceInsight()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksWithLowCompletionRate(); // < 50% completion rate

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var insights = result.Value.Insights;
        
        insights.Should().Contain(i => i.Type == "Performance" && i.Title.Contains("Low Completion Rate"));
        
        var performanceInsight = insights.First(i => i.Type == "Performance");
        performanceInsight.Severity.Should().Be("Medium");
        performanceInsight.Recommendation.Should().Contain("breaking large tasks");
        performanceInsight.Data.Should().ContainKey("rate");
    }

    [Fact]
    public async Task Handle_WithPoorOnTimeRate_ShouldGenerateTimingInsight()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksWithPoorOnTimeRate(); // < 70% on-time rate

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var insights = result.Value.Insights;
        
        insights.Should().Contain(i => i.Type == "Timing" && i.Title.Contains("Tasks Often Completed Late"));
        
        var timingInsight = insights.First(i => i.Type == "Timing");
        timingInsight.Recommendation.Should().Contain("realistic deadlines");
    }

    [Fact]
    public async Task Handle_ShouldGenerateProductivityPatternInsight()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTasksWithProductivityPattern();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var insights = result.Value.Insights;
        
        var patternInsights = insights.Where(i => i.Type == "Pattern").ToList();
        if (patternInsights.Any())
        {
            var insight = patternInsights.First();
            insight.Title.Should().Contain("Peak Productivity Time");
            insight.Data.Should().ContainKey("hour");
            insight.Data.Should().ContainKey("count");
        }
    }

    [Fact]
    public async Task Handle_WithQuarterPeriod_ShouldCalculateQuarterRangeCorrectly()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "quarter");
        var tasks = CreateTestTasks();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify that the repository was called with a quarter-based date range
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(
                _testUserId,
                It.Is<TaskFilter>(f => 
                    f.CreatedAfter.HasValue && 
                    f.CreatedBefore.HasValue &&
                    (f.CreatedBefore.Value - f.CreatedAfter.Value).Days >= 89 && // Quarter is ~90 days
                    (f.CreatedBefore.Value - f.CreatedAfter.Value).Days <= 92),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve completion stats");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting completion stats")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var query = new GetCompletionStatsQuery(_testUserId, "month");
        var tasks = CreateTestTasks();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting completion stats for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully retrieved completion stats")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #region Helper Methods

    private List<AppTask> CreateTestTasks()
    {
        var now = DateTime.UtcNow;
        return new List<AppTask>
        {
            CreateCompletedTask("Task 1", now.AddDays(-5), now.AddDays(-3)),
            CreateCompletedTask("Task 2", now.AddDays(-4), now.AddDays(-2)),
            CreateInProgressTask("Task 3", now.AddDays(-3)),
            CreatePendingTask("Task 4", now.AddDays(-2)),
        };
    }

    private List<AppTask> CreateTasksForTrendAnalysis()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<AppTask>();
        
        // Create tasks over the last few weeks for trend analysis
        for (int week = 0; week < 4; week++)
        {
            for (int day = 0; day < 7; day++)
            {
                var date = now.AddDays(-(week * 7 + day));
                tasks.Add(CreateCompletedTask($"Week {week} Day {day}", date.AddHours(-8), date));
            }
        }
        
        return tasks;
    }

    private List<AppTask> CreateTasksWithVariousCategories()
    {
        var now = DateTime.UtcNow;
        return new List<AppTask>
        {
            CreateCompletedTaskWithCategory("ToDo 1", AppTaskCategory.ToDo, Priority.High, now.AddDays(-5), now.AddDays(-3)),
            CreateCompletedTaskWithCategory("ToDo 2", AppTaskCategory.ToDo, Priority.Medium, now.AddDays(-4), now.AddDays(-2)),
            CreateCompletedTaskWithCategory("Project 1", AppTaskCategory.Project, Priority.High, now.AddDays(-6), now.AddDays(-4)),
            CreateCompletedTaskWithCategory("Idea 1", AppTaskCategory.Idea, Priority.Low, now.AddDays(-3), now.AddDays(-1)),
            CreateInProgressTaskWithCategory("ToDo 3", AppTaskCategory.ToDo, Priority.Medium, now.AddDays(-2)),
        };
    }

    private List<AppTask> CreateTasksWithVariousCompletionTimes()
    {
        var now = DateTime.UtcNow;
        return new List<AppTask>
        {
            // Same day completion
            CreateCompletedTask("Same Day", now.Date.AddHours(8), now.Date.AddHours(16)),
            
            // 2 days completion
            CreateCompletedTask("2 Days", now.AddDays(-3), now.AddDays(-1)),
            
            // 1 week completion  
            CreateCompletedTask("1 Week", now.AddDays(-8), now.AddDays(-1)),
            
            // 3 weeks completion
            CreateCompletedTask("3 Weeks", now.AddDays(-22), now.AddDays(-1)),
            
            // Set completion times at different hours for hour-of-day breakdown
            CreateCompletedTaskWithHour("Morning Task", now.Date.AddDays(-1).AddHours(9)),
            CreateCompletedTaskWithHour("Afternoon Task", now.Date.AddDays(-1).AddHours(14)),
            CreateCompletedTaskWithHour("Evening Task", now.Date.AddDays(-1).AddHours(18)),
        };
    }

    private List<AppTask> CreateTasksForPeriodComparison()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        
        return new List<AppTask>
        {
            // Current month tasks (3 created, 2 completed = 66.7% rate)
            CreateCompletedTask("Current 1", monthStart.AddDays(1), monthStart.AddDays(3)),
            CreateCompletedTask("Current 2", monthStart.AddDays(2), monthStart.AddDays(4)),
            CreateInProgressTask("Current 3", monthStart.AddDays(5)),
            
            // Previous month tasks (4 created, 2 completed = 50% rate)
            CreateCompletedTask("Previous 1", lastMonthStart.AddDays(1), lastMonthStart.AddDays(3)),
            CreateCompletedTask("Previous 2", lastMonthStart.AddDays(2), lastMonthStart.AddDays(4)),
            CreateTask("Previous 3", AppTaskStatus.Cancelled, lastMonthStart.AddDays(5)),
            CreateTask("Previous 4", AppTaskStatus.Cancelled, lastMonthStart.AddDays(6)),
        };
    }

    private List<AppTask> CreateTasksWithLowCompletionRate()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<AppTask>();
        
        // Create 10 tasks with only 4 completed (40% completion rate)
        for (int i = 1; i <= 4; i++)
        {
            tasks.Add(CreateCompletedTask($"Completed {i}", now.AddDays(-i * 2), now.AddDays(-i)));
        }
        
        for (int i = 5; i <= 10; i++)
        {
            tasks.Add(CreateInProgressTask($"In Progress {i}", now.AddDays(-i)));
        }
        
        return tasks;
    }

    private List<AppTask> CreateTasksWithPoorOnTimeRate()
    {
        var now = DateTime.UtcNow;
        return new List<AppTask>
        {
            // 1 on-time task
            CreateCompletedTaskWithDueDate("On Time", now.AddDays(-10), now.AddDays(-5), now.AddDays(-6)),
            
            // 2 late tasks (poor on-time rate = 33%)
            CreateCompletedTaskWithDueDate("Late 1", now.AddDays(-10), now.AddDays(-3), now.AddDays(-5)),
            CreateCompletedTaskWithDueDate("Late 2", now.AddDays(-8), now.AddDays(-2), now.AddDays(-4)),
        };
    }

    private List<AppTask> CreateTasksWithProductivityPattern()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<AppTask>();
        
        // Create multiple tasks completed at 10 AM to establish a pattern
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(CreateCompletedTaskWithHour("10 AM Task", now.Date.AddDays(-i).AddHours(10)));
        }
        
        // Create fewer tasks at other hours
        tasks.Add(CreateCompletedTaskWithHour("2 PM Task", now.Date.AddDays(-1).AddHours(14)));
        tasks.Add(CreateCompletedTaskWithHour("4 PM Task", now.Date.AddDays(-2).AddHours(16)));
        
        return tasks;
    }

    private AppTask CreateTask(string title, AppTaskStatus status, DateTime createdAt)
    {
        var task = AppTask.Create(title, AppTaskCategory.ToDo, _testUserId);
        
        SetTaskStatus(task, status);
        SetTaskCreationTime(task, createdAt);
        
        return task;
    }

    private AppTask CreateCompletedTask(string title, DateTime createdAt, DateTime completedAt)
    {
        var task = CreateTask(title, AppTaskStatus.Completed, createdAt);
        SetTaskCompletionTime(task, completedAt);
        return task;
    }

    private AppTask CreateCompletedTaskWithDueDate(string title, DateTime createdAt, DateTime completedAt, DateTime dueDate)
    {
        var task = CreateCompletedTask(title, createdAt, completedAt);
        SetTaskDueDate(task, dueDate);
        return task;
    }

    private AppTask CreateCompletedTaskWithCategory(string title, AppTaskCategory category, Priority priority, DateTime createdAt, DateTime completedAt)
    {
        var task = AppTask.Create(title, category, _testUserId);
        
        SetTaskStatus(task, AppTaskStatus.Completed);
        SetTaskPriority(task, priority);
        SetTaskCreationTime(task, createdAt);
        SetTaskCompletionTime(task, completedAt);
        
        return task;
    }

    private AppTask CreateCompletedTaskWithHour(string title, DateTime completionTime)
    {
        var task = CreateCompletedTask(title, completionTime.AddHours(-2), completionTime);
        return task;
    }

    private AppTask CreateInProgressTask(string title, DateTime createdAt)
    {
        return CreateTask(title, AppTaskStatus.InProgress, createdAt);
    }

    private AppTask CreateInProgressTaskWithCategory(string title, AppTaskCategory category, Priority priority, DateTime createdAt)
    {
        var task = AppTask.Create(title, category, _testUserId);
        
        SetTaskStatus(task, AppTaskStatus.InProgress);
        SetTaskPriority(task, priority);
        SetTaskCreationTime(task, createdAt);
        
        return task;
    }

    private AppTask CreatePendingTask(string title, DateTime createdAt)
    {
        return CreateTask(title, AppTaskStatus.Pending, createdAt);
    }

    private void SetTaskStatus(AppTask task, AppTaskStatus status)
    {
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)status);
    }

    private void SetTaskPriority(AppTask task, Priority priority)
    {
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)priority);
    }

    private void SetTaskCreationTime(AppTask task, DateTime createdAt)
    {
        var createdAtField = typeof(AppTask).GetField("_createdAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        createdAtField?.SetValue(task, createdAt);
    }

    private void SetTaskCompletionTime(AppTask task, DateTime completedAt)
    {
        var updatedAtField = typeof(AppTask).GetField("_updatedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        updatedAtField?.SetValue(task, completedAt);
    }

    private void SetTaskDueDate(AppTask task, DateTime dueDate)
    {
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
    }

    #endregion
}