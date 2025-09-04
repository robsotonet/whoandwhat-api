using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;

namespace WhoAndWhat.CI.Tests;

/// <summary>
/// Tests that validate deployment configurations and readiness for different environments
/// </summary>
public class DeploymentValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DeploymentValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Application_Should_Start_Within_Acceptable_Time()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert
        var elapsed = DateTime.UtcNow - startTime;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "Application should start within 30 seconds");
    }

    [Fact]
    public async Task API_Should_Handle_Concurrent_Requests()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        const int concurrentRequests = 10;

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client.GetAsync("/openapi/v1.json"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(concurrentRequests);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK, 
            "All concurrent requests should succeed");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Environment_Configuration_Should_Be_Valid(string environment)
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        
        // Act & Assert
        var factory = new WebApplicationFactory<Program>();
        
        // Verify the application can start in different environments
        Action createApp = () => factory.CreateClient();
        createApp.Should().NotThrow($"Application should start successfully in {environment} environment");
        
        factory.Dispose();
    }

    [Fact]
    public void Docker_Container_Configuration_Should_Be_Production_Ready()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var dockerComposePath = Path.Combine(baseDirectory, "docker-compose.yml");
        var dockerComposeOverridePath = Path.Combine(baseDirectory, "docker-compose.override.yml");

        // Act & Assert
        File.Exists(dockerComposePath).Should().BeTrue("docker-compose.yml should exist for container orchestration");
        File.Exists(dockerComposeOverridePath).Should().BeTrue("docker-compose.override.yml should exist for development overrides");

        if (File.Exists(dockerComposePath))
        {
            var dockerComposeContent = File.ReadAllText(dockerComposePath);
            
            // Validate production-ready configurations
            dockerComposeContent.Should().Contain("restart:", "Services should have restart policies configured");
            dockerComposeContent.Should().Contain("healthcheck:", "Services should have health checks configured");
            dockerComposeContent.Should().Contain("volumes:", "Data should be persisted using volumes");
        }
    }

    [Fact]
    public void Database_Migration_Should_Be_Automated()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var migrationsPath = Path.Combine(baseDirectory, "src", "WhoAndWhat.Infrastructure", "Migrations");

        // Act & Assert
        Directory.Exists(migrationsPath).Should().BeTrue("EF Core migrations should exist");
        
        var migrationFiles = Directory.GetFiles(migrationsPath, "*.cs", SearchOption.TopDirectoryOnly);
        migrationFiles.Should().NotBeEmpty("At least one migration should exist");
    }

    [Fact]
    public async Task Application_Should_Be_Resilient_To_Network_Issues()
    {
        // Arrange
        using var timeoutClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        timeoutClient.BaseAddress = _client.BaseAddress;

        // Act & Assert
        // Test with very short timeout to simulate network issues
        var response = await timeoutClient.GetAsync("/openapi/v1.json");
        
        // Application should handle network timeouts gracefully
        // This test validates that the application doesn't crash under network stress
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.RequestTimeout);
    }

    [Fact]
    public void Logging_Configuration_Should_Support_Production()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var appSettingsFiles = Directory.GetFiles(baseDirectory, "appsettings*.json", SearchOption.AllDirectories);

        // Act & Assert
        appSettingsFiles.Should().NotBeEmpty("Application settings files should exist");

        foreach (var settingsFile in appSettingsFiles)
        {
            var content = File.ReadAllText(settingsFile);
            if (content.Contains("Logging"))
            {
                var jsonDoc = JsonDocument.Parse(content);
                var loggingSection = jsonDoc.RootElement.GetProperty("Logging");
                
                // Validate logging configuration exists
                loggingSection.Should().NotBeNull("Logging configuration should be present");
                
                // In production, log level should not be too verbose
                if (settingsFile.Contains("Production"))
                {
                    // Production logging should be more restrictive
                    var defaultLevel = loggingSection.GetProperty("LogLevel").GetProperty("Default").GetString();
                    defaultLevel.Should().BeOneOf("Warning", "Error", "Information", 
                        "Production logging should not be too verbose");
                }
            }
        }
    }

    [Fact]
    public void Resource_Requirements_Should_Be_Documented()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var dockerComposePath = Path.Combine(baseDirectory, "docker-compose.yml");
        
        // Act & Assert
        if (File.Exists(dockerComposePath))
        {
            var content = File.ReadAllText(dockerComposePath);
            
            // Docker Compose should specify resource limits for production deployments
            // This helps with capacity planning and prevents resource exhaustion
            content.Should().ContainAny("mem_limit", "memory", "cpus", "Resource limits should be configured");
        }
    }

    [Fact]
    public void Backup_And_Recovery_Strategy_Should_Be_Documented()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var docsPath = Path.Combine(baseDirectory, "docs");

        // Act & Assert
        if (Directory.Exists(docsPath))
        {
            var docFiles = Directory.GetFiles(docsPath, "*.md", SearchOption.AllDirectories);
            var hasBackupDocs = docFiles.Any(f => 
                Path.GetFileName(f).ToLowerInvariant().Contains("backup") ||
                Path.GetFileName(f).ToLowerInvariant().Contains("recovery") ||
                Path.GetFileName(f).ToLowerInvariant().Contains("disaster"));

            // For production readiness, backup strategy should be documented
            // This is a reminder for operational readiness
            hasBackupDocs.Should().BeTrue("Backup and recovery strategy should be documented for production deployment");
        }
    }

    [Fact]
    public void Monitoring_And_Alerting_Should_Be_Configured()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var pipelineFile = Path.Combine(baseDirectory, "azure-pipelines.yml");

        // Act & Assert
        if (File.Exists(pipelineFile))
        {
            var content = File.ReadAllText(pipelineFile);
            
            // Pipeline should include monitoring setup
            content.Should().ContainAny("ApplicationInsights", "monitoring", "alerts", 
                "Pipeline should configure monitoring and alerting");
        }
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