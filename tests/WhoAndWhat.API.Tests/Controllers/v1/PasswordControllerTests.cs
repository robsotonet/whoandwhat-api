using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Features.Auth;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers.v1;

public class PasswordControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IPasswordResetService> _passwordResetServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<PasswordController>> _loggerMock;
    private readonly PasswordController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public PasswordControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _passwordResetServiceMock = new Mock<IPasswordResetService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<PasswordController>>();
        
        _controller = new PasswordController(
            _userServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);

        // Setup authenticated user context
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task ForgotPassword_Should_Return_Success_Message_Always()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "test@example.com"
        };

        var user = new User("test@example.com", "testuser", Language.en);

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _emailServiceMock.Setup(x => x.SendPasswordResetEmailAsync(
            request.Email, user.Username, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Message.Should().Contain("If an account with that email address exists");
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_Should_Return_Success_Even_When_Email_Not_Found()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "nonexistent@example.com"
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Message.Should().Contain("If an account with that email address exists");
        response.Success.Should().BeTrue();
        
        // Verify that no email was sent for non-existent user
        _emailServiceMock.Verify(x => x.SendPasswordResetEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task ResetPassword_Should_Return_Success_When_Token_Is_Valid()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-token",
            NewPassword = "NewPassword123!"
        };

        var resetResult = Result.Success();

        _userServiceMock.Setup(x => x.ResetPasswordAsync(
            request.Email, request.Token, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetResult);

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Message.Should().Contain("successfully reset");
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_Should_Return_BadRequest_When_Token_Is_Invalid()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "invalid-token",
            NewPassword = "NewPassword123!"
        };

        var resetResult = Result.Failure("Invalid or expired reset token");

        _userServiceMock.Setup(x => x.ResetPasswordAsync(
            request.Email, request.Token, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetResult);

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var problemDetails = badRequestResult!.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Detail.Should().Be("Invalid or expired reset token");
    }

    [Fact]
    public async Task ChangePassword_Should_Return_Success_When_Current_Password_Is_Valid()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        var changeResult = Result.Success();

        _userServiceMock.Setup(x => x.ChangePasswordAsync(
            _userId, request.CurrentPassword, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changeResult);

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Message.Should().Contain("successfully changed");
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_Should_Return_BadRequest_When_Current_Password_Is_Invalid()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword",
            NewPassword = "NewPassword123!"
        };

        var changeResult = Result.Failure("Current password is incorrect");

        _userServiceMock.Setup(x => x.ChangePasswordAsync(
            _userId, request.CurrentPassword, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changeResult);

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var problemDetails = badRequestResult!.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Detail.Should().Be("Current password is incorrect");
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_Success_When_Token_Is_Valid()
    {
        // Arrange
        var request = new VerifyEmailRequest
        {
            UserId = _userId,
            Token = "valid-verification-token"
        };

        var verificationResult = Result.Success();

        _userServiceMock.Setup(x => x.VerifyEmailAsync(request.UserId, request.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verificationResult);

        // Act
        var result = await _controller.VerifyEmail(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Message.Should().Contain("successfully verified");
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_BadRequest_When_Token_Is_Invalid()
    {
        // Arrange
        var request = new VerifyEmailRequest
        {
            UserId = _userId,
            Token = "invalid-token"
        };

        var verificationResult = Result.Failure("Invalid or expired verification token");

        _userServiceMock.Setup(x => x.VerifyEmailAsync(request.UserId, request.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verificationResult);

        // Act
        var result = await _controller.VerifyEmail(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var problemDetails = badRequestResult!.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Detail.Should().Be("Invalid or expired verification token");
    }

    [Fact]
    public async Task ChangePassword_Should_Return_Unauthorized_When_User_Not_Authenticated()
    {
        // Arrange
        var controllerWithoutAuth = new PasswordController(
            _userServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
        
        controllerWithoutAuth.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = await controllerWithoutAuth.ChangePassword(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}