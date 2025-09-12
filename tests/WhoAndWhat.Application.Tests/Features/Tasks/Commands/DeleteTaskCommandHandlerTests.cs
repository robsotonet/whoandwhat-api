using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands;

public class DeleteTaskCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<ILogger<DeleteTaskCommandHandler>> _mockLogger;
    private readonly DeleteTaskCommandHandler _handler;

    public DeleteTaskCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockLogger = new Mock<ILogger<DeleteTaskCommandHandler>>();
        _handler = new DeleteTaskCommandHandler(
            _mockTaskRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Soft_Delete_Task_By_Default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Task to Delete",
            IsArchived = false,
            ArchivedAt = null
        };

        var command = new DeleteTaskCommand(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingTask.IsArchived.Should().BeTrue();
        existingTask.ArchivedAt.Should().NotBeNull();
        existingTask.ArchivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        _mockTaskRepository.Verify(x => x.UpdateAsync(existingTask, It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.DeleteAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Hard_Delete_Task_When_HardDelete_Is_True()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Task to Hard Delete"
        };

        var command = new DeleteTaskCommand(taskId, userId, true);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _mockTaskRepository.Setup(x => x.DeleteAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockTaskRepository.Verify(x => x.DeleteAsync(existingTask, It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Not_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new DeleteTaskCommand(taskId, Guid.NewGuid(), false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainTask?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.DeleteAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
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
            Title = "Task"
        };

        var command = new DeleteTaskCommand(taskId, requestUserId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task not found");

        _mockTaskRepository.Verify(x => x.DeleteAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_For_Soft_Delete()
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
            Title = "Task",
            UpdatedAt = originalUpdatedAt
        };

        var command = new DeleteTaskCommand(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingTask.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        existingTask.UpdatedAt.Should().BeOnOrBefore(afterExecution);
        existingTask.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = userId,
            Title = "Task"
        };

        var command = new DeleteTaskCommand(taskId, userId, false);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while deleting the task");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deleting task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
