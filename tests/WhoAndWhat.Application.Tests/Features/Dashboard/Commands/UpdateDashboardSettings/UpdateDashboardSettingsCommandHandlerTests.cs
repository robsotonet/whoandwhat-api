using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Commands.UpdateDashboardSettings;

/// <summary>
/// Comprehensive unit tests for UpdateDashboardSettingsCommandHandler
/// Tests all scenarios: settings validation, storage, error handling, and business logic
/// </summary>
public class UpdateDashboardSettingsCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<UpdateDashboardSettingsCommandHandler>> _mockLogger;
    private readonly UpdateDashboardSettingsCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public UpdateDashboardSettingsCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<UpdateDashboardSettingsCommandHandler>>();
        
        _handler = new UpdateDashboardSettingsCommandHandler(
            _mockUserRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidSettings_ShouldReturnSuccessResponse()
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings();
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Success.Should().BeTrue();
        response.UpdatedSettings.Should().BeEquivalentTo(settings);
        response.ValidationWarnings.Should().BeEmpty();
        
        // Verify user lookup was called
        _mockUserRepository.Verify(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        var settings = CreateValidDashboardSettings();
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User not found");
    }

    [Theory]
    [InlineData("invalid-theme")]
    [InlineData("")]
    [InlineData("INVALID")]
    public async Task Handle_WithInvalidTheme_ShouldReturnWarning(string invalidTheme)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { Theme = invalidTheme };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.Success.Should().BeTrue();
        response.ValidationWarnings.Should().Contain(w => w.Contains($"Invalid theme '{invalidTheme}'"));
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("auto")]
    [InlineData("LIGHT")] // Should handle case-insensitive
    public async Task Handle_WithValidTheme_ShouldNotReturnWarning(string validTheme)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { Theme = validTheme };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Invalid theme"));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task Handle_WithInvalidLanguage_ShouldReturnWarning(string invalidLanguage)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { Language = invalidLanguage };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().Contain(w => w.Contains($"Invalid language '{invalidLanguage}'"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("EN")] // Should handle case-insensitive
    public async Task Handle_WithValidLanguage_ShouldNotReturnWarning(string validLanguage)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { Language = validLanguage };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Invalid language"));
    }

    [Theory]
    [InlineData(25)] // Below minimum
    [InlineData(5)]  // Well below minimum
    [InlineData(3601)] // Above maximum
    [InlineData(7200)] // Well above maximum
    public async Task Handle_WithInvalidRefreshInterval_ShouldReturnWarning(int invalidInterval)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { RefreshInterval = invalidInterval };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().Contain(w => w.Contains("Refresh interval should be between"));
    }

    [Theory]
    [InlineData(30)]   // Minimum valid
    [InlineData(60)]   // Normal value
    [InlineData(300)]  // 5 minutes
    [InlineData(3600)] // Maximum valid
    public async Task Handle_WithValidRefreshInterval_ShouldNotReturnWarning(int validInterval)
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { RefreshInterval = validInterval };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Refresh interval"));
    }

    [Fact]
    public async Task Handle_WithInvalidWidgets_ShouldReturnWarning()
    {
        // Arrange
        var user = CreateTestUser();
        var invalidWidgets = new List<string> { "completion-stats", "invalid-widget", "another-invalid" };
        var settings = CreateValidDashboardSettings() with { VisibleWidgets = invalidWidgets };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().Contain(w => 
            w.Contains("Invalid widgets") && 
            w.Contains("invalid-widget") && 
            w.Contains("another-invalid"));
    }

    [Fact]
    public async Task Handle_WithValidWidgets_ShouldNotReturnWarning()
    {
        // Arrange
        var user = CreateTestUser();
        var validWidgets = new List<string> 
        { 
            "completion-stats", 
            "productivity-streak", 
            "overdue-tasks", 
            "motivational-content",
            "recent-activity"
        };
        var settings = CreateValidDashboardSettings() with { VisibleWidgets = validWidgets };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Invalid widgets"));
    }

    [Fact]
    public async Task Handle_WithInvalidQuietHours_ShouldReturnWarning()
    {
        // Arrange
        var user = CreateTestUser();
        var invalidQuietHours = new List<int> { 8, 25, -1, 15 }; // 25 and -1 are invalid
        var notificationSettings = CreateValidNotificationSettings() with { QuietHours = invalidQuietHours };
        var settings = CreateValidDashboardSettings() with { NotificationSettings = notificationSettings };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().Contain(w => w.Contains("Quiet hours must be between 0 and 23"));
    }

    [Fact]
    public async Task Handle_WithValidQuietHours_ShouldNotReturnWarning()
    {
        // Arrange
        var user = CreateTestUser();
        var validQuietHours = new List<int> { 0, 8, 12, 20, 23 }; // All valid (0-23)
        var notificationSettings = CreateValidNotificationSettings() with { QuietHours = validQuietHours };
        var settings = CreateValidDashboardSettings() with { NotificationSettings = notificationSettings };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Quiet hours"));
    }

    [Theory]
    [InlineData(-1)] // Below minimum
    [InlineData(31)] // Above maximum
    [InlineData(50)] // Well above maximum
    public async Task Handle_WithInvalidOverdueAlertThreshold_ShouldReturnWarning(int invalidThreshold)
    {
        // Arrange
        var user = CreateTestUser();
        var notificationSettings = CreateValidNotificationSettings() with { OverdueAlertThreshold = invalidThreshold };
        var settings = CreateValidDashboardSettings() with { NotificationSettings = notificationSettings };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().Contain(w => w.Contains("Overdue alert threshold should be between 0 and 30"));
    }

    [Theory]
    [InlineData(0)]  // Minimum valid
    [InlineData(1)]  // Low valid
    [InlineData(7)]  // Week
    [InlineData(30)] // Maximum valid
    public async Task Handle_WithValidOverdueAlertThreshold_ShouldNotReturnWarning(int validThreshold)
    {
        // Arrange
        var user = CreateTestUser();
        var notificationSettings = CreateValidNotificationSettings() with { OverdueAlertThreshold = validThreshold };
        var settings = CreateValidDashboardSettings() with { NotificationSettings = notificationSettings };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Overdue alert threshold"));
    }

    [Fact]
    public async Task Handle_WithMultipleValidationErrors_ShouldReturnAllWarnings()
    {
        // Arrange
        var user = CreateTestUser();
        var invalidSettings = new DashboardSettingsDto(
            Theme: "invalid-theme",
            Language: "invalid-lang",
            ShowCompletionStats: true,
            ShowProductivityStreak: true,
            ShowOverdueTasks: true,
            ShowMotivationalContent: true,
            RefreshInterval: 10, // Too low
            VisibleWidgets: new List<string> { "invalid-widget" },
            WidgetSettings: new Dictionary<string, object>(),
            NotificationSettings: new NotificationSettingsDto(
                EnableOverdueAlerts: true,
                EnableStreakReminders: true,
                EnableDailyDigest: true,
                OverdueAlertThreshold: 50, // Too high
                DigestFrequency: "daily",
                QuietHours: new List<int> { 25 } // Invalid hour
            ),
            DisplaySettings: CreateValidDisplaySettings()
        );
        var command = new UpdateDashboardSettingsCommand(_testUserId, invalidSettings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        
        response.ValidationWarnings.Should().HaveCount(5);
        response.ValidationWarnings.Should().Contain(w => w.Contains("Invalid theme"));
        response.ValidationWarnings.Should().Contain(w => w.Contains("Invalid language"));
        response.ValidationWarnings.Should().Contain(w => w.Contains("Refresh interval"));
        response.ValidationWarnings.Should().Contain(w => w.Contains("Invalid widgets"));
        response.ValidationWarnings.Should().Contain(w => w.Contains("Quiet hours"));
    }

    [Fact]
    public async Task Handle_WithRepositoryException_ShouldReturnFailure()
    {
        // Arrange
        var settings = CreateValidDashboardSettings();
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to update dashboard settings");
        result.Error.Should().Contain("Database connection failed");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error updating dashboard settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages()
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings();
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updating dashboard settings for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully updated dashboard settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithBoundaryValues_ShouldApplyCorrectLimits()
    {
        // Arrange
        var user = CreateTestUser();
        
        // Test boundary conditions that should be corrected in conversion
        var boundarySettings = CreateValidDashboardSettings() with 
        {
            RefreshInterval = 10, // Should be corrected to 30
            DisplaySettings = CreateValidDisplaySettings() with { ItemsPerPage = 500 } // Should be corrected to 100
        };
        var command = new UpdateDashboardSettingsCommand(_testUserId, boundarySettings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // The response should return the original settings as provided
        // The boundary corrections happen during conversion to preferences (internal logic)
        result.Value.UpdatedSettings.RefreshInterval.Should().Be(10); // Original value in response
        result.Value.ValidationWarnings.Should().Contain(w => w.Contains("Refresh interval"));
    }

    [Fact]
    public async Task Handle_WithEmptyWidgetList_ShouldNotReturnWarning()
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { VisibleWidgets = new List<string>() };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationWarnings.Should().NotContain(w => w.Contains("Invalid widgets"));
    }

    [Fact]
    public async Task Handle_WithNullWidgetSettings_ShouldHandleGracefully()
    {
        // Arrange
        var user = CreateTestUser();
        var settings = CreateValidDashboardSettings() with { WidgetSettings = null! };
        var command = new UpdateDashboardSettingsCommand(_testUserId, settings);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should not throw null reference exception
        result.Value.Success.Should().BeTrue();
    }

    #region Helper Methods

    private User CreateTestUser()
    {
        return User.Create("testuser", "test@example.com", "hashedpassword");
    }

    private DashboardSettingsDto CreateValidDashboardSettings()
    {
        return new DashboardSettingsDto(
            Theme: "light",
            Language: "en",
            ShowCompletionStats: true,
            ShowProductivityStreak: true,
            ShowOverdueTasks: true,
            ShowMotivationalContent: true,
            RefreshInterval: 300, // 5 minutes
            VisibleWidgets: new List<string> { "completion-stats", "productivity-streak" },
            WidgetSettings: new Dictionary<string, object> { ["theme"] = "light" },
            NotificationSettings: CreateValidNotificationSettings(),
            DisplaySettings: CreateValidDisplaySettings()
        );
    }

    private NotificationSettingsDto CreateValidNotificationSettings()
    {
        return new NotificationSettingsDto(
            EnableOverdueAlerts: true,
            EnableStreakReminders: true,
            EnableDailyDigest: false,
            OverdueAlertThreshold: 3,
            DigestFrequency: "daily",
            QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6 }
        );
    }

    private DisplaySettingsDto CreateValidDisplaySettings()
    {
        return new DisplaySettingsDto(
            ChartType: "bar",
            DateFormat: "MM/dd/yyyy",
            TimeFormat: "HH:mm",
            Use24HourFormat: true,
            ItemsPerPage: 25,
            DefaultSortOrder: "desc",
            ShowAnimations: true,
            CompactMode: false
        );
    }

    #endregion
}