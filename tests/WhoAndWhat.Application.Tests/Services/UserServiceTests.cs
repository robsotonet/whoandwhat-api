using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUserDomainService> _userDomainServiceMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly UserService _userService;
    private readonly Guid _userId = Guid.NewGuid();

    public UserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _userDomainServiceMock = new Mock<IUserDomainService>();
        _loggerMock = new Mock<ILogger<UserService>>();

        _userService = new UserService(
            _userRepositoryMock.Object,
            _userDomainServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ValidatePasswordAsync_Should_Return_True_When_Password_Is_Valid()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        var password = "ValidPassword123!";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userDomainServiceMock.Setup(x => x.ValidatePassword(user, password))
            .Returns(true);

        // Act
        var result = await _userService.ValidatePasswordAsync(_userId, password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePasswordAsync_Should_Return_False_When_User_Not_Found()
    {
        // Arrange
        var password = "ValidPassword123!";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.ValidatePasswordAsync(_userId, password);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePasswordAsync_Should_Return_False_When_Password_Is_Invalid()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        var password = "InvalidPassword";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userDomainServiceMock.Setup(x => x.ValidatePassword(user, password))
            .Returns(false);

        // Act
        var result = await _userService.ValidatePasswordAsync(_userId, password);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ValidatePasswordAsync_Should_Return_False_When_Password_Is_Empty(string? password)
    {
        // Act
        var result = await _userService.ValidatePasswordAsync(_userId, password!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateUserAsync_Should_Return_Success_When_User_Exists()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        var reason = "User requested deactivation";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.DeactivateUserAsync(_userId, reason);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAsync_Should_Return_Failure_When_User_Not_Found()
    {
        // Arrange
        var reason = "User requested deactivation";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.DeactivateUserAsync(_userId, reason);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateUserProfileAsync_Should_Update_Username_When_Valid()
    {
        // Arrange
        var user = new User("test@example.com", "oldusername", Language.en);
        var newUsername = "newusername";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(newUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.UpdateUserProfileAsync(_userId, newUsername, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Username.Should().Be(newUsername);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_Should_Update_Language_When_Valid()
    {
        // Arrange
        var user = new User("test@example.com", "username", Language.en);
        var newLanguage = "es";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.UpdateUserProfileAsync(_userId, null, newLanguage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredLanguage.Should().Be(Language.es);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_Should_Return_Failure_When_Username_Exists()
    {
        // Arrange
        var user = new User("test@example.com", "oldusername", Language.en);
        var existingUser = new User("other@example.com", "newusername", Language.en);
        var newUsername = "newusername";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.GetByUsernameAsync(newUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _userService.UpdateUserProfileAsync(_userId, newUsername, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("This username is already taken");
    }

    [Fact]
    public async Task UpdateUserProfileAsync_Should_Return_Failure_When_Language_Is_Invalid()
    {
        // Arrange
        var user = new User("test@example.com", "username", Language.en);
        var invalidLanguage = "invalid";

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.UpdateUserProfileAsync(_userId, null, invalidLanguage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid language");
    }

    [Fact]
    public async Task ExportUserDataAsync_Should_Return_Json_Data_When_Profile_Included()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.ExportUserDataAsync(_userId, "json", true, false, false, false, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/json");
        result.Value.Data.Length.Should().BeGreaterThan(0);
        result.Value.RecordCount.Should().Be(1);

        // Verify JSON content
        var jsonContent = System.Text.Encoding.UTF8.GetString(result.Value.Data);
        var jsonData = JsonDocument.Parse(jsonContent);
        jsonData.RootElement.TryGetProperty("profile", out var profileElement).Should().BeTrue();
        profileElement.TryGetProperty("email", out var emailElement).Should().BeTrue();
        emailElement.GetString().Should().Be("test@example.com");
    }

    [Fact]
    public async Task ExportUserDataAsync_Should_Return_Csv_Data_When_Format_Is_Csv()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.ExportUserDataAsync(_userId, "csv", true, false, false, false, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("text/csv");
        result.Value.Data.Length.Should().BeGreaterThan(0);

        // Verify CSV content
        var csvContent = System.Text.Encoding.UTF8.GetString(result.Value.Data);
        csvContent.Should().Contain("Type,Id,Email,Username");
        csvContent.Should().Contain("Profile");
        csvContent.Should().Contain("test@example.com");
    }

    [Fact]
    public async Task ExportUserDataAsync_Should_Return_Failure_When_Format_Is_Invalid()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.ExportUserDataAsync(_userId, "invalid", true, false, false, false, false);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported export format");
    }

    [Fact]
    public async Task ExportUserDataAsync_Should_Return_Failure_When_User_Not_Found()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.ExportUserDataAsync(_userId, "json", true, false, false, false, false);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task ExportUserDataAsync_Should_Include_Placeholder_Data_For_Related_Entities()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.ExportUserDataAsync(_userId, "json", true, true, true, true, true);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify JSON content includes placeholder data structures
        var jsonContent = System.Text.Encoding.UTF8.GetString(result.Value.Data);
        var jsonData = JsonDocument.Parse(jsonContent);
        
        jsonData.RootElement.TryGetProperty("profile", out _).Should().BeTrue();
        jsonData.RootElement.TryGetProperty("tasks", out _).Should().BeTrue();
        jsonData.RootElement.TryGetProperty("projects", out _).Should().BeTrue();
        jsonData.RootElement.TryGetProperty("contacts", out _).Should().BeTrue();
        jsonData.RootElement.TryGetProperty("oauthAccounts", out _).Should().BeTrue();
    }
}