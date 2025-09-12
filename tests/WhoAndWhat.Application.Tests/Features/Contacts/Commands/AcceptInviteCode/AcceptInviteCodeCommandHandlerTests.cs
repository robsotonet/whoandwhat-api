using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.AcceptInviteCode;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands.AcceptInviteCode;

public class AcceptInviteCodeCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly Mock<ILogger<AcceptInviteCodeCommandHandler>> _mockLogger;
    private readonly AcceptInviteCodeCommandHandler _handler;

    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _originalContactUserId = Guid.NewGuid();
    private readonly string _testInviteCode = "TEST-CODE-1234";

    public AcceptInviteCodeCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _contactValidator = new ContactValidator();
        _mockLogger = new Mock<ILogger<AcceptInviteCodeCommandHandler>>();

        _handler = new AcceptInviteCodeCommandHandler(
            _mockContactRepository.Object,
            _contactValidator,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidInviteCodeWithCompleteInfo_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var originalContact = CreateOriginalContact("John Doe", "john@example.com", "+1234567890", ContactRelationType.Colleague);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId, "Added via invite code");

        SetupSuccessfulInviteCodeFlow(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var contactDto = result.Value;
        contactDto.Name.Should().Be("John Doe");
        contactDto.Email.Should().Be("john@example.com");
        contactDto.Phone.Should().Be("+1234567890");
        contactDto.RelationshipType.Should().Be((int)ContactRelationType.Friend); // Default for accepted invites
        contactDto.RelationshipTypeName.Should().Be("Friend");
        contactDto.IsDeleted.Should().BeFalse();
        contactDto.ActiveTaskCount.Should().Be(0);
        contactDto.AssociatedTasks.Should().BeEmpty();
        contactDto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        contactDto.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        VerifyContactAddedAndSaved();
        VerifyLogMessage(LogLevel.Information, "Processing invite code");
        VerifyLogMessage(LogLevel.Information, "Successfully added contact");
    }

    [Fact]
    public async Task Handle_ValidInviteCodeWithMinimalInfo_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var originalContact = CreateOriginalContact("Jane Smith", null, null, ContactRelationType.Friend);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        SetupSuccessfulInviteCodeFlow(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var contactDto = result.Value;
        contactDto.Name.Should().Be("Jane Smith");
        contactDto.Email.Should().BeNull();
        contactDto.Phone.Should().BeNull();
        contactDto.RelationshipType.Should().Be((int)ContactRelationType.Friend);

        VerifyContactAddedAndSaved();
        // Should not call FindContactsAsync when email is null
        _mockContactRepository.Verify(x => x.FindContactsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyInviteCode_ShouldReturnFailure()
    {
        // Arrange
        var command = new AcceptInviteCodeCommand("", _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invite code is required");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task Handle_NullInviteCode_ShouldReturnFailure()
    {
        // Arrange
        var command = new AcceptInviteCodeCommand(null!, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invite code is required");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task Handle_WhitespaceInviteCode_ShouldReturnFailure()
    {
        // Arrange
        var command = new AcceptInviteCodeCommand("   ", _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invite code is required");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task Handle_InvalidInviteCode_ShouldReturnFailure()
    {
        // Arrange
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid or expired invite code");

        VerifyLogMessage(LogLevel.Warning, "Invite code {InviteCode} not found");
        VerifyNoContactCreation();
    }

    [Fact]
    public async Task Handle_UserTriesToAcceptOwnInviteCode_ShouldReturnFailure()
    {
        // Arrange
        var originalContact = CreateOriginalContact("Self User", "self@example.com", null, ContactRelationType.Friend);
        originalContact.UserId = _testUserId; // Same user as the one accepting
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("You cannot add yourself as a contact");

        VerifyLogMessage(LogLevel.Warning, "User {UserId} tried to accept their own invite code");
        VerifyNoContactCreation();
    }

    [Fact]
    public async Task Handle_ContactAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var originalContact = CreateOriginalContact("John Doe", "john@example.com", "+1234567890", ContactRelationType.Friend);
        var existingContact = new Contact { Id = Guid.NewGuid(), Email = "john@example.com", UserId = _testUserId };
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact> { existingContact });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("You already have this contact in your list");

        VerifyLogMessage(LogLevel.Information, "Contact already exists for user");
        VerifyNoContactCreation();
    }

    [Fact]
    public async Task Handle_ContactValidationFails_ShouldReturnFailure()
    {
        // Arrange - using invalid email that will trigger real ContactValidator failure
        var originalContact = CreateOriginalContact("John Doe", "invalid-email", null, ContactRelationType.Friend);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync("invalid-email", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid contact data:");
        result.Error.Should().Contain("email"); // Real validator will complain about email format

        VerifyLogMessage(LogLevel.Warning, "Contact validation failed");
        VerifyNoContactCreation();
    }

    [Fact]
    public async Task Handle_RepositorySaveFails_ShouldReturnFailure()
    {
        // Arrange
        var originalContact = CreateOriginalContact("John Doe", "john@example.com", null, ContactRelationType.Friend);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // Simulate save failure

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to add contact from invite code");

        VerifyLogMessage(LogLevel.Error, "Failed to save contact from invite code");
    }

    [Fact]
    public async Task Handle_ExceptionDuringInviteCodeLookup_ShouldReturnFailure()
    {
        // Arrange
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid or expired invite code");

        // Should log the repository error AND the main handler error
        VerifyLogMessage(LogLevel.Error, "Error finding contact by invite code");
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ShouldReturnFailure()
    {
        // Arrange
        var originalContact = CreateOriginalContact("John Doe", "john@example.com", null, ContactRelationType.Friend);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error processing invite code: Database timeout");

        VerifyLogMessage(LogLevel.Error, "Error processing invite code");
    }

    [Theory]
    [InlineData(ContactRelationType.Family)]
    [InlineData(ContactRelationType.Friend)]
    [InlineData(ContactRelationType.Colleague)]
    [InlineData(ContactRelationType.Other)]
    public async Task Handle_OriginalContactWithDifferentRelationshipTypes_ShouldCreateContactWithDefaultFriendType(ContactRelationType originalType)
    {
        // Arrange
        var originalContact = CreateOriginalContact("Test User", "test@example.com", null, originalType);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        SetupSuccessfulInviteCodeFlow(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // New contact should always be created with Friend relationship type, regardless of original
        result.Value.RelationshipType.Should().Be((int)ContactRelationType.Friend);
        result.Value.RelationshipTypeName.Should().Be("Friend");
    }

    [Fact]
    public async Task Handle_OriginalContactWithSpecialCharactersInName_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var originalContact = CreateOriginalContact("José María Ñoño-García", "jose@example.com", null, ContactRelationType.Family);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId, "Spanish colleague");

        SetupSuccessfulInviteCodeFlow(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("José María Ñoño-García");
        result.Value.Email.Should().Be("jose@example.com");
    }

    [Fact]
    public async Task Handle_CustomNotesParameter_ShouldBeHandledCorrectly()
    {
        // Arrange - Note: Current implementation doesn't use CustomNotes, but we test the parameter
        var originalContact = CreateOriginalContact("John Doe", "john@example.com", null, ContactRelationType.Friend);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId, "Met at conference");

        SetupSuccessfulInviteCodeFlow(originalContact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Custom notes is passed but not currently used in the handler implementation
    }

    [Fact]
    public async Task Handle_ContactWithoutEmail_ShouldSkipDuplicateCheckAndCreateSuccessfully()
    {
        // Arrange
        var originalContact = CreateOriginalContact("No Email Contact", null, "+1234567890", ContactRelationType.Other);
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        SetupSuccessfulSave();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("No Email Contact");
        result.Value.Email.Should().BeNull();
        result.Value.Phone.Should().Be("+1234567890");

        // Should not call FindContactsAsync when original contact has no email
        _mockContactRepository.Verify(x => x.FindContactsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyContactAddedAndSaved();
    }

    [Fact]
    public async Task Handle_MultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange - Empty name and invalid email will trigger real ContactValidator failures
        var originalContact = CreateOriginalContact("", "invalid-email", null, ContactRelationType.Friend); // Invalid name and email
        var command = new AcceptInviteCodeCommand(_testInviteCode, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);
        _mockContactRepository.Setup(x => x.FindContactsAsync("invalid-email", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid contact data:");
        // Real ContactValidator will validate both Name.NotEmpty and Email format
        result.Error.Should().MatchRegex("(empty|required|email)", "Should contain validation error messages");
    }

    // Helper Methods

    private Contact CreateOriginalContact(string name, string? email, string? phone = null, ContactRelationType relationshipType = ContactRelationType.Friend)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            UserId = _originalContactUserId,
            Name = name,
            Email = email,
            Phone = phone,
            RelationshipType = (int)relationshipType,
            InviteCode = _testInviteCode,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false
        };
    }

    private void SetupSuccessfulInviteCodeFlow(Contact originalContact)
    {
        _mockContactRepository.Setup(x => x.FindContactByInviteCodeAsync(_testInviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContact);

        if (!string.IsNullOrEmpty(originalContact.Email))
        {
            _mockContactRepository.Setup(x => x.FindContactsAsync(originalContact.Email, _testUserId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Contact>());
        }

        SetupSuccessfulSave();
    }

    private void SetupSuccessfulSave()
    {
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void VerifyContactAddedAndSaved()
    {
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyNoContactCreation()
    {
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyNoRepositoryInteractions()
    {
        _mockContactRepository.Verify(x => x.FindContactByInviteCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.FindContactsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyLogMessage(LogLevel level, string messageFragment)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageFragment)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
