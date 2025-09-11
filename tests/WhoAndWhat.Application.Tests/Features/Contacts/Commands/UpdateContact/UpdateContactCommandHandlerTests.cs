using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.UpdateContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Validators;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands.UpdateContact;

public class UpdateContactCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly Mock<ILogger<UpdateContactCommandHandler>> _mockLogger;
    private readonly UpdateContactCommandHandler _handler;
    private Contact? _capturedContact;

    public UpdateContactCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _contactValidator = new ContactValidator();
        _mockLogger = new Mock<ILogger<UpdateContactCommandHandler>>();
        _handler = new UpdateContactCommandHandler(
            _mockContactRepository.Object,
            _contactValidator,
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
            Name = "Original Name",
            Email = "original@example.com",
            Phone = "+1234567890",
            RelationshipType = 1, // Friend
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false
        };
    }

    /// <summary>
    /// Sets up successful repository mocks for update operations
    /// </summary>
    private void SetupSuccessfulUpdateRepositoryMocks(Contact existingContact)
    {
        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock to capture the updated contact
    /// </summary>
    private void SetupContactCaptureUpdateRepositoryMocks(Contact existingContact)
    {
        _capturedContact = null;
        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Callback<Contact, CancellationToken>((contact, ct) => _capturedContact = contact)
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static UpdateContactCommand CreateValidUpdateCommand(Guid? contactId = null, Guid? userId = null) => new(
        ContactId: contactId ?? Guid.NewGuid(),
        Name: "Updated Name",
        Email: "updated@example.com",
        Phone: "+0987654321",
        RelationshipType: 2, // Family
        UserId: userId ?? Guid.NewGuid()
    );

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Update_Contact_Successfully()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        SetupSuccessfulUpdateRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.Email.Should().Be("updated@example.com");
        result.Value.Phone.Should().Be("+0987654321");
        result.Value.RelationshipType.Should().Be(2);
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Update_Contact_With_Correct_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var existingContact = CreateValidExistingContact(userId, contactId);
        var command = new UpdateContactCommand(
            ContactId: contactId,
            Name: "Jane Smith Updated",
            Email: "jane.updated@example.com", 
            Phone: "+5551234567",
            RelationshipType: 3, // Colleague
            UserId: userId
        );

        SetupContactCaptureUpdateRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _capturedContact.Should().NotBeNull();
        _capturedContact!.Id.Should().Be(contactId);
        _capturedContact.UserId.Should().Be(userId);
        _capturedContact.Name.Should().Be("Jane Smith Updated");
        _capturedContact.Email.Should().Be("jane.updated@example.com");
        _capturedContact.Phone.Should().Be("+5551234567");
        _capturedContact.RelationshipType.Should().Be(3);
        _capturedContact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _capturedContact.IsDeleted.Should().BeFalse();
        // CreatedAt should remain unchanged
        _capturedContact.CreatedAt.Should().Be(existingContact.CreatedAt);
    }

    [Fact]
    public async Task Handle_Should_Update_Contact_With_Null_Email()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: "Updated Name",
            Email: null,
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: existingContact.UserId
        );
        SetupSuccessfulUpdateRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().BeNull();
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Update_Contact_With_Null_Phone()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: "Updated Name",
            Email: "updated@example.com",
            Phone: null,
            RelationshipType: 1,
            UserId: existingContact.UserId
        );
        SetupSuccessfulUpdateRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().BeNull();
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation Failure Scenarios

    [Theory]
    [InlineData("", "john@example.com", "+123456", 1)]
    [InlineData(null, "john@example.com", "+123456", 1)]
    [InlineData("   ", "john@example.com", "+123456", 1)]
    public async Task Handle_Should_Return_Failure_When_Name_Is_Invalid(string name, string email, string phone, int relationshipType)
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: name,
            Email: email,
            Phone: phone,
            RelationshipType: relationshipType,
            UserId: existingContact.UserId
        );

        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("must not be empty");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@invalid.com")]
    [InlineData("invalid@")]
    [InlineData("invalid")]
    public async Task Handle_Should_Return_Failure_When_Email_Is_Invalid(string invalidEmail)
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: "John Doe",
            Email: invalidEmail,
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: existingContact.UserId
        );

        _mockContactRepository.Setup(x => x.GetByIdAsync(existingContact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("valid email address");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: "John Doe",
            Email: "john@example.com",
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: Guid.Empty
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("User ID is required");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_ContactId_Is_Empty()
    {
        // Arrange
        var command = new UpdateContactCommand(
            ContactId: Guid.Empty,
            Name: "John Doe",
            Email: "john@example.com",
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact ID is required");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Contact Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var command = CreateValidUpdateCommand();
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
        
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Is_Deleted()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        existingContact.IsDeleted = true;
        existingContact.DeletedAt = DateTime.UtcNow;
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found or has been deleted");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var differentUserId = Guid.NewGuid();
        var command = CreateValidUpdateCommand(existingContact.Id, differentUserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Duplicate Email Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Email_Already_Exists_For_Different_Contact()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        
        var otherContactWithSameEmail = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = existingContact.UserId,
            Email = command.Email,
            Name = "Other Contact",
            IsDeleted = false
        };

        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync(command.Email!, command.UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact> { otherContactWithSameEmail });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("A contact with this email already exists");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Allow_Same_Email_For_Same_Contact()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        existingContact.Email = "same@example.com";
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: "Updated Name",
            Email: "same@example.com", // Same email as existing
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: existingContact.UserId
        );

        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync(command.Email, command.UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact> { existingContact }); // Only the same contact has this email
        _mockContactRepository.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Save_Fails()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // Save failed

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to update contact");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);
        _mockContactRepository.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while updating the contact");
        
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_Should_Handle_Very_Long_Name()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var longName = new string('A', 255); // Maximum typical name length
        var command = new UpdateContactCommand(
            ContactId: existingContact.Id,
            Name: longName,
            Email: "longname@example.com",
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: existingContact.UserId
        );
        SetupSuccessfulUpdateRepositoryMocks(existingContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(longName);
    }

    [Fact]
    public async Task Handle_Should_Handle_Various_Relationship_Types()
    {
        // Arrange & Act & Assert for different relationship types (0-3: Family, Friend, Colleague, Other)
        for (int relationshipType = 0; relationshipType <= 3; relationshipType++)
        {
            var existingContact = CreateValidExistingContact();
            var command = new UpdateContactCommand(
                ContactId: existingContact.Id,
                Name: $"Contact {relationshipType}",
                Email: $"contact{relationshipType}@example.com",
                Phone: "+1234567890",
                RelationshipType: relationshipType,
                UserId: existingContact.UserId
            );
            SetupSuccessfulUpdateRepositoryMocks(existingContact);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.RelationshipType.Should().Be(relationshipType);
        }
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetupSuccessfulUpdateRepositoryMocks(existingContact);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.GetByIdAsync(command.ContactId, cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.UpdateAsync(It.IsAny<Contact>(), cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var existingContact = CreateValidExistingContact();
        var command = CreateValidUpdateCommand(existingContact.Id, existingContact.UserId);
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately
        
        _mockContactRepository.Setup(x => x.GetByIdAsync(command.ContactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while updating the contact");
    }

    #endregion
}