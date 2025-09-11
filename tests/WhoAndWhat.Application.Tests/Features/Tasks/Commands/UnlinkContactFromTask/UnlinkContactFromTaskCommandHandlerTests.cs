using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Features.Tasks.Commands.UnlinkContactFromTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands.UnlinkContactFromTask;

public class UnlinkContactFromTaskCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<UnlinkContactFromTaskCommandHandler>> _mockLogger;
    private readonly UnlinkContactFromTaskCommandHandler _handler;

    public UnlinkContactFromTaskCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<UnlinkContactFromTaskCommandHandler>>();
        _handler = new UnlinkContactFromTaskCommandHandler(
            _mockTaskRepository.Object,
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static AppTask CreateValidTask(Guid? userId = null, Guid? taskId = null)
    {
        return new AppTask
        {
            Id = taskId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Title = "Test Task",
            Status = (int)AppTaskStatus.InProgress,
            Category = AppTaskCategory.ToDo,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            IsArchived = false,
            IsDeleted = false,
            TaskContacts = new List<TaskContact>()
        };
    }

    private static Contact CreateValidContact(Guid? userId = null, Guid? contactId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Name = "Test Contact",
            Email = "test@example.com",
            Phone = "+1234567890",
            RelationshipType = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false
        };
    }

    private static TaskContact CreateTaskContact(Guid taskId, Guid contactId, string role = "Collaborator")
    {
        return new TaskContact
        {
            TaskId = taskId,
            ContactId = contactId,
            Role = role,
            LinkedAt = DateTime.UtcNow.AddDays(-3),
            Notes = "Test notes",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static UnlinkContactFromTaskCommand CreateValidCommand(Guid? taskId = null, Guid? contactId = null, Guid? userId = null)
    {
        return new UnlinkContactFromTaskCommand(
            TaskId: taskId ?? Guid.NewGuid(),
            ContactId: contactId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid()
        );
    }

    private void SetupSuccessfulUnlinking(AppTask task, Contact contact)
    {
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Unlink_Contact_From_Task_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Owner");
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);
        SetupSuccessfulUnlinking(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Remove_TaskContact_From_Task()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Reviewer");
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);

        AppTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .Callback<AppTask, CancellationToken>((t, ct) => capturedTask = t)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.Should().NotBeNull();
        capturedTask.TaskContacts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Update_Task_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id);
        task.TaskContacts.Add(taskContact);
        var originalUpdateTime = task.UpdatedAt;

        var command = CreateValidCommand(task.Id, contact.Id, userId);

        AppTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .Callback<AppTask, CancellationToken>((t, ct) => capturedTask = t)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.UpdatedAt.Should().BeAfter(originalUpdateTime);
        capturedTask.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_Should_Unlink_Specific_Contact_Without_Affecting_Others()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact1 = CreateValidContact(userId);
        var contact2 = CreateValidContact(userId);

        var taskContact1 = CreateTaskContact(task.Id, contact1.Id, "Owner");
        var taskContact2 = CreateTaskContact(task.Id, contact2.Id, "Collaborator");
        task.TaskContacts.Add(taskContact1);
        task.TaskContacts.Add(taskContact2);

        var command = CreateValidCommand(task.Id, contact1.Id, userId);

        AppTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact1);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .Callback<AppTask, CancellationToken>((t, ct) => capturedTask = t)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.TaskContacts.Should().HaveCount(1);
        capturedTask.TaskContacts.First().ContactId.Should().Be(contact2.Id);
        capturedTask.TaskContacts.First().Role.Should().Be("Collaborator");
    }

    #endregion

    #region Task Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Not_Found()
    {
        // Arrange
        var command = CreateValidCommand();
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppTask?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Task not found");

        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Belongs_To_Different_User()
    {
        // Arrange
        var taskOwner = Guid.NewGuid();
        var requestingUser = Guid.NewGuid(); // Different user
        var task = CreateValidTask(taskOwner);
        var command = CreateValidCommand(task.Id, userId: requestingUser);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Task not found");

        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Contact Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var command = CreateValidCommand(task.Id, userId: userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var taskOwner = Guid.NewGuid();
        var contactOwner = Guid.NewGuid(); // Different from task owner
        var task = CreateValidTask(taskOwner);
        var contact = CreateValidContact(contactOwner);
        var command = CreateValidCommand(task.Id, contact.Id, taskOwner);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region No Existing Link Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Is_Not_Linked_To_Task()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact is not linked to this task");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Has_No_Contacts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        task.TaskContacts = null; // No contacts collection
        var command = CreateValidCommand(task.Id, contact.Id, userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact is not linked to this task");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id);
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error unlinking contact from task");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Repository_GetById_Throws_Exception()
    {
        // Arrange
        var command = CreateValidCommand();
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error unlinking contact from task");

        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id);
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);
        var cancellationToken = new CancellationToken();
        SetupSuccessfulUnlinking(task, contact);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockTaskRepository.Verify(x => x.GetByIdAsync(command.TaskId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, cancellationToken), Times.Once);
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), cancellationToken), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var command = CreateValidCommand();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error unlinking contact from task");
    }

    #endregion

    #region Logging Verification

    [Fact]
    public async Task Handle_Should_Log_Successful_Unlink_Operation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Owner");
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);
        SetupSuccessfulUnlinking(task, contact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unlinking contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully unlinked contact") &&
                                                v.ToString()!.Contains("Owner")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Not_Linked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No existing relationship found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Errors()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id);
        task.TaskContacts.Add(taskContact);

        var command = CreateValidCommand(task.Id, contact.Id, userId);

        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockTaskRepository.Setup(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error unlinking contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
