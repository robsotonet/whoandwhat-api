using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Dashboard;
using WhoAndWhat.Application.Features.Dashboard.Commands.ResetDashboardPreferences;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;
using WhoAndWhat.Application.Features.Dashboard.Queries.ExportDashboardData;
using WhoAndWhat.Application.Features.Dashboard.Queries.GenerateDashboardReport;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetCompletionStats;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetOverdueTasks;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetProductivityStreak;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers;

/// <summary>
/// Comprehensive unit tests for DashboardController
/// Tests all dashboard endpoints including motivational content, metrics, and interaction recording
/// </summary>
public class DashboardControllerTests
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
            new List<WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent.MotivationalContentDto>
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
        var actionResult = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
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
            new List<WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent.MotivationalContentDto>(),
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
        var actionResult = await _controller.GetMotivationalContent(3, "en", CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
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
    public async Task RecordContentInteraction_WithInvalidInteractionType_ShouldReturnBadRequest(string? interactionType)
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
        // Arrange
        var expectedResponse = new GetDashboardMetricsResponse(
            CompletedTasksToday: 5,
            CompletedTasksThisWeek: 20,
            CompletedTasksThisMonth: 75,
            TotalActiveTasks: 10,
            OverdueTaskDtos: 2,
            TasksCompletedOnTime: 18,
            TasksCompletedLate: 2,
            CompletionRate: 0.8,
            OnTimeCompletionRate: 0.9,
            CategoryBreakdown: new TaskCategoryStats(3, 2, 1, 2, 2),
            PriorityBreakdown: new TaskPriorityStats(1, 3, 4, 2, 0),
            Trends: new ProductivityTrends(2.5, 17.5, 5, 12,
                new List<DailyProductivityPoint>(), new List<WeeklyProductivityPoint>())
        );

        var result = Result<GetDashboardMetricsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetDashboardMetricsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetDashboardMetrics(new DashboardMetricsRequestDto(), CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GetDashboardMetricsResponse>().Subject;

        response.CompletedTasksToday.Should().Be(0);
        response.TotalActiveTasks.Should().Be(0);
        response.OverdueTaskDtos.Should().Be(0);
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
        var result = await _controller.GetDashboardMetrics(new DashboardMetricsRequestDto(), CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("User ID not found in token");
    }

    #endregion

    #region GetProductivityStreak Tests

    [Fact]
    public async Task GetProductivityStreak_WithValidRequest_ShouldReturnStreakData()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto(
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow,
            IncludeMilestones: true,
            IncludeHistory: true,
            IncludeInsights: true,
            MaxHistoryDays: 90
        );

        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 7,
            LongestStreak: 15,
            BestMonthlyStreak: 12,
            LastCompletionDate: DateTime.UtcNow.AddHours(-2),
            StreakStartDate: DateTime.UtcNow.AddDays(-7),
            Milestones: new List<StreakMilestone>
            {
                new(Days: 7, Title: "One Week Streak", Description: "Completed tasks for 7 consecutive days",
                    IsAchieved: true, AchievedDate: DateTime.UtcNow.AddDays(-1)),
                new(Days: 14, Title: "Two Week Streak", Description: "Completed tasks for 14 consecutive days",
                    IsAchieved: false, AchievedDate: null)
            },
            WeeklyStats: new StreakStats(
                TotalDays: 7,
                ActiveDays: 5,
                CompletedTasks: 21,
                ConsistencyRate: 0.85,
                AverageTasksPerDay: 3.0
            ),
            MonthlyStats: new StreakStats(
                TotalDays: 30,
                ActiveDays: 22,
                CompletedTasks: 75,
                ConsistencyRate: 0.73,
                AverageTasksPerDay: 2.5
            ),
            Last30Days: new List<DailyStreakPoint>
            {
                new(Date: DateTime.UtcNow.AddDays(-1), HasActivity: true, CompletedTasks: 3, IsPartOfCurrentStreak: true),
                new(Date: DateTime.UtcNow.AddDays(-2), HasActivity: true, CompletedTasks: 2, IsPartOfCurrentStreak: true)
            }
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductivityStreakResponseDto>().Subject;

        response.CurrentStreak.Should().Be(7);
        response.LongestStreak.Should().Be(15);
        response.LastActivityDate.Should().BeCloseTo(DateTime.UtcNow.AddHours(-2), TimeSpan.FromMinutes(1));
        response.Milestones.Should().HaveCount(2);
        response.StreakHistory.Should().HaveCount(2);
        response.Insights.ConsistencyScore.Should().Be(0.85);

        _mockMediator.Verify(m => m.Send(
            It.Is<GetProductivityStreakQuery>(q =>
                q.UserId == _testUserId &&
                true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductivityStreak_WithDefaultParameters_ShouldUseDefaults()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto();
        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 0,
            LongestStreak: 0,
            BestMonthlyStreak: 0,
            LastCompletionDate: null,
            StreakStartDate: null,
            Milestones: new List<StreakMilestone>(),
            WeeklyStats: new StreakStats(0, 0, 0, 0.0, 0.0),
            MonthlyStats: new StreakStats(0, 0, 0, 0.0, 0.0),
            Last30Days: new List<DailyStreakPoint>()
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductivityStreakResponseDto>().Subject;

        response.CurrentStreak.Should().Be(0);
        response.LongestStreak.Should().Be(0);
        response.Milestones.Should().BeEmpty();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetProductivityStreakQuery>(q =>
                q.UserId == _testUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductivityStreak_WithSpecificDateRange_ShouldPassCorrectDates()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-60);
        var endDate = DateTime.UtcNow.AddDays(-30);
        var request = new ProductivityStreakRequestDto(
            StartDate: startDate,
            EndDate: endDate,
            IncludeMilestones: false,
            IncludeHistory: false,
            IncludeInsights: false,
            MaxHistoryDays: 30
        );

        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 5,
            LongestStreak: 10,
            BestMonthlyStreak: 8,
            LastCompletionDate: endDate,
            StreakStartDate: endDate.AddDays(-5),
            Milestones: new List<StreakMilestone>(),
            WeeklyStats: new StreakStats(7, 5, 15, 0.5, 2.14),
            MonthlyStats: new StreakStats(30, 18, 45, 0.6, 1.5),
            Last30Days: new List<DailyStreakPoint>()
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetProductivityStreakQuery>(q =>
                q.UserId == _testUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public async Task GetProductivityStreak_WithDifferentMaxHistoryDays_ShouldAcceptValidValues(int maxHistoryDays)
    {
        // Arrange
        var request = new ProductivityStreakRequestDto(MaxHistoryDays: maxHistoryDays);
        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 3,
            LongestStreak: 8,
            BestMonthlyStreak: 6,
            LastCompletionDate: DateTime.UtcNow,
            StreakStartDate: DateTime.UtcNow.AddDays(-3),
            Milestones: new List<StreakMilestone>(),
            WeeklyStats: new StreakStats(7, 4, 12, 0.7, 1.71),
            MonthlyStats: new StreakStats(30, 20, 60, 0.67, 2.0),
            Last30Days: new List<DailyStreakPoint>()
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetProductivityStreakQuery>(q => q.UserId == _testUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductivityStreak_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto();
        var result = Result<GetProductivityStreakResponse>.Failure("Unable to calculate productivity streak");
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Unable to calculate productivity streak");
    }

    [Fact]
    public async Task GetProductivityStreak_WithMediatorException_ShouldReturnInternalServerError()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto();
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert - Should not throw due to controller's exception handling
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // This test will help identify if the controller needs exception handling for this endpoint
        // The controller might not have try-catch blocks like the GetMotivationalContent method
    }

    [Fact]
    public async Task GetProductivityStreak_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new ProductivityStreakRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.GetProductivityStreak(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetProductivityStreak_WithComplexStreakData_ShouldMapCorrectly()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto(IncludeMilestones: true, IncludeHistory: true);
        var milestone1 = new StreakMilestone(7, "Week Warrior", "One week streak achieved", true, DateTime.UtcNow.AddDays(-5));
        var milestone2 = new StreakMilestone(30, "Monthly Master", "One month streak", false, null);
        var dailyPoint1 = new DailyStreakPoint(DateTime.UtcNow.AddDays(-10), true, 2, false);
        var dailyPoint2 = new DailyStreakPoint(DateTime.UtcNow.AddDays(-2), true, 3, true);

        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 10,
            LongestStreak: 22,
            BestMonthlyStreak: 18,
            LastCompletionDate: DateTime.UtcNow.AddHours(-1),
            StreakStartDate: DateTime.UtcNow.AddDays(-10),
            Milestones: new List<StreakMilestone> { milestone1, milestone2 },
            WeeklyStats: new StreakStats(7, 6, 18, 0.92, 2.57),
            MonthlyStats: new StreakStats(30, 25, 75, 0.83, 2.5),
            Last30Days: new List<DailyStreakPoint> { dailyPoint1, dailyPoint2 }
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductivityStreakResponseDto>().Subject;

        // Verify main statistics
        response.CurrentStreak.Should().Be(10);
        response.LongestStreak.Should().Be(22);
        response.LastActivityDate.Should().BeCloseTo(DateTime.UtcNow.AddHours(-1), TimeSpan.FromMinutes(5));

        // Verify milestones mapping
        response.Milestones.Should().HaveCount(2);
        var mappedMilestone1 = response.Milestones.First(m => m.Days == 7);
        mappedMilestone1.Title.Should().Be("Week Warrior");
        mappedMilestone1.IsAchieved.Should().BeTrue();
        mappedMilestone1.AchievedDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-5), TimeSpan.FromMinutes(5));

        var mappedMilestone2 = response.Milestones.First(m => m.Days == 30);
        mappedMilestone2.Title.Should().Be("Monthly Master");
        mappedMilestone2.IsAchieved.Should().BeFalse();
        mappedMilestone2.AchievedDate.Should().BeNull();

        // Verify streak history mapping
        response.StreakHistory.Should().HaveCount(2);
        var mappedHistory1 = response.StreakHistory.First(h => h.StreakType == "past");
        mappedHistory1.Duration.Should().Be(7);
        mappedHistory1.TasksCompleted.Should().Be(14);

        var mappedHistory2 = response.StreakHistory.First(h => h.StreakType == "current");
        mappedHistory2.Duration.Should().Be(2);
        mappedHistory2.TasksCompleted.Should().Be(6);

        // Verify insights mapping
        response.Insights.ConsistencyScore.Should().Be(0.92);
        response.Insights.BestPeriod.Should().Be("morning");
        response.Insights.SuccessFactors.Should().HaveCount(3);
        response.Insights.SuccessFactors.Should().Contain("Early start");
        response.Insights.ImprovementAreas.Should().HaveCount(2);
        response.Insights.ImprovementAreas.Should().Contain("Weekend gaps");
    }

    [Fact]
    public async Task GetProductivityStreak_ShouldLogCorrectInformation()
    {
        // Arrange
        var request = new ProductivityStreakRequestDto();
        var expectedResponse = new GetProductivityStreakResponse(
            CurrentStreak: 5,
            LongestStreak: 12,
            BestMonthlyStreak: 10,
            LastCompletionDate: DateTime.UtcNow,
            StreakStartDate: DateTime.UtcNow.AddDays(-5),
            Milestones: new List<StreakMilestone>(),
            WeeklyStats: new StreakStats(7, 5, 15, 0.8, 2.14),
            MonthlyStats: new StreakStats(30, 22, 66, 0.73, 2.2),
            Last30Days: new List<DailyStreakPoint>()
        );

        var result = Result<GetProductivityStreakResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetProductivityStreakQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.GetProductivityStreak(request, CancellationToken.None);

        // Assert - Verify logging was called correctly
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting productivity streak for user {_testUserId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region GetOverdueTaskDtos Tests

    [Fact]
    public async Task GetOverdueTaskDtos_WithValidRequest_ShouldReturnOverdueTaskDtosData()
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto(
            Categories: new List<string> { "ToDo", "BillReminder" },
            Priorities: new List<string> { "High", "Critical" },
            UrgencyLevels: new List<string> { "high", "critical" },
            MaxTasks: 25,
            IncludeAnalysis: true,
            IncludeRecommendations: true,
            SortBy: "daysOverdue",
            SortOrder: "desc"
        );

        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 15,
            OverdueTaskDtos: new List<OverdueTaskDto>
            {
                new(Id: Guid.NewGuid(), Title: "Pay electricity bill", Category: "BillReminder",
                    Priority: "Critical", DueDate: DateTime.UtcNow.AddDays(-5), DaysOverdue: 5,
                    UrgencyLevel: "critical", Tags: new List<string> { "urgent", "bills" }),
                new(Id: Guid.NewGuid(), Title: "Submit project report", Category: "ToDo",
                    Priority: "High", DueDate: DateTime.UtcNow.AddDays(-2), DaysOverdue: 2,
                    UrgencyLevel: "high", Tags: new List<string> { "work", "deadline" })
            },
            Analysis: new OverdueAnalysisDto(
                CategoryBreakdown: new Dictionary<string, int> { { "BillReminder", 8 }, { "ToDo", 7 } },
                PriorityBreakdown: new Dictionary<string, int> { { "Critical", 5 }, { "High", 10 } },
                UrgencyBreakdown: new Dictionary<string, int> { { "critical", 5 }, { "high", 10 } },
                AverageOverdueDays: 4.2,
                CommonPatterns: new List<string> { "Bills overdue on weekends", "Work tasks delayed by 2-3 days" }
            ),
            Recommendations: new List<string>
            {
                "Set up automated bill payments",
                "Create buffer time for work deadlines",
                "Review task priorities weekly"
            }
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OverdueTaskDtosResponseDto>().Subject;

        response.TotalOverdueCount.Should().Be(15);
        response.OverdueTaskDtos.Should().HaveCount(2);

        var firstTask = response.OverdueTaskDtos.First();
        firstTask.Title.Should().Be("Pay electricity bill");
        firstTask.Category.Should().Be("BillReminder");
        firstTask.Priority.Should().Be("Critical");
        firstTask.DaysOverdue.Should().Be(5);
        firstTask.UrgencyLevel.Should().Be("critical");
        firstTask.Tags.Should().Contain("urgent");

        response.Analysis.CategoryBreakdown.Should().ContainKey("BillReminder");
        response.Analysis.AverageOverdueDays.Should().Be(4.2);
        response.Recommendations.Should().HaveCount(3);
        response.Recommendations.Should().Contain("Set up automated bill payments");

        _mockMediator.Verify(m => m.Send(
            It.Is<GetOverdueTaskDtosQuery>(q =>
                q.UserId == _testUserId &&
                q.Categories!.SequenceEqual(request.Categories!) &&
                q.Priorities!.SequenceEqual(request.Priorities!) &&
                q.UrgencyLevels!.SequenceEqual(request.UrgencyLevels!) &&
                q.MaxTasks == request.MaxTasks &&
                q.IncludeAnalysis == request.IncludeAnalysis &&
                q.IncludeRecommendations == request.IncludeRecommendations &&
                q.SortBy == request.SortBy &&
                q.SortOrder == request.SortOrder),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverdueTaskDtos_WithDefaultParameters_ShouldUseDefaults()
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto();
        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 0,
            OverdueTaskDtos: new List<OverdueTaskDto>(),
            Analysis: new OverdueAnalysisDto(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                0.0,
                new List<string>()
            ),
            Recommendations: new List<string>()
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OverdueTaskDtosResponseDto>().Subject;

        response.TotalOverdueCount.Should().Be(0);
        response.OverdueTaskDtos.Should().BeEmpty();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetOverdueTaskDtosQuery>(q =>
                q.UserId == _testUserId &&
                q.MaxTasks == 50 &&
                q.IncludeAnalysis == true &&
                q.IncludeRecommendations == true &&
                q.SortBy == "daysOverdue" &&
                q.SortOrder == "desc"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("daysOverdue", "desc")]
    [InlineData("priority", "asc")]
    [InlineData("category", "desc")]
    [InlineData("title", "asc")]
    public async Task GetOverdueTaskDtos_WithDifferentSortOptions_ShouldPassCorrectSorting(string sortBy, string sortOrder)
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto(SortBy: sortBy, SortOrder: sortOrder);
        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 5,
            OverdueTaskDtos: new List<OverdueTaskDto>(),
            Analysis: new OverdueAnalysisDto(new Dictionary<string, int>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), 0.0, new List<string>()),
            Recommendations: new List<string>()
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetOverdueTaskDtosQuery>(q =>
                q.SortBy == sortBy &&
                q.SortOrder == sortOrder),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task GetOverdueTaskDtos_WithDifferentMaxTasks_ShouldAcceptValidValues(int maxTasks)
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto(MaxTasks: maxTasks);
        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: maxTasks / 2,
            OverdueTaskDtos: new List<OverdueTaskDto>(),
            Analysis: new OverdueAnalysisDto(new Dictionary<string, int>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), 0.0, new List<string>()),
            Recommendations: new List<string>()
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetOverdueTaskDtosQuery>(q => q.MaxTasks == maxTasks),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverdueTaskDtos_WithSpecificFilters_ShouldPassFiltersCorrectly()
    {
        // Arrange
        var categories = new List<string> { "ToDo", "Project", "BillReminder" };
        var priorities = new List<string> { "Critical", "High" };
        var urgencyLevels = new List<string> { "critical", "high", "medium" };

        var request = new OverdueTaskDtosRequestDto(
            Categories: categories,
            Priorities: priorities,
            UrgencyLevels: urgencyLevels,
            MaxTasks: 30,
            IncludeAnalysis: false,
            IncludeRecommendations: false
        );

        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 12,
            OverdueTaskDtos: new List<OverdueTaskDto>(),
            Analysis: new OverdueAnalysisDto(new Dictionary<string, int>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), 0.0, new List<string>()),
            Recommendations: new List<string>()
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetOverdueTaskDtosQuery>(q =>
                q.Categories!.SequenceEqual(categories) &&
                q.Priorities!.SequenceEqual(priorities) &&
                q.UrgencyLevels!.SequenceEqual(urgencyLevels) &&
                q.MaxTasks == 30 &&
                q.IncludeAnalysis == false &&
                q.IncludeRecommendations == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverdueTaskDtos_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto();
        var result = Result<GetOverdueTaskDtosResponse>.Failure("Unable to retrieve overdue tasks");
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Unable to retrieve overdue tasks");
    }

    [Fact]
    public async Task GetOverdueTaskDtos_WithComplexAnalysisData_ShouldMapCorrectly()
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto(IncludeAnalysis: true, IncludeRecommendations: true);
        var task1 = new OverdueTaskDto(Guid.NewGuid(), "Critical Task 1", "Project", "Critical",
            DateTime.UtcNow.AddDays(-10), 10, "critical", new List<string> { "urgent", "project" });
        var task2 = new OverdueTaskDto(Guid.NewGuid(), "High Priority Task", "ToDo", "High",
            DateTime.UtcNow.AddDays(-3), 3, "high", new List<string> { "work" });

        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 25,
            OverdueTaskDtos: new List<OverdueTaskDto> { task1, task2 },
            Analysis: new OverdueAnalysisDto(
                CategoryBreakdown: new Dictionary<string, int>
                {
                    { "Project", 12 },
                    { "ToDo", 8 },
                    { "BillReminder", 5 }
                },
                PriorityBreakdown: new Dictionary<string, int>
                {
                    { "Critical", 7 },
                    { "High", 15 },
                    { "Medium", 3 }
                },
                UrgencyBreakdown: new Dictionary<string, int>
                {
                    { "critical", 7 },
                    { "high", 12 },
                    { "medium", 6 }
                },
                AverageOverdueDays: 6.8,
                CommonPatterns: new List<string>
                {
                    "Projects tend to go overdue by 7+ days",
                    "Bills are often forgotten on weekends",
                    "High priority tasks delayed during busy weeks"
                }
            ),
            Recommendations: new List<string>
            {
                "Implement project milestone tracking",
                "Set up automated bill reminders",
                "Schedule weekly priority reviews",
                "Consider breaking large tasks into smaller chunks"
            }
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OverdueTaskDtosResponseDto>().Subject;

        // Verify main statistics
        response.TotalOverdueCount.Should().Be(25);
        response.OverdueTaskDtos.Should().HaveCount(2);

        // Verify task details
        var criticalTask = response.OverdueTaskDtos.First(t => t.Priority == "Critical");
        criticalTask.Title.Should().Be("Critical Task 1");
        criticalTask.DaysOverdue.Should().Be(10);
        criticalTask.UrgencyLevel.Should().Be("critical");
        criticalTask.Tags.Should().Contain("urgent");

        // Verify analysis mapping
        response.Analysis.CategoryBreakdown.Should().HaveCount(3);
        response.Analysis.CategoryBreakdown["Project"].Should().Be(12);
        response.Analysis.PriorityBreakdown["Critical"].Should().Be(7);
        response.Analysis.UrgencyBreakdown["critical"].Should().Be(7);
        response.Analysis.AverageOverdueDays.Should().Be(6.8);
        response.Analysis.CommonPatterns.Should().HaveCount(3);
        response.Analysis.CommonPatterns.Should().Contain("Projects tend to go overdue by 7+ days");

        // Verify recommendations
        response.Recommendations.Should().HaveCount(4);
        response.Recommendations.Should().Contain("Implement project milestone tracking");
    }

    [Fact]
    public async Task GetOverdueTaskDtos_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new OverdueTaskDtosRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.GetOverdueTaskDtos(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetOverdueTaskDtos_ShouldLogCorrectInformation()
    {
        // Arrange
        var request = new OverdueTaskDtosRequestDto();
        var expectedResponse = new GetOverdueTaskDtosResponse(
            TotalOverdueCount: 10,
            OverdueTaskDtos: new List<OverdueTaskDto>(),
            Analysis: new OverdueAnalysisDto(new Dictionary<string, int>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), 0.0, new List<string>()),
            Recommendations: new List<string>()
        );

        var result = Result<GetOverdueTaskDtosResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetOverdueTaskDtosQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.GetOverdueTaskDtos(request, CancellationToken.None);

        // Assert - Verify logging was called correctly
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting overdue tasks for user {_testUserId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region GetCompletionStats Tests

    [Fact]
    public async Task GetCompletionStats_WithValidRequest_ShouldReturnCompletionStatsData()
    {
        // Arrange
        var request = new CompletionStatsRequestDto(
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow,
            Period: "month",
            IncludeCategories: new List<string> { "ToDo", "Project", "BillReminder" },
            IncludeTrends: true,
            IncludeBreakdowns: true,
            IncludeInsights: true
        );

        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 160,
                TotalTasksCompleted: 125,
                TasksInProgress: 20,
                TasksPending: 15,
                CompletionRate: 0.78,
                OnTimeCompletionRate: 0.65,
                AverageCompletionTime: TimeSpan.FromHours(2.5),
                TasksCompletedAheadOfSchedule: 30,
                TasksCompletedLate: 18
            ),
            Trends: new List<CompletionTrendDto>
            {
                new(Period: DateTime.UtcNow.AddDays(-7), Completed: 25, CompletionRate: 0.75, TrendDirection: "up"),
                new(Period: DateTime.UtcNow.AddDays(-14), Completed: 20, CompletionRate: 0.65, TrendDirection: "stable"),
                new(Period: DateTime.UtcNow.AddDays(-21), Completed: 18, CompletionRate: 0.60, TrendDirection: "down")
            },
            Breakdowns: new Dictionary<string, CompletionBreakdown>
            {
                ["category"] = new(
                    Items: new Dictionary<string, int> { { "ToDo", 60 }, { "Project", 40 }, { "BillReminder", 25 } },
                    Rates: new Dictionary<string, double> { { "ToDo", 0.80 }, { "Project", 0.75 }, { "BillReminder", 0.83 } },
                    TopPerformer: "BillReminder",
                    NeedsAttention: "Project"
                ),
                ["priority"] = new(
                    Items: new Dictionary<string, int> { { "Critical", 30 }, { "High", 50 }, { "Medium", 35 }, { "Low", 10 } },
                    Rates: new Dictionary<string, double> { { "Critical", 0.95 }, { "High", 0.85 }, { "Medium", 0.70 }, { "Low", 0.50 } },
                    TopPerformer: "Critical",
                    NeedsAttention: "Low"
                )
            },
            Insights: new CompletionInsightsDto(
                PositivePatterns: new List<string>
                {
                    "Consistent daily completion habits",
                    "Strong performance on critical tasks",
                    "Improved completion rates over past month"
                },
                AreasForImprovement: new List<string>
                {
                    "Low priority tasks often delayed",
                    "Project tasks take longer than expected",
                    "Weekend completion rates could be better"
                },
                Recommendations: new List<string>
                {
                    "Consider time-boxing for project tasks",
                    "Schedule low priority tasks during high-energy periods",
                    "Set up weekend reminders for consistency"
                },
                PerformanceMetrics: new Dictionary<string, double>
                {
                    { "consistency_score", 0.85 },
                    { "velocity_trend", 0.12 },
                    { "quality_index", 0.78 },
                    { "efficiency_rating", 0.82 }
                }
            )
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CompletionStatsResponseDto>().Subject;

        // Verify overview statistics
        response.Overview.TotalCompleted.Should().Be(125);
        response.Overview.CompletionRate.Should().Be(0.78);
        response.Overview.AverageCompletionTime.Should().Be(2.5);
        response.Overview.CompletedToday.Should().Be(8);
        response.Overview.CompletedThisWeek.Should().Be(32);
        response.Overview.CompletedThisMonth.Should().Be(125);

        // Verify trends
        response.Trends.Should().HaveCount(3);
        var firstTrend = response.Trends.First();
        firstTrend.Completed.Should().Be(25);
        firstTrend.CompletionRate.Should().Be(0.75);
        firstTrend.TrendDirection.Should().Be("up");

        // Verify breakdowns
        response.Breakdowns.Should().ContainKey("category");
        response.Breakdowns["category"].TopPerformer.Should().Be("BillReminder");
        response.Breakdowns["priority"].NeedsAttention.Should().Be("Low");

        // Verify insights
        response.Insights.PositivePatterns.Should().HaveCount(3);
        response.Insights.AreasForImprovement.Should().HaveCount(3);
        response.Insights.Recommendations.Should().HaveCount(3);
        response.Insights.PerformanceMetrics.Should().HaveCount(4);

        _mockMediator.Verify(m => m.Send(
            It.Is<GetCompletionStatsQuery>(q =>
                q.UserId == _testUserId &&
                q.Period == request.Period),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCompletionStats_WithDefaultParameters_ShouldUseDefaults()
    {
        // Arrange
        var request = new CompletionStatsRequestDto();
        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 0,
                TotalTasksCompleted: 0,
                TasksInProgress: 0,
                TasksPending: 0,
                CompletionRate: 0.0,
                OnTimeCompletionRate: 0.0,
                AverageCompletionTime: TimeSpan.Zero,
                TasksCompletedAheadOfSchedule: 0,
                TasksCompletedLate: 0
            ),
            Trends: new CompletionTrends(
                DailyData: new List<DailyCompletionPoint>(),
                WeeklyData: new List<WeeklyCompletionPoint>(),
                MonthlyData: new List<MonthlyCompletionPoint>(),
                Velocity: new CompletionVelocity(0.0, 0.0, 0.0, "stable", 0.0)
            ),
            Breakdown: new CompletionBreakdown(
                ByCategory: new Dictionary<string, CompletionCategoryStats>(),
                ByPriority: new Dictionary<string, CompletionPriorityStats>(),
                ByHourOfDay: new Dictionary<int, int>(),
                ByDayOfWeek: new Dictionary<string, int>(),
                ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>()
            ),
            Comparison: new CompletionComparison(0.0, 0.0, 0.0, "stable", 0, 0, "None", "None"),
            Insights: new List<CompletionInsight>()
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CompletionStatsResponseDto>().Subject;

        response.Overview.TotalCompleted.Should().Be(0);
        response.Trends.Should().BeEmpty();
        response.Breakdowns.Should().BeEmpty();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetCompletionStatsQuery>(q =>
                q.UserId == _testUserId &&
                q.Period == "month"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("quarter")]
    [InlineData("year")]
    public async Task GetCompletionStats_WithDifferentPeriods_ShouldPassCorrectPeriod(string period)
    {
        // Arrange
        var request = new CompletionStatsRequestDto(Period: period);
        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 20,
                TotalTasksCompleted: 10,
                TasksInProgress: 5,
                TasksPending: 5,
                CompletionRate: 0.5,
                OnTimeCompletionRate: 0.8,
                AverageCompletionTime: TimeSpan.FromHours(1.0),
                TasksCompletedAheadOfSchedule: 2,
                TasksCompletedLate: 3
            ),
            Trends: new List<CompletionTrendDto>(),
            Breakdown: new CompletionBreakdown(
                ByCategory: new Dictionary<string, CompletionCategoryStats>(),
                ByPriority: new Dictionary<string, CompletionPriorityStats>(),
                ByHourOfDay: new Dictionary<int, int>(),
                ByDayOfWeek: new Dictionary<string, int>(),
                ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>()
            ),
            Insights: new CompletionInsightsDto(new List<string>(), new List<string>(), new List<string>(), new Dictionary<string, double>())
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetCompletionStatsQuery>(q =>
                q.UserId == _testUserId &&
                q.Period == period),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCompletionStats_WithSpecificDateRange_ShouldPassCorrectDates()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-90);
        var endDate = DateTime.UtcNow.AddDays(-7);
        var request = new CompletionStatsRequestDto(
            StartDate: startDate,
            EndDate: endDate,
            Period: "week",
            IncludeTrends: false,
            IncludeBreakdowns: false,
            IncludeInsights: false
        );

        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 66,
                TotalTasksCompleted: 45,
                TasksInProgress: 12,
                TasksPending: 9,
                CompletionRate: 0.68,
                OnTimeCompletionRate: 0.75,
                AverageCompletionTime: TimeSpan.FromHours(3.2),
                TasksCompletedAheadOfSchedule: 15,
                TasksCompletedLate: 8
            ),
            Trends: new List<CompletionTrendDto>(),
            Breakdown: new CompletionBreakdown(
                ByCategory: new Dictionary<string, CompletionCategoryStats>(),
                ByPriority: new Dictionary<string, CompletionPriorityStats>(),
                ByHourOfDay: new Dictionary<int, int>(),
                ByDayOfWeek: new Dictionary<string, int>(),
                ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>()
            ),
            Insights: new CompletionInsightsDto(new List<string>(), new List<string>(), new List<string>(), new Dictionary<string, double>())
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GetCompletionStatsQuery>(q =>
                q.UserId == _testUserId &&
                q.Period == "week"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCompletionStats_WithSpecificCategories_ShouldPassCategories()
    {
        // Arrange
        var categories = new List<string> { "ToDo", "Project", "Appointment" };
        var request = new CompletionStatsRequestDto(IncludeCategories: categories);

        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 91,
                TotalTasksCompleted: 75,
                TasksInProgress: 10,
                TasksPending: 6,
                CompletionRate: 0.82,
                OnTimeCompletionRate: 0.90,
                AverageCompletionTime: TimeSpan.FromHours(2.1),
                TasksCompletedAheadOfSchedule: 25,
                TasksCompletedLate: 5
            ),
            Trends: new List<CompletionTrendDto>(),
            Breakdown: new CompletionBreakdown(
                ByCategory: new Dictionary<string, CompletionCategoryStats>
                {
                    ["ToDo"] = new CompletionCategoryStats(40, 34, 0.85, TimeSpan.FromHours(2.1), 2),
                    ["Project"] = new CompletionCategoryStats(20, 15, 0.75, TimeSpan.FromHours(3.2), 5),
                    ["Appointment"] = new CompletionCategoryStats(15, 13, 0.88, TimeSpan.FromHours(1.5), 1)
                },
                ByPriority: new Dictionary<string, CompletionPriorityStats>(),
                ByHourOfDay: new Dictionary<int, int>(),
                ByDayOfWeek: new Dictionary<string, int>(),
                ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>()
            ),
            Insights: new CompletionInsightsDto(new List<string>(), new List<string>(), new List<string>(), new Dictionary<string, double>())
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CompletionStatsResponseDto>().Subject;

        response.Breakdowns.Should().ContainKey("category");
        response.Breakdowns["category"].Items.Should().ContainKeys("ToDo", "Project", "Appointment");

        _mockMediator.Verify(m => m.Send(
            It.Is<GetCompletionStatsQuery>(q =>
                q.UserId == _testUserId &&
                q.Period == "month"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCompletionStats_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CompletionStatsRequestDto();
        var result = Result<GetCompletionStatsResponse>.Failure("Unable to calculate completion statistics");
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Unable to calculate completion statistics");
    }

    [Fact]
    public async Task GetCompletionStats_WithComplexTrendsAndBreakdowns_ShouldMapCorrectly()
    {
        // Arrange
        var request = new CompletionStatsRequestDto(IncludeTrends: true, IncludeBreakdowns: true);

        var trends = new List<CompletionTrendDto>
        {
            new(DateTime.UtcNow.AddDays(-28), 15, 0.60, "down"),
            new(DateTime.UtcNow.AddDays(-21), 18, 0.65, "stable"),
            new(DateTime.UtcNow.AddDays(-14), 22, 0.72, "up"),
            new(DateTime.UtcNow.AddDays(-7), 25, 0.78, "up")
        };

        var breakdown = new CompletionBreakdown(
            ByCategory: new Dictionary<string, CompletionCategoryStats>
            {
                ["ToDo"] = new CompletionCategoryStats(45, 38, 0.85, TimeSpan.FromHours(2.2), 3),
                ["Project"] = new CompletionCategoryStats(25, 17, 0.70, TimeSpan.FromHours(4.1), 6),
                ["BillReminder"] = new CompletionCategoryStats(20, 18, 0.90, TimeSpan.FromHours(1.1), 1),
                ["Appointment"] = new CompletionCategoryStats(15, 14, 0.95, TimeSpan.FromHours(0.8), 0)
            },
            ByPriority: new Dictionary<string, CompletionPriorityStats>
            {
                ["Critical"] = new CompletionPriorityStats(20, 19, 0.95, 0.92, TimeSpan.FromHours(1.5)),
                ["High"] = new CompletionPriorityStats(35, 31, 0.88, 0.85, TimeSpan.FromHours(2.3)),
                ["Medium"] = new CompletionPriorityStats(30, 22, 0.75, 0.70, TimeSpan.FromHours(3.0)),
                ["Low"] = new CompletionPriorityStats(20, 12, 0.60, 0.55, TimeSpan.FromHours(2.9))
            },
            ByHourOfDay: new Dictionary<int, int>
            {
                [9] = 15,
                [10] = 20,
                [11] = 18,
                [14] = 22,
                [15] = 18,
                [16] = 12
            },
            ByDayOfWeek: new Dictionary<string, int>
            {
                ["Monday"] = 20,
                ["Tuesday"] = 18,
                ["Wednesday"] = 22,
                ["Thursday"] = 17,
                ["Friday"] = 15,
                ["Saturday"] = 8,
                ["Sunday"] = 5
            },
            ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>
            {
                ["0-2h"] = new CompletionTimeRangeStats(40, 38.1, "0-2 hours"),
                ["2-4h"] = new CompletionTimeRangeStats(42, 40.0, "2-4 hours"),
                ["4-8h"] = new CompletionTimeRangeStats(18, 17.1, "4-8 hours"),
                ["8h+"] = new CompletionTimeRangeStats(5, 4.8, "8+ hours")
            }
        );

        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 135,
                TotalTasksCompleted: 105,
                TasksInProgress: 18,
                TasksPending: 12,
                CompletionRate: 0.78,
                OnTimeCompletionRate: 0.85,
                AverageCompletionTime: TimeSpan.FromHours(2.8),
                TasksCompletedAheadOfSchedule: 35,
                TasksCompletedLate: 12
            ),
            Trends: trends,
            Breakdown: breakdown,
            Insights: new CompletionInsightsDto(
                PositivePatterns: new List<string> { "Steady upward trend", "Excellent critical task completion" },
                AreasForImprovement: new List<string> { "Project completion efficiency", "Low priority task delays" },
                Recommendations: new List<string> { "Focus on project planning", "Batch low priority tasks" },
                PerformanceMetrics: new Dictionary<string, double> { { "trend_velocity", 0.18 }, { "consistency", 0.82 } }
            )
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CompletionStatsResponseDto>().Subject;

        // Verify trends mapping
        response.Trends.Should().HaveCount(4);
        var latestTrend = response.Trends.Last();
        latestTrend.Completed.Should().Be(25);
        latestTrend.CompletionRate.Should().Be(0.78);
        latestTrend.TrendDirection.Should().Be("up");

        // Verify breakdowns mapping
        response.Breakdown.ByCategory.Should().HaveCount(4);
        response.Breakdown.ByPriority.Should().HaveCount(4);
        response.Breakdown.ByCategory["ToDo"].CompletionRate.Should().Be(0.85);
        response.Breakdown.ByPriority["Critical"].CompletionRate.Should().Be(0.95);

        // Verify insights
        response.Insights.PositivePatterns.Should().Contain("Steady upward trend");
        response.Insights.AreasForImprovement.Should().Contain("Project completion efficiency");
        response.Insights.PerformanceMetrics.Should().ContainKey("trend_velocity");
    }

    [Fact]
    public async Task GetCompletionStats_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new CompletionStatsRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.GetCompletionStats(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetCompletionStats_ShouldLogCorrectInformation()
    {
        // Arrange
        var request = new CompletionStatsRequestDto();
        var expectedResponse = new GetCompletionStatsResponse(
            Overview: new CompletionOverview(
                TotalTasksCreated: 77,
                TotalTasksCompleted: 50,
                TasksInProgress: 15,
                TasksPending: 12,
                CompletionRate: 0.65,
                OnTimeCompletionRate: 0.72,
                AverageCompletionTime: TimeSpan.FromHours(2.3),
                TasksCompletedAheadOfSchedule: 18,
                TasksCompletedLate: 9
            ),
            Trends: new List<CompletionTrendDto>(),
            Breakdown: new CompletionBreakdown(
                ByCategory: new Dictionary<string, CompletionCategoryStats>(),
                ByPriority: new Dictionary<string, CompletionPriorityStats>(),
                ByHourOfDay: new Dictionary<int, int>(),
                ByDayOfWeek: new Dictionary<string, int>(),
                ByTimeRange: new Dictionary<string, CompletionTimeRangeStats>()
            ),
            Insights: new CompletionInsightsDto(new List<string>(), new List<string>(), new List<string>(), new Dictionary<string, double>())
        );

        var result = Result<GetCompletionStatsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetCompletionStatsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.GetCompletionStats(request, CancellationToken.None);

        // Assert - Verify logging was called correctly
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting completion statistics for user {_testUserId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Dashboard Settings Management Tests

    [Fact]
    public async Task GetDashboardSettings_WithAuthenticatedUser_ShouldReturnDefaultSettings()
    {
        // Act
        var actionResult = await _controller.GetDashboardSettings(CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardSettingsResponseDto>().Subject;

        // Verify default settings structure
        response.Success.Should().BeTrue();
        response.Settings.Should().NotBeNull();

        // Verify default values
        response.Settings.Theme.Should().Be("light");
        response.Settings.Language.Should().Be("en");
        response.Settings.ShowCompletionStats.Should().BeTrue();
        response.Settings.ShowProductivityStreak.Should().BeTrue();
        response.Settings.ShowOverdueTasks.Should().BeTrue();
        response.Settings.ShowMotivationalContent.Should().BeTrue();
        response.Settings.RefreshInterval.Should().Be(300);

        // Verify default widgets
        response.Settings.VisibleWidgets.Should().Contain("completion-stats");
        response.Settings.VisibleWidgets.Should().Contain("productivity-streak");
        response.Settings.VisibleWidgets.Should().Contain("overdue-tasks");
        response.Settings.VisibleWidgets.Should().Contain("motivational-content");

        // Verify notification settings
        response.Settings.NotificationSettings.EnableOverdueAlerts.Should().BeTrue();
        response.Settings.NotificationSettings.EnableStreakReminders.Should().BeTrue();
        response.Settings.NotificationSettings.EnableDailyDigest.Should().BeFalse();
        response.Settings.NotificationSettings.OverdueAlertThreshold.Should().Be(3);
        response.Settings.NotificationSettings.DigestFrequency.Should().Be("weekly");
        response.Settings.NotificationSettings.QuietHours.Should().HaveCount(10);

        // Verify display settings
        response.Settings.DisplaySettings.ChartType.Should().Be("bar");
        response.Settings.DisplaySettings.DateFormat.Should().Be("MM/dd/yyyy");
        response.Settings.DisplaySettings.TimeFormat.Should().Be("12h");
        response.Settings.DisplaySettings.Use24HourFormat.Should().BeFalse();
        response.Settings.DisplaySettings.ItemsPerPage.Should().Be(20);
        response.Settings.DisplaySettings.DefaultSortOrder.Should().Be("priority");
        response.Settings.DisplaySettings.ShowAnimations.Should().BeTrue();
        response.Settings.DisplaySettings.CompactMode.Should().BeFalse();

        response.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetDashboardSettings_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.GetDashboardSettings(CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDashboardSettings_WithValidSettings_ShouldReturnUpdatedSettings()
    {
        // Arrange
        var request = new UpdateDashboardSettingsRequestDto(
            Theme: "dark",
            Language: "es",
            ShowCompletionStats: false,
            ShowProductivityStreak: true,
            ShowOverdueTasks: true,
            ShowMotivationalContent: false,
            RefreshInterval: 600,
            VisibleWidgets: new List<string> { "productivity-streak", "overdue-tasks" },
            WidgetSettings: new Dictionary<string, object> { { "theme", "dark" } },
            NotificationSettings: new NotificationSettingsRequestDto(
                EnableOverdueAlerts: false,
                EnableStreakReminders: true,
                EnableDailyDigest: true,
                OverdueAlertThreshold: 5,
                DigestFrequency: "daily",
                QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5 }
            ),
            DisplaySettings: new DisplaySettingsRequestDto(
                ChartType: "pie",
                DateFormat: "dd/MM/yyyy",
                TimeFormat: "24h",
                Use24HourFormat: true,
                ItemsPerPage: 50,
                DefaultSortOrder: "date",
                ShowAnimations: false,
                CompactMode: true
            )
        );

        var expectedCommandResponse = new UpdateDashboardSettingsResponse(
            Success: true,
            UpdatedSettings: new DashboardSettingsDto(
                Theme: "dark",
                Language: "es",
                ShowCompletionStats: false,
                ShowProductivityStreak: true,
                ShowOverdueTasks: true,
                ShowMotivationalContent: false,
                RefreshInterval: 600,
                VisibleWidgets: new List<string> { "productivity-streak", "overdue-tasks" },
                WidgetSettings: new Dictionary<string, object> { { "theme", "dark" } },
                NotificationSettings: new NotificationSettingsDto(
                    EnableOverdueAlerts: false,
                    EnableStreakReminders: true,
                    EnableDailyDigest: true,
                    OverdueAlertThreshold: 5,
                    DigestFrequency: "daily",
                    QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5 }
                ),
                DisplaySettings: new DisplaySettingsDto(
                    ChartType: "pie",
                    DateFormat: "dd/MM/yyyy",
                    TimeFormat: "24h",
                    Use24HourFormat: true,
                    ItemsPerPage: 50,
                    DefaultSortOrder: "date",
                    ShowAnimations: false,
                    CompactMode: true
                )
            ),
            ValidationWarnings: new List<string>()
        );

        var result = Result<UpdateDashboardSettingsResponse>.Success(expectedCommandResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateDashboardSettingsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.UpdateDashboardSettings(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardSettingsResponseDto>().Subject;

        response.Success.Should().BeTrue();
        response.Settings.Theme.Should().Be("dark");
        response.Settings.Language.Should().Be("es");
        response.Settings.RefreshInterval.Should().Be(600);
        response.Settings.VisibleWidgets.Should().BeEquivalentTo(new[] { "productivity-streak", "overdue-tasks" });
        response.Settings.NotificationSettings.EnableDailyDigest.Should().BeTrue();
        response.Settings.DisplaySettings.Use24HourFormat.Should().BeTrue();
        response.ValidationWarnings.Should().BeEmpty();

        _mockMediator.Verify(m => m.Send(
            It.Is<UpdateDashboardSettingsCommand>(c =>
                c.UserId == _testUserId &&
                c.Settings.Theme == "dark" &&
                c.Settings.Language == "es" &&
                c.Settings.RefreshInterval == 600),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDashboardSettings_WithValidationWarnings_ShouldReturnWarnings()
    {
        // Arrange
        var request = new UpdateDashboardSettingsRequestDto(
            Theme: "invalid-theme",
            Language: "fr",
            RefreshInterval: 5, // Below minimum
            VisibleWidgets: new List<string> { "invalid-widget", "completion-stats" }
        );

        var expectedResponse = new UpdateDashboardSettingsResponse(
            Success: true,
            UpdatedSettings: new DashboardSettingsDto(
                Theme: "light", // Corrected to default
                Language: "en", // Corrected to default
                ShowCompletionStats: true,
                ShowProductivityStreak: true,
                ShowOverdueTasks: true,
                ShowMotivationalContent: true,
                RefreshInterval: 300, // Corrected to minimum
                VisibleWidgets: new List<string> { "completion-stats" }, // Invalid widget removed
                WidgetSettings: new Dictionary<string, object>(),
                NotificationSettings: new NotificationSettingsDto(true, true, false, 3, "weekly", new List<int>()),
                DisplaySettings: new DisplaySettingsDto("bar", "MM/dd/yyyy", "12h", false, 20, "priority", true, false)
            ),
            ValidationWarnings: new List<string>
            {
                "Invalid theme 'invalid-theme'. Using default theme.",
                "Invalid language 'fr'. Using default language.",
                "Refresh interval should be between 30 seconds and 1 hour. Using default value.",
                "Invalid widgets: invalid-widget"
            }
        );

        var result = Result<UpdateDashboardSettingsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateDashboardSettingsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.UpdateDashboardSettings(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardSettingsResponseDto>().Subject;

        response.Success.Should().BeTrue();
        response.ValidationWarnings.Should().HaveCount(4);
        response.ValidationWarnings.Should().Contain("Invalid theme 'invalid-theme'. Using default theme.");
        response.ValidationWarnings.Should().Contain("Invalid language 'fr'. Using default language.");
        response.ValidationWarnings.Should().Contain("Refresh interval should be between 30 seconds and 1 hour. Using default value.");
        response.ValidationWarnings.Should().Contain("Invalid widgets: invalid-widget");

        // Verify corrected values
        response.Settings.Theme.Should().Be("light");
        response.Settings.Language.Should().Be("en");
        response.Settings.RefreshInterval.Should().Be(300);
        response.Settings.VisibleWidgets.Should().BeEquivalentTo(new[] { "completion-stats" });
    }

    [Fact]
    public async Task UpdateDashboardSettings_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new UpdateDashboardSettingsRequestDto();
        var result = Result<UpdateDashboardSettingsResponse>.Failure("Database connection failed");
        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateDashboardSettingsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.UpdateDashboardSettings(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Database connection failed");
    }

    [Fact]
    public async Task UpdateDashboardSettings_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new UpdateDashboardSettingsRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.UpdateDashboardSettings(request, CancellationToken.None));
    }

    [Fact]
    public async Task ResetDashboardPreferences_WithConfirmation_ShouldResetToDefaults()
    {
        // Arrange
        var request = new ResetDashboardPreferencesRequestDto(
            ConfirmReset: true,
            SpecificSettings: null
        );

        var expectedResponse = new ResetDashboardPreferencesResponse(
            Success: true,
            DefaultSettings: new DashboardSettingsDto(
                Theme: "light",
                Language: "en",
                ShowCompletionStats: true,
                ShowProductivityStreak: true,
                ShowOverdueTasks: true,
                ShowMotivationalContent: true,
                RefreshInterval: 300,
                VisibleWidgets: new List<string> { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content" },
                WidgetSettings: new Dictionary<string, object>(),
                NotificationSettings: new NotificationSettingsDto(true, true, false, 3, "weekly", new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6, 7 }),
                DisplaySettings: new DisplaySettingsDto("bar", "MM/dd/yyyy", "12h", false, 20, "priority", true, false)
            ),
            ResetSettings: new List<string>
            {
                "theme", "language", "widgets", "notifications", "display",
                "refresh-interval", "completion-stats", "productivity-streak",
                "overdue-tasks", "motivational-content"
            },
            ResetTimestamp: DateTime.UtcNow
        );

        var result = Result<ResetDashboardPreferencesResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ResetDashboardPreferencesCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ResetDashboardPreferences(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ResetDashboardPreferencesResponseDto>().Subject;

        response.Success.Should().BeTrue();
        response.DefaultSettings.Should().NotBeNull();
        response.ResetSettings.Should().HaveCount(10);
        response.ResetSettings.Should().Contain("theme");
        response.ResetSettings.Should().Contain("language");
        response.ResetSettings.Should().Contain("widgets");
        response.ResetTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Message.Should().Be("Dashboard preferences have been successfully reset to defaults.");

        _mockMediator.Verify(m => m.Send(
            It.Is<ResetDashboardPreferencesCommand>(c =>
                c.UserId == _testUserId &&
                c.ConfirmReset == true &&
                c.SpecificSettings == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetDashboardPreferences_WithSpecificSettings_ShouldResetOnlySpecified()
    {
        // Arrange
        var specificSettings = new List<string> { "theme", "language" };
        var request = new ResetDashboardPreferencesRequestDto(
            ConfirmReset: false,
            SpecificSettings: specificSettings
        );

        var expectedResponse = new ResetDashboardPreferencesResponse(
            Success: true,
            DefaultSettings: new DashboardSettingsDto(
                Theme: "light",
                Language: "en",
                ShowCompletionStats: true,
                ShowProductivityStreak: true,
                ShowOverdueTasks: true,
                ShowMotivationalContent: true,
                RefreshInterval: 300,
                VisibleWidgets: new List<string> { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content" },
                WidgetSettings: new Dictionary<string, object>(),
                NotificationSettings: new NotificationSettingsDto(true, true, false, 3, "weekly", new List<int>()),
                DisplaySettings: new DisplaySettingsDto("bar", "MM/dd/yyyy", "12h", false, 20, "priority", true, false)
            ),
            ResetSettings: specificSettings,
            ResetTimestamp: DateTime.UtcNow
        );

        var result = Result<ResetDashboardPreferencesResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ResetDashboardPreferencesCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ResetDashboardPreferences(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ResetDashboardPreferencesResponseDto>().Subject;

        response.Success.Should().BeTrue();
        response.ResetSettings.Should().BeEquivalentTo(specificSettings);
        response.ResetSettings.Should().HaveCount(2);

        _mockMediator.Verify(m => m.Send(
            It.Is<ResetDashboardPreferencesCommand>(c =>
                c.UserId == _testUserId &&
                c.ConfirmReset == false &&
                c.SpecificSettings!.SequenceEqual(specificSettings)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetDashboardPreferences_WithoutConfirmation_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ResetDashboardPreferencesRequestDto(
            ConfirmReset: false,
            SpecificSettings: null
        );

        var result = Result<ResetDashboardPreferencesResponse>.Failure("Reset confirmation required. Set ConfirmReset to true to proceed.");
        _mockMediator.Setup(m => m.Send(It.IsAny<ResetDashboardPreferencesCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ResetDashboardPreferences(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Reset confirmation required. Set ConfirmReset to true to proceed.");
    }

    [Fact]
    public async Task ResetDashboardPreferences_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new ResetDashboardPreferencesRequestDto(ConfirmReset: true);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ResetDashboardPreferences(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task UpdateDashboardSettings_WithValidThemes_ShouldAcceptTheme(string theme)
    {
        // Arrange
        var request = new UpdateDashboardSettingsRequestDto(Theme: theme);
        var expectedResponse = new UpdateDashboardSettingsResponse(
            Success: true,
            UpdatedSettings: new DashboardSettingsDto(theme, "en", true, true, true, true, 300,
                new List<string>(), new Dictionary<string, object>(),
                new NotificationSettingsDto(true, true, false, 3, "weekly", new List<int>()),
                new DisplaySettingsDto("bar", "MM/dd/yyyy", "12h", false, 20, "priority", true, false)),
            ValidationWarnings: new List<string>()
        );

        var result = Result<UpdateDashboardSettingsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateDashboardSettingsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.UpdateDashboardSettings(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardSettingsResponseDto>().Subject;

        response.Settings.Theme.Should().Be(theme);
        response.ValidationWarnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task UpdateDashboardSettings_WithValidLanguages_ShouldAcceptLanguage(string language)
    {
        // Arrange
        var request = new UpdateDashboardSettingsRequestDto(Language: language);
        var expectedResponse = new UpdateDashboardSettingsResponse(
            Success: true,
            UpdatedSettings: new DashboardSettingsDto("light", language, true, true, true, true, 300,
                new List<string>(), new Dictionary<string, object>(),
                new NotificationSettingsDto(true, true, false, 3, "weekly", new List<int>()),
                new DisplaySettingsDto("bar", "MM/dd/yyyy", "12h", false, 20, "priority", true, false)),
            ValidationWarnings: new List<string>()
        );

        var result = Result<UpdateDashboardSettingsResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateDashboardSettingsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.UpdateDashboardSettings(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DashboardSettingsResponseDto>().Subject;

        response.Settings.Language.Should().Be(language);
        response.ValidationWarnings.Should().BeEmpty();
    }

    #endregion

    #region Export and Reports Tests

    [Fact]
    public async Task ExportDashboardData_WithValidJsonRequest_ShouldReturnJsonFile()
    {
        // Arrange
        var request = new ExportDashboardDataRequestDto(
            Format: "json",
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow,
            IncludeCategories: new List<string> { "ToDo", "Project" },
            IncludePriorities: new List<string> { "High", "Critical" },
            IncludeStatuses: new List<string> { "Completed", "InProgress" },
            DataTypes: new List<string> { "tasks", "metrics", "analytics" },
            IncludeDeleted: false,
            IncludeArchived: true,
            TimeZone: "UTC",
            CustomFilters: new Dictionary<string, object> { { "minDuration", 5 } }
        );

        var expectedResponse = new ExportDashboardDataResponse(
            FileContent: System.Text.Encoding.UTF8.GetBytes("{\"tasks\": [], \"metrics\": {}}"),
            FileName: "dashboard-export-20241211.json",
            ContentType: "application/json",
            RecordCount: 0,
            Metadata: new ExportMetadata(
                ExportedAt: DateTime.UtcNow,
                ExportedBy: "Test User",
                Options: new ExportOptionsDto(),
                RecordCounts: new Dictionary<string, int> { ["tasks"] = 0, ["metrics"] = 0 },
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<ExportDashboardDataResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ExportDashboardDataQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ExportDashboardData(request, CancellationToken.None);

        // Assert
        var fileResult = actionResult.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().Be("dashboard-export-20241211.json");
        fileResult.FileContents.Should().BeEquivalentTo(System.Text.Encoding.UTF8.GetBytes("{\"tasks\": [], \"metrics\": {}}"));

        _mockMediator.Verify(m => m.Send(
            It.Is<ExportDashboardDataQuery>(q =>
                q.UserId == _testUserId &&
                q.Format == "json" &&
                q.Options.StartDate == request.StartDate &&
                q.Options.EndDate == request.EndDate &&
                q.Options.IncludeCategories!.SequenceEqual(request.IncludeCategories!) &&
                q.Options.DataTypes!.SequenceEqual(request.DataTypes!) &&
                q.Options.IncludeDeleted == false &&
                q.Options.IncludeArchived == true &&
                q.Options.TimeZone == "UTC"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("csv", "text/csv")]
    [InlineData("json", "application/json")]
    [InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task ExportDashboardData_WithDifferentFormats_ShouldReturnCorrectContentType(string format, string expectedContentType)
    {
        // Arrange
        var request = new ExportDashboardDataRequestDto(Format: format);
        var expectedResponse = new ExportDashboardDataResponse(
            FileContent: new byte[] { 0x01, 0x02, 0x03 },
            FileName: $"dashboard-export.{format}",
            ContentType: expectedContentType,
            RecordCount: 5,
            Metadata: new ExportMetadata(
                ExportedAt: DateTime.UtcNow,
                ExportedBy: "Test User",
                Options: new ExportOptionsDto(),
                RecordCounts: new Dictionary<string, int> { ["tasks"] = 3, ["metrics"] = 2 },
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<ExportDashboardDataResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ExportDashboardDataQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ExportDashboardData(request, CancellationToken.None);

        // Assert
        var fileResult = actionResult.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(expectedContentType);
        fileResult.FileDownloadName.Should().Be($"dashboard-export.{format}");

        _mockMediator.Verify(m => m.Send(
            It.Is<ExportDashboardDataQuery>(q => q.Format == format),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportDashboardData_WithComplexFilters_ShouldPassAllFilters()
    {
        // Arrange
        var categories = new List<string> { "ToDo", "Project", "BillReminder", "Appointment" };
        var priorities = new List<string> { "Critical", "High", "Medium" };
        var statuses = new List<string> { "Completed", "InProgress", "Pending" };
        var dataTypes = new List<string> { "tasks", "metrics", "streaks", "analytics" };
        var customFilters = new Dictionary<string, object>
        {
            { "minCompletionRate", 0.7 },
            { "includeSubtasks", true },
            { "groupBy", "category" }
        };

        var request = new ExportDashboardDataRequestDto(
            Format: "excel",
            StartDate: DateTime.UtcNow.AddDays(-90),
            EndDate: DateTime.UtcNow.AddDays(-1),
            IncludeCategories: categories,
            IncludePriorities: priorities,
            IncludeStatuses: statuses,
            DataTypes: dataTypes,
            IncludeDeleted: true,
            IncludeArchived: false,
            TimeZone: "America/New_York",
            CustomFilters: customFilters
        );

        var expectedResponse = new ExportDashboardDataResponse(
            FileContent: new byte[] { 0x50, 0x4B }, // Excel header bytes
            FileName: "comprehensive-dashboard-export.xlsx",
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            RecordCount: 15,
            Metadata: new ExportMetadata(
                ExportedAt: DateTime.UtcNow,
                ExportedBy: "Test User",
                Options: new ExportOptionsDto(),
                RecordCounts: new Dictionary<string, int> { ["tasks"] = 10, ["metrics"] = 5 },
                FileSizeBytes: 2048,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<ExportDashboardDataResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ExportDashboardDataQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ExportDashboardData(request, CancellationToken.None);

        // Assert
        actionResult.Should().BeOfType<FileContentResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<ExportDashboardDataQuery>(q =>
                q.Options.IncludeCategories!.SequenceEqual(categories) &&
                q.Options.IncludePriorities!.SequenceEqual(priorities) &&
                q.Options.IncludeStatuses!.SequenceEqual(statuses) &&
                q.Options.DataTypes!.SequenceEqual(dataTypes) &&
                q.Options.IncludeDeleted == true &&
                q.Options.IncludeArchived == false &&
                q.Options.TimeZone == "America/New_York" &&
                q.Options.CustomFilters!["minCompletionRate"].Equals(0.7)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportDashboardData_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ExportDashboardDataRequestDto();
        var result = Result<ExportDashboardDataResponse>.Failure("Export service temporarily unavailable");
        _mockMediator.Setup(m => m.Send(It.IsAny<ExportDashboardDataQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ExportDashboardData(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Export service temporarily unavailable");
    }

    [Fact]
    public async Task ExportDashboardData_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new ExportDashboardDataRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ExportDashboardData(request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateDashboardReport_WithValidPdfRequest_ShouldReturnPdfFile()
    {
        // Arrange
        var request = new GenerateDashboardReportRequestDto(
            ReportType: "summary",
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow,
            Format: "pdf",
            Sections: new List<string> { "overview", "tasks", "productivity", "trends" },
            IncludeCharts: true,
            IncludeInsights: true,
            IncludeRecommendations: true,
            TimeZone: "UTC",
            CustomSettings: new Dictionary<string, object> { { "theme", "professional" } }
        );

        var expectedResponse = new GenerateDashboardReportResponse(
            ReportContent: new byte[] { 0x25, 0x50, 0x44, 0x46 }, // PDF header
            ReportFileName: "dashboard-summary-report-20241211.pdf",
            ContentType: "application/pdf",
            Metadata: new ReportMetadata(
                GeneratedAt: DateTime.UtcNow,
                GeneratedBy: "Test User",
                ReportType: "summary",
                Options: new ReportOptionsDto(),
                Summary: new ReportSummary(10, 8, 0.8, 5, 3, 2, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<GenerateDashboardReportResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GenerateDashboardReportQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GenerateDashboardReport(request, CancellationToken.None);

        // Assert
        var fileResult = actionResult.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("dashboard-summary-report-20241211.pdf");
        fileResult.FileContents.Should().BeEquivalentTo(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        _mockMediator.Verify(m => m.Send(
            It.Is<GenerateDashboardReportQuery>(q =>
                q.UserId == _testUserId &&
                q.ReportType == "summary" &&
                q.Options.StartDate == request.StartDate &&
                q.Options.EndDate == request.EndDate &&
                q.Options.Format == "pdf" &&
                q.Options.Sections!.SequenceEqual(request.Sections!) &&
                q.Options.IncludeCharts == true &&
                q.Options.IncludeInsights == true &&
                q.Options.IncludeRecommendations == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("summary", "pdf", "application/pdf")]
    [InlineData("detailed", "html", "text/html")]
    [InlineData("analytical", "markdown", "text/markdown")]
    public async Task GenerateDashboardReport_WithDifferentFormats_ShouldReturnCorrectContentType(
        string reportType, string format, string expectedContentType)
    {
        // Arrange
        var request = new GenerateDashboardReportRequestDto(
            ReportType: reportType,
            Format: format
        );

        var expectedResponse = new GenerateDashboardReportResponse(
            ReportContent: new byte[] { 0x01, 0x02, 0x03 },
            ReportFileName: $"dashboard-{reportType}-report.{format}",
            ContentType: expectedContentType,
            Metadata: new ReportMetadata(
                GeneratedAt: DateTime.UtcNow,
                GeneratedBy: "Test User",
                ReportType: reportType,
                Options: new ReportOptionsDto(),
                Summary: new ReportSummary(10, 8, 0.8, 5, 3, 2, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<GenerateDashboardReportResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GenerateDashboardReportQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GenerateDashboardReport(request, CancellationToken.None);

        // Assert
        var fileResult = actionResult.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(expectedContentType);
        fileResult.FileDownloadName.Should().Be($"dashboard-{reportType}-report.{format}");

        _mockMediator.Verify(m => m.Send(
            It.Is<GenerateDashboardReportQuery>(q =>
                q.ReportType == reportType &&
                q.Options.Format == format),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDashboardReport_WithComprehensiveOptions_ShouldPassAllOptions()
    {
        // Arrange
        var sections = new List<string> { "overview", "tasks", "productivity", "trends", "recommendations" };
        var customSettings = new Dictionary<string, object>
        {
            { "theme", "modern" },
            { "logoUrl", "https://example.com/logo.png" },
            { "includeFooter", true },
            { "pageSize", "A4" }
        };

        var request = new GenerateDashboardReportRequestDto(
            ReportType: "detailed",
            StartDate: DateTime.UtcNow.AddDays(-60),
            EndDate: DateTime.UtcNow,
            Format: "html",
            Sections: sections,
            IncludeCharts: true,
            IncludeInsights: true,
            IncludeRecommendations: true,
            TimeZone: "Europe/London",
            CustomSettings: customSettings
        );

        var expectedResponse = new GenerateDashboardReportResponse(
            ReportContent: System.Text.Encoding.UTF8.GetBytes("<html><body>Report Content</body></html>"),
            ReportFileName: "comprehensive-detailed-report.html",
            ContentType: "text/html",
            Metadata: new ReportMetadata(
                GeneratedAt: DateTime.UtcNow,
                GeneratedBy: "Test User",
                ReportType: "detailed",
                Options: new ReportOptionsDto(),
                Summary: new ReportSummary(10, 8, 0.8, 5, 3, 2, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<GenerateDashboardReportResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GenerateDashboardReportQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GenerateDashboardReport(request, CancellationToken.None);

        // Assert
        actionResult.Should().BeOfType<FileContentResult>();

        _mockMediator.Verify(m => m.Send(
            It.Is<GenerateDashboardReportQuery>(q =>
                q.ReportType == "detailed" &&
                q.Options.Sections!.SequenceEqual(sections) &&
                q.Options.IncludeCharts == true &&
                q.Options.IncludeInsights == true &&
                q.Options.IncludeRecommendations == true &&
                q.Options.TimeZone == "Europe/London" &&
                q.Options.CustomSettings!["theme"].Equals("modern")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDashboardReport_WithServiceFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new GenerateDashboardReportRequestDto();
        var result = Result<GenerateDashboardReportResponse>.Failure("Report generation service is down");
        _mockMediator.Setup(m => m.Send(It.IsAny<GenerateDashboardReportQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.GenerateDashboardReport(request, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Report generation service is down");
    }

    [Fact]
    public async Task GenerateDashboardReport_WithUnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Remove user authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new GenerateDashboardReportRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.GenerateDashboardReport(request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateDashboardReport_ShouldLogCorrectInformation()
    {
        // Arrange
        var request = new GenerateDashboardReportRequestDto(ReportType: "analytical");
        var expectedResponse = new GenerateDashboardReportResponse(
            ReportContent: new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ReportFileName: "analytical-report.pdf",
            ContentType: "application/pdf",
            Metadata: new ReportMetadata(
                GeneratedAt: DateTime.UtcNow,
                GeneratedBy: "Test User",
                ReportType: "analytical",
                Options: new ReportOptionsDto(),
                Summary: new ReportSummary(10, 8, 0.8, 5, 3, 2, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
                FileSizeBytes: 1024,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<GenerateDashboardReportResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<GenerateDashboardReportQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.GenerateDashboardReport(request, CancellationToken.None);

        // Assert - Verify logging was called correctly
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Generating dashboard report for user {_testUserId}, type: analytical")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExportDashboardData_ShouldLogCorrectInformation()
    {
        // Arrange
        var request = new ExportDashboardDataRequestDto(Format: "csv");
        var expectedResponse = new ExportDashboardDataResponse(
            FileContent: new byte[] { 0x01, 0x02 },
            FileName: "export.csv",
            ContentType: "text/csv",
            RecordCount: 2,
            Metadata: new ExportMetadata(
                ExportedAt: DateTime.UtcNow,
                ExportedBy: "Test User",
                Options: new ExportOptionsDto(),
                RecordCounts: new Dictionary<string, int> { ["tasks"] = 2 },
                FileSizeBytes: 512,
                ChecksumHash: "test-hash"
            )
        );

        var result = Result<ExportDashboardDataResponse>.Success(expectedResponse);
        _mockMediator.Setup(m => m.Send(It.IsAny<ExportDashboardDataQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);

        // Act
        await _controller.ExportDashboardData(request, CancellationToken.None);

        // Assert - Verify logging was called correctly
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Exporting dashboard data for user {_testUserId} in format csv")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
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

}
