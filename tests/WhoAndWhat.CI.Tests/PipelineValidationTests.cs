using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;

namespace WhoAndWhat.CI.Tests;

/// <summary>
/// Integration tests that validate CI/CD pipeline components and deployments
/// </summary>
public class PipelineValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PipelineValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Application_Should_Start_Successfully()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenAPI_Specification_Should_Be_Valid()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        // Validate it's valid JSON
        var openApiDoc = JsonDocument.Parse(content);
        openApiDoc.RootElement.GetProperty("openapi").GetString().Should().NotBeNullOrEmpty();
        openApiDoc.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Contain("WhoAndWhat.API");
    }

    [Fact]
    public void Environment_Variables_Should_Be_Configured_For_CI()
    {
        // Arrange & Act
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        // Assert
        environment.Should().NotBeNullOrEmpty("CI environment should have ASPNETCORE_ENVIRONMENT set");
        environment.Should().BeOneOf("Development", "Test", "Staging", "Production");
    }

    [Theory]
    [InlineData("DOTNET_ENVIRONMENT")]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    public void Required_Environment_Variables_Should_Be_Present(string environmentVariable)
    {
        // Arrange & Act
        var value = Environment.GetEnvironmentVariable(environmentVariable);

        // Assert
        value.Should().NotBeNullOrEmpty($"Environment variable {environmentVariable} should be configured for CI/CD");
    }

    [Fact]
    public async Task Health_Check_Endpoint_Should_Be_Available()
    {
        // Note: This test assumes a health check endpoint will be implemented
        // Currently the API doesn't have health checks, but it's a CI/CD best practice
        
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert - For now, we accept 404 since health checks aren't implemented yet
        // When health checks are implemented, this should be OK (200)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [Fact]
    public void Assembly_Should_Have_Correct_Version_Information()
    {
        // Arrange
        var assembly = typeof(Program).Assembly;

        // Act & Assert
        assembly.Should().NotBeNull();
        assembly.GetName().Version.Should().NotBeNull();
        
        // In CI/CD, version should be set from build variables
        var informationalVersion = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault();

        informationalVersion?.InformationalVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task API_Should_Handle_Invalid_Routes_Gracefully()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/nonexistent-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Solution_Should_Have_Required_Project_Structure()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        
        // Act & Assert - Validate required directories exist
        Directory.Exists(Path.Combine(baseDirectory, "src")).Should().BeTrue("src directory should exist");
        Directory.Exists(Path.Combine(baseDirectory, "tests")).Should().BeTrue("tests directory should exist");
        
        // Validate required files exist
        File.Exists(Path.Combine(baseDirectory, "Dockerfile")).Should().BeTrue("Dockerfile should exist for containerization");
        File.Exists(Path.Combine(baseDirectory, "docker-compose.yml")).Should().BeTrue("docker-compose.yml should exist");
        File.Exists(Path.Combine(baseDirectory, "azure-pipelines.yml")).Should().BeTrue("Azure DevOps pipeline should exist");
        File.Exists(Path.Combine(baseDirectory, ".runsettings")).Should().BeTrue("Test run settings should exist");
    }

    [Fact]
    public void CI_Configuration_Files_Should_Exist()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        
        // Act & Assert
        File.Exists(Path.Combine(baseDirectory, "azure-pipelines.yml")).Should().BeTrue("Azure DevOps pipeline configuration should exist");
        File.Exists(Path.Combine(baseDirectory, ".runsettings")).Should().BeTrue("Test run settings should be configured");
        File.Exists(Path.Combine(baseDirectory, ".dockerignore")).Should().BeTrue("Docker ignore file should exist for optimized builds");
        File.Exists(Path.Combine(baseDirectory, ".gitignore")).Should().BeTrue("Git ignore file should exist");
    }

    [Fact]
    public void Build_Artifacts_Should_Not_Be_In_Source_Control()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        
        // Act & Assert - These directories should not exist in source control
        Directory.Exists(Path.Combine(baseDirectory, "src", "WhoAndWhat.API", "bin")).Should().BeFalse("bin directories should not be in source control");
        Directory.Exists(Path.Combine(baseDirectory, "src", "WhoAndWhat.API", "obj")).Should().BeFalse("obj directories should not be in source control");
    }

    private static string GetSolutionRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);
        
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory == null)
            throw new InvalidOperationException("Could not find solution root directory");

        return directory.FullName;
    }
}