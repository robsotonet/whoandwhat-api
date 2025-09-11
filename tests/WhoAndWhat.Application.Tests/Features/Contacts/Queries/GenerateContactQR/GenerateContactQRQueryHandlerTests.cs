using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Queries.GenerateContactQR;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Queries.GenerateContactQR;

public class GenerateContactQRQueryHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<GenerateContactQRQueryHandler>> _mockLogger;
    private readonly GenerateContactQRQueryHandler _handler;

    public GenerateContactQRQueryHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<GenerateContactQRQueryHandler>>();
        _handler = new GenerateContactQRQueryHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    private static Contact CreateValidContact(Guid? contactId = null, Guid? userId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1234567890",
            RelationshipType = 1, // Friend
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false
        };
    }

    private static GenerateContactQRQuery CreateValidQuery(
        Guid? contactId = null,
        Guid? userId = null,
        string? customMessage = null,
        int expirationHours = 24,
        QRCodeFormat format = QRCodeFormat.PNG,
        QRCodeSize size = QRCodeSize.Medium)
    {
        return new GenerateContactQRQuery(
            ContactId: contactId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            CustomMessage: customMessage,
            ExpirationHours: expirationHours,
            Format: format,
            Size: size);
    }

    private void SetupSuccessfulContactLookup(Contact contact)
    {
        _mockContactRepository
            .Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
    }

    private void SetupContactNotFound(Guid contactId)
    {
        _mockContactRepository
            .Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);
    }

    private void SetupRepositoryException(Guid contactId, Exception exception)
    {
        _mockContactRepository
            .Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
    }

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Generate_QR_Code_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ContactId.Should().Be(contactId);
        result.Value.Format.Should().Be("PNG");
        result.Value.Size.Should().Be("Medium");
        result.Value.ContentType.Should().Be("image/png");
        result.Value.PixelSize.Should().Be(200);
        result.Value.QRCodeData.Should().NotBeEmpty();
        result.Value.EncodedPayload.Should().NotBeEmpty();
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _mockContactRepository.Verify(
            x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(QRCodeFormat.PNG, "image/png")]
    [InlineData(QRCodeFormat.SVG, "image/svg+xml")]
    [InlineData(QRCodeFormat.JPEG, "image/jpeg")]
    public async Task Handle_Should_Support_Different_QR_Code_Formats(QRCodeFormat format, string expectedContentType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId, format: format);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be(format.ToString());
        result.Value.ContentType.Should().Be(expectedContentType);
        result.Value.QRCodeData.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(QRCodeSize.Small, 100)]
    [InlineData(QRCodeSize.Medium, 200)]
    [InlineData(QRCodeSize.Large, 400)]
    [InlineData(QRCodeSize.XLarge, 600)]
    public async Task Handle_Should_Support_Different_QR_Code_Sizes(QRCodeSize size, int expectedPixelSize)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId, size: size);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Size.Should().Be(size.ToString());
        result.Value.PixelSize.Should().Be(expectedPixelSize);
    }

    [Fact]
    public async Task Handle_Should_Include_Custom_Message_When_Provided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var customMessage = "Connect with me on WhoAndWhat!";
        var query = CreateValidQuery(contactId, userId, customMessage: customMessage);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CustomMessage.Should().Be(customMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(48)]
    [InlineData(168)] // 7 days
    public async Task Handle_Should_Set_Correct_Expiration_Time(int expirationHours)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId, expirationHours: expirationHours);
        var beforeExecution = DateTime.UtcNow;

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expectedExpiration = beforeExecution.AddHours(expirationHours);
        result.Value.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_Should_Generate_Valid_QR_Payload_With_Contact_Data()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var customMessage = "Test message";
        var query = CreateValidQuery(contactId, userId, customMessage: customMessage);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Decode and verify payload
        var payloadBytes = Convert.FromBase64String(result.Value.EncodedPayload);
        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        payload.GetProperty("ContactId").GetGuid().Should().Be(contactId);
        payload.GetProperty("Name").GetString().Should().Be(contact.Name);
        payload.GetProperty("Email").GetString().Should().Be(contact.Email);
        payload.GetProperty("Phone").GetString().Should().Be(contact.Phone);
        payload.GetProperty("RelationshipType").GetInt32().Should().Be(contact.RelationshipType);
        payload.GetProperty("CustomMessage").GetString().Should().Be(customMessage);
        payload.GetProperty("Signature").GetString().Should().NotBeNullOrEmpty();

        var expiresAtString = payload.GetProperty("ExpiresAt").GetString();
        DateTime.Parse(expiresAtString!).Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);

        SetupContactNotFound(contactId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");

        _mockContactRepository.Verify(
            x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, differentUserId); // Contact belongs to different user
        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found"); // Security: don't reveal existence
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);
        var exception = new Exception("Database connection failed");

        SetupRepositoryException(contactId, exception);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Error generating QR code");
        result.Error.Should().Contain(exception.Message);
    }

    #endregion

    #region Edge Cases and Additional Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);
        var cancellationToken = new CancellationTokenSource().Token;

        SetupSuccessfulContactLookup(contact);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockContactRepository.Verify(
            x => x.GetByIdAsync(contactId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_Null_Optional_Fields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        contact.Email = null; // Optional field
        contact.Phone = null; // Optional field

        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Verify payload contains null values correctly
        var payloadBytes = Convert.FromBase64String(result.Value.EncodedPayload);
        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        payload.GetProperty("Email").ValueKind.Should().Be(JsonValueKind.Null);
        payload.GetProperty("Phone").ValueKind.Should().Be(JsonValueKind.Null);
        payload.GetProperty("Name").GetString().Should().Be(contact.Name); // Required field should be there
    }

    [Fact]
    public async Task Handle_Should_Generate_Consistent_Signatures_For_Same_Input()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert - Signatures should be similar for same contact and similar expiration time
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Both results should have valid signatures (not empty)
        var payload1 = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Convert.FromBase64String(result1.Value.EncodedPayload)));
        var payload2 = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Convert.FromBase64String(result2.Value.EncodedPayload)));

        payload1.GetProperty("Signature").GetString().Should().NotBeNullOrEmpty();
        payload2.GetProperty("Signature").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task Handle_Should_Log_Information_On_Success()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Generating QR code for contact {contactId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Successfully generated QR code for contact {contactId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);

        SetupContactNotFound(contactId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Contact {contactId} not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Belongs_To_Different_User()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = CreateValidContact(contactId, differentUserId);
        var query = CreateValidQuery(contactId, userId);

        SetupSuccessfulContactLookup(contact);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Contact {contactId} does not belong to user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Error_When_Exception_Occurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);
        var exception = new Exception("Database error");

        SetupRepositoryException(contactId, exception);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error generating QR code for contact {contactId} for user {userId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
