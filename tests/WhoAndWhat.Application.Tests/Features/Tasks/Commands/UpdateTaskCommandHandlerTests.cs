using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;
using AppTaskUpdateRequest = WhoAndWhat.Domain.Services.AppTaskUpdateRequest;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands;

public class UpdateTaskCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly CategoryBusinessRuleService _businessRuleService;
    private readonly Mock<ILogger<UpdateTaskCommandHandler>> _mockLogger;
    private readonly UpdateTaskCommandHandler _handler;

    public UpdateTaskCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _businessRuleService = new CategoryBusinessRuleService();
        _mockLogger = new Mock<ILogger<UpdateTaskCommandHandler>>();
        _handler = new UpdateTaskCommandHandler(
            _mockTaskRepository.Object,
            _businessRuleService,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Update_Task_Successfully_With_Valid_Data()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Original Title",
            Description = "Original Description",
            Category = (int)AppTaskCategory.ToDo,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Low,
            DueDate = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            TaskContacts = new List<TaskContact>()
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: "Updated Description",
            Category: null, // Keep same category - don't convert
            Status: (int)DomainTaskStatus.InProgress,
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddDays(10),
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - no setup needed

        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Updated Title");
        result.Value.Description.Should().Be("Updated Description");
        result.Value.Category.Should().Be((int)AppTaskCategory.ToDo);
        result.Value.Status.Should().Be((int)DomainTaskStatus.InProgress);
        result.Value.Priority.Should().Be((int)Priority.High);

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Not_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainTask?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Does_Not_Own_Task()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var taskOwnerId = Guid.NewGuid();
        var requestUserId = Guid.NewGuid();
        
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = taskOwnerId, // Different user
            Title = "Original Title"
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: requestUserId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Clear_DueDate_When_ClearDueDate_Is_True()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Title",
            DueDate = DateTime.UtcNow.AddDays(5),
            TaskContacts = new List<TaskContact>()
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: null,
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: true,
            ContactIds: null,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DueDate.Should().BeNull();
        
        capturedTask.Should().NotBeNull();
        capturedTask.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Update_Contacts_When_ContactIds_Provided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var newContactIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Title",
            TaskContacts = new List<TaskContact>
            {
                new TaskContact { TaskId = taskId, ContactId = Guid.NewGuid(), Role = "Participant" }
            }
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: null,
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: newContactIds,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskContacts.Should().HaveCount(2);
        
        capturedTask.Should().NotBeNull();
        capturedTask.TaskContacts.Should().HaveCount(2);
        capturedTask.TaskContacts.All(tc => newContactIds.Contains(tc.ContactId)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Not_Update_When_No_Changes_Made()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Title",
            Description = "Description",
            TaskContacts = new List<TaskContact>()
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Title", // Same as existing
            Description: "Description", // Same as existing
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - no setup needed

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // UpdateAsync should not be called since no changes were made
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Validation_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Title",
            Category = (int)AppTaskCategory.BillReminder,
            DueDate = DateTime.UtcNow.AddDays(7), // Give it a due date so clearing it will trigger validation
            TaskContacts = new List<TaskContact>()
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "", // Empty title should definitely fail validation
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - BillReminder without due date should fail naturally

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Title is required");
        
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);
        var beforeExecution = DateTime.UtcNow;
        
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Original Title",
            UpdatedAt = originalUpdatedAt,
            TaskContacts = new List<TaskContact>()
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: false,
            ContactIds: null,
            Metadata: null,
            UserId: userId
        );

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        result.Value.UpdatedAt.Should().BeOnOrBefore(afterExecution);
        result.Value.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        
        capturedTask.Should().NotBeNull();
        capturedTask.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        capturedTask.UpdatedAt.Should().BeOnOrBefore(afterExecution);
    }
}