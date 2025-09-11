using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Features.Contacts.Commands.PermanentlyDeleteContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands;

public class PermanentlyDeleteContactCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<PermanentlyDeleteContactCommandHandler>> _mockLogger;
    private readonly PermanentlyDeleteContactCommandHandler _handler;

    public PermanentlyDeleteContactCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<PermanentlyDeleteContactCommandHandler>>();
        _handler = new PermanentlyDeleteContactCommandHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test contact that is soft-deleted and safe to permanently delete
    /// </summary>
    private static Contact CreateSafeToDeleteContact(Guid? contactId = null, Guid? userId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            Name = "Safe To Delete Contact",
            Email = "safetodelete@example.com",
            Phone = "+1234567890",
            RelationshipType = 1,
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            TaskContacts = new List<TaskContact>() // No task associations
        };
    }

    /// <summary>
    /// Creates a test contact that is soft-deleted but has task associations
    /// </summary>
    private static Contact CreateContactWithTaskAssociations(Guid? contactId = null, Guid? userId = null)
    {
        var id = contactId ?? Guid.NewGuid();
        var contact = new Contact
        {
            Id = id,
            Name = "Contact With Tasks",
            Email = "withtasks@example.com",
            Phone = "+1234567890",
            RelationshipType = 1,
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            TaskContacts = new List<TaskContact>
            {
                new TaskContact { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), ContactId = id, Role = "Owner" }
            }
        };
        return contact;
    }

    /// <summary>
    /// Creates a test contact that is NOT soft-deleted
    /// </summary>
    private static Contact CreateActiveContact(Guid? contactId = null, Guid? userId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            Name = "Active Contact",
            Email = "active@example.com",
            Phone = "+1234567890",
            RelationshipType = 1,
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false,
            DeletedAt = null,
            TaskContacts = new List<TaskContact>()
        };
    }

    /// <summary>
    /// Sets up repository mock for successful permanent deletion
    /// </summary>
    private void SetupSuccessfulDeletionRepositoryMocks(Contact contact)
    {
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        
        _mockContactRepository.Setup(x => x.Remove(It.IsAny<Contact>()));
        
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static PermanentlyDeleteContactCommand CreateValidCommand(Guid? contactId = null, Guid? userId = null) => 
        new(contactId ?? Guid.NewGuid(), userId ?? Guid.NewGuid());

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Permanently_Delete_Soft_Deleted_Contact_Successfully()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulDeletionRepositoryMocks(deletedContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Remove(deletedContact), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Call_Repository_Methods_In_Correct_Order()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        var sequence = new MockSequence();
        _mockContactRepository.InSequence(sequence)
            .Setup(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);
        
        _mockContactRepository.InSequence(sequence)
            .Setup(x => x.Remove(deletedContact));
        
        _mockContactRepository.InSequence(sequence)
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Contact Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var command = CreateValidCommand();
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Remove(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var commandUserId = Guid.NewGuid();
        var contactOwnerUserId = Guid.NewGuid(); // Different user
        
        var command = CreateValidCommand(contactId, commandUserId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, commandUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!); // Repository should filter by user

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
    }

    #endregion

    #region Contact Not Soft Deleted Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Is_Not_Soft_Deleted()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activeContact = CreateActiveContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact must be soft-deleted before permanent deletion");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Remove(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Task Association Prevention Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Has_Task_Associations()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contactWithTasks = CreateContactWithTaskAssociations(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactWithTasks);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact has task associations and cannot be permanently deleted");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Remove(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_Contact_Has_Empty_Task_Associations()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateSafeToDeleteContact(contactId, userId);
        contact.TaskContacts = new List<TaskContact>(); // Explicitly empty
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulDeletionRepositoryMocks(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.Remove(contact), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_Contact_Has_Null_Task_Associations()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateSafeToDeleteContact(contactId, userId);
        contact.TaskContacts = null; // Null collection
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulDeletionRepositoryMocks(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.Remove(contact), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Get_Throws_Exception()
    {
        // Arrange
        var command = CreateValidCommand();
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while permanently deleting the contact");
        
        _mockContactRepository.Verify(x => x.Remove(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Remove_Throws_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);
        
        _mockContactRepository.Setup(x => x.Remove(It.IsAny<Contact>()))
            .Throws(new Exception("Remove operation failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while permanently deleting the contact");
        
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Save_Throws_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);
        
        _mockContactRepository.Setup(x => x.Remove(It.IsAny<Contact>()));
        
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Save operation failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while permanently deleting the contact");
    }

    #endregion

    #region Validation Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_ContactId_Is_Empty()
    {
        // Arrange
        var command = new PermanentlyDeleteContactCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new PermanentlyDeleteContactCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        
        SetupSuccessfulDeletionRepositoryMocks(deletedContact);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var command = CreateValidCommand();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while permanently deleting the contact");
    }

    #endregion

    #region Logging Scenarios

    [Fact]
    public async Task Handle_Should_Log_Information_On_Successful_Deletion()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSafeToDeleteContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulDeletionRepositoryMocks(deletedContact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Permanently deleting contact {contactId} for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Successfully permanently deleted contact {contactId} for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Not_Found()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Contact {contactId} not found for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Has_Task_Associations()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contactWithTasks = CreateContactWithTaskAssociations(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactWithTasks);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Contact {contactId} has task associations and cannot be permanently deleted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Error_On_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = CreateValidCommand(contactId, userId);
        var expectedException = new Exception("Database error");
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error permanently deleting contact {contactId} for user {userId}")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}