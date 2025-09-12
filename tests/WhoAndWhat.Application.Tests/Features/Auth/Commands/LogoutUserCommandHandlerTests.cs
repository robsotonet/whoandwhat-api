using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Auth.Commands.LogoutUser;
using WhoAndWhat.Application.Interfaces;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth.Commands;

public class LogoutUserCommandHandlerTests
{
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<LogoutUserCommandHandler>> _loggerMock;
    private readonly LogoutUserCommandHandler _handler;

    public LogoutUserCommandHandlerTests()
    {
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<LogoutUserCommandHandler>>();

        _handler = new LogoutUserCommandHandler(
            _jwtTokenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Logout_Is_Successful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutUserCommand(userId);

        _jwtTokenServiceMock.Setup(x => x.RevokeAllUserTokensAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Message.Should().Be("Logged out successfully");
        result.Value.TokensRevoked.Should().Be(1);
        result.Value.LogoutAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(userId), Times.Once);

        // Verify that success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All tokens revoked for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Specific_Refresh_Token_Provided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshToken = "valid-refresh-token";
        var command = new LogoutUserCommand(userId, refreshToken);

        _jwtTokenServiceMock.Setup(x => x.RevokeRefreshTokenAsync(refreshToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Message.Should().Be("Logged out successfully");
        result.Value.TokensRevoked.Should().Be(1);

        _jwtTokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(refreshToken), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(It.IsAny<Guid>()), Times.Never);

        // Verify that success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Specific refresh token revoked")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_RevokeAllTokens_Is_True()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutUserCommand(userId, RevokeAllTokens: true);

        _jwtTokenServiceMock.Setup(x => x.RevokeAllUserTokensAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("Logged out successfully");
        result.Value.TokensRevoked.Should().Be(1);

        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(userId), Times.Once);

        // Verify that success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All tokens revoked for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_When_Revoking_Tokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutUserCommand(userId);

        _jwtTokenServiceMock.Setup(x => x.RevokeAllUserTokensAsync(userId))
            .ThrowsAsync(new InvalidOperationException("Token revocation failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during logout. Please try again.");

        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(userId), Times.Once);

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during user logout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_When_Revoking_Refresh_Token()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshToken = "invalid-refresh-token";
        var command = new LogoutUserCommand(userId, refreshToken);

        _jwtTokenServiceMock.Setup(x => x.RevokeRefreshTokenAsync(refreshToken))
            .ThrowsAsync(new InvalidOperationException("Refresh token revocation failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during logout. Please try again.");

        _jwtTokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(refreshToken), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(It.IsAny<Guid>()), Times.Never);

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during user logout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Track_Logout_Timestamp_Accurately()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LogoutUserCommand(userId);

        _jwtTokenServiceMock.Setup(x => x.RevokeAllUserTokensAsync(userId))
            .Returns(Task.CompletedTask);

        var beforeLogout = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        var afterLogout = DateTime.UtcNow;

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.LogoutAt.Should().BeAfter(beforeLogout);
        result.Value.LogoutAt.Should().BeBefore(afterLogout.AddSeconds(1)); // Allow 1 second tolerance
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Even_With_Empty_UserId()
    {
        // Arrange
        var command = new LogoutUserCommand(Guid.Empty);

        _jwtTokenServiceMock.Setup(x => x.RevokeAllUserTokensAsync(Guid.Empty))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert  
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("Logged out successfully");
        result.Value.TokensRevoked.Should().Be(1);

        _jwtTokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(Guid.Empty), Times.Once);
    }

}
