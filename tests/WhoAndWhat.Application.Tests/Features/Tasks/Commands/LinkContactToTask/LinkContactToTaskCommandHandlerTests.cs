using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Commands.LinkContactToTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands.LinkContactToTask;

public class LinkContactToTaskCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<LinkContactToTaskCommandHandler>> _mockLogger;
    private readonly LinkContactToTaskCommandHandler _handler;

    public LinkContactToTaskCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<LinkContactToTaskCommandHandler>>();
        _handler = new LinkContactToTaskCommandHandler(
            _mockTaskRepository.Object,
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static AppTask CreateValidTask(Guid? userId = null, Guid? taskId = null, bool isArchived = false, bool isDeleted = false)
    {
        return new AppTask
        {
            Id = taskId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Title = "Test Task",
            Status = (int)AppTaskStatus.Pending,
            Category = AppTaskCategory.ToDo,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            IsArchived = isArchived,
            IsDeleted = isDeleted,
            TaskContacts = new List<TaskContact>()
        };
    }

    private static Contact CreateValidContact(Guid? userId = null, Guid? contactId = null, bool isDeleted = false)
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
            IsDeleted = isDeleted
        };
    }

    private static LinkContactToTaskCommand CreateValidCommand(Guid? taskId = null, Guid? contactId = null, Guid? userId = null, string role = "Collaborator")
    {
        return new LinkContactToTaskCommand(
            TaskId: taskId ?? Guid.NewGuid(),
            ContactId: contactId ?? Guid.NewGuid(),
            Role: role,
            Notes: "Test notes",
            UserId: userId ?? Guid.NewGuid()
        );
    }

    private void SetupSuccessfulLinking(AppTask task, Contact contact)
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
    public async Task Handle_Should_Link_Contact_To_Task_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        SetupSuccessfulLinking(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TaskId.Should().Be(task.Id);
        result.Value.ContactId.Should().Be(contact.Id);
        result.Value.ContactName.Should().Be(contact.Name);
        result.Value.ContactEmail.Should().Be(contact.Email);
        result.Value.Role.Should().Be("Owner");

        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Collaborator")]
    [InlineData("Reviewer")]
    [InlineData("Observer")]
    public async Task Handle_Should_Accept_All_Valid_Roles(string role)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId, role);
        SetupSuccessfulLinking(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(role);
    }

    [Fact]
    public async Task Handle_Should_Add_TaskContact_To_Task()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
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
        capturedTask.TaskContacts.Should().HaveCount(1);
        
        var taskContact = capturedTask.TaskContacts.First();
        taskContact.TaskId.Should().Be(task.Id);
        taskContact.ContactId.Should().Be(contact.Id);
        taskContact.Role.Should().Be(command.Role);
        taskContact.Notes.Should().Be(command.Notes);
        taskContact.LinkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_Should_Update_Task_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId);
        var originalUpdateTime = task.UpdatedAt;

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

    #endregion

    #region Validation Failure Scenarios

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidRole")]
    [InlineData("OWNER")] // Case sensitivity test
    public async Task Handle_Should_Return_Failure_For_Invalid_Role(string invalidRole)
    {
        // Arrange
        var command = CreateValidCommand(role: invalidRole);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid role");
        
        _mockTaskRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Is_Archived()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId, isArchived: true);
        var command = CreateValidCommand(task.Id, userId: userId);
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot link contact to archived or deleted task");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Task_Is_Deleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId, isDeleted: true);
        var command = CreateValidCommand(task.Id, userId: userId);
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot link contact to archived or deleted task");
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

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Is_Deleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId, isDeleted: true);
        var command = CreateValidCommand(task.Id, contact.Id, userId);
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot link deleted contact to task");
        
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Duplicate Link Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Already_Linked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        
        // Add existing link
        task.TaskContacts.Add(new TaskContact
        {
            TaskId = task.Id,
            ContactId = contact.Id,
            Role = "Reviewer",
            LinkedAt = DateTime.UtcNow.AddDays(-1)
        });
        
        var command = CreateValidCommand(task.Id, contact.Id, userId);
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact is already linked to this task with role 'Reviewer'");
        
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
        result.Error.Should().Contain("Error linking contact to task");
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
        var command = CreateValidCommand(task.Id, contact.Id, userId);
        var cancellationToken = new CancellationToken();
        SetupSuccessfulLinking(task, contact);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockTaskRepository.Verify(x => x.GetByIdAsync(command.TaskId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, cancellationToken), Times.Once);
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), cancellationToken), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    #endregion

    #region Logging Verification

    [Fact]
    public async Task Handle_Should_Log_Successful_Link_Operation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        SetupSuccessfulLinking(task, contact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Linking contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully linked contact")),
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error linking contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}