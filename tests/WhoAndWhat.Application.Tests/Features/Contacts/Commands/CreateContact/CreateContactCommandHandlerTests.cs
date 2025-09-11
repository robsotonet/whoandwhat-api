using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Commands.CreateContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Validators;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Commands;

public class CreateContactCommandHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly Mock<ILogger<CreateContactCommandHandler>> _mockLogger;
    private readonly CreateContactCommandHandler _handler;
    private Contact? _capturedContact;

    public CreateContactCommandHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _contactValidator = new ContactValidator();
        _mockLogger = new Mock<ILogger<CreateContactCommandHandler>>();
        _handler = new CreateContactCommandHandler(
            _mockContactRepository.Object,
            _contactValidator,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Sets up the standard repository mocks for successful contact creation
    /// </summary>
    private void SetupSuccessfulRepositoryMocks()
    {
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock to capture the created contact
    /// </summary>
    private void SetupContactCaptureRepositoryMocks()
    {
        _capturedContact = null;
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Callback<Contact, CancellationToken>((contact, ct) => _capturedContact = contact)
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock for save failure scenarios
    /// </summary>
    private void SetupFailedSaveRepositoryMocks()
    {
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockContactRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static CreateContactCommand CreateValidCommand() => new(
        Name: "John Doe",
        Email: "john.doe@example.com",
        Phone: "+1234567890",
        RelationshipType: 1, // Friend
        UserId: Guid.NewGuid()
    );

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Create_Contact_Successfully()
    {
        // Arrange
        var command = CreateValidCommand();
        SetupSuccessfulRepositoryMocks();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("John Doe");
        result.Value.Email.Should().Be("john.doe@example.com");
        result.Value.Phone.Should().Be("+1234567890");
        result.Value.RelationshipType.Should().Be(1);
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Create_Contact_With_Correct_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateContactCommand(
            Name: "Jane Smith",
            Email: "jane.smith@example.com", 
            Phone: "+0987654321",
            RelationshipType: 2, // Family
            UserId: userId
        );

        SetupContactCaptureRepositoryMocks();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _capturedContact.Should().NotBeNull();
        _capturedContact!.Name.Should().Be("Jane Smith");
        _capturedContact.Email.Should().Be("jane.smith@example.com");
        _capturedContact.Phone.Should().Be("+0987654321");
        _capturedContact.RelationshipType.Should().Be(2);
        _capturedContact.UserId.Should().Be(userId);
        _capturedContact.Id.Should().NotBe(Guid.Empty);
        _capturedContact.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _capturedContact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _capturedContact.IsDeleted.Should().BeFalse();
        _capturedContact.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Create_Contact_Without_Email()
    {
        // Arrange
        var command = new CreateContactCommand(
            Name: "No Email Contact",
            Email: null,
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: Guid.NewGuid()
        );
        SetupSuccessfulRepositoryMocks();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().BeNull();
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Create_Contact_Without_Phone()
    {
        // Arrange
        var command = new CreateContactCommand(
            Name: "No Phone Contact",
            Email: "nophone@example.com",
            Phone: null,
            RelationshipType: 1,
            UserId: Guid.NewGuid()
        );
        SetupSuccessfulRepositoryMocks();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().BeNull();
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
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
        var command = new CreateContactCommand(
            Name: name,
            Email: email,
            Phone: phone,
            RelationshipType: relationshipType,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("must not be empty");
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var command = new CreateContactCommand(
            Name: "John Doe",
            Email: invalidEmail,
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("valid email address");
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new CreateContactCommand(
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
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Save_Fails()
    {
        // Arrange
        var command = CreateValidCommand();
        SetupFailedSaveRepositoryMocks();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to create contact");
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var command = CreateValidCommand();
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while creating the contact");
        
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_Should_Handle_Very_Long_Name()
    {
        // Arrange
        var longName = new string('A', 255); // Maximum typical name length
        var command = new CreateContactCommand(
            Name: longName,
            Email: "longname@example.com",
            Phone: "+1234567890",
            RelationshipType: 1,
            UserId: Guid.NewGuid()
        );
        SetupSuccessfulRepositoryMocks();

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
            var command = new CreateContactCommand(
                Name: $"Contact {relationshipType}",
                Email: $"contact{relationshipType}@example.com",
                Phone: "+1234567890",
                RelationshipType: relationshipType,
                UserId: Guid.NewGuid()
            );
            SetupSuccessfulRepositoryMocks();

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
        var command = CreateValidCommand();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetupSuccessfulRepositoryMocks();

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.AddAsync(It.IsAny<Contact>(), cancellationToken), Times.Once);
        _mockContactRepository.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var command = CreateValidCommand();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately
        
        _mockContactRepository.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while creating the contact");
    }

    #endregion
}