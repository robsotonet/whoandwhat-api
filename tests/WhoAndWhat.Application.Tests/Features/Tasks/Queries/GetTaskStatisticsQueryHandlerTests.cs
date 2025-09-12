using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskStatistics;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Queries;

public class GetTaskStatisticsQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetTaskStatisticsQueryHandler>> _mockLogger;
    private readonly GetTaskStatisticsQueryHandler _handler;

    public GetTaskStatisticsQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetTaskStatisticsQueryHandler>>();
        _handler = new GetTaskStatisticsQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Statistics_Successfully_With_Basic_Data()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics
        {
            TotalTasks = 10,
            CompletedTasks = 6,
            OverdueTasks = 2,
            TasksDueToday = 1,
            TasksDueThisWeek = 3,
            AverageCompletionTime = TimeSpan.FromHours(24)
        };

        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var response = result.Value;
        response.TotalTasks.Should().Be(10);
        response.CompletedTasks.Should().Be(6);
        response.OverdueTasks.Should().Be(2);
        response.TasksDueToday.Should().Be(1);
        response.TasksDueThisWeek.Should().Be(3);
        response.CompletionRate.Should().Be(60); // 6/10 * 100
        response.AverageCompletionTime.Should().Be(TimeSpan.FromHours(24));
        response.CategoryStats.Should().NotBeEmpty();
        response.PriorityStats.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Apply_Date_Range_Filter_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-30);
        var toDate = DateTime.UtcNow;
        var query = new GetTaskStatisticsQuery(userId, fromDate, toDate);

        var overallStats = new TaskStatistics
        {
            TotalTasks = 5,
            CompletedTasks = 3,
            OverdueTasks = 1,
            TasksDueToday = 0,
            TasksDueThisWeek = 2,
            AverageCompletionTime = TimeSpan.FromHours(12)
        };

        SetupOverallStatisticsMock(userId, overallStats, fromDate, toDate);
        SetupCategoryStatisticsMocks(userId, fromDate, toDate);
        SetupPriorityStatisticsMocks(userId, fromDate, toDate);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTasks.Should().Be(5);
        result.Value.CompletedTasks.Should().Be(3);
        result.Value.CompletionRate.Should().Be(60); // 3/5 * 100

        _mockTaskRepository.Verify(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.UserId == userId &&
                c.CreatedFrom == fromDate &&
                c.CreatedTo == toDate &&
                c.IncludeArchived == true),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_Should_Calculate_Category_Statistics_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 20, CompletedTasks = 10 };
        SetupOverallStatisticsMock(userId, overallStats);

        // Setup specific category statistics
        var todoStats = new TaskStatistics
        {
            TotalTasks = 8,
            CompletedTasks = 5,
            OverdueTasks = 1,
            AverageCompletionTime = TimeSpan.FromHours(6)
        };

        _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.UserId == userId &&
                c.Categories != null &&
                c.Categories.Contains((int)AppTaskCategory.ToDo)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(todoStats);

        SetupOtherCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryStats.Should().NotBeEmpty();

        var todoCategory = result.Value.CategoryStats.FirstOrDefault(c => c.Category == (int)AppTaskCategory.ToDo);
        todoCategory.Should().NotBeNull();
        todoCategory!.CategoryName.Should().Be("To-Do");
        todoCategory.TotalTasks.Should().Be(8);
        todoCategory.CompletedTasks.Should().Be(5);
        todoCategory.OverdueTasks.Should().Be(1);
        todoCategory.CompletionPercentage.Should().Be(62.5m); // 5/8 * 100
        todoCategory.AverageCompletionTime.Should().Be(TimeSpan.FromHours(6));
    }

    [Fact]
    public async Task Handle_Should_Calculate_Priority_Statistics_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 15, CompletedTasks = 8 };
        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);

        // Setup specific priority statistics
        var urgentStats = new TaskStatistics
        {
            TotalTasks = 3,
            CompletedTasks = 2,
            OverdueTasks = 1
        };

        _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.UserId == userId &&
                c.Priorities != null &&
                c.Priorities.Contains((int)Priority.Urgent)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(urgentStats);

        SetupOtherPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PriorityStats.Should().NotBeEmpty();

        var urgentPriority = result.Value.PriorityStats.FirstOrDefault(p => p.Priority == (int)Priority.Urgent);
        urgentPriority.Should().NotBeNull();
        urgentPriority!.PriorityName.Should().Be("Urgent");
        urgentPriority.TotalTasks.Should().Be(3);
        urgentPriority.CompletedTasks.Should().Be(2);
        urgentPriority.OverdueTasks.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_Handle_Zero_Tasks_Scenario()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var emptyStats = new TaskStatistics
        {
            TotalTasks = 0,
            CompletedTasks = 0,
            OverdueTasks = 0,
            TasksDueToday = 0,
            TasksDueThisWeek = 0,
            AverageCompletionTime = TimeSpan.Zero
        };

        SetupOverallStatisticsMock(userId, emptyStats);
        SetupEmptyCategoryStatisticsMocks(userId);
        SetupEmptyPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTasks.Should().Be(0);
        result.Value.CompletedTasks.Should().Be(0);
        result.Value.CompletionRate.Should().Be(0); // 0/0 should default to 0
        result.Value.CategoryStats.Should().NotBeEmpty(); // Should still have categories with 0 values
        result.Value.PriorityStats.Should().NotBeEmpty(); // Should still have priorities with 0 values
    }

    [Fact]
    public async Task Handle_Should_Handle_Category_With_Zero_Division_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 5, CompletedTasks = 2 };
        SetupOverallStatisticsMock(userId, overallStats);

        // Setup a category with zero tasks
        var emptyCategory = new TaskStatistics
        {
            TotalTasks = 0,
            CompletedTasks = 0,
            OverdueTasks = 0,
            AverageCompletionTime = TimeSpan.Zero
        };

        _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.Categories != null &&
                c.Categories.Contains((int)AppTaskCategory.Idea)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyCategory);

        SetupOtherCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ideaCategory = result.Value.CategoryStats.FirstOrDefault(c => c.Category == (int)AppTaskCategory.Idea);
        ideaCategory.Should().NotBeNull();
        ideaCategory!.CompletionPercentage.Should().Be(0); // Should handle division by zero
    }

    [Fact]
    public async Task Handle_Should_Include_All_Category_Types()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 25, CompletedTasks = 15 };
        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var expectedCategories = AppTaskCategory.GetAll().ToList();
        result.Value.CategoryStats.Should().HaveCount(expectedCategories.Count);

        foreach (var expectedCategory in expectedCategories)
        {
            result.Value.CategoryStats.Should().Contain(c => c.Category == expectedCategory.Value);
        }
    }

    [Fact]
    public async Task Handle_Should_Include_All_Priority_Types()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 20, CompletedTasks = 12 };
        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var expectedPriorities = Priority.GetAll().ToList();
        result.Value.PriorityStats.Should().HaveCount(expectedPriorities.Count);

        foreach (var expectedPriority in expectedPriorities)
        {
            result.Value.PriorityStats.Should().Contain(p => p.Priority == expectedPriority.Value);
        }
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        _mockTaskRepository.Setup(x => x.GetStatisticsAsync(It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving task statistics");
    }

    [Fact]
    public async Task Handle_Should_Calculate_Completion_Rate_With_Decimal_Precision()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics
        {
            TotalTasks = 7,
            CompletedTasks = 3, // 3/7 = 42.857...
            OverdueTasks = 1,
            TasksDueToday = 1,
            TasksDueThisWeek = 2,
            AverageCompletionTime = TimeSpan.FromHours(18)
        };

        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompletionRate.Should().Be(42.86m); // Rounded to 2 decimal places
    }

    [Fact]
    public async Task Handle_Should_Set_Include_Archived_To_True_For_All_Statistics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskStatisticsQuery(userId);

        var overallStats = new TaskStatistics { TotalTasks = 10, CompletedTasks = 5 };
        SetupOverallStatisticsMock(userId, overallStats);
        SetupCategoryStatisticsMocks(userId);
        SetupPriorityStatisticsMocks(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that all calls included archived tasks
        _mockTaskRepository.Verify(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c => c.IncludeArchived == true),
            It.IsAny<CancellationToken>()),
            Times.AtLeast(1 + AppTaskCategory.GetAll().Count() + Priority.GetAll().Count()));
    }

    // Helper methods to setup mocks
    private void SetupOverallStatisticsMock(Guid userId, TaskStatistics stats, DateTime? from = null, DateTime? to = null)
    {
        _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.UserId == userId &&
                c.CreatedFrom == from &&
                c.CreatedTo == to &&
                c.IncludeArchived == true &&
                c.Categories == null &&
                c.Priorities == null),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
    }

    private void SetupCategoryStatisticsMocks(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        foreach (var category in AppTaskCategory.GetAll())
        {
            var categoryStats = new TaskStatistics
            {
                TotalTasks = 2,
                CompletedTasks = 1,
                OverdueTasks = 0,
                AverageCompletionTime = TimeSpan.FromHours(12)
            };

            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.UserId == userId &&
                    c.CreatedFrom == from &&
                    c.CreatedTo == to &&
                    c.IncludeArchived == true &&
                    c.Categories != null &&
                    c.Categories.Contains(category.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(categoryStats);
        }
    }

    private void SetupOtherCategoryStatisticsMocks(Guid userId)
    {
        foreach (var category in AppTaskCategory.GetAll().Where(c => c != AppTaskCategory.ToDo))
        {
            var categoryStats = new TaskStatistics { TotalTasks = 2, CompletedTasks = 1 };
            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.Categories != null &&
                    c.Categories.Contains(category.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(categoryStats);
        }
    }

    private void SetupPriorityStatisticsMocks(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        foreach (var priority in Priority.GetAll())
        {
            var priorityStats = new TaskStatistics
            {
                TotalTasks = 2,
                CompletedTasks = 1,
                OverdueTasks = 0
            };

            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.UserId == userId &&
                    c.CreatedFrom == from &&
                    c.CreatedTo == to &&
                    c.IncludeArchived == true &&
                    c.Priorities != null &&
                    c.Priorities.Contains(priority.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(priorityStats);
        }
    }

    private void SetupOtherPriorityStatisticsMocks(Guid userId)
    {
        foreach (var priority in Priority.GetAll().Where(p => p != Priority.Urgent))
        {
            var priorityStats = new TaskStatistics { TotalTasks = 1, CompletedTasks = 1 };
            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.Priorities != null &&
                    c.Priorities.Contains(priority.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(priorityStats);
        }
    }

    private void SetupEmptyCategoryStatisticsMocks(Guid userId)
    {
        var emptyStats = new TaskStatistics();
        foreach (var category in AppTaskCategory.GetAll())
        {
            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.Categories != null &&
                    c.Categories.Contains(category.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyStats);
        }
    }

    private void SetupEmptyPriorityStatisticsMocks(Guid userId)
    {
        var emptyStats = new TaskStatistics();
        foreach (var priority in Priority.GetAll())
        {
            _mockTaskRepository.Setup(x => x.GetStatisticsAsync(
                It.Is<AppTaskSearchCriteria>(c =>
                    c.Priorities != null &&
                    c.Priorities.Contains(priority.Value)),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyStats);
        }
    }
}
