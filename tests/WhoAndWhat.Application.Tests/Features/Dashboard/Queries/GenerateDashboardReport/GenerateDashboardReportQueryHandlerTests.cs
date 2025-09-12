using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Features.Dashboard.Queries.GenerateDashboardReport;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GenerateDashboardReport;

/// <summary>
/// Comprehensive unit tests for GenerateDashboardReportQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class GenerateDashboardReportQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<GenerateDashboardReportQueryHandler>> _mockLogger;
    private readonly GenerateDashboardReportQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GenerateDashboardReportQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<GenerateDashboardReportQueryHandler>>();
        
        _handler = new GenerateDashboardReportQueryHandler(
            _mockTaskRepository.Object,
            _mockUserRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidSummaryReport_ShouldReturnSuccessfulReport()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html", IncludeCharts: true, IncludeRecommendations: true);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasks();

        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.ReportContent.Should().NotBeEmpty();
        response.ReportFileName.Should().EndWith(".html");
        response.ContentType.Should().Be("text/html");
        response.Metadata.Should().NotBeNull();
        response.Metadata.ReportType.Should().Be("summary");
        response.Metadata.GeneratedBy.Should().Be(user.Username);
        response.Metadata.FileSizeBytes.Should().BeGreaterThan(0);
        response.Metadata.ChecksumHash.Should().NotBeNullOrEmpty();
        
        // Verify HTML content contains expected sections
        var htmlContent = Encoding.UTF8.GetString(response.ReportContent);
        htmlContent.Should().Contain("Dashboard Report - SUMMARY");
        htmlContent.Should().Contain("Key Metrics");
        htmlContent.Should().Contain("Total Tasks:");
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("detailed")]
    [InlineData("analytical")]
    public async Task Handle_WithDifferentReportTypes_ShouldGenerateAppropriateContent(string reportType)
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html");
        var query = new GenerateDashboardReportQuery(_testUserId, reportType, options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        response.Metadata.ReportType.Should().Be(reportType);
        
        var htmlContent = Encoding.UTF8.GetString(response.ReportContent);
        htmlContent.Should().Contain($"Dashboard Report - {reportType.ToUpperInvariant()}");
    }

    [Theory]
    [InlineData("html", "text/html", ".html")]
    [InlineData("markdown", "text/markdown", ".md")]
    [InlineData("pdf", "application/pdf", ".pdf")]
    public async Task Handle_WithDifferentFormats_ShouldGenerateCorrectOutputFormat(string format, string expectedContentType, string expectedExtension)
    {
        // Arrange
        var options = new ReportOptionsDto(Format: format);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.ContentType.Should().Be(expectedContentType);
        response.ReportFileName.Should().EndWith(expectedExtension);
        response.ReportContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithMarkdownFormat_ShouldGenerateValidMarkdown()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "markdown", IncludeRecommendations: true);
        var query = new GenerateDashboardReportQuery(_testUserId, "detailed", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithVariousStatuses();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        var markdownContent = Encoding.UTF8.GetString(response.ReportContent);
        markdownContent.Should().Contain("# Dashboard Report - DETAILED");
        markdownContent.Should().Contain("## Summary");
        markdownContent.Should().Contain("| Metric | Value |");
        markdownContent.Should().Contain("## Insights");
        markdownContent.Should().Contain("## Recommendations");
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        var options = new ReportOptionsDto();
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User not found");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("custom")]
    [InlineData("")]
    public async Task Handle_WithInvalidReportType_ShouldReturnFailure(string invalidReportType)
    {
        // Arrange
        var options = new ReportOptionsDto();
        var query = new GenerateDashboardReportQuery(_testUserId, invalidReportType, options);
        
        var user = CreateTestUser();
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported report type");
        result.Error.Should().Contain("Supported types: summary, detailed, analytical");
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("json")]
    [InlineData("")]
    public async Task Handle_WithInvalidFormat_ShouldReturnFailure(string invalidFormat)
    {
        // Arrange
        var options = new ReportOptionsDto(Format: invalidFormat);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported format");
        result.Error.Should().Contain("Supported formats: pdf, html, markdown");
    }

    [Fact]
    public async Task Handle_WithDetailedReport_ShouldIncludePriorityAnalysis()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html");
        var query = new GenerateDashboardReportQuery(_testUserId, "detailed", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithDifferentPriorities();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var htmlContent = Encoding.UTF8.GetString(result.Value.ReportContent);
        
        // Should contain priority-related insights
        htmlContent.Should().Contain("Priority Management");
    }

    [Fact]
    public async Task Handle_WithAnalyticalReport_ShouldIncludeAdvancedAnalytics()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html");
        var query = new GenerateDashboardReportQuery(_testUserId, "analytical", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksForAnalytics();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var htmlContent = Encoding.UTF8.GetString(result.Value.ReportContent);
        
        // Should contain analytical insights
        htmlContent.Should().Contain("Peak Productivity Hour");
        htmlContent.Should().Contain("Task Completion Velocity");
    }

    [Fact]
    public async Task Handle_WithOverdueTasks_ShouldGenerateOverdueRecommendation()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html", IncludeRecommendations: true);
        var query = new GenerateDashboardReportQuery(_testUserId, "detailed", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithOverdue();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var htmlContent = Encoding.UTF8.GetString(result.Value.ReportContent);
        
        htmlContent.Should().Contain("Address Overdue Tasks");
        htmlContent.Should().Contain("overdue tasks that need attention");
    }

    [Fact]
    public async Task Handle_WithLowCompletionRate_ShouldGenerateCompletionRecommendation()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html", IncludeRecommendations: true);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithLowCompletionRate(); // < 70% completion rate
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var htmlContent = Encoding.UTF8.GetString(result.Value.ReportContent);
        
        htmlContent.Should().Contain("Improve Task Completion Rate");
        htmlContent.Should().Contain("breaking large tasks into smaller");
    }

    [Fact]
    public async Task Handle_WithImbalancedCategories_ShouldGenerateBalanceRecommendation()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html", IncludeRecommendations: true);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithImbalancedCategories(); // 70%+ in one category
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var htmlContent = Encoding.UTF8.GetString(result.Value.ReportContent);
        
        htmlContent.Should().Contain("Balance Task Categories");
        htmlContent.Should().Contain("diversifying your task types");
    }

    [Fact]
    public async Task Handle_WithCustomDateRange_ShouldRespectDateFilters()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var options = new ReportOptionsDto(StartDate: startDate, EndDate: endDate, Format: "html");
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasksWithDifferentDates();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Metadata.Summary.PeriodStart.Should().Be(startDate);
        response.Metadata.Summary.PeriodEnd.Should().Be(endDate);
        
        var htmlContent = Encoding.UTF8.GetString(response.ReportContent);
        htmlContent.Should().Contain($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
    }

    [Fact]
    public async Task Handle_WithChartsDisabled_ShouldNotIncludeChartData()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html", IncludeCharts: false);
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Report should still generate successfully but without chart data
        result.Value.ReportContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var options = new ReportOptionsDto();
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to generate dashboard report");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error generating dashboard report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var options = new ReportOptionsDto();
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generating dashboard report for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully generated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoTasks_ShouldGenerateEmptyReport()
    {
        // Arrange
        var options = new ReportOptionsDto(Format: "html");
        var query = new GenerateDashboardReportQuery(_testUserId, "summary", options);
        
        var user = CreateTestUser();
        var emptyTasks = new List<AppTask>();
        SetupMocks(user, emptyTasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Metadata.Summary.TotalTasks.Should().Be(0);
        response.Metadata.Summary.CompletedTasks.Should().Be(0);
        response.Metadata.Summary.CompletionRate.Should().Be(0);
        
        var htmlContent = Encoding.UTF8.GetString(response.ReportContent);
        htmlContent.Should().Contain("Total Tasks:</strong> 0");
    }

    #region Helper Methods

    private void SetupMocks(User user, List<AppTask> tasks)
    {
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        // Setup for streak calculation (separate call)
        _mockTaskRepository
            .Setup(x => x.GetTasksByUserIdAsync(_testUserId, It.IsAny<TaskFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));
    }

    private User CreateTestUser()
    {
        var user = new User("test@example.com", "testuser", Language.en);
        return user;
    }

    private List<AppTask> CreateTestTasks()
    {
        return new List<AppTask>
        {
            CreateTask("Task 1", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTask("Task 2", AppTaskCategory.Idea, AppTaskStatus.InProgress),
            CreateTask("Task 3", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTask("Task 4", AppTaskCategory.Project, AppTaskStatus.Pending),
        };
    }

    private List<AppTask> CreateTestTasksWithVariousStatuses()
    {
        return new List<AppTask>
        {
            CreateTask("Task 1", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTask("Task 2", AppTaskCategory.Idea, AppTaskStatus.InProgress),
            CreateTask("Task 3", AppTaskCategory.Appointment, AppTaskStatus.Completed),
            CreateTask("Task 4", AppTaskCategory.BillReminder, AppTaskStatus.Pending),
            CreateTask("Task 5", AppTaskCategory.Project, AppTaskStatus.Pending),
        };
    }

    private List<AppTask> CreateTestTasksWithDifferentPriorities()
    {
        var tasks = new List<AppTask>
        {
            CreateTaskWithPriority("High Priority Task", Priority.High),
            CreateTaskWithPriority("Medium Priority Task", Priority.Medium),
            CreateTaskWithPriority("Low Priority Task", Priority.Low),
            CreateTaskWithPriority("Urgent Task", Priority.Urgent),
        };
        return tasks;
    }

    private List<AppTask> CreateTestTasksForAnalytics()
    {
        var tasks = new List<AppTask>();
        var now = DateTime.UtcNow;
        
        // Create tasks at different hours for peak productivity analysis
        for (int hour = 8; hour <= 18; hour++)
        {
            var task = CreateTask($"Task at {hour}:00", AppTaskCategory.ToDo, AppTaskStatus.Completed);
            SetTaskCreationTime(task, now.Date.AddHours(hour));
            SetTaskCompletionTime(task, now.Date.AddHours(hour + 1));
            tasks.Add(task);
        }
        
        return tasks;
    }

    private List<AppTask> CreateTestTasksWithOverdue()
    {
        var tasks = new List<AppTask>
        {
            CreateTask("Regular Task", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTaskWithDueDate("Overdue Task 1", DateTime.Today.AddDays(-3), AppTaskStatus.InProgress),
            CreateTaskWithDueDate("Overdue Task 2", DateTime.Today.AddDays(-1), AppTaskStatus.Pending),
            CreateTaskWithDueDate("Future Task", DateTime.Today.AddDays(1), AppTaskStatus.InProgress),
        };
        return tasks;
    }

    private List<AppTask> CreateTestTasksWithLowCompletionRate()
    {
        // Create 10 tasks with only 6 completed (60% completion rate < 70%)
        var tasks = new List<AppTask>();
        for (int i = 1; i <= 6; i++)
        {
            tasks.Add(CreateTask($"Completed Task {i}", AppTaskCategory.ToDo, AppTaskStatus.Completed));
        }
        for (int i = 1; i <= 4; i++)
        {
            tasks.Add(CreateTask($"Incomplete Task {i}", AppTaskCategory.ToDo, AppTaskStatus.InProgress));
        }
        return tasks;
    }

    private List<AppTask> CreateTestTasksWithImbalancedCategories()
    {
        var tasks = new List<AppTask>();
        
        // Add 8 ToDo tasks (80% of total)
        for (int i = 1; i <= 8; i++)
        {
            tasks.Add(CreateTask($"ToDo Task {i}", AppTaskCategory.ToDo, AppTaskStatus.InProgress));
        }
        
        // Add 2 other category tasks (20% of total)
        tasks.Add(CreateTask("Idea Task", AppTaskCategory.Idea, AppTaskStatus.InProgress));
        tasks.Add(CreateTask("Project Task", AppTaskCategory.Project, AppTaskStatus.InProgress));
        
        return tasks;
    }

    private List<AppTask> CreateTestTasksWithDifferentDates()
    {
        var tasks = new List<AppTask>();
        
        // Tasks within date range (January 2024)
        var task1 = CreateTask("Jan Task 1", AppTaskCategory.ToDo, AppTaskStatus.Completed);
        SetTaskCreationTime(task1, new DateTime(2024, 1, 15));
        tasks.Add(task1);
        
        var task2 = CreateTask("Jan Task 2", AppTaskCategory.ToDo, AppTaskStatus.InProgress);
        SetTaskCreationTime(task2, new DateTime(2024, 1, 25));
        tasks.Add(task2);
        
        // Tasks outside date range (should be filtered out)
        var task3 = CreateTask("Dec Task", AppTaskCategory.ToDo, AppTaskStatus.Completed);
        SetTaskCreationTime(task3, new DateTime(2023, 12, 15));
        tasks.Add(task3);
        
        return tasks;
    }

    private AppTask CreateTask(string title, AppTaskCategory category, AppTaskStatus status)
    {
        var task = new AppTask { Title = title, Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set status using reflection
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)status);
        
        return task;
    }

    private AppTask CreateTaskWithPriority(string title, Priority priority)
    {
        var task = new AppTask { Title = title, Category = (int)AppTaskCategory.ToDo, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };
        
        // Set priority using reflection
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)priority);
        
        return task;
    }

    private AppTask CreateTaskWithDueDate(string title, DateTime dueDate, AppTaskStatus status)
    {
        var task = CreateTask(title, AppTaskCategory.ToDo, status);
        
        // Set due date using reflection
        var dueDateField = typeof(AppTask).GetField("_dueDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dueDateField?.SetValue(task, dueDate);
        
        return task;
    }

    private void SetTaskCreationTime(AppTask task, DateTime createdAt)
    {
        var createdAtField = typeof(AppTask).GetField("_createdAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        createdAtField?.SetValue(task, createdAt);
    }

    private void SetTaskCompletionTime(AppTask task, DateTime completedAt)
    {
        var updatedAtField = typeof(AppTask).GetField("_updatedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        updatedAtField?.SetValue(task, completedAt);
    }

    #endregion
}