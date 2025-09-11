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
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<LoginUserCommandHandler>> _loggerMock;
    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<LoginUserCommandHandler>>();

        _handler = new LoginUserCommandHandler(
            _userServiceMock.Object,
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

        _userServiceMock.Setup(x => x.AuthenticateAsync(command.Email, command.Password, default))
            .ReturnsAsync(Result<User>.Success(user));

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userServiceMock.Setup(x => x.UpdateUserAsync(user, default))
            .ReturnsAsync(Result.Success());

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

        _userServiceMock.Verify(x => x.AuthenticateAsync(command.Email, command.Password, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(user), Times.Once);
        _userServiceMock.Verify(x => x.UpdateUserAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Authentication_Fails()
    {
        // Arrange
        var command = new LoginUserCommand("nonexistent@example.com", "TestPassword123!", false);

        _userServiceMock.Setup(x => x.AuthenticateAsync(command.Email, command.Password, default))
            .ReturnsAsync(Result<User>.Failure("Invalid email or password"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password");

        _userServiceMock.Verify(x => x.AuthenticateAsync(command.Email, command.Password, default), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateTokensAsync(It.IsAny<User>()), Times.Never);
        _userServiceMock.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), default), Times.Never);
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

        _userServiceMock.Setup(x => x.AuthenticateAsync(command.Email, command.Password, default))
            .ReturnsAsync(Result<User>.Success(user));

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userServiceMock.Setup(x => x.UpdateUserAsync(user, default))
            .ReturnsAsync(Result.Success());

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
    public async Task Handle_Should_Handle_JWT_Token_Generation_Failure()
    {
        // Arrange
        var command = new LoginUserCommand("test@example.com", "TestPassword123!", false);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");

        _userServiceMock.Setup(x => x.AuthenticateAsync(command.Email, command.Password, default))
            .ReturnsAsync(Result<User>.Success(user));

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

        _userServiceMock.Setup(x => x.AuthenticateAsync(command.Email, command.Password, default))
            .ReturnsAsync(Result<User>.Success(user));

        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        _userServiceMock.Setup(x => x.UpdateUserAsync(user, default))
            .ReturnsAsync(Result.Success());

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
