using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTaskScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using static WhoAndWhat.Domain.Services.CategoryBusinessRuleService;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Queries;

public class GetTaskSchedulingQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<CategoryBusinessRuleService> _mockCategoryBusinessRuleService;
    private readonly Mock<ILogger<GetTaskSchedulingQueryHandler>> _mockLogger;
    private readonly GetTaskSchedulingQueryHandler _handler;

    public GetTaskSchedulingQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockCategoryBusinessRuleService = new Mock<CategoryBusinessRuleService>();
        _mockLogger = new Mock<ILogger<GetTaskSchedulingQueryHandler>>();
        _handler = new GetTaskSchedulingQueryHandler(
            _mockTaskRepository.Object,
            _mockCategoryBusinessRuleService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Scheduling_Suggestions_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);

        var schedulingSuggestions = CreateSchedulingSuggestions(tasks);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            40,
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingSuggestions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Suggestions.Should().HaveCount(2);
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.TotalEstimatedTime.Should().Be(TimeSpan.FromHours(3)); // 1 + 2 hours

        var firstSuggestion = result.Value.Suggestions.First();
        firstSuggestion.TaskId.Should().Be(tasks[0].Id);
        firstSuggestion.TaskTitle.Should().Be("Urgent Task");
        firstSuggestion.EstimatedDuration.Should().Be(TimeSpan.FromHours(1));
        firstSuggestion.Reason.Should().Be("Due date approaching");
        firstSuggestion.Priority.Should().Be((int)Priority.Urgent);
    }

    [Fact]
    public async Task Handle_Should_Apply_Correct_Search_Criteria()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);
        var schedulingSuggestions = CreateSchedulingSuggestions(tasks);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            40,
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingSuggestions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.UserId == userId &&
                c.Statuses != null &&
                c.IncludeArchived == false),
            1,
            40, // MaxSuggestions * 2
            "DueDate",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Limit_Results_To_MaxSuggestions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var maxSuggestions = 2;
        var query = new GetTaskSchedulingQuery(userId, MaxSuggestions: maxSuggestions);

        var tasks = CreateSampleTasks(userId, count: 5); // Create more tasks than max suggestions
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 10);
        var schedulingSuggestions = CreateSchedulingSuggestions(tasks); // Returns 5 suggestions

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            4, // MaxSuggestions * 2
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingSuggestions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().HaveCount(maxSuggestions); // Should be limited to 2
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Response_When_No_Tasks_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var emptyTasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(emptyTasks, 0, 1, 40);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            40,
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().BeEmpty();
        result.Value.TotalEstimatedTime.Should().Be(TimeSpan.Zero);
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // CategoryBusinessRuleService should not be called when no tasks
        _mockCategoryBusinessRuleService.Verify(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Calculate_Total_Estimated_Time_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);

        // Create suggestions with specific durations for calculation test
        var suggestions = new List<SchedulingSuggestion>
        {
            new SchedulingSuggestion
            {
                Task = tasks[0],
                RecommendedDate = DateTime.UtcNow.AddDays(1),
                EstimatedDuration = TimeSpan.FromMinutes(30),
                Reason = "Quick task"
            },
            new SchedulingSuggestion
            {
                Task = tasks[1],
                RecommendedDate = DateTime.UtcNow.AddDays(2),
                EstimatedDuration = TimeSpan.FromHours(2.5),
                Reason = "Complex task"
            }
        };

        var schedulingResult = new SchedulingSuggestions { Suggestions = suggestions };

        _mockTaskRepository.Setup(x => x.SearchAsync(It.IsAny<AppTaskSearchCriteria>(), 1, 40, "DueDate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEstimatedTime.Should().Be(TimeSpan.FromMinutes(180)); // 30 min + 150 min = 180 min
    }

    [Fact]
    public async Task Handle_Should_Map_Suggestion_Properties_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var task = new DomainTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Test Task with Complex Mapping",
            Priority = (int)Priority.High,
            Status = (int)DomainTaskStatus.InProgress,
            TaskContacts = new List<TaskContact>()
        };

        var tasks = new List<DomainTask> { task };
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 1, 1, 40);

        var expectedDate = DateTime.UtcNow.AddDays(3);
        var expectedDuration = TimeSpan.FromHours(4);
        var expectedReason = "Optimal time for complex task";

        var suggestion = new SchedulingSuggestion
        {
            Task = task,
            RecommendedDate = expectedDate,
            EstimatedDuration = expectedDuration,
            Reason = expectedReason
        };

        var schedulingResult = new SchedulingSuggestions { Suggestions = new List<SchedulingSuggestion> { suggestion } };

        _mockTaskRepository.Setup(x => x.SearchAsync(It.IsAny<AppTaskSearchCriteria>(), 1, 40, "DueDate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().HaveCount(1);

        var mappedSuggestion = result.Value.Suggestions.First();
        mappedSuggestion.TaskId.Should().Be(task.Id);
        mappedSuggestion.TaskTitle.Should().Be("Test Task with Complex Mapping");
        mappedSuggestion.RecommendedDate.Should().Be(expectedDate);
        mappedSuggestion.EstimatedDuration.Should().Be(expectedDuration);
        mappedSuggestion.Reason.Should().Be(expectedReason);
        mappedSuggestion.Priority.Should().Be((int)Priority.High);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            40,
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while generating scheduling suggestions");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_BusinessRuleService_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);

        _mockTaskRepository.Setup(x => x.SearchAsync(It.IsAny<AppTaskSearchCriteria>(), 1, 40, "DueDate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Throws(new Exception("Business rule processing failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while generating scheduling suggestions");
    }

    [Fact]
    public async Task Handle_Should_Search_For_Incomplete_Tasks_Only()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);
        var schedulingSuggestions = CreateSchedulingSuggestions(tasks);

        _mockTaskRepository.Setup(x => x.SearchAsync(It.IsAny<AppTaskSearchCriteria>(), 1, 40, "DueDate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(schedulingSuggestions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.Statuses != null &&
                c.Statuses.Contains((int)DomainTaskStatus.Pending) &&
                c.Statuses.Contains((int)DomainTaskStatus.InProgress) &&
                c.Statuses.Contains((int)DomainTaskStatus.Confirmed)),
            1,
            40,
            "DueDate",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Empty_Suggestions_From_BusinessRuleService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTaskSchedulingQuery(userId);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 40);

        var emptySuggestions = new SchedulingSuggestions { Suggestions = new List<SchedulingSuggestion>() };

        _mockTaskRepository.Setup(x => x.SearchAsync(It.IsAny<AppTaskSearchCriteria>(), 1, 40, "DueDate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(emptySuggestions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().BeEmpty();
        result.Value.TotalEstimatedTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_Should_Use_Custom_MaxSuggestions_Parameter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customMaxSuggestions = 5;
        var query = new GetTaskSchedulingQuery(userId, MaxSuggestions: customMaxSuggestions);

        var tasks = CreateSampleTasks(userId);
        var pagedResult = PagedResult<DomainTask>.Create(tasks, tasks.Count, 1, 10); // MaxSuggestions * 2

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            10, // Should be customMaxSuggestions * 2
            "DueDate",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _mockCategoryBusinessRuleService.Setup(x => x.GetSchedulingSuggestions(It.IsAny<IEnumerable<DomainTask>>()))
            .Returns(new SchedulingSuggestions { Suggestions = new List<SchedulingSuggestion>() });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            1,
            10, // Verify that customMaxSuggestions * 2 was used
            "DueDate",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper methods
    private List<DomainTask> CreateSampleTasks(Guid userId, int count = 2)
    {
        var tasks = new List<DomainTask>();

        for (int i = 0; i < count; i++)
        {
            tasks.Add(new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = i == 0 ? "Urgent Task" : $"Task {i + 1}",
                Priority = i == 0 ? (int)Priority.Urgent : (int)Priority.Medium,
                Status = (int)DomainTaskStatus.Pending,
                DueDate = DateTime.UtcNow.AddDays(i + 1),
                TaskContacts = new List<TaskContact>()
            });
        }

        return tasks;
    }

    private SchedulingSuggestions CreateSchedulingSuggestions(List<DomainTask> tasks)
    {
        var suggestions = new List<SchedulingSuggestion>();

        for (int i = 0; i < tasks.Count; i++)
        {
            suggestions.Add(new SchedulingSuggestion
            {
                Task = tasks[i],
                RecommendedDate = DateTime.UtcNow.AddDays(i + 1),
                EstimatedDuration = TimeSpan.FromHours(i + 1),
                Reason = i == 0 ? "Due date approaching" : $"Scheduled for optimal time {i + 1}"
            });
        }

        return new SchedulingSuggestions { Suggestions = suggestions };
    }
}
