using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Auth;
using WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth.Commands;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IAccountVerificationService> _accountVerificationServiceMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<RegisterUserCommandHandler>> _loggerMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _accountVerificationServiceMock = new Mock<IAccountVerificationService>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<RegisterUserCommandHandler>>();

        _handler = new RegisterUserCommandHandler(
            _userServiceMock.Object,
            _accountVerificationServiceMock.Object,
            _jwtTokenServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Registration_Is_Valid()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        var user = new User("test@example.com", "testuser", Language.en);
        var verificationToken = "verification-token-123";

        _userServiceMock.Setup(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default))
            .ReturnsAsync(Result<User>.Success(user));

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync(verificationToken);

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(user.Email, user.Username, verificationToken, user.Id, default))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(user.Id);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);
        result.Value.RequiresEmailVerification.Should().BeTrue();

        _userServiceMock.Verify(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(user.Id, default), Times.Once);
        _emailServiceMock.Verify(x => x.SendEmailVerificationAsync(user.Email, user.Username, verificationToken, user.Id, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Terms_Not_Accepted()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            false); // Terms not accepted

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("You must accept the terms and conditions to register");

        _userServiceMock.Verify(x => x.RegisterUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Language>(), default), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-language")]
    [InlineData("fr")]
    public async Task Handle_Should_Return_Failure_When_Language_Is_Invalid(string invalidLanguage)
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            invalidLanguage,
            true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid language");

        _userServiceMock.Verify(x => x.RegisterUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Language>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Service_Registration_Fails()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        _userServiceMock.Setup(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default))
            .ReturnsAsync(Result<User>.Failure("User with this email already exists"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User with this email already exists");

        _userServiceMock.Verify(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Continue_When_Email_Verification_Token_Generation_Fails()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        var user = new User("test@example.com", "testuser", Language.en);

        _userServiceMock.Setup(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default))
            .ReturnsAsync(Result<User>.Success(user));

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync((string?)null); // Simulate token generation failure

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresEmailVerification.Should().BeFalse(); // Should still succeed but without verification requirement

        _userServiceMock.Verify(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(user.Id, default), Times.Once);
        _emailServiceMock.Verify(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), default), Times.Never);

        // Verify that warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to generate email verification token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("en", Language.en)]
    [InlineData("es", Language.es)]
    [InlineData("EN", Language.en)]
    [InlineData("ES", Language.es)]
    public async Task Handle_Should_Parse_Language_Correctly(string languageString, Language expectedLanguage)
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            languageString,
            true);

        var user = new User("test@example.com", "testuser", expectedLanguage);
        var verificationToken = "verification-token";

        _userServiceMock.Setup(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, expectedLanguage, default))
            .ReturnsAsync(Result<User>.Success(user));

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync(verificationToken);

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(user.Email, user.Username, verificationToken, user.Id, default))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredLanguage.Should().Be(expectedLanguage.ToString());

        _userServiceMock.Verify(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, expectedLanguage, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        _userServiceMock.Setup(x => x.RegisterUserAsync(command.Email, command.Username, command.Password, Language.en, default))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during registration. Please try again.");

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during user registration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
