using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GetDashboardMetrics;

/// <summary>
/// Comprehensive unit tests for GetDashboardMetricsQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class GetDashboardMetricsQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetDashboardMetricsQueryHandler>> _mockLogger;
    private readonly GetDashboardMetricsQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetDashboardMetricsQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetDashboardMetricsQueryHandler>>();
        
        _handler = new GetDashboardMetricsQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithNoTasks_ShouldReturnEmptyMetrics()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var emptyTasks = new List<AppTask>();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((emptyTasks, 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.CompletedTasksToday.Should().Be(0);
        response.CompletedTasksThisWeek.Should().Be(0);
        response.CompletedTasksThisMonth.Should().Be(0);
        response.TotalActiveTasks.Should().Be(0);
        response.OverdueTasks.Should().Be(0);
        response.TasksCompletedOnTime.Should().Be(0);
        response.TasksCompletedLate.Should().Be(0);
        response.CompletionRate.Should().Be(0.0);
        response.OnTimeCompletionRate.Should().Be(0.0);
        
        // Category breakdown should all be zero
        response.CategoryBreakdown.TodoTasks.Should().Be(0);
        response.CategoryBreakdown.IdeaTasks.Should().Be(0);
        response.CategoryBreakdown.AppointmentTasks.Should().Be(0);
        response.CategoryBreakdown.BillReminderTasks.Should().Be(0);
        response.CategoryBreakdown.ProjectTasks.Should().Be(0);
        
        // Priority breakdown should all be zero
        response.PriorityBreakdown.CriticalTasks.Should().Be(0);
        response.PriorityBreakdown.HighTasks.Should().Be(0);
        response.PriorityBreakdown.MediumTasks.Should().Be(0);
        response.PriorityBreakdown.LowTasks.Should().Be(0);
        response.PriorityBreakdown.NoneTasks.Should().Be(0);
        
        // Trends should be empty but not null
        response.Trends.Should().NotBeNull();
        response.Trends.CurrentStreak.Should().Be(0);
        response.Trends.LongestStreak.Should().Be(0);
        response.Trends.Last7Days.Should().HaveCount(7);
        response.Trends.Last4Weeks.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_WithCompletedTasksToday_ShouldCalculateCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            CreateCompletedTask(today, AppTaskCategory.ToDo), // Completed today
            CreateCompletedTask(today, AppTaskCategory.Idea), // Completed today
            CreateCompletedTask(today.AddDays(-1), AppTaskCategory.ToDo), // Completed yesterday
            CreateActiveTask(AppTaskCategory.ToDo), // Active task
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.CompletedTasksToday.Should().Be(2);
        response.TotalActiveTasks.Should().Be(1); // Only the active task
        response.CompletionRate.Should().Be(75.0); // 3 completed out of 4 total
    }

    [Fact]
    public async Task Handle_WithOverdueTasks_ShouldCalculateCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            CreateActiveTaskWithDueDate(today.AddDays(-1)), // Overdue (due yesterday)
            CreateActiveTaskWithDueDate(today.AddDays(-2)), // Overdue (due 2 days ago)
            CreateActiveTaskWithDueDate(today.AddDays(1)),  // Not overdue (due tomorrow)
            CreateActiveTask(AppTaskCategory.ToDo),          // No due date, not overdue
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.TotalActiveTasks.Should().Be(4);
        response.OverdueTasks.Should().Be(2); // 2 tasks are overdue
    }

    [Fact]
    public async Task Handle_WithOnTimeAndLateCompletions_ShouldCalculateRates()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            // On-time completions
            CreateCompletedTaskWithDueDate(today.AddDays(-1), today, today), // Completed on time
            CreateCompletedTaskWithDueDate(today.AddDays(-2), today.AddDays(-1), today.AddDays(-3)), // Completed early
            
            // Late completions
            CreateCompletedTaskWithDueDate(today.AddDays(-5), today.AddDays(-3), today.AddDays(-1)), // Completed late
            
            // Completed without due date (not counted in on-time calculations)
            CreateCompletedTask(today.AddDays(-1), AppTaskCategory.ToDo),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.TasksCompletedOnTime.Should().Be(2); // 2 tasks completed on time or early
        response.TasksCompletedLate.Should().Be(1);   // 1 task completed late
        response.OnTimeCompletionRate.Should().Be(66.7); // 2/3 * 100, rounded to 1 decimal
        response.CompletionRate.Should().Be(100.0); // All tasks are completed
    }

    [Fact]
    public async Task Handle_ShouldCalculateCategoryBreakdownCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        
        var tasks = new List<AppTask>
        {
            CreateActiveTask(AppTaskCategory.ToDo),
            CreateActiveTask(AppTaskCategory.ToDo),
            CreateActiveTask(AppTaskCategory.Idea),
            CreateActiveTask(AppTaskCategory.Appointment),
            CreateActiveTask(AppTaskCategory.BillReminder),
            CreateActiveTask(AppTaskCategory.Project),
            CreateActiveTask(AppTaskCategory.Project),
            CreateActiveTask(AppTaskCategory.Project),
            
            // Completed tasks should not count in active breakdown
            CreateCompletedTask(DateTime.UtcNow.Date, AppTaskCategory.ToDo),
            
            // Archived tasks should not count in active breakdown
            CreateArchivedTask(AppTaskCategory.Idea),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var categoryBreakdown = response.CategoryBreakdown;
        categoryBreakdown.TodoTasks.Should().Be(2);
        categoryBreakdown.IdeaTasks.Should().Be(1);
        categoryBreakdown.AppointmentTasks.Should().Be(1);
        categoryBreakdown.BillReminderTasks.Should().Be(1);
        categoryBreakdown.ProjectTasks.Should().Be(3);
        
        response.TotalActiveTasks.Should().Be(8); // Only active, non-archived tasks
    }

    [Fact]
    public async Task Handle_ShouldCalculatePriorityBreakdownCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        
        var tasks = new List<AppTask>
        {
            CreateActiveTaskWithPriority(Priority.Urgent),
            CreateActiveTaskWithPriority(Priority.Urgent),
            CreateActiveTaskWithPriority(Priority.High),
            CreateActiveTaskWithPriority(Priority.Medium),
            CreateActiveTaskWithPriority(Priority.Medium),
            CreateActiveTaskWithPriority(Priority.Medium),
            CreateActiveTaskWithPriority(Priority.Low),
            
            // Completed tasks should not count in priority breakdown
            CreateCompletedTaskWithPriority(Priority.Urgent),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var priorityBreakdown = response.PriorityBreakdown;
        priorityBreakdown.CriticalTasks.Should().Be(2); // Urgent maps to Critical
        priorityBreakdown.HighTasks.Should().Be(1);
        priorityBreakdown.MediumTasks.Should().Be(3);
        priorityBreakdown.LowTasks.Should().Be(1);
        priorityBreakdown.NoneTasks.Should().Be(0); // Always 0 as per handler implementation
    }

    [Fact]
    public async Task Handle_ShouldCalculateWeeklyAndMonthlyStatsCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var today = new DateTime(2024, 6, 15); // Fixed date for predictable testing
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek); // Sunday of current week
        var startOfMonth = new DateTime(2024, 6, 1);
        
        var tasks = new List<AppTask>
        {
            // This week (2 tasks)
            CreateCompletedTask(today, AppTaskCategory.ToDo),
            CreateCompletedTask(startOfWeek.AddDays(1), AppTaskCategory.ToDo),
            
            // This month but not this week (1 task)
            CreateCompletedTask(startOfMonth.AddDays(2), AppTaskCategory.ToDo),
            
            // Last month (shouldn't count)
            CreateCompletedTask(startOfMonth.AddDays(-5), AppTaskCategory.ToDo),
        };

        // Use reflection to set the completion dates properly
        SetTaskCompletionDate(tasks[0], today);
        SetTaskCompletionDate(tasks[1], startOfWeek.AddDays(1));
        SetTaskCompletionDate(tasks[2], startOfMonth.AddDays(2));
        SetTaskCompletionDate(tasks[3], startOfMonth.AddDays(-5));

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.CompletedTasksThisWeek.Should().Be(2);
        response.CompletedTasksThisMonth.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldCalculateProductivityTrends()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        // Create a 5-day streak
        var tasks = new List<AppTask>();
        for (int i = 0; i < 5; i++)
        {
            var task = CreateCompletedTask(today.AddDays(-i), AppTaskCategory.ToDo);
            SetTaskCompletionDate(task, today.AddDays(-i));
            tasks.Add(task);
        }

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var trends = response.Trends;
        trends.Should().NotBeNull();
        trends.CurrentStreak.Should().Be(5);
        trends.LongestStreak.Should().Be(5);
        trends.DailyAverageCompletions.Should().BeGreaterThan(0);
        trends.WeeklyAverageCompletions.Should().BeGreaterThan(0);
        
        // Should have 7 days of data
        trends.Last7Days.Should().HaveCount(7);
        trends.Last7Days.Should().BeInAscendingOrder(d => d.Date);
        
        // Should have 4 weeks of data
        trends.Last4Weeks.Should().HaveCount(4);
        trends.Last4Weeks.Should().BeInAscendingOrder(w => w.WeekStarting);
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve dashboard metrics");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting dashboard metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        var tasks = new List<AppTask> { CreateActiveTask(AppTaskCategory.ToDo) };

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting dashboard metrics for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully retrieved dashboard metrics for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMixedTaskStatuses_ShouldFilterCorrectly()
    {
        // Arrange
        var query = new GetDashboardMetricsQuery(_testUserId);
        
        var tasks = new List<AppTask>
        {
            CreateActiveTask(AppTaskCategory.ToDo),      // Should count in active
            CreateCompletedTask(DateTime.UtcNow.Date, AppTaskCategory.ToDo), // Should count in completed
            CreateArchivedTask(AppTaskCategory.ToDo),   // Should not count in active
            CreateDeletedTask(AppTaskCategory.ToDo),    // Should not count in active
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.TotalActiveTasks.Should().Be(1); // Only the active task
        response.CompletionRate.Should().Be(25.0); // 1 completed out of 4 total tasks
    }

    #region Helper Methods

    private AppTask CreateActiveTask(AppTaskCategory category)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        return task;
    }

    private AppTask CreateActiveTaskWithPriority(Priority priority)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)AppTaskCategory.ToDo, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set priority using reflection
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)priority);
        
        return task;
    }

    private AppTask CreateActiveTaskWithDueDate(DateTime dueDate)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)AppTaskCategory.ToDo, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set due date using reflection
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
        
        return task;
    }

    private AppTask CreateCompletedTask(DateTime completionDate, AppTaskCategory category)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set as completed
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Completed);
        
        SetTaskCompletionDate(task, completionDate);
        
        return task;
    }

    private AppTask CreateCompletedTaskWithPriority(Priority priority)
    {
        var task = CreateCompletedTask(DateTime.UtcNow.Date, AppTaskCategory.ToDo);
        
        // Set priority
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)priority);
        
        return task;
    }

    private AppTask CreateCompletedTaskWithDueDate(DateTime completionDate, DateTime dueDate, DateTime createdDate)
    {
        var task = CreateCompletedTask(completionDate, AppTaskCategory.ToDo);
        
        // Set due date
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
        
        return task;
    }

    private AppTask CreateArchivedTask(AppTaskCategory category)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set as archived
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Archived);
        
        return task;
    }

    private AppTask CreateDeletedTask(AppTaskCategory category)
    {
        var task = new AppTask { Title = $"Test Task {Guid.NewGuid()}", Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set as deleted
        var isDeletedField = typeof(AppTask).GetField("_isDeleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        isDeletedField?.SetValue(task, true);
        
        return task;
    }

    private void SetTaskCompletionDate(AppTask task, DateTime completionDate)
    {
        var updatedAtField = typeof(AppTask).GetField("_updatedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        updatedAtField?.SetValue(task, completionDate);
    }

    #endregion
}