using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Commands.ResetDashboardPreferences;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Commands.ResetDashboardPreferences;

public class ResetDashboardPreferencesCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<ResetDashboardPreferencesCommandHandler>> _loggerMock;
    private readonly ResetDashboardPreferencesCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ResetDashboardPreferencesCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<ResetDashboardPreferencesCommandHandler>>();
        _handler = new ResetDashboardPreferencesCommandHandler(_userRepositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidUserAndConfirmation_ShouldResetAllSettingsSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.DefaultSettings.Should().NotBeNull();
        result.Value.ResetSettings.Should().NotBeEmpty();
        result.Value.ResetSettings.Should().Contain(new[]
        {
            "theme", "language", "widgets", "notifications", "display",
            "refresh-interval", "completion-stats", "productivity-streak",
            "overdue-tasks", "motivational-content"
        });
        result.Value.ResetTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify logging
        VerifyLogMessage(LogLevel.Information, "Resetting dashboard preferences for user");
        VerifyLogMessage(LogLevel.Information, "Successfully reset dashboard preferences for user");
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task Handle_WithNoConfirmationAndNoSpecificSettings_ShouldRequireConfirmation()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Reset confirmation required. Set ConfirmReset to true to proceed.");
    }

    [Fact]
    public async Task Handle_WithNoConfirmationAndEmptySpecificSettings_ShouldRequireConfirmation()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false, SpecificSettings: new List<string>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Reset confirmation required. Set ConfirmReset to true to proceed.");
    }

    [Theory]
    [InlineData("theme")]
    [InlineData("language")]
    [InlineData("widgets")]
    [InlineData("notifications")]
    [InlineData("display")]
    public async Task Handle_WithSpecificValidSettings_ShouldResetOnlySpecifiedSettings(string settingToReset)
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: new List<string> { settingToReset });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().ContainSingle().Which.Should().Be(settingToReset);
        result.Value.DefaultSettings.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithMultipleSpecificSettings_ShouldResetAllSpecifiedSettings()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var settingsToReset = new List<string> { "theme", "language", "notifications" };
        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: settingsToReset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().HaveCount(3);
        result.Value.ResetSettings.Should().BeEquivalentTo(settingsToReset);
    }

    [Fact]
    public async Task Handle_WithInvalidSpecificSettings_ShouldFilterInvalidSettings()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var settingsToReset = new List<string> { "theme", "invalid-setting", "language", "another-invalid" };
        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: settingsToReset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().HaveCount(2);
        result.Value.ResetSettings.Should().BeEquivalentTo(new[] { "theme", "language" });
    }

    [Fact]
    public async Task Handle_WithOnlyInvalidSpecificSettings_ShouldReturnEmptyResetList()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var settingsToReset = new List<string> { "invalid-setting", "another-invalid" };
        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: settingsToReset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("THEME", "theme")]
    [InlineData("LANGUAGE", "language")]
    [InlineData("Widgets", "widgets")]
    public async Task Handle_WithCaseInsensitiveSpecificSettings_ShouldHandleCorrectly(string inputSetting, string expectedSetting)
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: new List<string> { inputSetting });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().ContainSingle().Which.Should().Be(expectedSetting);
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectDefaultSettings()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var defaultSettings = result.Value.DefaultSettings;

        defaultSettings.Theme.Should().Be("light");
        defaultSettings.Language.Should().Be("en");
        defaultSettings.ShowCompletionStats.Should().BeTrue();
        defaultSettings.ShowProductivityStreak.Should().BeTrue();
        defaultSettings.ShowOverdueTasks.Should().BeTrue();
        defaultSettings.ShowMotivationalContent.Should().BeTrue();
        defaultSettings.RefreshInterval.Should().Be(300);

        defaultSettings.VisibleWidgets.Should().BeEquivalentTo(new[]
        {
            "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content"
        });

        defaultSettings.NotificationSettings.EnableOverdueAlerts.Should().BeTrue();
        defaultSettings.NotificationSettings.EnableStreakReminders.Should().BeTrue();
        defaultSettings.NotificationSettings.EnableDailyDigest.Should().BeFalse();
        defaultSettings.NotificationSettings.OverdueAlertThreshold.Should().Be(3);
        defaultSettings.NotificationSettings.DigestFrequency.Should().Be("weekly");
        defaultSettings.NotificationSettings.QuietHours.Should().HaveCount(10);

        defaultSettings.DisplaySettings.ChartType.Should().Be("bar");
        defaultSettings.DisplaySettings.DateFormat.Should().Be("MM/dd/yyyy");
        defaultSettings.DisplaySettings.TimeFormat.Should().Be("12h");
        defaultSettings.DisplaySettings.Use24HourFormat.Should().BeFalse();
        defaultSettings.DisplaySettings.ItemsPerPage.Should().Be(20);
        defaultSettings.DisplaySettings.DefaultSortOrder.Should().Be("priority");
        defaultSettings.DisplaySettings.ShowAnimations.Should().BeTrue();
        defaultSettings.DisplaySettings.CompactMode.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldReturnFailure()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to reset dashboard preferences: Database connection failed");

        VerifyLogMessage(LogLevel.Error, "Error resetting dashboard preferences for user");
    }

    [Theory]
    [InlineData("refresh-interval")]
    [InlineData("completion-stats")]
    [InlineData("productivity-streak")]
    [InlineData("overdue-tasks")]
    [InlineData("motivational-content")]
    public async Task Handle_WithSpecializedSettings_ShouldResetCorrectly(string setting)
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: new List<string> { setting });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResetSettings.Should().ContainSingle().Which.Should().Be(setting);
    }

    [Fact]
    public async Task Handle_ShouldLogCorrectInformation()
    {
        // Arrange
        var user = CreateTestUser(_testUserId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var settingsToReset = new List<string> { "theme", "language" };
        var command = new ResetDashboardPreferencesCommand(_testUserId, ConfirmReset: false,
            SpecificSettings: settingsToReset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        VerifyLogMessage(LogLevel.Information, $"Resetting dashboard preferences for user {_testUserId}");
        VerifyLogMessage(LogLevel.Information, $"Successfully reset dashboard preferences for user {_testUserId}");
        VerifyLogMessage(LogLevel.Debug, $"Storing dashboard preferences for user {_testUserId}");
    }

    private User CreateTestUser(Guid userId)
    {
        // Use the public constructor with required parameters
        var user = new User("test@example.com", "testuser", Language.en);

        // If we need to set a specific ID, use reflection as a fallback
        var idField = typeof(BaseEntity).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(user, userId);

        return user;
    }

    private void VerifyLogMessage(LogLevel level, string message)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce
        );
    }
}
