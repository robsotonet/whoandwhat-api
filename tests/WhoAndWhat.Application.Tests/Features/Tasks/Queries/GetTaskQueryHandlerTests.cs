using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Queries;

public class GetTaskQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<GetTaskQueryHandler>> _mockLogger;
    private readonly GetTaskQueryHandler _handler;

    public GetTaskQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<GetTaskQueryHandler>>();
        _handler = new GetTaskQueryHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Task_Successfully_With_Valid_Data()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Test Task",
            Description = "Test Description",
            Category = (int)AppTaskCategory.ToDo,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Medium,
            DueDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            TaskContacts = new List<TaskContact>()
        };

        var query = new GetTaskQuery(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(taskId);
        result.Value.Title.Should().Be("Test Task");
        result.Value.Description.Should().Be("Test Description");
        result.Value.Category.Should().Be((int)AppTaskCategory.ToDo);
        result.Value.CategoryName.Should().Be("To-Do");
        result.Value.Status.Should().Be((int)DomainTaskStatus.Pending);
        result.Value.StatusName.Should().Be("Pending");
        result.Value.Priority.Should().Be((int)Priority.Medium);
        result.Value.PriorityName.Should().Be("Medium");

        _mockTaskRepository.Verify(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Task_Without_Subtasks_When_IncludeSubtasks_Is_False()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Test Task",
            Description = "Test Description",
            Category = (int)AppTaskCategory.Project,
            Status = (int)DomainTaskStatus.InProgress,
            Priority = (int)Priority.High,
            TaskContacts = new List<TaskContact>()
        };

        var query = new GetTaskQuery(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(taskId);
        result.Value.Title.Should().Be("Test Task");

        _mockTaskRepository.Verify(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.GetByIdWithSubtasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var query = new GetTaskQuery(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainTask?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Does_Not_Own_Task()
    {
        // Arrange
        var taskOwnerId = Guid.NewGuid();
        var requestUserId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = taskOwnerId, // Different user
            Title = "Test Task",
            TaskContacts = new List<TaskContact>()
        };

        var query = new GetTaskQuery(taskId, requestUserId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Map_Subtasks_Correctly_When_IncludeSubtasks_Is_True()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var subtask1Id = Guid.NewGuid();
        var subtask2Id = Guid.NewGuid();

        var subtask1 = new DomainTask
        {
            Id = subtask1Id,
            UserId = userId,
            Title = "Subtask 1",
            Category = (int)AppTaskCategory.ToDo,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Low,
            ParentTaskId = taskId,
            TaskContacts = new List<TaskContact>()
        };

        var subtask2 = new DomainTask
        {
            Id = subtask2Id,
            UserId = userId,
            Title = "Subtask 2",
            Category = (int)AppTaskCategory.ToDo,
            Status = (int)DomainTaskStatus.Completed,
            Priority = (int)Priority.Medium,
            ParentTaskId = taskId,
            TaskContacts = new List<TaskContact>()
        };

        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Parent Task",
            Category = (int)AppTaskCategory.Project,
            Status = (int)DomainTaskStatus.InProgress,
            Priority = (int)Priority.High,
            TaskContacts = new List<TaskContact>(),
            Subtasks = new List<DomainTask> { subtask1, subtask2 }
        };

        var query = new GetTaskQuery(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Subtasks.Should().HaveCount(2);

        var mappedSubtask1 = result.Value.Subtasks.FirstOrDefault(s => s.Id == subtask1Id);
        mappedSubtask1.Should().NotBeNull();
        mappedSubtask1!.Title.Should().Be("Subtask 1");
        mappedSubtask1.ParentTaskId.Should().Be(taskId);

        var mappedSubtask2 = result.Value.Subtasks.FirstOrDefault(s => s.Id == subtask2Id);
        mappedSubtask2.Should().NotBeNull();
        mappedSubtask2!.Title.Should().Be("Subtask 2");
        mappedSubtask2.StatusName.Should().Be("Completed");
    }

    [Fact]
    public async Task Handle_Should_Map_TaskContacts_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var contactId1 = Guid.NewGuid();
        var contactId2 = Guid.NewGuid();

        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Task with Contacts",
            Category = (int)AppTaskCategory.Appointment,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Urgent,
            TaskContacts = new List<TaskContact>
            {
                new TaskContact { TaskId = taskId, ContactId = contactId1, Role = "Organizer" },
                new TaskContact { TaskId = taskId, ContactId = contactId2, Role = "Participant" }
            }
        };

        var query = new GetTaskQuery(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TaskContacts.Should().HaveCount(2);

        var organizer = result.Value.TaskContacts.FirstOrDefault(tc => tc.ContactId == contactId1);
        organizer.Should().NotBeNull();
        organizer!.Role.Should().Be("Organizer");

        var participant = result.Value.TaskContacts.FirstOrDefault(tc => tc.ContactId == contactId2);
        participant.Should().NotBeNull();
        participant!.Role.Should().Be("Participant");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var query = new GetTaskQuery(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving the task");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Map_All_Task_Properties_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var parentTaskId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var archivedAt = DateTime.UtcNow.AddHours(-2);
        var dueDate = DateTime.UtcNow.AddDays(3);

        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Complete Task Mapping",
            Description = "Test all property mapping",
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

        var query = new GetTaskQuery(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var taskDto = result.Value;
        taskDto.Id.Should().Be(taskId);
        taskDto.Title.Should().Be("Complete Task Mapping");
        taskDto.Description.Should().Be("Test all property mapping");
        taskDto.Category.Should().Be((int)AppTaskCategory.BillReminder);
        taskDto.CategoryName.Should().Be("Bill Reminder");
        taskDto.Status.Should().Be((int)DomainTaskStatus.Completed);
        taskDto.StatusName.Should().Be("Completed");
        taskDto.Priority.Should().Be((int)Priority.Urgent);
        taskDto.PriorityName.Should().Be("Urgent");
        taskDto.DueDate.Should().Be(dueDate);
        taskDto.CreatedAt.Should().Be(createdAt);
        taskDto.UpdatedAt.Should().Be(updatedAt);
        taskDto.IsArchived.Should().BeTrue();
        taskDto.ArchivedAt.Should().Be(archivedAt);
        taskDto.ParentTaskId.Should().Be(parentTaskId);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Collections_For_Null_Navigation_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Simple Task",
            Category = (int)AppTaskCategory.Idea,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Low,
            TaskContacts = null, // Null navigation property
            Subtasks = null      // Null navigation property
        };

        var query = new GetTaskQuery(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdWithSubtasksAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TaskContacts.Should().NotBeNull().And.BeEmpty();
        result.Value.Subtasks.Should().NotBeNull().And.BeEmpty();
    }
}
