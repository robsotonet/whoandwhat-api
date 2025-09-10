using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.DeleteContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands.DeleteContact;

public class DeleteContactCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<DeleteContactCommandHandler>> _mockLogger;
    private readonly DeleteContactCommandHandler _handler;

    public DeleteContactCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<DeleteContactCommandHandler>>();
        _handler = new DeleteContactCommandHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a valid existing contact for testing
    /// </summary>
    private static Contact CreateValidExistingContact(Guid? userId = null, Guid? contactId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Name = "Test Contact",
            Email = "test@example.com",
            Phone = "+1234567890",
            RelationshipType = 1, // Friend
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false,
            Tasks = new List<AppTask>()
        };
    }

    /// <summary>
    /// Creates a task associated with a contact
    /// </summary>
    private static AppTask CreateAssociatedTask(Guid contactId, Guid userId, string title = "Test Task", AppTaskStatus? status = null)
    {
        return new AppTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            UserId = userId,
            Status = (int)(status ?? AppTaskStatus.Pending),
            Category = AppTaskCategory.ToDo,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
            TaskContacts = new List<TaskContact>
            {
                new TaskContact
                {
                    ContactId = contactId,
                    Role = "Owner",
                    LinkedAt = DateTime.UtcNow.AddDays(-3)
                }
            }
        };
    }

    /// <summary>
    /// Sets up successful repository mocks for soft delete operations
    /// </summary>
    private void SetupSuccessfulSoftDeleteRepositoryMocks(Contact existingContact)
    {
        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.CountActiveTaskAssociationsAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockContactRepository.Setup(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock to capture the soft deleted contact
    /// </summary>
    private void SetupContactCaptureSoftDeleteRepositoryMocks(Contact existingContact, out Contact capturedContact)
    {
        Contact captured = null!;
        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.CountActiveTaskAssociationsAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockContactRepository.Setup(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((cId, uId, ct) => captured = existingContact)
            .ReturnsAsync(true);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        capturedContact = captured;
    }

    private static DeleteContactCommand CreateValidDeleteCommand(Guid? contactId = null, Guid? userId = null) => new(
        ContactId: contactId ?? Guid.NewGuid(),
        UserId: userId ?? Guid.NewGuid()
    );

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Soft_Delete_Contact_Successfully()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.CountActiveTaskAssociationsAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Set_Correct_Soft_Delete_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var existingContact = CreateValidExistingContact(userId, contactId);
        var command = CreateValidDeleteCommand(contactId, userId);

        SetupContactCaptureSoftDeleteRepositoryMocks(existingContact, out var capturedContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        // Verify the repository methods were called correctly
        _mockContactRepository.Verify(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.CountActiveTaskAssociationsAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Soft_Delete_Contact_With_Active_Tasks()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var activeTask = CreateAssociatedTask(existingContact.Id, existingContact.UserId, "Active Task", AppTaskStatus.InProgress);
        existingContact.Tasks.Add(activeTask);
        
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Soft_Delete_Contact_With_Completed_Tasks()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var completedTask = CreateAssociatedTask(existingContact.Id, existingContact.UserId, "Completed Task", AppTaskStatus.Completed);
        existingContact.Tasks.Add(completedTask);
        
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_ContactId_Is_Empty()
    {
        // Arrange
        var command = new DeleteContactCommand(
            ContactId: Guid.Empty,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact ID is required");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new DeleteContactCommand(
            ContactId: Guid.NewGuid(),
            UserId: Guid.Empty
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("User ID is required");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Contact Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var command = CreateValidDeleteCommand();
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Already_Deleted()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        existingContact.IsDeleted = true;
        existingContact.DeletedAt = DateTime.UtcNow.AddDays(-1);
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found or has already been deleted");
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var differentUserId = Guid.NewGuid();
        var command = CreateValidDeleteCommand(existingContact.Id, differentUserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Save_Fails()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.CountActiveTaskAssociationsAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockContactRepository.Setup(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Delete failed

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to delete contact");
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.CountActiveTaskAssociationsAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockContactRepository.Setup(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error deleting contact");
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_GetById_Throws_Exception()
    {
        // Arrange
        var command = CreateValidDeleteCommand();
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while deleting the contact");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_Multiple_Tasks()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var task1 = CreateAssociatedTask(existingContact.Id, existingContact.UserId, "Task 1", AppTaskStatus.Pending);
        var task2 = CreateAssociatedTask(existingContact.Id, existingContact.UserId, "Task 2", AppTaskStatus.InProgress);
        var task3 = CreateAssociatedTask(existingContact.Id, existingContact.UserId, "Task 3", AppTaskStatus.Completed);
        
        ((List<AppTask>)existingContact.Tasks).AddRange(new[] { task1, task2, task3 });
        
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_No_Tasks()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_Null_Email_And_Phone()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        existingContact.Email = null;
        existingContact.Phone = null;
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(existingContact.Id, existingContact.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logging Verification

    [Fact]
    public async Task Handle_Should_Log_Successful_Deletion()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify information logs are called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Soft deleting contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Contact") && v.ToString()!.Contains("soft deleted successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Contact_Not_Found()
    {
        // Arrange
        var command = CreateValidDeleteCommand();
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Contact") && v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Errors()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.CountActiveTaskAssociationsAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockContactRepository.Setup(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deleting contact")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidDeleteCommand(existingContact.Id, existingContact.UserId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetupSuccessfulSoftDeleteRepositoryMocks(existingContact);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.CountActiveTaskAssociationsAsync(command.ContactId, command.UserId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.SoftDeleteContactAsync(command.ContactId, command.UserId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var command = CreateValidDeleteCommand();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error deleting contact");
    }

    #endregion
}