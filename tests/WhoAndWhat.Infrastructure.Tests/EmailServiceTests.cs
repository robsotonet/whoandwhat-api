using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests;

public class EmailServiceTests
{
    private readonly Mock<ILogger<EmailService>> _loggerMock;
    private readonly EmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public EmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmailService>>();
        _emailSettings = new EmailSettings
        {
            Enabled = false, // Set to false for testing to avoid actual email sending
            FromName = "WhoAndWhat Test",
            FromEmail = "test@whoandwhat.com",
            SmtpHost = "smtp.test.com",
            SmtpPort = 587,
            Username = "test@whoandwhat.com",
            Password = "testpassword",
            UseSsl = true,
            Templates = new EmailTemplateSettings
            {
                PasswordResetExpirationHours = 1,
                EmailVerificationExpirationHours = 24,
                SupportEmail = "support@test.com",
                CompanyName = "WhoAndWhat Test"
            }
        };

        var optionsMock = new Mock<IOptions<EmailSettings>>();
        optionsMock.Setup(x => x.Value).Returns(_emailSettings);

        _emailService = new EmailService(optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_Should_Return_True_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";
        var resetToken = "reset-token-123";

        // Act
        var result = await _emailService.SendPasswordResetEmailAsync(email, username, resetToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_Should_Log_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";
        var resetToken = "reset-token-123";

        // Act
        await _emailService.SendPasswordResetEmailAsync(email, username, resetToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email sending is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_Should_Return_True_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";
        var verificationToken = "verification-token-123";
        var userId = Guid.NewGuid();

        // Act
        var result = await _emailService.SendEmailVerificationAsync(email, username, verificationToken, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailVerificationAsync_Should_Log_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";
        var verificationToken = "verification-token-123";
        var userId = Guid.NewGuid();

        // Act
        await _emailService.SendEmailVerificationAsync(email, username, verificationToken, userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email sending is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_Should_Return_True_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";

        // Act
        var result = await _emailService.SendWelcomeEmailAsync(email, username);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendPasswordChangedEmailAsync_Should_Return_True_When_Email_Disabled()
    {
        // Arrange
        _emailSettings.Enabled = false;
        var email = "test@example.com";
        var username = "testuser";

        // Act
        var result = await _emailService.SendPasswordChangedEmailAsync(email, username);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void BuildPasswordResetEmailBody_Should_Generate_Valid_Html_Content()
    {
        // Arrange
        var username = "testuser";
        var resetToken = "reset-token-123";

        // Act
        var htmlContent = _emailService.GetType()
            .GetMethod("BuildPasswordResetEmailBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_emailService, new object[] { username, resetToken, true }) as string;

        // Assert
        htmlContent.Should().NotBeNullOrEmpty();
        htmlContent.Should().Contain(username);
        htmlContent.Should().Contain(resetToken);
        htmlContent.Should().Contain("<!DOCTYPE html");
        htmlContent.Should().Contain("Reset Password");
    }

    [Fact]
    public void BuildPasswordResetEmailBody_Should_Generate_Valid_Plain_Text()
    {
        // Arrange
        var username = "testuser";
        var resetToken = "reset-token-123";

        // Act
        var textContent = _emailService.GetType()
            .GetMethod("BuildPasswordResetEmailBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_emailService, new object[] { username, resetToken, false }) as string;

        // Assert
        textContent.Should().NotBeNullOrEmpty();
        textContent.Should().Contain(username);
        textContent.Should().Contain(resetToken);
        textContent.Should().NotContain("<!DOCTYPE html");
        textContent.Should().Contain("Reset Password");
    }

    [Fact]
    public void BuildEmailVerificationBody_Should_Generate_Valid_Html_Content()
    {
        // Arrange
        var username = "testuser";
        var verificationToken = "verification-token-123";
        var userId = Guid.NewGuid();

        // Act
        var htmlContent = _emailService.GetType()
            .GetMethod("BuildEmailVerificationBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_emailService, new object[] { username, verificationToken, userId, true }) as string;

        // Assert
        htmlContent.Should().NotBeNullOrEmpty();
        htmlContent.Should().Contain(username);
        htmlContent.Should().Contain(verificationToken);
        htmlContent.Should().Contain(userId.ToString());
        htmlContent.Should().Contain("<!DOCTYPE html");
        htmlContent.Should().Contain("Verify Email");
    }

    [Fact]
    public void BuildWelcomeEmailBody_Should_Generate_Valid_Html_Content()
    {
        // Arrange
        var username = "testuser";

        // Act
        var htmlContent = _emailService.GetType()
            .GetMethod("BuildWelcomeEmailBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_emailService, new object[] { username, true }) as string;

        // Assert
        htmlContent.Should().NotBeNullOrEmpty();
        htmlContent.Should().Contain(username);
        htmlContent.Should().Contain("<!DOCTYPE html");
        htmlContent.Should().Contain("Welcome");
    }

    [Fact]
    public void EmailSettings_Should_Have_Valid_Template_Configuration()
    {
        // Assert
        _emailSettings.Templates.Should().NotBeNull();
        _emailSettings.Templates.PasswordResetExpirationHours.Should().BeGreaterThan(0);
        _emailSettings.Templates.EmailVerificationExpirationHours.Should().BeGreaterThan(0);
        _emailSettings.Templates.SupportEmail.Should().NotBeNullOrEmpty();
        _emailSettings.Templates.CompanyName.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendPasswordResetEmailAsync_Should_Return_False_When_Email_Is_Invalid(string? email)
    {
        // Arrange
        _emailSettings.Enabled = true;
        var username = "testuser";
        var resetToken = "reset-token-123";

        // Act
        var result = await _emailService.SendPasswordResetEmailAsync(email!, username, resetToken);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendPasswordResetEmailAsync_Should_Return_False_When_Token_Is_Invalid(string? token)
    {
        // Arrange
        _emailSettings.Enabled = true;
        var email = "test@example.com";
        var username = "testuser";

        // Act
        var result = await _emailService.SendPasswordResetEmailAsync(email, username, token!);

        // Assert
        result.Should().BeFalse();
    }
}