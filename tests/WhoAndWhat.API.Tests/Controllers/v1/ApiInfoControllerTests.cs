using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using WhoAndWhat.API.Controllers.v1;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers.v1;

/// <summary>
/// Unit tests for ApiInfoController
/// </summary>
public class ApiInfoControllerTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ApiInfoController _controller;

    public ApiInfoControllerTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _controller = new ApiInfoController(_mockConfiguration.Object);

        SetupDefaultConfiguration();
    }

    [Fact]
    public void GetApiInfo_Should_Return_OK_Result()
    {
        // Act
        var result = _controller.GetApiInfo();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetApiInfo_Should_Return_ApiInfoResponse_With_Correct_Structure()
    {
        // Act
        var result = _controller.GetApiInfo();
        var okResult = result.Result as OkObjectResult;
        var apiInfo = okResult?.Value as ApiInfoResponse;

        // Assert
        apiInfo.Should().NotBeNull();
        apiInfo!.Name.Should().NotBeNullOrEmpty();
        apiInfo.Version.Should().NotBeNullOrEmpty();
        apiInfo.Description.Should().NotBeNullOrEmpty();
        apiInfo.Status.Should().Be("Healthy");
        apiInfo.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        apiInfo.Environment.Should().NotBeNullOrEmpty();
        apiInfo.AvailableEndpoints.Should().NotBeEmpty();
    }

    [Fact]
    public void GetApiInfo_Should_Use_Configuration_Values_When_Available()
    {
        // Arrange
        var expectedTitle = "Test API Title";
        var expectedVersion = "v2.0";
        var expectedDescription = "Test API Description";

        _mockConfiguration.Setup(c => c["ApiSettings:Title"]).Returns(expectedTitle);
        _mockConfiguration.Setup(c => c["ApiSettings:Version"]).Returns(expectedVersion);
        _mockConfiguration.Setup(c => c["ApiSettings:Description"]).Returns(expectedDescription);

        // Act
        var result = _controller.GetApiInfo();
        var okResult = result.Result as OkObjectResult;
        var apiInfo = okResult?.Value as ApiInfoResponse;

        // Assert
        apiInfo.Should().NotBeNull();
        apiInfo!.Name.Should().Be(expectedTitle);
        apiInfo.Version.Should().Be(expectedVersion);
        apiInfo.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void GetApiInfo_Should_Use_Default_Values_When_Configuration_Is_Null()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["ApiSettings:Title"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["ApiSettings:Version"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["ApiSettings:Description"]).Returns((string?)null);

        // Act
        var result = _controller.GetApiInfo();
        var okResult = result.Result as OkObjectResult;
        var apiInfo = okResult?.Value as ApiInfoResponse;

        // Assert
        apiInfo.Should().NotBeNull();
        apiInfo!.Name.Should().Be("WhoAndWhat Task Management API");
        apiInfo.Version.Should().Be("v1");
        apiInfo.Description.Should().Be("A comprehensive task management platform");
    }

    [Fact]
    public void GetApiInfo_Should_Include_All_Required_Endpoints()
    {
        // Act
        var result = _controller.GetApiInfo();
        var okResult = result.Result as OkObjectResult;
        var apiInfo = okResult?.Value as ApiInfoResponse;

        // Assert
        apiInfo.Should().NotBeNull();
        apiInfo!.AvailableEndpoints.Should().Contain("/api/v1/apiinfo");
        apiInfo.AvailableEndpoints.Should().Contain("/health");
        apiInfo.AvailableEndpoints.Should().Contain("/health/live");
        apiInfo.AvailableEndpoints.Should().Contain("/health/ready");
        apiInfo.AvailableEndpoints.Should().Contain("/swagger");
    }

    [Fact]
    public void GetVersion_Should_Return_OK_Result()
    {
        // Act
        var result = _controller.GetVersion();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetVersion_Should_Return_VersionInfo_With_Correct_Structure()
    {
        // Act
        var result = _controller.GetVersion();
        var okResult = result.Result as OkObjectResult;
        var versionInfo = okResult?.Value as VersionInfo;

        // Assert
        versionInfo.Should().NotBeNull();
        versionInfo!.ApiVersion.Should().Be("1.0");
        versionInfo.BuildNumber.Should().NotBeNullOrEmpty();
        versionInfo.BuildDate.Should().NotBeNullOrEmpty();
        versionInfo.Framework.Should().Be(".NET 9.0");
        versionInfo.SupportedVersions.Should().Contain("1.0");
    }

    [Fact]
    public void GetVersion_Should_Use_Environment_BuildNumber_When_Available()
    {
        // Arrange
        var expectedBuildNumber = "12345";
        Environment.SetEnvironmentVariable("BUILD_NUMBER", expectedBuildNumber);

        try
        {
            // Act
            var result = _controller.GetVersion();
            var okResult = result.Result as OkObjectResult;
            var versionInfo = okResult?.Value as VersionInfo;

            // Assert
            versionInfo.Should().NotBeNull();
            versionInfo!.BuildNumber.Should().Be(expectedBuildNumber);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("BUILD_NUMBER", null);
        }
    }

    [Fact]
    public void GetVersion_Should_Use_Local_BuildNumber_When_Environment_Not_Set()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BUILD_NUMBER", null);

        // Act
        var result = _controller.GetVersion();
        var okResult = result.Result as OkObjectResult;
        var versionInfo = okResult?.Value as VersionInfo;

        // Assert
        versionInfo.Should().NotBeNull();
        versionInfo!.BuildNumber.Should().Be("local");
    }

    [Fact]
    public void GetVersion_Should_Return_Current_Date_As_BuildDate()
    {
        // Act
        var result = _controller.GetVersion();
        var okResult = result.Result as OkObjectResult;
        var versionInfo = okResult?.Value as VersionInfo;

        // Assert
        versionInfo.Should().NotBeNull();
        var expectedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        versionInfo!.BuildDate.Should().Be(expectedDate);
    }

    [Fact]
    public void GetVersion_Should_Return_Correct_SupportedVersions_Array()
    {
        // Act
        var result = _controller.GetVersion();
        var okResult = result.Result as OkObjectResult;
        var versionInfo = okResult?.Value as VersionInfo;

        // Assert
        versionInfo.Should().NotBeNull();
        versionInfo!.SupportedVersions.Should().HaveCount(1);
        versionInfo.SupportedVersions.Should().Equal("1.0");
    }

    private void SetupDefaultConfiguration()
    {
        _mockConfiguration.Setup(c => c["ApiSettings:Title"]).Returns("WhoAndWhat Task Management API");
        _mockConfiguration.Setup(c => c["ApiSettings:Version"]).Returns("v1");
        _mockConfiguration.Setup(c => c["ApiSettings:Description"]).Returns("A comprehensive task management platform");
    }
}
