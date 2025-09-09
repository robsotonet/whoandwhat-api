using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers.v1;

public class OAuthControllerTests
{
    private readonly Mock<IOAuthService> _oAuthServiceMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<OAuthController>> _loggerMock;
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly OAuthController _controller;

    public OAuthControllerTests()
    {
        _oAuthServiceMock = new Mock<IOAuthService>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<OAuthController>>();
        _authServiceMock = new Mock<IAuthenticationService>();
        
        _controller = new OAuthController(
            _oAuthServiceMock.Object,
            _jwtTokenServiceMock.Object,
            _loggerMock.Object);

        // Setup HTTP context
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        
        // Setup authentication service
        _controller.HttpContext.RequestServices = CreateServiceProvider();
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IAuthenticationService)))
            .Returns(_authServiceMock.Object);
        return services.Object;
    }

    [Fact]
    public void GoogleLogin_Should_Return_Challenge_Result()
    {
        // Arrange
        string? returnUrl = "https://example.com/callback";

        // Act
        var result = _controller.GoogleLogin(returnUrl);

        // Assert
        result.Should().BeOfType<ChallengeResult>();
        var challengeResult = result as ChallengeResult;
        challengeResult!.Properties!.RedirectUri.Should().NotBeNull();
    }

    [Fact]
    public async Task GoogleCallback_Should_Return_Success_When_Authentication_Succeeds()
    {
        // Arrange
        var externalId = "google123";
        var email = "test@example.com";
        var name = "Test User";
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, externalId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };
        var identity = new ClaimsIdentity(claims, "GoogleAuth");
        var principal = new ClaimsPrincipal(identity);
        
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, "GoogleAuth"));
        var user = new User(email, "testuser", Language.en);
        var tokenResult = new TokenResult
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Google"))
            .ReturnsAsync(authResult);
        _oAuthServiceMock.Setup(x => x.AuthenticateWithOAuthAsync(
            OAuthProviders.Google, externalId, email, name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        // Act
        var result = await _controller.GoogleCallback(null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.AccessToken.Should().Be("access_token");
        response.RefreshToken.Should().Be("refresh_token");
        response.Email.Should().Be(email);
    }

    [Fact]
    public async Task GoogleCallback_Should_Return_BadRequest_When_Authentication_Fails()
    {
        // Arrange
        var authResult = AuthenticateResult.Fail("Authentication failed");

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Google"))
            .ReturnsAsync(authResult);

        // Act
        var result = await _controller.GoogleCallback(null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GoogleCallback_Should_Return_BadRequest_When_External_Id_Is_Missing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "test@example.com"),
            new(ClaimTypes.Name, "Test User")
            // Missing NameIdentifier claim
        };
        var identity = new ClaimsIdentity(claims, "GoogleAuth");
        var principal = new ClaimsPrincipal(identity);
        
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, "GoogleAuth"));

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Google"))
            .ReturnsAsync(authResult);

        // Act
        var result = await _controller.GoogleCallback(null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void FacebookLogin_Should_Return_Challenge_Result()
    {
        // Arrange
        string? returnUrl = "https://example.com/callback";

        // Act
        var result = _controller.FacebookLogin(returnUrl);

        // Assert
        result.Should().BeOfType<ChallengeResult>();
        var challengeResult = result as ChallengeResult;
        challengeResult!.Properties!.RedirectUri.Should().NotBeNull();
    }

    [Fact]
    public async Task FacebookCallback_Should_Return_Success_When_Authentication_Succeeds()
    {
        // Arrange
        var externalId = "facebook123";
        var email = "test@example.com";
        var name = "Test User";
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, externalId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };
        var identity = new ClaimsIdentity(claims, "FacebookAuth");
        var principal = new ClaimsPrincipal(identity);
        
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, "FacebookAuth"));
        var user = new User(email, "testuser", Language.en);
        var tokenResult = new TokenResult
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Facebook"))
            .ReturnsAsync(authResult);
        _oAuthServiceMock.Setup(x => x.AuthenticateWithOAuthAsync(
            OAuthProviders.Facebook, externalId, email, name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        // Act
        var result = await _controller.FacebookCallback(null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.AccessToken.Should().Be("access_token");
        response.RefreshToken.Should().Be("refresh_token");
        response.Email.Should().Be(email);
    }

    [Fact]
    public void AppleLogin_Should_Return_Challenge_Result()
    {
        // Arrange
        string? returnUrl = "https://example.com/callback";

        // Act
        var result = _controller.AppleLogin(returnUrl);

        // Assert
        result.Should().BeOfType<ChallengeResult>();
        var challengeResult = result as ChallengeResult;
        challengeResult!.Properties!.RedirectUri.Should().NotBeNull();
    }

    [Fact]
    public async Task AppleCallback_Should_Return_Success_When_Authentication_Succeeds()
    {
        // Arrange
        var externalId = "apple123";
        var email = "test@example.com";
        var name = "Test User";
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, externalId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };
        var identity = new ClaimsIdentity(claims, "AppleAuth");
        var principal = new ClaimsPrincipal(identity);
        
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, "AppleAuth"));
        var user = new User(email, "testuser", Language.en);
        var tokenResult = new TokenResult
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Apple"))
            .ReturnsAsync(authResult);
        _oAuthServiceMock.Setup(x => x.AuthenticateWithOAuthAsync(
            OAuthProviders.Apple, externalId, email, name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        // Act
        var result = await _controller.AppleCallback(null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.AccessToken.Should().Be("access_token");
        response.RefreshToken.Should().Be("refresh_token");
        response.Email.Should().Be(email);
    }

    [Fact]
    public async Task AppleCallback_Should_Redirect_When_ReturnUrl_Is_Provided()
    {
        // Arrange
        var returnUrl = "https://example.com/callback";
        var externalId = "apple123";
        var email = "test@example.com";
        var name = "Test User";
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, externalId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };
        var identity = new ClaimsIdentity(claims, "AppleAuth");
        var principal = new ClaimsPrincipal(identity);
        
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, "AppleAuth"));
        var user = new User(email, "testuser", Language.en);
        var tokenResult = new TokenResult
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), "Apple"))
            .ReturnsAsync(authResult);
        _oAuthServiceMock.Setup(x => x.AuthenticateWithOAuthAsync(
            OAuthProviders.Apple, externalId, email, name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(tokenResult);

        // Act
        var result = await _controller.AppleCallback(returnUrl);

        // Assert
        result.Should().BeOfType<RedirectResult>();
        var redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().StartWith(returnUrl);
        redirectResult.Url.Should().Contain("access_token=access_token");
        redirectResult.Url.Should().Contain("refresh_token=refresh_token");
    }
}