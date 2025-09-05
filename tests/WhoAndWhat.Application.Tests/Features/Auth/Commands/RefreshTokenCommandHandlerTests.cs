using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Auth.Commands.RefreshToken;
using WhoAndWhat.Application.Interfaces;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Auth.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<RefreshTokenCommandHandler>>();
        
        _handler = new RefreshTokenCommandHandler(
            _jwtTokenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Refresh_Token_Is_Valid()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token");
        var tokenResult = new Application.DTOs.Authentication.TokenResult
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresIn = 900,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };

        var successResult = Result<Application.DTOs.Authentication.TokenResult>.Success(tokenResult);

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync(command.RefreshToken))
            .ReturnsAsync(successResult);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(tokenResult);
        result.Value.AccessToken.Should().Be("new-access-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.ExpiresIn.Should().Be(900);
        result.Value.TokenType.Should().Be("Bearer");

        _jwtTokenServiceMock.Verify(x => x.RefreshTokensAsync(command.RefreshToken), Times.Once);

        // Verify that success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Token refreshed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Refresh_Token_Is_Invalid()
    {
        // Arrange
        var command = new RefreshTokenCommand("invalid-refresh-token");
        var failureResult = Result<Application.DTOs.Authentication.TokenResult>.Failure("Invalid refresh token");

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync(command.RefreshToken))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid refresh token");

        _jwtTokenServiceMock.Verify(x => x.RefreshTokensAsync(command.RefreshToken), Times.Once);

        // Verify that failure was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to refresh token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Should_Return_Failure_When_Refresh_Token_Is_Empty_Or_Whitespace(string invalidToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(invalidToken);
        var failureResult = Result<Application.DTOs.Authentication.TokenResult>.Failure("Invalid refresh token format");

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync(invalidToken))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid refresh token format");

        _jwtTokenServiceMock.Verify(x => x.RefreshTokensAsync(invalidToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Refresh_Token_Is_Null()
    {
        // Arrange
        var command = new RefreshTokenCommand(null!);
        var failureResult = Result<Application.DTOs.Authentication.TokenResult>.Failure("Invalid refresh token format");

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync(null!))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid refresh token format");

        _jwtTokenServiceMock.Verify(x => x.RefreshTokensAsync(null!), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Database_Exception_Gracefully()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token");

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync(command.RefreshToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred during token refresh. Please try again.");

        _jwtTokenServiceMock.Verify(x => x.RefreshTokensAsync(command.RefreshToken), Times.Once);

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during token refresh")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}