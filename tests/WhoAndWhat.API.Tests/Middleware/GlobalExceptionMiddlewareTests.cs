using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.API.Middleware;
using Xunit;

namespace WhoAndWhat.API.Tests.Middleware;

/// <summary>
/// Unit tests for GlobalExceptionMiddleware
/// </summary>
public class GlobalExceptionMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionMiddleware>> _mockLogger;
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly GlobalExceptionMiddleware _middleware;

    public GlobalExceptionMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        _mockNext = new Mock<RequestDelegate>();
        _middleware = new GlobalExceptionMiddleware(_mockNext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_Should_Call_Next_When_No_Exception_Occurs()
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
    public async Task InvokeAsync_Should_Handle_ArgumentException_With_BadRequest_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        var exceptionMessage = "Invalid argument provided";
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new ArgumentException(exceptionMessage));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        await AssertErrorResponse(context, "INVALID_ARGUMENT", "The provided argument is invalid.", exceptionMessage);
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_UnauthorizedAccessException_With_Unauthorized_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        await AssertErrorResponse(context, "UNAUTHORIZED", "Access denied. Please authenticate.", "You must be authenticated to access this resource.");
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_KeyNotFoundException_With_NotFound_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        var exceptionMessage = "Resource not found";
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new KeyNotFoundException(exceptionMessage));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        await AssertErrorResponse(context, "RESOURCE_NOT_FOUND", "The requested resource was not found.", exceptionMessage);
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_InvalidOperationException_With_BadRequest_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        var exceptionMessage = "Operation not valid";
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        await AssertErrorResponse(context, "INVALID_OPERATION", "The requested operation is not valid in the current state.", exceptionMessage);
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_TimeoutException_With_RequestTimeout_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new TimeoutException("Operation timed out"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.RequestTimeout);
        await AssertErrorResponse(context, "REQUEST_TIMEOUT", "The request timed out.", "Please try again later.");
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_GenericException_With_InternalServerError_Status()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new Exception("Something went wrong"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        await AssertErrorResponse(context, "INTERNAL_SERVER_ERROR", "An unexpected error occurred.", "Please contact support if this problem persists.");
    }

    [Fact]
    public async Task InvokeAsync_Should_Set_ContentType_To_ApplicationJson()
    {
        // Arrange
        var context = CreateHttpContext();
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_Should_Include_TraceId_In_Response()
    {
        // Arrange
        var context = CreateHttpContext();
        var expectedTraceId = "test-trace-id-123";
        context.TraceIdentifier = expectedTraceId;
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        var responseJson = await GetResponseJson(context);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.TraceId.Should().Be(expectedTraceId);
    }

    [Fact]
    public async Task InvokeAsync_Should_Include_Timestamp_In_Response()
    {
        // Arrange
        var context = CreateHttpContext();
        var beforeException = DateTime.UtcNow;
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);
        var afterException = DateTime.UtcNow;

        // Assert
        var responseJson = await GetResponseJson(context);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Timestamp.Should().BeAfter(beforeException);
        errorResponse.Timestamp.Should().BeBefore(afterException);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Exception_With_TraceId()
    {
        // Arrange
        var context = CreateHttpContext();
        var expectedTraceId = "test-trace-id-456";
        context.TraceIdentifier = expectedTraceId;
        var exception = new Exception("Test exception for logging");
        _mockNext.Setup(n => n(It.IsAny<HttpContext>()))
               .ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"TraceId: {expectedTraceId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = Guid.NewGuid().ToString();
        return context;
    }

    private static async Task<string> GetResponseJson(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task AssertErrorResponse(HttpContext context, string expectedCode, string expectedMessage, string expectedDetails)
    {
        var responseJson = await GetResponseJson(context);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be(expectedCode);
        errorResponse.Error.Message.Should().Be(expectedMessage);
        errorResponse.Error.Details.Should().Be(expectedDetails);
        errorResponse.TraceId.Should().NotBeNullOrEmpty();
        errorResponse.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
