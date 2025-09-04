using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using WhoAndWhat.API.Middleware;
using Xunit;

namespace WhoAndWhat.API.Tests.Middleware;

/// <summary>
/// Unit tests for SecurityHeadersMiddleware
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly SecurityHeadersMiddleware _middleware;

    public SecurityHeadersMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _middleware = new SecurityHeadersMiddleware(_mockNext.Object);
    }

    [Fact]
    public async Task InvokeAsync_Should_Call_Next_Middleware()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(n => n(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Remove_Server_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.Headers.Append("Server", "TestServer/1.0");
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Server");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_XContentTypeOptions_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_XFrameOptions_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Frame-Options");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_XXSSProtection_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-XSS-Protection");
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_ReferrerPolicy_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Referrer-Policy");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_PermissionsPolicy_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Permissions-Policy");
        context.Response.Headers["Permissions-Policy"].ToString().Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_ContentSecurityPolicy_Header()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Content-Security-Policy");
        var cspValue = context.Response.Headers["Content-Security-Policy"].ToString();
        cspValue.Should().Contain("default-src 'self'");
        cspValue.Should().Contain("script-src 'self' 'unsafe-inline'");
        cspValue.Should().Contain("style-src 'self' 'unsafe-inline'");
        cspValue.Should().Contain("img-src 'self' data: https:");
        cspValue.Should().Contain("font-src 'self'");
        cspValue.Should().Contain("connect-src 'self'");
        cspValue.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_HSTS_Header_For_HTTPS_Request()
    {
        // Arrange
        var context = CreateHttpContext(isHttps: true);
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Add_HSTS_Header_For_HTTP_Request()
    {
        // Arrange
        var context = CreateHttpContext(isHttps: false);
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Overwrite_Existing_Headers()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.Headers.Append("X-Content-Type-Options", "existing-value");
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("existing-value");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task InvokeAsync_Should_Add_All_Required_Security_Headers()
    {
        // Arrange
        var context = CreateHttpContext(isHttps: true);
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .Returns(Task.CompletedTask);

        var expectedHeaders = new[]
        {
            "X-Content-Type-Options",
            "X-Frame-Options", 
            "X-XSS-Protection",
            "Referrer-Policy",
            "Permissions-Policy",
            "Content-Security-Policy",
            "Strict-Transport-Security"
        };

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        foreach (var header in expectedHeaders)
        {
            context.Response.Headers.Should().ContainKey(header, 
                $"Security header '{header}' should be present");
        }
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_Exception_From_Next_Middleware()
    {
        // Arrange
        var context = CreateHttpContext();
        var expectedException = new InvalidOperationException("Test exception");
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _middleware.InvokeAsync(context));

        exception.Should().Be(expectedException);
        
        // Verify headers were still added before the exception
        context.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        context.Response.Headers.Should().ContainKey("X-Frame-Options");
    }

    private static HttpContext CreateHttpContext(bool isHttps = false)
    {
        var context = new DefaultHttpContext();
        context.Request.IsHttps = isHttps;
        context.Response.Body = new MemoryStream();
        return context;
    }
}