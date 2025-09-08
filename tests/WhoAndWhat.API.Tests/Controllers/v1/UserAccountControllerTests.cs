using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers.v1;

public class UserAccountControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ILogger<UserAccountController>> _loggerMock;
    private readonly UserAccountController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public UserAccountControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _loggerMock = new Mock<ILogger<UserAccountController>>();
        _controller = new UserAccountController(_userServiceMock.Object, _loggerMock.Object);

        // Setup authenticated user context
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetProfile_Should_Return_User_Profile_When_User_Exists()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.RecordLoginAttempt(true); // This sets LastLoginAt to DateTime.UtcNow
        
        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetProfile();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        
        response.UserId.Should().Be(user.Id);
        response.Email.Should().Be("test@example.com");
        response.Username.Should().Be("testuser");
        response.PreferredLanguage.Should().Be("en");
        response.IsEmailVerified.Should().Be(user.IsEmailVerified);
        response.IsActive.Should().Be(user.IsActive);
    }

    [Fact]
    public async Task GetProfile_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.GetProfile();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_Success_When_Update_Is_Valid()
    {
        // Arrange
        var request = new UpdateProfileRequest
        {
            Username = "newusername",
            PreferredLanguage = "es"
        };

        var updatedUser = new User("test@example.com", "newusername", Language.es);
        var updateResult = Result<User>.Success(updatedUser);

        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedUser);
        _userServiceMock.Setup(x => x.UpdateUserProfileAsync(_userId, request.Username, request.PreferredLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<UpdateProfileResponse>().Subject;
        
        response.UserId.Should().Be(_userId);
        response.Username.Should().Be("newusername");
        response.PreferredLanguage.Should().Be("es");
        response.Message.Should().Be("Profile updated successfully");
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_BadRequest_When_Update_Fails()
    {
        // Arrange
        var request = new UpdateProfileRequest
        {
            Username = "existingusername"
        };

        var updateResult = Result<User>.Failure("Username already exists");

        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User("test@example.com", "testuser", Language.en));
        _userServiceMock.Setup(x => x.UpdateUserProfileAsync(_userId, request.Username, request.PreferredLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeactivateAccount_Should_Return_Success_When_Password_Is_Valid()
    {
        // Arrange
        var request = new DeactivateAccountRequest
        {
            CurrentPassword = "ValidPassword123!",
            Reason = "Test reason",
            ConfirmDeactivation = true
        };

        var user = new User("test@example.com", "testuser", Language.en);
        
        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userServiceMock.Setup(x => x.ValidatePasswordAsync(_userId, request.CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userServiceMock.Setup(x => x.DeactivateUserAsync(_userId, request.Reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _controller.DeactivateAccount(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value.Should().BeOfType<MessageResponse>().Subject;
        
        response.Success.Should().BeTrue();
        response.Message.Should().Contain("successfully deactivated");
    }

    [Fact]
    public async Task DeactivateAccount_Should_Return_BadRequest_When_Password_Is_Invalid()
    {
        // Arrange
        var request = new DeactivateAccountRequest
        {
            CurrentPassword = "InvalidPassword",
            ConfirmDeactivation = true
        };

        var user = new User("test@example.com", "testuser", Language.en);
        
        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userServiceMock.Setup(x => x.ValidatePasswordAsync(_userId, request.CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeactivateAccount(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExportData_Should_Return_File_When_Export_Is_Successful()
    {
        // Arrange
        var request = new ExportDataRequest
        {
            Format = "json",
            IncludeProfile = true,
            IncludeTasks = true
        };

        var exportData = new ExportData
        {
            Data = System.Text.Encoding.UTF8.GetBytes("{\"profile\": {\"id\": \"test\"}}"),
            ContentType = "application/json",
            FileName = "user-data-export.json",
            SizeBytes = 100,
            RecordCount = 1
        };

        var user = new User("test@example.com", "testuser", Language.en);
        var exportResult = Result<ExportData>.Success(exportData);

        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userServiceMock.Setup(x => x.ExportUserDataAsync(
            _userId, 
            request.Format, 
            request.IncludeProfile, 
            request.IncludeTasks, 
            request.IncludeProjects, 
            request.IncludeContacts, 
            request.IncludeOAuthAccounts, 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportResult);

        // Act
        var result = await _controller.ExportData(request);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().EndWith(".json");
        fileResult.FileContents.Should().BeEquivalentTo(exportData.Data);
    }

    [Fact]
    public async Task ExportData_Should_Return_BadRequest_When_Export_Fails()
    {
        // Arrange
        var request = new ExportDataRequest
        {
            Format = "invalid-format"
        };

        var user = new User("test@example.com", "testuser", Language.en);
        var exportResult = Result<ExportData>.Failure("Unsupported format");

        _userServiceMock.Setup(x => x.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userServiceMock.Setup(x => x.ExportUserDataAsync(
            It.IsAny<Guid>(), 
            It.IsAny<string>(), 
            It.IsAny<bool>(), 
            It.IsAny<bool>(), 
            It.IsAny<bool>(), 
            It.IsAny<bool>(), 
            It.IsAny<bool>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportResult);

        // Act
        var result = await _controller.ExportData(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}