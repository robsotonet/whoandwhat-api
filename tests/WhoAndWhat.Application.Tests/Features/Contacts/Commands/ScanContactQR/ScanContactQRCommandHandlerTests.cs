using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.ScanContactQR;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands.ScanContactQR;

public class ScanContactQRCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly Mock<ILogger<ScanContactQRCommandHandler>> _mockLogger;
    private readonly ScanContactQRCommandHandler _handler;
    
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testContactId = Guid.NewGuid();
    private readonly DateTime _futureExpiry = DateTime.UtcNow.AddHours(2);

    public ScanContactQRCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _contactValidator = new ContactValidator();
        _mockLogger = new Mock<ILogger<ScanContactQRCommandHandler>>();
        
        _handler = new ScanContactQRCommandHandler(
            _mockContactRepository.Object,
            _contactValidator,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidQRCodeWithCompleteInfo_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId, "Custom notes");

        SetupSuccessfulSave();
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("John Doe");
        result.Value.Email.Should().Be("john@example.com");
        result.Value.Phone.Should().Be("+1234567890");
        result.Value.RelationshipType.Should().Be((int)ContactRelationType.Friend);
        result.Value.IsDeleted.Should().BeFalse();
        result.Value.ActiveTaskCount.Should().Be(0);

        VerifyContactAddedAndSaved();
        VerifyLogMessage(LogLevel.Information, "Processing scanned QR code for user");
        VerifyLogMessage(LogLevel.Information, "Successfully created contact");
    }

    [Fact]
    public async Task Handle_ValidQRCodeWithMinimalInfo_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "Jane Smith", null, null, 
            (int)ContactRelationType.Other, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        SetupSuccessfulSave();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Jane Smith");
        result.Value.Email.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.RelationshipType.Should().Be((int)ContactRelationType.Other);

        VerifyContactAddedAndSaved();
    }

    [Fact]
    public async Task Handle_InvalidBase64Payload_ShouldReturnFailure()
    {
        // Arrange
        var command = new ScanContactQRCommand("invalid-base64", _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to decode QR code payload");
        
        VerifyLogMessage(LogLevel.Warning, "Invalid QR code payload for user");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_InvalidJsonPayload_ShouldReturnFailure()
    {
        // Arrange
        var invalidJson = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid json"));
        var command = new ScanContactQRCommand(invalidJson, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to decode QR code payload");
        
        VerifyLogMessage(LogLevel.Warning, "Invalid QR code payload for user");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_EmptyContactId_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(Guid.Empty, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid contact ID in QR code");
        
        VerifyLogMessage(LogLevel.Warning, "Invalid QR code payload for user");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_EmptyContactName_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact name is required");
        
        VerifyLogMessage(LogLevel.Warning, "Invalid QR code payload for user");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_ExpiredQRCode_ShouldReturnFailure()
    {
        // Arrange
        var expiredTime = DateTime.UtcNow.AddHours(-1);
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, expiredTime);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("QR code has expired");
        
        VerifyLogMessage(LogLevel.Warning, "QR code expired for contact");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_QRCodeExpiresAtCurrentTime_ShouldReturnFailure()
    {
        // Arrange - Set expiry to current time
        var currentTime = DateTime.UtcNow;
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, currentTime);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("QR code has expired");
    }

    [Fact]
    public async Task Handle_InvalidSignature_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        payload.Signature = "invalid-signature";
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid QR code signature");
        
        VerifyLogMessage(LogLevel.Warning, "Invalid QR code signature for contact");
        VerifyNoContactInteractions();
    }

    [Fact]
    public async Task Handle_ContactAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        var existingContact = new Contact { Id = Guid.NewGuid(), Email = "john@example.com", UserId = _testUserId };
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact> { existingContact });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("A contact with this email already exists");
        
        VerifyLogMessage(LogLevel.Information, "Contact with email john@example.com already exists for user");
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ContactWithoutEmail_ShouldSkipDuplicateCheck()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", null, "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        SetupSuccessfulSave();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockContactRepository.Verify(x => x.FindContactsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ContactValidationFails_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "invalid-email", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactsAsync("invalid-email", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid contact data:");
        result.Error.Should().Contain("email"); // Real validator will complain about email format
        
        VerifyLogMessage(LogLevel.Warning, "Contact validation failed");
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RepositorySaveFails_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

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
        result.Error.Should().Be("Failed to create contact from QR code");
        
        VerifyLogMessage(LogLevel.Error, "Failed to save contact from QR scan for user");
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ShouldReturnFailure()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        _mockContactRepository.Setup(x => x.FindContactsAsync(It.IsAny<string>(), It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error processing QR code: Database connection error");
        
        VerifyLogMessage(LogLevel.Error, "Error processing QR code scan for user");
    }

    [Theory]
    [InlineData(ContactRelationType.Family)]
    [InlineData(ContactRelationType.Friend)]
    [InlineData(ContactRelationType.Colleague)]
    [InlineData(ContactRelationType.Other)]
    public async Task Handle_DifferentRelationshipTypes_ShouldCreateContactWithCorrectType(ContactRelationType relationshipType)
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "Test Contact", "test@example.com", null, 
            (int)relationshipType, _futureExpiry);
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        SetupSuccessfulSave();
        _mockContactRepository.Setup(x => x.FindContactsAsync("test@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationshipType.Should().Be((int)relationshipType);
        result.Value.RelationshipTypeName.Should().Be(relationshipType.ToString());
    }

    [Fact]
    public async Task Handle_CustomMessageInPayload_ShouldCreateContactSuccessfully()
    {
        // Arrange
        var payload = CreateValidQRPayload(_testContactId, "John Doe", "john@example.com", "+1234567890", 
            (int)ContactRelationType.Friend, _futureExpiry, "Hello from QR code");
        var encodedPayload = EncodePayload(payload);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId, "Command custom notes");

        SetupSuccessfulSave();
        _mockContactRepository.Setup(x => x.FindContactsAsync("john@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        
        // Note: CustomMessage in payload and command notes are for different purposes in this implementation
        VerifyContactAddedAndSaved();
    }

    [Fact]
    public async Task Handle_ConsistentSignatureGeneration_ShouldValidateCorrectly()
    {
        // Arrange - Create two payloads with same data to ensure signature consistency
        var contactId = Guid.NewGuid();
        var expiryTime = DateTime.UtcNow.AddHours(1);
        
        var payload1 = CreateValidQRPayload(contactId, "Test User", "test@example.com", null, 
            (int)ContactRelationType.Friend, expiryTime);
        var payload2 = CreateValidQRPayload(contactId, "Test User", "test@example.com", null, 
            (int)ContactRelationType.Friend, expiryTime);

        // Both payloads should have the same signature
        payload1.Signature.Should().Be(payload2.Signature);

        var encodedPayload = EncodePayload(payload1);
        var command = new ScanContactQRCommand(encodedPayload, _testUserId);

        SetupSuccessfulSave();
        _mockContactRepository.Setup(x => x.FindContactsAsync("test@example.com", _testUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // Helper Methods

    private static QRPayload CreateValidQRPayload(Guid contactId, string name, string? email, string? phone, 
        int relationshipType, DateTime expiresAt, string? customMessage = null)
    {
        var signature = GenerateSignature(contactId.ToString(), expiresAt);
        
        return new QRPayload
        {
            ContactId = contactId,
            Name = name,
            Email = email,
            Phone = phone,
            RelationshipType = relationshipType,
            CustomMessage = customMessage,
            ExpiresAt = expiresAt,
            Signature = signature
        };
    }

    private static string GenerateSignature(string contactId, DateTime expiresAt)
    {
        var data = $"{contactId}:{expiresAt:yyyy-MM-ddTHH:mm:ssZ}";
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    private static string EncodePayload(QRPayload payload)
    {
        var jsonString = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var jsonBytes = Encoding.UTF8.GetBytes(jsonString);
        return Convert.ToBase64String(jsonBytes);
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

    private void VerifyNoContactInteractions()
    {
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

    // Internal payload class matching the handler's private class
    private class QRPayload
    {
        public Guid ContactId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int RelationshipType { get; set; }
        public string? CustomMessage { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Signature { get; set; } = string.Empty;
    }
}