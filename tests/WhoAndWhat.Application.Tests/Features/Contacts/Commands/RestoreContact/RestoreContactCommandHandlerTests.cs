using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.RestoreContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands;

public class RestoreContactCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<RestoreContactCommandHandler>> _mockLogger;
    private readonly RestoreContactCommandHandler _handler;

    public RestoreContactCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<RestoreContactCommandHandler>>();
        _handler = new RestoreContactCommandHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test contact that is soft-deleted
    /// </summary>
    private static Contact CreateSoftDeletedContact(Guid? contactId = null, Guid? userId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            Name = "Deleted Contact",
            Email = "deleted@example.com",
            Phone = "+1234567890",
            RelationshipType = 1,
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1)
        };
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
            DeletedAt = null
        };
    }

    /// <summary>
    /// Sets up repository mock for successful restore operation
    /// </summary>
    private void SetupSuccessfulRestoreRepositoryMocks(Contact contact)
    {
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        
        _mockContactRepository.Setup(x => x.Update(It.IsAny<Contact>()));
        
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock to capture the updated contact during restore
    /// </summary>
    private Contact SetupContactCaptureRepositoryMocks(Contact originalContact)
    {
        Contact updatedContact = null!;
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        
        _mockContactRepository.Setup(x => x.Update(It.IsAny<Contact>()))
            .Callback<Contact>(contact => updatedContact = contact);
        
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        return updatedContact;
    }

    private static RestoreContactCommand CreateValidCommand(Guid? contactId = null, Guid? userId = null) => 
        new(contactId ?? Guid.NewGuid(), userId ?? Guid.NewGuid());

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Restore_Soft_Deleted_Contact_Successfully()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulRestoreRepositoryMocks(deletedContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(contactId);
        result.Value.Name.Should().Be("Deleted Contact");
        result.Value.Email.Should().Be("deleted@example.com");
        result.Value.IsDeleted.Should().BeFalse();
        result.Value.DeletedAt.Should().BeNull();
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Update(It.IsAny<Contact>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Update_Contact_Properties_Correctly_During_Restore()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        var updatedContact = SetupContactCaptureRepositoryMocks(deletedContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        updatedContact.Should().NotBeNull();
        updatedContact.IsDeleted.Should().BeFalse();
        updatedContact.DeletedAt.Should().BeNull();
        updatedContact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify other properties remain unchanged
        updatedContact.Id.Should().Be(contactId);
        updatedContact.Name.Should().Be("Deleted Contact");
        updatedContact.Email.Should().Be("deleted@example.com");
        updatedContact.Phone.Should().Be("+1234567890");
        updatedContact.RelationshipType.Should().Be(1);
        updatedContact.UserId.Should().Be(userId);
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
        _mockContactRepository.Verify(x => x.Update(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var commandUserId = Guid.NewGuid();
        var contactOwnerUserId = Guid.NewGuid(); // Different user
        
        var deletedContact = CreateSoftDeletedContact(contactId, contactOwnerUserId);
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

    #region Contact Not Deleted Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Is_Not_Deleted()
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
        result.Error.Should().Be("Contact is not deleted and cannot be restored");
        
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.Update(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        result.Error.Should().Be("An error occurred while restoring the contact");
        
        _mockContactRepository.Verify(x => x.Update(It.IsAny<Contact>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Update_Throws_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);
        
        _mockContactRepository.Setup(x => x.Update(It.IsAny<Contact>()))
            .Throws(new Exception("Update operation failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while restoring the contact");
        
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Save_Throws_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(
            contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);
        
        _mockContactRepository.Setup(x => x.Update(It.IsAny<Contact>()));
        
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Save operation failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while restoring the contact");
    }

    #endregion

    #region Validation Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_ContactId_Is_Empty()
    {
        // Arrange
        var command = new RestoreContactCommand(Guid.Empty, Guid.NewGuid());

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
        var command = new RestoreContactCommand(Guid.NewGuid(), Guid.Empty);

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
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        
        SetupSuccessfulRestoreRepositoryMocks(deletedContact);

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
        result.Error.Should().Be("An error occurred while restoring the contact");
    }

    #endregion

    #region Logging Scenarios

    [Fact]
    public async Task Handle_Should_Log_Information_On_Successful_Restore()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var command = CreateValidCommand(contactId, userId);
        
        SetupSuccessfulRestoreRepositoryMocks(deletedContact);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Restoring contact {contactId} for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Successfully restored contact {contactId} for user {userId}")),
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error restoring contact {contactId} for user {userId}")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}