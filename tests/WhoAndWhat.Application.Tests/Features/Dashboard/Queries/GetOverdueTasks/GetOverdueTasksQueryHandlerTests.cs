using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetOverdueTasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GetOverdueTasks;

/// <summary>
/// Comprehensive unit tests for GetOverdueTasksQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class GetOverdueTasksQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetOverdueTasksQueryHandler>> _mockLogger;
    private readonly GetOverdueTasksQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetOverdueTasksQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetOverdueTasksQueryHandler>>();
        
        _handler = new GetOverdueTasksQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithNoOverdueTasks_ShouldReturnEmptyResponse()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var tasks = CreateNonOverdueTasks();

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().BeEmpty();
        response.Summary.TotalOverdue.Should().Be(0);
        response.Summary.CriticalPriorityCount.Should().Be(0);
        response.Summary.AverageDaysOverdue.Should().Be(0);
        response.Summary.MostOverdueCategory.Should().Be("None");
        response.Summary.OldestOverdueDate.Should().BeNull();
        
        response.Analytics.Should().NotBeNull();
        response.Analytics.CategoryBreakdown.Should().BeEmpty();
        response.Analytics.PriorityBreakdown.Should().BeEmpty();
        response.Analytics.OverdueRate.Should().Be(0.0);
        response.Analytics.RecommendedActions.Should().ContainSingle("Great job! You have no overdue tasks.");
    }

    [Fact]
    public async Task Handle_WithOverdueTasks_ShouldReturnCorrectTasks()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var tasks = CreateOverdueTasksScenario(today);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().HaveCount(3); // 3 overdue tasks from test scenario
        response.Tasks.Should().AllSatisfy(t => t.DaysOverdue.Should().BeGreaterThan(0));
        response.Tasks.Should().AllSatisfy(t => t.DueDate.Date.Should().BeBefore(today));
        
        // Tasks should be sorted by priority (descending) then by days overdue (descending)
        var firstTask = response.Tasks.First();
        firstTask.Priority.Should().Be("Urgent"); // Highest priority first
        
        // Verify DTO mapping
        response.Tasks.Should().AllSatisfy(t =>
        {
            t.Id.Should().NotBeEmpty();
            t.Title.Should().NotBeNullOrEmpty();
            t.Category.Should().NotBeNullOrEmpty();
            t.Priority.Should().NotBeNullOrEmpty();
            t.UrgencyLevel.Should().BeOneOf("High", "Medium", "Low");
        });
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId, CategoryFilter: "ToDo");
        var today = DateTime.UtcNow.Date;
        var tasks = CreateMixedOverdueTasks(today);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().AllSatisfy(t => t.Category.Should().Be("ToDo"));
        response.Tasks.Should().HaveCount(2); // Only ToDo tasks should be returned
    }

    [Fact]
    public async Task Handle_WithPriorityFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId, PriorityFilter: "High");
        var today = DateTime.UtcNow.Date;
        var tasks = CreateMixedOverdueTasks(today);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().AllSatisfy(t => t.Priority.Should().Be("High"));
        response.Tasks.Should().HaveCount(1); // Only High priority tasks should be returned
    }

    [Fact]
    public async Task Handle_WithLimit_ShouldLimitResults()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId, Limit: 2);
        var today = DateTime.UtcNow.Date;
        var tasks = CreateManyOverdueTasks(today, 5); // Create 5 overdue tasks

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().HaveCount(2); // Limited to 2 results
        response.Summary.TotalOverdue.Should().Be(5); // But summary should reflect total count
    }

    [Theory]
    [InlineData(3, 1, "High")] // Urgent priority = High urgency
    [InlineData(2, 5, "Medium")] // High priority + 5 days = Medium urgency  
    [InlineData(1, 8, "High")] // Medium priority + 8 days = High urgency (>7 days)
    [InlineData(1, 2, "Low")] // Medium priority + 2 days = Low urgency
    [InlineData(0, 4, "Medium")] // Low priority + 4 days = Medium urgency (>3 days)
    [InlineData(0, 1, "Low")] // Low priority + 1 day = Low urgency
    public async Task Handle_ShouldCalculateUrgencyLevelCorrectly(int priorityValue, int daysOverdue, string expectedUrgency)
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        var dueDate = today.AddDays(-daysOverdue);
        var priority = Priority.FromValue(priorityValue);
        
        var tasks = new List<AppTask>
        {
            CreateOverdueTask($"Test Task", AppTaskCategory.ToDo, priority, dueDate)
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().HaveCount(1);
        var task = response.Tasks.First();
        task.UrgencyLevel.Should().Be(expectedUrgency);
        task.DaysOverdue.Should().Be(daysOverdue);
        task.Priority.Should().Be(priority.ToString());
    }

    [Fact]
    public async Task Handle_ShouldSortTasksCorrectly()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            CreateOverdueTask("Low Priority - 5 days", AppTaskCategory.ToDo, Priority.Low, today.AddDays(-5)),
            CreateOverdueTask("High Priority - 2 days", AppTaskCategory.ToDo, Priority.High, today.AddDays(-2)),
            CreateOverdueTask("Urgent Priority - 1 day", AppTaskCategory.ToDo, Priority.Urgent, today.AddDays(-1)),
            CreateOverdueTask("High Priority - 10 days", AppTaskCategory.ToDo, Priority.High, today.AddDays(-10)),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().HaveCount(4);
        
        // Should be sorted by priority (Urgent first), then by days overdue (most overdue first)
        response.Tasks[0].Priority.Should().Be("Urgent");
        response.Tasks[0].DaysOverdue.Should().Be(1);
        
        response.Tasks[1].Priority.Should().Be("High");
        response.Tasks[1].DaysOverdue.Should().Be(10); // More days overdue comes first among High priority
        
        response.Tasks[2].Priority.Should().Be("High");
        response.Tasks[2].DaysOverdue.Should().Be(2);
        
        response.Tasks[3].Priority.Should().Be("Low");
        response.Tasks[3].DaysOverdue.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldCalculateSummaryCorrectly()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            CreateOverdueTask("Urgent Task", AppTaskCategory.ToDo, Priority.Urgent, today.AddDays(-3)),
            CreateOverdueTask("High Task", AppTaskCategory.Project, Priority.High, today.AddDays(-7)),
            CreateOverdueTask("Medium Task", AppTaskCategory.ToDo, Priority.Medium, today.AddDays(-1)),
            CreateOverdueTask("Low Task", AppTaskCategory.Idea, Priority.Low, today.AddDays(-14)), // Oldest
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var summary = response.Summary;
        summary.TotalOverdue.Should().Be(4);
        summary.CriticalPriorityCount.Should().Be(1); // Urgent
        summary.HighPriorityCount.Should().Be(1);
        summary.MediumPriorityCount.Should().Be(1);
        summary.LowPriorityCount.Should().Be(1);
        
        summary.AverageDaysOverdue.Should().Be(6.3); // (3+7+1+14)/4 = 6.25, rounded to 6.3
        summary.MostOverdueDays.Should().Be(14);
        summary.MostOverdueCategory.Should().Be("ToDo"); // 2 tasks in ToDo category
        summary.OldestOverdueDate.Should().Be(today.AddDays(-14));
    }

    [Fact]
    public async Task Handle_ShouldCalculateAnalyticsCorrectly()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var overdueTasks = CreateOverdueTasksForAnalytics(today);
        var allTasks = new List<AppTask>(overdueTasks);
        allTasks.AddRange(CreateNonOverdueTasks()); // Add some non-overdue tasks

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((allTasks, allTasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var analytics = response.Analytics;
        analytics.CategoryBreakdown.Should().NotBeEmpty();
        analytics.PriorityBreakdown.Should().NotBeEmpty();
        analytics.TrendData.Should().HaveCount(7); // Last 7 days
        analytics.OverdueRate.Should().BeGreaterThan(0);
        
        // Trend data should be ordered chronologically
        analytics.TrendData.Should().BeInAscendingOrder(t => t.Date);
        analytics.TrendData.First().Date.Should().Be(today.AddDays(-6));
        analytics.TrendData.Last().Date.Should().Be(today);
    }

    [Fact]
    public async Task Handle_ShouldGenerateRecommendationsForHighOverdueRate()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        // Create scenario with high overdue rate (more than 50%)
        var tasks = new List<AppTask>();
        
        // Add 10 overdue tasks
        for (int i = 1; i <= 10; i++)
        {
            tasks.Add(CreateOverdueTask($"Overdue Task {i}", AppTaskCategory.ToDo, Priority.Medium, today.AddDays(-i)));
        }
        
        // Add only 5 non-overdue tasks with due dates (total 15 tasks with due dates = 66% overdue rate)
        for (int i = 1; i <= 5; i++)
        {
            tasks.Add(CreateActiveTaskWithDueDate($"Future Task {i}", today.AddDays(i)));
        }

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var recommendations = response.Analytics.RecommendedActions;
        recommendations.Should().Contain(r => r.Contains("breaking large tasks into smaller"));
        recommendations.Should().Contain(r => r.Contains("Review and adjust due dates"));
        recommendations.Should().Contain(r => r.Contains("rescheduling or delegating")); // >10 overdue tasks
    }

    [Fact]
    public async Task Handle_ShouldExcludeDeletedAndCompletedTasks()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var today = DateTime.UtcNow.Date;
        
        var tasks = new List<AppTask>
        {
            CreateOverdueTask("Valid Overdue", AppTaskCategory.ToDo, Priority.High, today.AddDays(-1)),
            CreateCompletedOverdueTask("Completed Overdue", AppTaskCategory.ToDo, Priority.High, today.AddDays(-1)),
            CreateArchivedOverdueTask("Archived Overdue", AppTaskCategory.ToDo, Priority.High, today.AddDays(-1)),
            CreateDeletedOverdueTask("Deleted Overdue", AppTaskCategory.ToDo, Priority.High, today.AddDays(-1)),
        };

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Tasks.Should().HaveCount(1); // Only the valid overdue task
        response.Tasks.Single().Title.Should().Be("Valid Overdue");
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve overdue tasks");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting overdue tasks")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(_testUserId);
        var tasks = CreateNonOverdueTasks();

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting overdue tasks for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully retrieved")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #region Helper Methods

    private List<AppTask> CreateNonOverdueTasks()
    {
        var today = DateTime.UtcNow.Date;
        return new List<AppTask>
        {
            CreateActiveTask("Future Task 1", AppTaskCategory.ToDo),
            CreateActiveTaskWithDueDate("Future Task 2", today.AddDays(5)),
            CreateCompletedTask("Completed Task", AppTaskCategory.Idea),
        };
    }

    private List<AppTask> CreateOverdueTasksScenario(DateTime today)
    {
        return new List<AppTask>
        {
            CreateOverdueTask("Critical Overdue", AppTaskCategory.ToDo, Priority.Urgent, today.AddDays(-2)),
            CreateOverdueTask("High Priority Overdue", AppTaskCategory.Project, Priority.High, today.AddDays(-5)),
            CreateOverdueTask("Medium Priority Overdue", AppTaskCategory.Idea, Priority.Medium, today.AddDays(-1)),
            
            // Non-overdue tasks (should be excluded)
            CreateActiveTaskWithDueDate("Future Task", today.AddDays(3)),
            CreateCompletedTask("Completed Task", AppTaskCategory.ToDo),
        };
    }

    private List<AppTask> CreateMixedOverdueTasks(DateTime today)
    {
        return new List<AppTask>
        {
            CreateOverdueTask("ToDo Overdue 1", AppTaskCategory.ToDo, Priority.High, today.AddDays(-1)),
            CreateOverdueTask("ToDo Overdue 2", AppTaskCategory.ToDo, Priority.Medium, today.AddDays(-3)),
            CreateOverdueTask("Project Overdue", AppTaskCategory.Project, Priority.High, today.AddDays(-2)),
            CreateOverdueTask("Idea Overdue", AppTaskCategory.Idea, Priority.Low, today.AddDays(-4)),
        };
    }

    private List<AppTask> CreateManyOverdueTasks(DateTime today, int count)
    {
        var tasks = new List<AppTask>();
        var priorities = new[] { Priority.Urgent, Priority.High, Priority.Medium, Priority.Low };
        
        for (int i = 0; i < count; i++)
        {
            var priority = priorities[i % priorities.Length];
            var daysOverdue = i + 1;
            tasks.Add(CreateOverdueTask($"Overdue Task {i + 1}", AppTaskCategory.ToDo, priority, today.AddDays(-daysOverdue)));
        }
        
        return tasks;
    }

    private List<AppTask> CreateOverdueTasksForAnalytics(DateTime today)
    {
        return new List<AppTask>
        {
            CreateOverdueTask("ToDo High", AppTaskCategory.ToDo, Priority.High, today.AddDays(-3)),
            CreateOverdueTask("ToDo Medium", AppTaskCategory.ToDo, Priority.Medium, today.AddDays(-1)),
            CreateOverdueTask("Project Urgent", AppTaskCategory.Project, Priority.Urgent, today.AddDays(-5)),
        };
    }

    private AppTask CreateActiveTask(string title, AppTaskCategory category)
    {
        var task = AppTask.Create(title, category, _testUserId);
        
        // Set as in progress
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.InProgress);
        
        return task;
    }

    private AppTask CreateActiveTaskWithDueDate(string title, DateTime dueDate)
    {
        var task = CreateActiveTask(title, AppTaskCategory.ToDo);
        
        // Set due date
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
        
        return task;
    }

    private AppTask CreateOverdueTask(string title, AppTaskCategory category, Priority priority, DateTime dueDate)
    {
        var task = AppTask.Create(title, category, _testUserId);
        
        // Set as in progress (overdue)
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.InProgress);
        
        // Set priority
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)priority);
        
        // Set due date in the past
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
        
        return task;
    }

    private AppTask CreateCompletedTask(string title, AppTaskCategory category)
    {
        var task = AppTask.Create(title, category, _testUserId);
        
        // Set as completed
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Completed);
        
        return task;
    }

    private AppTask CreateCompletedOverdueTask(string title, AppTaskCategory category, Priority priority, DateTime dueDate)
    {
        var task = CreateOverdueTask(title, category, priority, dueDate);
        
        // Set as completed (should be excluded from overdue results)
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Completed);
        
        return task;
    }

    private AppTask CreateArchivedOverdueTask(string title, AppTaskCategory category, Priority priority, DateTime dueDate)
    {
        var task = CreateOverdueTask(title, category, priority, dueDate);
        
        // Set as archived (should be excluded from overdue results)
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)AppTaskStatus.Archived);
        
        return task;
    }

    private AppTask CreateDeletedOverdueTask(string title, AppTaskCategory category, Priority priority, DateTime dueDate)
    {
        var task = CreateOverdueTask(title, category, priority, dueDate);
        
        // Set as deleted (should be excluded from overdue results)
        var isDeletedField = typeof(AppTask).GetField("_isDeleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        isDeletedField?.SetValue(task, true);
        
        return task;
    }

    #endregion
}