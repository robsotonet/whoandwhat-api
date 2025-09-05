using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Auth.Commands.LoginUser;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth.Commands;

public class LoginUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<LoginUserCommandHandler>> _loggerMock;
    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<LoginUserCommandHandler>>();
        
        _handler = new LoginUserCommandHandler(
            _userRepositoryMock.Object,
            _jwtTokenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Login_Is_Valid()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.VerifyEmail();

        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(user.Id);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);
        result.Value.AccessToken.Should().Be(tokenResult.AccessToken);
        result.Value.RefreshToken.Should().Be(tokenResult.RefreshToken);
        result.Value.TokenType.Should().Be(tokenResult.TokenType);
        result.Value.ExpiresIn.Should().Be(tokenResult.ExpiresIn);
        result.Value.IsEmailVerified.Should().BeTrue();

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(user), Times.Once);
        _userRepositoryMock.Verify(x => x.UpdateAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Does_Not_Exist()
    {
        // Arrange
        var command = new LoginUserCommand("nonexistent@example.com", "TestPassword123!", false);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password");

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(It.IsAny<User>()), Times.Never);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), default), Times.Never);

        // Verify that warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Login attempt with non-existent email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Is_Inactive()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.DeactivateAccount();

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Account is deactivated. Please contact support.");

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(It.IsAny<User>()), Times.Never);

        // Verify that warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Login attempt with inactive user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Is_Locked_And_Lock_Not_Expired()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.LockAccount(); // This sets LockedUntil to 24 hours from now

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("Account is locked until");

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(command.Email, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(It.IsAny<User>()), Times.Never);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), default), Times.Never);

        // Verify that warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Login attempt with locked user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Unlock_User_When_Lock_Period_Expired()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        
        // Manually set lock state to simulate expired lock
        typeof(User).GetProperty("IsLocked")!.SetValue(user, true);
        typeof(User).GetProperty("LockedUntil")!.SetValue(user, DateTime.UtcNow.AddMinutes(-1)); // Expired 1 minute ago

        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        user.IsLocked.Should().BeFalse();
        user.LockedUntil.Should().BeNull();

        _userRepositoryMock.Verify(x => x.UpdateAsync(user, default), Times.Exactly(2)); // First unlock, then successful login
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_And_Increment_Failed_Attempts_When_Password_Invalid()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "WrongPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        var initialFailedAttempts = user.FailedLoginAttempts;

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password");
        user.FailedLoginAttempts.Should().Be(initialFailedAttempts + 1);

        _userRepositoryMock.Verify(x => x.UpdateAsync(user, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(It.IsAny<User>()), Times.Never);

        // Verify that warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid password attempt for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Lock_User_After_5_Failed_Attempts()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "WrongPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");

        // Simulate 4 previous failed attempts
        for (int i = 0; i < 4; i++)
        {
            user.RecordLoginAttempt(false);
        }

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Account has been locked due to too many failed login attempts");
        user.IsLocked.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(5);

        _userRepositoryMock.Verify(x => x.UpdateAsync(user, default), Times.Once);

        // Verify that lock warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User locked due to failed login attempts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Reset_Failed_Attempts_On_Successful_Login()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        
        // Simulate some failed attempts
        user.RecordLoginAttempt(false);
        user.RecordLoginAttempt(false);
        user.FailedLoginAttempts.Should().Be(2);

        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(0);
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _userRepositoryMock.Verify(x => x.UpdateAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Allow_Login_With_Unverified_Email()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        // Email is not verified (IsEmailVerified = false by default)

        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.IsEmailVerified.Should().BeFalse();

        // Verify that info log was written for unverified email
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Login attempt with unverified email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Database_Exception_Gracefully()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during login. Please try again.");

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during user login")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_JWT_Token_Generation_Failure()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ThrowsAsync(new InvalidOperationException("Token generation failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during login. Please try again.");

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during user login")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Successful_Login()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");

        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, default))
            .ReturnsAsync(user);

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userRepositoryMock.Setup(x => x.UpdateAsync(user, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        // Verify that success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User logged in successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}