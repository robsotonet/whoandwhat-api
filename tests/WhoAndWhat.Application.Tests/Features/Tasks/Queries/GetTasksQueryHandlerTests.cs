using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;
using TaskSearchCriteria = WhoAndWhat.Domain.ValueObjects.AppTaskSearchCriteria;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Queries;

public class GetTasksQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetTasksQueryHandler>> _mockLogger;
    private readonly GetTasksQueryHandler _handler;

    public GetTasksQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetTasksQueryHandler>>();
        _handler = new GetTasksQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    #region Test Constants

    private const int TestTotalCount = 2;
    private const int TestPageNumber = 1;
    private const int TestPageSize = 20;

    #endregion

    [Fact]
    public async Task Handle_Should_Return_Paged_Tasks_Successfully_With_Default_Parameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();

        var tasks = new List<DomainTask>
        {
            new DomainTask
            {
                Id = taskId1,
                UserId = userId,
                Title = "Task 1",
                Description = "Description 1",
                Category = (int)AppTaskCategory.ToDo,
                Status = (int)DomainTaskStatus.Pending,
                Priority = (int)Priority.Medium,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                TaskContacts = new List<TaskContact>()
            },
            new DomainTask
            {
                Id = taskId2,
                UserId = userId,
                Title = "Task 2",
                Description = "Description 2",
                Category = (int)AppTaskCategory.Project,
                Status = (int)DomainTaskStatus.InProgress,
                Priority = (int)Priority.High,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow,
                TaskContacts = new List<TaskContact>()
            }
        };

        var pagedResult = PagedResult<DomainTask>.Create(tasks, TestTotalCount, TestPageNumber, TestPageSize);
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().HaveCount(TestTotalCount);
        result.Value.TotalCount.Should().Be(TestTotalCount);
        result.Value.Page.Should().Be(TestPageNumber);
        result.Value.PageSize.Should().Be(TestPageSize);

        var firstTask = result.Value.Items.First();
        firstTask.Id.Should().Be(taskId1);
        firstTask.Title.Should().Be("Task 1");
        firstTask.CategoryName.Should().Be("To-Do");
        firstTask.StatusName.Should().Be("Pending");
        firstTask.PriorityName.Should().Be("Medium");

        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c => c.UserId == userId),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Search_Filter_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchText = "important task";
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(userId, Search: searchText);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c => c.SearchText == searchText),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Category_Filter_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<int> { (int)AppTaskCategory.ToDo, (int)AppTaskCategory.Project };
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(userId, Categories: categories);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c => c.Categories != null),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Status_And_Priority_Filters_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var statuses = new List<int> { (int)DomainTaskStatus.Pending, (int)DomainTaskStatus.InProgress };
        var priorities = new List<int> { (int)Priority.High, (int)Priority.Urgent };
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(userId, Statuses: statuses, Priorities: priorities);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.Statuses != null && c.Priorities != null),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Date_Range_Filters_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dueDateFrom = DateTime.UtcNow.Date;
        var dueDateTo = DateTime.UtcNow.AddDays(7).Date;
        var createdFrom = DateTime.UtcNow.AddDays(-30).Date;
        var createdTo = DateTime.UtcNow.Date;
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(
            userId,
            DueDateFrom: dueDateFrom,
            DueDateTo: dueDateTo,
            CreatedFrom: createdFrom,
            CreatedTo: createdTo);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.DueDateFrom == dueDateFrom &&
                c.DueDateTo == dueDateTo &&
                c.CreatedFrom == createdFrom &&
                c.CreatedTo == createdTo),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Boolean_Filters_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var parentTaskId = Guid.NewGuid();
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(
            userId,
            ContactIds: contactIds,
            HasDueDate: true,
            IsOverdue: false,
            HasSubtasks: true,
            ParentTaskId: parentTaskId,
            IncludeArchived: true);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.Is<AppTaskSearchCriteria>(c =>
                c.ContactIds != null &&
                c.HasDueDate == true &&
                c.IsOverdue == false &&
                c.HasSubtasks == true &&
                c.ParentTaskId == parentTaskId &&
                c.IncludeArchived == true),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Apply_Custom_Pagination_And_Sorting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, 3, 5);

        var query = new GetTasksQuery(
            userId,
            SortBy: "Title",
            SortDescending: false,
            PageSize: 5,
            PageNumber: 3);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            3,
            5,
            "Title",
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(3);
        result.Value.PageSize.Should().Be(5);

        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            3,
            5,
            "Title",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Null_SortBy_Parameter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);

        var query = new GetTasksQuery(userId, SortBy: null);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt", // Default value
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTaskRepository.Verify(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Map_Task_With_Subtasks_And_Contacts_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var subtaskId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var subtask = new DomainTask
        {
            Id = subtaskId,
            UserId = userId,
            Title = "Subtask",
            Category = (int)AppTaskCategory.ToDo,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Low,
            ParentTaskId = taskId,
            TaskContacts = new List<TaskContact>()
        };

        var task = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Parent Task",
            Description = "Parent Description",
            Category = (int)AppTaskCategory.Project,
            Status = (int)DomainTaskStatus.InProgress,
            Priority = (int)Priority.Urgent,
            DueDate = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            IsArchived = false,
            Subtasks = new List<DomainTask> { subtask },
            TaskContacts = new List<TaskContact>
            {
                new TaskContact { TaskId = taskId, ContactId = contactId, Role = "Organizer" }
            }
        };

        var tasks = new List<DomainTask> { task };
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 1, TestPageNumber, TestPageSize);
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var mappedTask = result.Value.Items.First();
        mappedTask.Id.Should().Be(taskId);
        mappedTask.Title.Should().Be("Parent Task");
        mappedTask.Description.Should().Be("Parent Description");
        mappedTask.CategoryName.Should().Be("Project");
        mappedTask.StatusName.Should().Be("In Progress");
        mappedTask.PriorityName.Should().Be("Urgent");
        mappedTask.IsArchived.Should().BeFalse();

        // Verify subtasks mapping
        mappedTask.Subtasks.Should().HaveCount(1);
        mappedTask.Subtasks.First().Id.Should().Be(subtaskId);
        mappedTask.Subtasks.First().Title.Should().Be("Subtask");
        mappedTask.Subtasks.First().ParentTaskId.Should().Be(taskId);

        // Verify task contacts mapping
        mappedTask.TaskContacts.Should().HaveCount(1);
        mappedTask.TaskContacts.First().TaskId.Should().Be(taskId);
        mappedTask.TaskContacts.First().ContactId.Should().Be(contactId);
        mappedTask.TaskContacts.First().Role.Should().Be("Organizer");
    }

    [Fact]
    public async Task Handle_Should_Handle_Empty_Results()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tasks = new List<DomainTask>();
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 0, TestPageNumber, TestPageSize);
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Page.Should().Be(TestPageNumber);
        result.Value.PageSize.Should().Be(TestPageSize);
    }

    [Fact]
    public async Task Handle_Should_Handle_Null_Navigation_Properties_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var task = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Simple Task",
            Category = (int)AppTaskCategory.Idea,
            Status = (int)DomainTaskStatus.Completed,
            Priority = (int)Priority.Medium,
            Subtasks = null,          // Null navigation property
            TaskContacts = null       // Null navigation property
        };

        var tasks = new List<DomainTask> { task };
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 1, TestPageNumber, TestPageSize);
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var mappedTask = result.Value.Items.First();
        mappedTask.Subtasks.Should().NotBeNull().And.BeEmpty();
        mappedTask.TaskContacts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving tasks");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving tasks")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Map_All_Task_Properties_Correctly_With_Archive_Info()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var parentTaskId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var archivedAt = DateTime.UtcNow.AddHours(-2);
        var dueDate = DateTime.UtcNow.AddDays(3);

        var task = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Complete Task",
            Description = "Full task description",
            Category = (int)AppTaskCategory.BillReminder,
            Status = (int)DomainTaskStatus.Completed,
            Priority = (int)Priority.Urgent,
            DueDate = dueDate,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsArchived = true,
            ArchivedAt = archivedAt,
            ParentTaskId = parentTaskId,
            TaskContacts = new List<TaskContact>()
        };

        var tasks = new List<DomainTask> { task };
        var pagedResult = PagedResult<DomainTask>.Create(tasks, 1, TestPageNumber, TestPageSize);
        var query = new GetTasksQuery(userId);

        _mockTaskRepository.Setup(x => x.SearchAsync(
            It.IsAny<AppTaskSearchCriteria>(),
            TestPageNumber,
            TestPageSize,
            "UpdatedAt",
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var mappedTask = result.Value.Items.First();
        mappedTask.Id.Should().Be(taskId);
        mappedTask.Title.Should().Be("Complete Task");
        mappedTask.Description.Should().Be("Full task description");
        mappedTask.Category.Should().Be((int)AppTaskCategory.BillReminder);
        mappedTask.CategoryName.Should().Be("Bill Reminder");
        mappedTask.Status.Should().Be((int)DomainTaskStatus.Completed);
        mappedTask.StatusName.Should().Be("Completed");
        mappedTask.Priority.Should().Be((int)Priority.Urgent);
        mappedTask.PriorityName.Should().Be("Urgent");
        mappedTask.DueDate.Should().Be(dueDate);
        mappedTask.CreatedAt.Should().Be(createdAt);
        mappedTask.UpdatedAt.Should().Be(updatedAt);
        mappedTask.IsArchived.Should().BeTrue();
        mappedTask.ArchivedAt.Should().Be(archivedAt);
        mappedTask.ParentTaskId.Should().Be(parentTaskId);
    }
}
