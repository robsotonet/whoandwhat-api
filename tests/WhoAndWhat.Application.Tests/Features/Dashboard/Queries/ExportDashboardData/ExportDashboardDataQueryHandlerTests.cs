using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Features.Dashboard.Queries.ExportDashboardData;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.ExportDashboardData;

/// <summary>
/// Comprehensive unit tests for ExportDashboardDataQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class ExportDashboardDataQueryHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<ExportDashboardDataQueryHandler>> _mockLogger;
    private readonly ExportDashboardDataQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ExportDashboardDataQueryHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<ExportDashboardDataQueryHandler>>();

        _handler = new ExportDashboardDataQueryHandler(
            _mockTaskRepository.Object,
            _mockUserRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCsvExport_ShouldReturnSuccessfulExport()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasks();

        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        response.FileContent.Should().NotBeEmpty();
        response.FileName.Should().EndWith(".csv");
        response.ContentType.Should().Be("text/csv");
        response.RecordCount.Should().BeGreaterThan(0);
        response.Metadata.Should().NotBeNull();
        response.Metadata.ExportedBy.Should().Be(user.Username);
        response.Metadata.FileSizeBytes.Should().BeGreaterThan(0);
        response.Metadata.ChecksumHash.Should().NotBeNullOrEmpty();

        // Verify CSV content
        var csvContent = Encoding.UTF8.GetString(response.FileContent);
        csvContent.Should().Contain("Type,Date,Title,Category,Priority,Status,DueDate,CompletedAt,Description");
        csvContent.Should().Contain("Task,");
    }

    [Theory]
    [InlineData("csv", "text/csv", ".csv")]
    [InlineData("json", "application/json", ".json")]
    [InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
    public async Task Handle_WithDifferentFormats_ShouldGenerateCorrectOutputFormat(string format, string expectedContentType, string expectedExtension)
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, format, options);

        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        response.ContentType.Should().Be(expectedContentType);
        response.FileName.Should().EndWith(expectedExtension);
        response.FileContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithJsonFormat_ShouldGenerateValidJson()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksWithVariousData();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);

        // Should be valid JSON
        var jsonDocument = JsonDocument.Parse(jsonContent);

        // Verify structure
        jsonDocument.RootElement.TryGetProperty("exportInfo", out var exportInfo).Should().BeTrue();
        jsonDocument.RootElement.TryGetProperty("tasks", out var tasksJson).Should().BeTrue();
        jsonDocument.RootElement.TryGetProperty("metrics", out var metricsJson).Should().BeTrue();

        // Verify tasks contain expected fields
        var firstTask = tasksJson.EnumerateArray().FirstOrDefault();
        firstTask.TryGetProperty("title", out var title).Should().BeTrue();
        firstTask.TryGetProperty("category", out var category).Should().BeTrue();
        firstTask.TryGetProperty("priority", out var priority).Should().BeTrue();
        firstTask.TryGetProperty("status", out var status).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

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
    [InlineData("xml")]
    [InlineData("pdf")]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task Handle_WithInvalidFormat_ShouldReturnFailure(string invalidFormat)
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, invalidFormat, options);

        var user = CreateTestUser();
        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported format");
        result.Error.Should().Contain("Supported formats: csv, json, excel");
    }

    [Fact]
    public async Task Handle_WithSpecificDataTypes_ShouldExportOnlyRequestedData()
    {
        // Arrange
        var options = new ExportOptionsDto(DataTypes: new List<string> { "tasks", "metrics" });
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        response.Metadata.RecordCounts.Should().ContainKey("tasks");
        response.Metadata.RecordCounts.Should().ContainKey("metrics");
        response.Metadata.RecordCounts.Should().NotContainKey("streaks");
        response.Metadata.RecordCounts.Should().NotContainKey("analytics");

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);
        var jsonDocument = JsonDocument.Parse(jsonContent);

        jsonDocument.RootElement.TryGetProperty("tasks", out var tasksJson).Should().BeTrue();
        jsonDocument.RootElement.TryGetProperty("metrics", out var metricsJson).Should().BeTrue();

        // Streaks and analytics should be empty arrays since not requested
        jsonDocument.RootElement.TryGetProperty("streaks", out var streaksJson).Should().BeTrue();
        streaksJson.EnumerateArray().Should().BeEmpty();

        jsonDocument.RootElement.TryGetProperty("analytics", out var analyticsJson).Should().BeTrue();
        analyticsJson.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_ShouldRespectDateFilters()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var options = new ExportOptionsDto(StartDate: startDate, EndDate: endDate);
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksWithDifferentDates();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        response.Metadata.Options.StartDate.Should().Be(startDate);
        response.Metadata.Options.EndDate.Should().Be(endDate);

        // Verify that task repository was called with correct filter
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(
                _testUserId,
                It.Is<TaskFilter>(f => f.CreatedAfter == startDate && f.CreatedBefore == endDate),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ShouldRespectCategoryFilters()
    {
        // Arrange
        var options = new ExportOptionsDto(IncludeCategories: new List<string> { "ToDo", "Project" });
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksWithDifferentCategories();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that task repository was called with category filter
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(
                _testUserId,
                It.Is<TaskFilter>(f => f.Categories != null && f.Categories.Any()),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithIncludeDeleted_ShouldIncludeDeletedTasks()
    {
        // Arrange
        var options = new ExportOptionsDto(IncludeDeleted: true);
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksIncludingDeleted();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that task repository was called with IncludeDeleted flag
        _mockTaskRepository.Verify(
            x => x.GetTasksByUserIdAsync(
                _testUserId,
                It.Is<TaskFilter>(f => f.IncludeDeleted == true),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithTasksData_ShouldCalculateCorrectMetrics()
    {
        // Arrange
        var options = new ExportOptionsDto(DataTypes: new List<string> { "tasks", "metrics" });
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksForMetricsCalculation();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);
        var jsonDocument = JsonDocument.Parse(jsonContent);

        jsonDocument.RootElement.TryGetProperty("metrics", out var metricsJson).Should().BeTrue();

        metricsJson.TryGetProperty("totalTasks", out var totalTasks).Should().BeTrue();
        totalTasks.GetInt32().Should().Be(5); // As defined in test data

        metricsJson.TryGetProperty("completedTasks", out var completedTasks).Should().BeTrue();
        completedTasks.GetInt32().Should().Be(3); // As defined in test data

        metricsJson.TryGetProperty("completionRate", out var completionRate).Should().BeTrue();
        completionRate.GetDouble().Should().Be(60.0); // 3/5 * 100
    }

    [Fact]
    public async Task Handle_WithStreaksData_ShouldCalculateProductivityStreaks()
    {
        // Arrange
        var options = new ExportOptionsDto(DataTypes: new List<string> { "streaks" });
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksWithStreaks();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);
        var jsonDocument = JsonDocument.Parse(jsonContent);

        jsonDocument.RootElement.TryGetProperty("streaks", out var streaksJson).Should().BeTrue();

        var streaksArray = streaksJson.EnumerateArray();
        streaksArray.Should().NotBeEmpty();

        var firstStreak = streaksArray.FirstOrDefault();
        firstStreak.TryGetProperty("duration", out var duration).Should().BeTrue();
        duration.GetInt32().Should().BeGreaterThan(0);

        firstStreak.TryGetProperty("tasksCompleted", out var tasksCompleted).Should().BeTrue();
        tasksCompleted.GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithAnalyticsData_ShouldCalculateDailyAndWeeklyStats()
    {
        // Arrange
        var options = new ExportOptionsDto(DataTypes: new List<string> { "analytics" });
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksForAnalytics();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);
        var jsonDocument = JsonDocument.Parse(jsonContent);

        jsonDocument.RootElement.TryGetProperty("analytics", out var analyticsJson).Should().BeTrue();

        var analyticsArray = analyticsJson.EnumerateArray();
        analyticsArray.Should().NotBeEmpty();

        // Should have daily productivity entries
        var dailyEntries = analyticsArray.Where(a => a.GetProperty("metric").GetString() == "DailyProductivity");
        dailyEntries.Should().NotBeEmpty();

        // Should have weekly trend entries
        var weeklyEntries = analyticsArray.Where(a => a.GetProperty("metric").GetString() == "WeeklyTrend");
        weeklyEntries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithCsvSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

        var user = CreateTestUser();
        var tasks = CreateTestTasksWithSpecialCharacters();
        SetupMocks(user, tasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        var csvContent = Encoding.UTF8.GetString(response.FileContent);

        // Should properly escape quotes and commas
        csvContent.Should().Contain("\"Task with, comma\"");
        csvContent.Should().Contain("\"Task with \"\"quotes\"\"\"");
        csvContent.Should().Contain("\"Task with\nnewline\"");
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

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
        result.Error.Should().Contain("Failed to export dashboard data");
        result.Error.Should().Contain("Database connection failed");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error exporting dashboard data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "csv", options);

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exporting dashboard data for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully exported")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoTasks_ShouldGenerateEmptyExport()
    {
        // Arrange
        var options = new ExportOptionsDto();
        var query = new ExportDashboardDataQuery(_testUserId, "json", options);

        var user = CreateTestUser();
        var emptyTasks = new List<AppTask>();
        SetupMocks(user, emptyTasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;

        response.RecordCount.Should().Be(1); // Only metrics record
        response.Metadata.RecordCounts["tasks"].Should().Be(0);
        response.Metadata.RecordCounts["metrics"].Should().Be(1);

        var jsonContent = Encoding.UTF8.GetString(response.FileContent);
        var jsonDocument = JsonDocument.Parse(jsonContent);

        jsonDocument.RootElement.TryGetProperty("tasks", out var tasksJson).Should().BeTrue();
        tasksJson.EnumerateArray().Should().BeEmpty();
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

    private List<AppTask> CreateTestTasksWithVariousData()
    {
        return new List<AppTask>
        {
            CreateTask("Task 1", AppTaskCategory.ToDo, AppTaskStatus.Completed, Priority.High),
            CreateTask("Task 2", AppTaskCategory.Idea, AppTaskStatus.InProgress, Priority.Medium),
            CreateTask("Task 3", AppTaskCategory.Appointment, AppTaskStatus.Completed, Priority.Low),
            CreateTask("Task 4", AppTaskCategory.BillReminder, AppTaskStatus.Pending, Priority.Urgent),
        };
    }

    private List<AppTask> CreateTestTasksWithDifferentCategories()
    {
        return new List<AppTask>
        {
            CreateTask("ToDo Task 1", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateTask("ToDo Task 2", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTask("Project Task", AppTaskCategory.Project, AppTaskStatus.InProgress),
            CreateTask("Idea Task", AppTaskCategory.Idea, AppTaskStatus.Pending),
        };
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

        // Tasks outside date range (should be filtered out based on filter)
        var task3 = CreateTask("Dec Task", AppTaskCategory.ToDo, AppTaskStatus.Completed);
        SetTaskCreationTime(task3, new DateTime(2023, 12, 15));
        tasks.Add(task3);

        return tasks;
    }

    private List<AppTask> CreateTestTasksIncludingDeleted()
    {
        var tasks = new List<AppTask>
        {
            CreateTask("Active Task", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateDeletedTask("Deleted Task", AppTaskCategory.ToDo),
        };
        return tasks;
    }

    private List<AppTask> CreateTestTasksForMetricsCalculation()
    {
        return new List<AppTask>
        {
            CreateTask("Completed 1", AppTaskCategory.ToDo, AppTaskStatus.Completed),
            CreateTask("Completed 2", AppTaskCategory.Idea, AppTaskStatus.Completed),
            CreateTask("Completed 3", AppTaskCategory.Project, AppTaskStatus.Completed),
            CreateTask("In Progress", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateTask("Not Started", AppTaskCategory.Appointment, AppTaskStatus.Pending),
        };
    }

    private List<AppTask> CreateTestTasksWithStreaks()
    {
        var tasks = new List<AppTask>();
        var today = DateTime.UtcNow.Date;

        // Create consecutive completed tasks for streak calculation
        for (int i = 0; i < 5; i++)
        {
            var task = CreateTask($"Streak Task {i + 1}", AppTaskCategory.ToDo, AppTaskStatus.Completed);
            SetTaskCompletionTime(task, today.AddDays(-i));
            tasks.Add(task);
        }

        return tasks;
    }

    private List<AppTask> CreateTestTasksForAnalytics()
    {
        var tasks = new List<AppTask>();
        var now = DateTime.UtcNow;

        // Create tasks at different dates for analytics
        for (int day = 1; day <= 7; day++)
        {
            var task = CreateTask($"Daily Task {day}", AppTaskCategory.ToDo, AppTaskStatus.Completed);
            SetTaskCreationTime(task, now.Date.AddDays(-day));
            tasks.Add(task);
        }

        return tasks;
    }

    private List<AppTask> CreateTestTasksWithSpecialCharacters()
    {
        var tasks = new List<AppTask>
        {
            CreateTask("Task with, comma", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateTask("Task with \"quotes\"", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateTask("Task with\nnewline", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
            CreateTask("Normal task", AppTaskCategory.ToDo, AppTaskStatus.InProgress),
        };

        // Set descriptions with special characters
        SetTaskDescription(tasks[0], "Description with, comma");
        SetTaskDescription(tasks[1], "Description with \"quotes\"");
        SetTaskDescription(tasks[2], "Description with\nnewline");
        SetTaskDescription(tasks[3], "Normal description");

        return tasks;
    }

    private AppTask CreateTask(string title, AppTaskCategory category, AppTaskStatus status, Priority? priority = null)
    {
        var task = new AppTask { Title = title, Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };

        // Set status using reflection
        var statusField = typeof(AppTask).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(task, (int)status);

        // Set priority using reflection
        var priorityField = typeof(AppTask).GetField("_priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        priorityField?.SetValue(task, (int)(priority ?? Priority.Medium));

        return task;
    }

    private AppTask CreateDeletedTask(string title, AppTaskCategory category)
    {
        var task = new AppTask { Title = title, Category = (int)category, UserId = _testUserId, Status = (int)AppTaskStatus.Pending };

        // Set as deleted using reflection
        var isDeletedField = typeof(AppTask).GetField("_isDeleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        isDeletedField?.SetValue(task, true);

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

    private void SetTaskDescription(AppTask task, string description)
    {
        var descriptionField = typeof(AppTask).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        descriptionField?.SetValue(task, description);
    }

    #endregion
}
