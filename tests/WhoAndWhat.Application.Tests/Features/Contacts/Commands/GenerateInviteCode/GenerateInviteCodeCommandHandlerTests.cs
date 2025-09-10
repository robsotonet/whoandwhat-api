using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.GenerateInviteCode;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands.GenerateInviteCode;

public class GenerateInviteCodeCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<GenerateInviteCodeCommandHandler>> _mockLogger;
    private readonly GenerateInviteCodeCommandHandler _handler;
    
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testContactId = Guid.NewGuid();

    public GenerateInviteCodeCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<GenerateInviteCodeCommandHandler>>();
        
        _handler = new GenerateInviteCodeCommandHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidContactAndUser_ShouldGenerateInviteCodeSuccessfully()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com", "+1234567890", ContactRelationType.Friend);
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId, 24, "Welcome to my network!", false);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        
        var inviteDto = result.Value;
        inviteDto.ContactId.Should().Be(_testContactId);
        inviteDto.InviteCode.Should().NotBeNullOrEmpty();
        inviteDto.InviteCode.Should().MatchRegex(@"^[A-Z234567]{4}-[A-Z234567]{4}-[A-Z234567]{4}$");
        inviteDto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        inviteDto.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddHours(25)); // Should be ~24 hours
        inviteDto.CustomMessage.Should().Be("Welcome to my network!");
        inviteDto.AllowMultipleUses.Should().BeFalse();
        inviteDto.UsageCount.Should().Be(0);
        inviteDto.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify ContactDto mapping
        inviteDto.ContactInfo.Should().NotBeNull();
        inviteDto.ContactInfo.Id.Should().Be(_testContactId);
        inviteDto.ContactInfo.Name.Should().Be("John Doe");
        inviteDto.ContactInfo.Email.Should().Be("john@example.com");
        inviteDto.ContactInfo.Phone.Should().Be("+1234567890");
        inviteDto.ContactInfo.RelationshipType.Should().Be((int)ContactRelationType.Friend);
        inviteDto.ContactInfo.RelationshipTypeName.Should().Be("Friend");
        
        // Verify shareable text format
        inviteDto.ShareableText.Should().Contain("🤝 Connect with John Doe!");
        inviteDto.ShareableText.Should().Contain("Message: Welcome to my network!");
        inviteDto.ShareableText.Should().Contain($"Use invite code: {inviteDto.InviteCode}");
        inviteDto.ShareableText.Should().Contain("📱 Add this contact to your WhoAndWhat app");

        VerifyContactUpdatedAndSaved(contact);
        VerifyLogMessage(LogLevel.Information, "Generating invite code for contact");
        VerifyLogMessage(LogLevel.Information, "Successfully generated invite code");
    }

    [Fact]
    public async Task Handle_ValidContactWithoutCustomMessage_ShouldGenerateInviteCodeWithoutMessage()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "Jane Smith", "jane@example.com", null, ContactRelationType.Colleague);
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId, 12, null, true);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        
        var inviteDto = result.Value;
        inviteDto.CustomMessage.Should().BeNull();
        inviteDto.AllowMultipleUses.Should().BeTrue();
        inviteDto.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(11));
        inviteDto.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddHours(13)); // Should be ~12 hours
        
        // Shareable text should not contain custom message
        inviteDto.ShareableText.Should().Contain("🤝 Connect with Jane Smith!");
        inviteDto.ShareableText.Should().NotContain("Message:");
        inviteDto.ShareableText.Should().Contain($"Use invite code: {inviteDto.InviteCode}");
    }

    [Theory]
    [InlineData(ContactRelationType.Family)]
    [InlineData(ContactRelationType.Friend)]
    [InlineData(ContactRelationType.Colleague)]
    [InlineData(ContactRelationType.Other)]
    public async Task Handle_DifferentRelationshipTypes_ShouldGenerateInviteCodeWithCorrectType(ContactRelationType relationshipType)
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "Test Contact", "test@example.com", null, relationshipType);
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContactInfo.RelationshipType.Should().Be((int)relationshipType);
        result.Value.ContactInfo.RelationshipTypeName.Should().Be(relationshipType.ToString());
    }

    [Theory]
    [InlineData(1)]    // 1 hour
    [InlineData(6)]    // 6 hours
    [InlineData(24)]   // 24 hours (default)
    [InlineData(48)]   // 48 hours
    [InlineData(168)]  // 1 week
    public async Task Handle_DifferentExpirationHours_ShouldSetCorrectExpirationTime(int expirationHours)
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "Test Contact", "test@example.com");
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId, expirationHours);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(expirationHours - 0.1));
        result.Value.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddHours(expirationHours + 0.1));
    }

    [Fact]
    public async Task Handle_ZeroExpirationHours_ShouldCreateAlreadyExpiredInvite()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "Test Contact", "test@example.com");
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId, 0);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Expires at approximately current time (should be expired immediately)
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(_testContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
        
        VerifyLogMessage(LogLevel.Warning, "Contact {ContactId} not found");
        VerifyNoSaveOperations();
    }

    [Fact]
    public async Task Handle_ContactDoesNotBelongToUser_ShouldReturnFailure()
    {
        // Arrange
        var differentUserId = Guid.NewGuid();
        var contact = CreateTestContact(_testContactId, differentUserId, "John Doe", "john@example.com"); // Different user
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(_testContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
        
        VerifyLogMessage(LogLevel.Warning, "Contact {ContactId} does not belong to user {UserId}");
        VerifyNoSaveOperations();
    }

    [Fact]
    public async Task Handle_RepositorySaveFails_ShouldReturnFailure()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com");
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(_testContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // Simulate save failure

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to generate invite code");
        
        VerifyLogMessage(LogLevel.Error, "Failed to save invite code for contact");
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ShouldReturnFailure()
    {
        // Arrange
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(_testContactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error generating invite code: Database connection error");
        
        VerifyLogMessage(LogLevel.Error, "Error generating invite code for contact");
    }

    [Fact]
    public async Task Handle_ContactNameWithSpecialCharacters_ShouldGenerateValidInviteCode()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "José María Ñoño-García", "jose@example.com");
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InviteCode.Should().MatchRegex(@"^[A-Z234567]{4}-[A-Z234567]{4}-[A-Z234567]{4}$");
        result.Value.ShareableText.Should().Contain("🤝 Connect with José María Ñoño-García!");
    }

    [Fact]
    public async Task Handle_MultipleInviteGenerations_ShouldCreateDifferentCodes()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com");
        var command1 = new GenerateInviteCodeCommand(_testContactId, _testUserId);
        var command2 = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result1 = await _handler.Handle(command1, CancellationToken.None);
        
        // Reset the contact for second call (simulate fresh retrieval)
        var contact2 = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com");
        SetupContactRepositoryForSuccess(contact2);
        
        // Add small delay to ensure different timestamps
        await Task.Delay(10);
        var result2 = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        
        // Invite codes should be different due to timestamp differences
        result1.Value.InviteCode.Should().NotBe(result2.Value.InviteCode);
    }

    [Fact]
    public async Task Handle_ContactInviteCodeUpdate_ShouldUpdateContactAndTimestamp()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com");
        var originalUpdatedAt = contact.UpdatedAt;
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify that the contact's InviteCode was set
        contact.InviteCode.Should().NotBeNullOrEmpty();
        contact.InviteCode.Should().Be(result.Value.InviteCode);
        
        // Verify UpdatedAt timestamp was updated
        contact.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        contact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_InviteCodeFormat_ShouldBeCorrectFormat()
    {
        // Arrange
        var contact = CreateTestContact(_testContactId, _testUserId, "John Doe", "john@example.com");
        var command = new GenerateInviteCodeCommand(_testContactId, _testUserId);

        SetupContactRepositoryForSuccess(contact);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var inviteCode = result.Value.InviteCode;
        
        // Should match XXXX-XXXX-XXXX format
        inviteCode.Should().MatchRegex(@"^[A-Z234567]{4}-[A-Z234567]{4}-[A-Z234567]{4}$");
        
        // Should be exactly 14 characters (12 chars + 2 hyphens)
        inviteCode.Length.Should().Be(14);
        
        // Should contain only valid base32-like characters and hyphens
        foreach (char c in inviteCode)
        {
            if (c != '-')
            {
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Should().Contain(c.ToString());
            }
        }
    }

    // Helper Methods

    private static Contact CreateTestContact(Guid contactId, Guid userId, string name, string? email = null, 
        string? phone = null, ContactRelationType relationshipType = ContactRelationType.Friend)
    {
        return new Contact
        {
            Id = contactId,
            UserId = userId,
            Name = name,
            Email = email,
            Phone = phone,
            RelationshipType = (int)relationshipType,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false
        };
    }

    private void SetupContactRepositoryForSuccess(Contact contact)
    {
        _mockContactRepository.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void VerifyContactUpdatedAndSaved(Contact contact)
    {
        contact.InviteCode.Should().NotBeNullOrEmpty();
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyNoSaveOperations()
    {
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