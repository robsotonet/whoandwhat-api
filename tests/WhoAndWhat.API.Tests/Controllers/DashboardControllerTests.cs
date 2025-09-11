using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers;

/// <summary>
/// Comprehensive unit tests for DashboardController
/// Tests all dashboard endpoints including motivational content, metrics, and interaction recording
/// </summary>
public class DashboardControllerTests : IDisposable
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<DashboardController>> _mockLogger;
    private readonly DashboardController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public DashboardControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<DashboardController>>();
        _controller = new DashboardController(_mockMediator.Object, _mockLogger.Object);
        
        // Setup controller context with authenticated user
        SetupAuthenticatedUser();
    }

    #region GetMotivationalContent Tests

    [Fact]
    public async Task GetMotivationalContent_WithValidRequest_ShouldReturnContent()
    {
        // Arrange
        var expectedResponse = new GetMotivationalContentResponse(
            new List<MotivationalContentDto>
            {
                new(Guid.NewGuid(), "Test Title", "Test Message", "Achievement", "Productivity", 80, null, 
                    new Dictionary<string, object>(), true, 0.95),
                new(Guid.NewGuid(), "Test Title 2", "Test Message 2", "Tip", "General", 70, null, 
                    new Dictionary<string, object>(), false, 0.85)
            },
            25,
            new PersonalizationInfoDto(2, 5, new List<int> { 9, 12, 15 }, "Achievement, Tip", 0.8)
        );

        var result = Result<GetMotivationalContentResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetMotivationalContentQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var result = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GetMotivationalContentResponse>().Subject;
        
        response.Contents.Should().HaveCount(2);
        response.TotalAvailable.Should().Be(25);
        response.PersonalizationInfo.DeliveredToday.Should().Be(2);
        
        _mockMediator.Verify(m => m.Send(
            It.Is<GetMotivationalContentQuery>(q => 
                q.UserId == _testUserId && 
                q.Count == 3 && 
                q.Language == "en"), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMotivationalContent_WithInvalidCount_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.GetMotivationalContent(0, "en", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Count must be between 1 and 10");
        
        _mockMediator.Verify(m => m.Send(It.IsAny<GetMotivationalContentQuery>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task GetMotivationalContent_WithCountTooHigh_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.GetMotivationalContent(15, "en", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Count must be between 1 and 10");
    }

    [Fact]
    public async Task GetMotivationalContent_WithInvalidLanguage_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.GetMotivationalContent(3, "fr", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Language must be 'en' or 'es'");
    }

    [Fact]
    public async Task GetMotivationalContent_WithSpanishLanguage_ShouldPassCorrectLanguage()
    {
        // Arrange
        var expectedResponse = new GetMotivationalContentResponse(
            new List<MotivationalContentDto>(),
            0,
            new PersonalizationInfoDto(0, 5, new List<int>(), "", 0.0)
        );

        var result = Result<GetMotivationalContentResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetMotivationalContentQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.GetMotivationalContent(3, "es", CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<GetMotivationalContentQuery>(q => q.Language == "es"), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMotivationalContent_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var result = Result<GetMotivationalContentResponse>.Failure(
            "Content service unavailable");
        _mockMediator.Setup(m => m.Send(It.IsAny<GetMotivationalContentQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var result = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Failed to retrieve motivational content");
        problemDetails.Detail.Should().Be("Content service unavailable");
    }

    [Fact]
    public async Task GetMotivationalContent_WithMediatorException_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockMediator.Setup(m => m.Send(It.IsAny<GetMotivationalContentQuery>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problemDetails = statusResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task GetMotivationalContent_WithUnauthenticatedUser_ShouldReturnUnauthorized()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("User ID not found in token");
    }

    #endregion

    #region RecordContentInteraction Tests

    [Fact]
    public async Task RecordContentInteraction_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var contentId = Guid.NewGuid();
        var request = new ContentInteractionRequest("click");

        // Act
        var result = await _controller.RecordContentInteraction(contentId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task RecordContentInteraction_WithEmptyContentId_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ContentInteractionRequest("click");

        // Act
        var result = await _controller.RecordContentInteraction(Guid.Empty, request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Content ID cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RecordContentInteraction_WithInvalidInteractionType_ShouldReturnBadRequest(string interactionType)
    {
        // Arrange
        var contentId = Guid.NewGuid();
        var request = new ContentInteractionRequest(interactionType!);

        // Act
        var result = await _controller.RecordContentInteraction(contentId, request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Interaction type is required");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("delete")]
    [InlineData("update")]
    public async Task RecordContentInteraction_WithUnsupportedInteractionType_ShouldReturnBadRequest(string interactionType)
    {
        // Arrange
        var contentId = Guid.NewGuid();
        var request = new ContentInteractionRequest(interactionType);

        // Act
        var result = await _controller.RecordContentInteraction(contentId, request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invalid interaction type. Must be one of: view, click, share, dismiss");
    }

    [Theory]
    [InlineData("view")]
    [InlineData("click")]
    [InlineData("share")]
    [InlineData("dismiss")]
    public async Task RecordContentInteraction_WithValidInteractionTypes_ShouldReturnOk(string interactionType)
    {
        // Arrange
        var contentId = Guid.NewGuid();
        var request = new ContentInteractionRequest(interactionType);

        // Act
        var result = await _controller.RecordContentInteraction(contentId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task RecordContentInteraction_WithUnauthenticatedUser_ShouldReturnUnauthorized()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        
        var contentId = Guid.NewGuid();
        var request = new ContentInteractionRequest("click");

        // Act
        var result = await _controller.RecordContentInteraction(contentId, request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("User ID not found in token");
    }

    #endregion

    #region GetDashboardMetrics Tests

    [Fact]
    public async Task GetDashboardMetrics_WithAuthenticatedUser_ShouldReturnMetrics()
    {
        // Act
        var result = await _controller.GetDashboardMetrics(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardMetricsResponse>().Subject;
        
        response.CompletedTasksToday.Should().Be(0);
        response.TotalActiveTasks.Should().Be(0);
        response.OverdueTasks.Should().Be(0);
        response.ProductivityStreak.Should().Be(0);
        response.MotivationalContentDelivered.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboardMetrics_WithUnauthenticatedUser_ShouldReturnUnauthorized()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetDashboardMetrics(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("User ID not found in token");
    }

    #endregion

    #region Helper Methods

    private void SetupAuthenticatedUser()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new(ClaimTypes.Email, "test@example.com")
        };
        
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    #endregion

    public void Dispose()
    {
        _controller.Dispose();
    }
}
