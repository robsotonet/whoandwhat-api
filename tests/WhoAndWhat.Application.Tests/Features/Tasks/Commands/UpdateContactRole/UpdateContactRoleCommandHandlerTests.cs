using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateContactRole;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands.UpdateContactRole;

public class UpdateContactRoleCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<UpdateContactRoleCommandHandler>> _mockLogger;
    private readonly UpdateContactRoleCommandHandler _handler;

    public UpdateContactRoleCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<UpdateContactRoleCommandHandler>>();
        _handler = new UpdateContactRoleCommandHandler(
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
            Status = (int)AppTaskStatus.InProgress,
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

    private static TaskContact CreateTaskContact(Guid taskId, Guid contactId, string role = "Collaborator", string? notes = null)
    {
        return new TaskContact
        {
            TaskId = taskId,
            ContactId = contactId,
            Role = role,
            LinkedAt = DateTime.UtcNow.AddDays(-3),
            Notes = notes ?? "Original notes",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static UpdateContactRoleCommand CreateValidCommand(Guid? taskId = null, Guid? contactId = null, Guid? userId = null, string newRole = "Owner", string? notes = null)
    {
        return new UpdateContactRoleCommand(
            TaskId: taskId ?? Guid.NewGuid(),
            ContactId: contactId ?? Guid.NewGuid(),
            NewRole: newRole,
            Notes: notes,
            UserId: userId ?? Guid.NewGuid()
        );
    }

    private void SetupSuccessfulRoleUpdate(AppTask task, Contact contact)
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
    public async Task Handle_Should_Update_Contact_Role_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        SetupSuccessfulRoleUpdate(task, contact);

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
    [InlineData("Collaborator", "Owner")]
    [InlineData("Owner", "Collaborator")]
    [InlineData("Reviewer", "Observer")]
    [InlineData("Observer", "Reviewer")]
    public async Task Handle_Should_Update_Between_All_Valid_Roles(string fromRole, string toRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, fromRole);
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, toRole);
        SetupSuccessfulRoleUpdate(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(toRole);
    }

    [Fact]
    public async Task Handle_Should_Update_TaskContact_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Reviewer", "Old notes");
        task.TaskContacts.Add(taskContact);
        var originalUpdateTime = taskContact.UpdatedAt;
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner", "New notes");

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
        
        var updatedTaskContact = capturedTask.TaskContacts.First();
        updatedTaskContact.Role.Should().Be("Owner");
        updatedTaskContact.Notes.Should().Be("New notes");
        updatedTaskContact.UpdatedAt.Should().BeAfter(originalUpdateTime);
        updatedTaskContact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_Should_Update_Task_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        var originalUpdateTime = task.UpdatedAt;
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");

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
    public async Task Handle_Should_Update_Notes_When_Provided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator", "Original notes");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner", "Updated notes");
        SetupSuccessfulRoleUpdate(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskContact.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task Handle_Should_Not_Update_Notes_When_Not_Provided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator", "Original notes");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner", null);
        SetupSuccessfulRoleUpdate(task, contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskContact.Notes.Should().Be("Original notes"); // Should remain unchanged
    }

    #endregion

    #region No Change Scenarios

    [Fact]
    public async Task Handle_Should_Return_Success_When_Role_Is_Already_Same()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Owner");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner"); // Same role
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("Owner");
        
        // Should not update or save when role is the same
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Handle_Case_Insensitive_Role_Comparison()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Owner");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "OWNER"); // Different case
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("Owner");
        
        // Should not update when role is effectively the same (case insensitive)
        _mockTaskRepository.Verify(x => x.UpdateAsync(It.IsAny<AppTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Validation Failure Scenarios

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidRole")]
    [InlineData("Admin")] // Not a valid role
    public async Task Handle_Should_Return_Failure_For_Invalid_Role(string invalidRole)
    {
        // Arrange
        var command = CreateValidCommand(newRole: invalidRole);

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
        var requestingUser = Guid.NewGuid();
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
        result.Error.Should().Contain("Cannot update contact role in archived or deleted task");
        
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
        result.Error.Should().Contain("Cannot update contact role in archived or deleted task");
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
        var contactOwner = Guid.NewGuid();
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
        result.Error.Should().Contain("Cannot update role for deleted contact");
        
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

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        
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
        result.Error.Should().Contain("Error updating contact role");
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
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        var cancellationToken = new CancellationToken();
        SetupSuccessfulRoleUpdate(task, contact);

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
    public async Task Handle_Should_Log_Successful_Role_Update()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        SetupSuccessfulRoleUpdate(task, contact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updating role for contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Changing contact") &&
                                                v.ToString()!.Contains("from 'Collaborator' to 'Owner'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully updated contact") &&
                                                v.ToString()!.Contains("from 'Collaborator' to 'Owner'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_No_Change_When_Role_Is_Same()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var task = CreateValidTask(userId);
        var contact = CreateValidContact(userId);
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Owner");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        
        _mockTaskRepository.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already has role") &&
                                                v.ToString()!.Contains("No update needed")),
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
        var taskContact = CreateTaskContact(task.Id, contact.Id, "Collaborator");
        task.TaskContacts.Add(taskContact);
        
        var command = CreateValidCommand(task.Id, contact.Id, userId, "Owner");
        
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error updating contact") &&
                                                v.ToString()!.Contains("role in task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}