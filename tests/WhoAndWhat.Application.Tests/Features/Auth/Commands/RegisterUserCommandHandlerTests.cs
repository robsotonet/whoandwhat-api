using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;
using WhoAndWhat.Application.Features.Auth;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth.Commands;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUserDomainService> _userDomainServiceMock;
    private readonly Mock<IAccountVerificationService> _accountVerificationServiceMock;
    private readonly Mock<ILogger<RegisterUserCommandHandler>> _loggerMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _userDomainServiceMock = new Mock<IUserDomainService>();
        _accountVerificationServiceMock = new Mock<IAccountVerificationService>();
        _loggerMock = new Mock<ILogger<RegisterUserCommandHandler>>();
        
        _handler = new RegisterUserCommandHandler(
            _userDomainServiceMock.Object,
            _userRepositoryMock.Object,
            _accountVerificationServiceMock.Object,
            Mock.Of<IJwtTokenService>(),
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

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);
        
        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userDomainServiceMock.Setup(x => x.CreateUser(command.Email, command.Username, command.Password, Language.en))
            .Returns(user);

        _userRepositoryMock.Setup(x => x.AddAsync(It.IsAny<User>(), default))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync(verificationToken);

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

        _userRepositoryMock.Verify(x => x.AddAsync(user, default), Times.Once);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(user.Id, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Email_Already_Exists()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        var existingUser = new User("test@example.com", "existinguser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User with this email already exists");

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
        _userDomainServiceMock.Verify(x => x.CreateUser(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Language>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Username_Already_Exists()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        var existingUser = new User("different@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(command.Username, default))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Username is already taken");

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _userRepositoryMock.Verify(x => x.GetByUsernameAsync(command.Username, default), Times.Once);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
        _userDomainServiceMock.Verify(x => x.CreateUser(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Language>()), Times.Never);
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

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(It.IsAny<string>(), default), Times.Never);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
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

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid language");

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserDomainService_Throws_ArgumentException()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "weakpassword", // This should trigger password validation in domain service
            "en",
            true);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userDomainServiceMock.Setup(x => x.CreateUser(command.Email, command.Username, command.Password, Language.en))
            .Throws(new ArgumentException("Password does not meet requirements"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Password does not meet requirements");

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Database_Save_Fails()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        var user = new User("test@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userDomainServiceMock.Setup(x => x.CreateUser(command.Email, command.Username, command.Password, Language.en))
            .Returns(user);

        _userRepositoryMock.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(0); // Simulate save failure

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to save user to database");

        _userRepositoryMock.Verify(x => x.AddAsync(user, default), Times.Once);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(user.Id, default), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Handle_Database_Exception_Gracefully()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "test@example.com",
            "testuser",
            "TestPassword123!",
            "en",
            true);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
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

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userDomainServiceMock.Setup(x => x.CreateUser(command.Email, command.Username, command.Password, Language.en))
            .Returns(user);

        _userRepositoryMock.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync((string?)null); // Simulate token generation failure

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresEmailVerification.Should().BeFalse(); // Should still succeed but without verification requirement

        _userRepositoryMock.Verify(x => x.AddAsync(user, default), Times.Once);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _accountVerificationServiceMock.Verify(x => x.GenerateVerificationTokenAsync(user.Id, default), Times.Once);

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

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        _userDomainServiceMock.Setup(x => x.CreateUser(command.Email, command.Username, command.Password, expectedLanguage))
            .Returns(user);

        _userRepositoryMock.Setup(x => x.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _accountVerificationServiceMock.Setup(x => x.GenerateVerificationTokenAsync(user.Id, default))
            .ReturnsAsync("verification-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredLanguage.Should().Be(expectedLanguage.ToString());

        _userDomainServiceMock.Verify(x => x.CreateUser(command.Email, command.Username, command.Password, expectedLanguage), Times.Once);
    }
}