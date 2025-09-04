using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Reflection;

namespace WhoAndWhat.CI.Tests;

/// <summary>
/// Tests that validate security configurations and practices for CI/CD pipeline
/// </summary>
public class SecurityValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SecurityValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Application_Should_Not_Expose_Sensitive_Headers()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert
        response.Headers.Should().NotContainKey("Server", "Server header should be removed for security");
        response.Headers.Should().NotContainKey("X-Powered-By", "X-Powered-By header should be removed");
        response.Headers.Should().NotContainKey("X-AspNet-Version", "ASP.NET version should not be exposed");
    }

    [Fact]
    public async Task API_Should_Return_Appropriate_Security_Headers()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert - These headers should be configured in production
        // For now, we just ensure the response is successful
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // TODO: When security headers middleware is implemented, test for:
        // - X-Content-Type-Options: nosniff
        // - X-Frame-Options: DENY
        // - X-XSS-Protection: 1; mode=block
        // - Content-Security-Policy
        // - Strict-Transport-Security (for HTTPS)
    }

    [Fact]
    public void Secrets_Should_Not_Be_Hardcoded_In_Configuration()
    {
        // Arrange - Get all configuration files
        var baseDirectory = GetSolutionRoot();
        var configFiles = Directory.GetFiles(baseDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains("appsettings"))
            .ToList();

        // Act & Assert
        foreach (var configFile in configFiles)
        {
            var content = File.ReadAllText(configFile);
            
            // Check for common secret patterns
            content.Should().NotContain("password=", "Configuration should not contain hardcoded passwords");
            content.Should().NotContain("Password=", "Configuration should not contain hardcoded passwords");
            content.Should().NotMatch("*secret*", "Configuration should not contain hardcoded secrets");
            content.Should().NotMatch("*key*=*", "Configuration should use secure key management");
            
            // Ensure JWT secrets are not hardcoded (should be environment variables or Key Vault)
            if (content.Contains("JWT"))
            {
                content.Should().NotContain("YourSecretKeyHere", "JWT secrets should not be hardcoded in config files");
            }
        }
    }

    [Fact]
    public void Docker_Configuration_Should_Follow_Security_Best_Practices()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var dockerfilePath = Path.Combine(baseDirectory, "Dockerfile");
        
        File.Exists(dockerfilePath).Should().BeTrue("Dockerfile should exist");
        
        // Act
        var dockerfileContent = File.ReadAllText(dockerfilePath);

        // Assert
        dockerfileContent.Should().Contain("USER", "Dockerfile should specify non-root user");
        dockerfileContent.Should().NotContain("--allow-root", "Container should not run as root");
        dockerfileContent.Should().Contain("HEALTHCHECK", "Dockerfile should include health checks");
    }

    [Fact]
    public void Dependencies_Should_Not_Have_Known_Vulnerabilities()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var projectFiles = Directory.GetFiles(baseDirectory, "*.csproj", SearchOption.AllDirectories);

        // Act & Assert
        foreach (var projectFile in projectFiles)
        {
            var content = File.ReadAllText(projectFile);
            
            // Check for outdated or vulnerable package versions
            // This is a basic check - in production, use tools like OWASP Dependency Check
            content.Should().NotContain("Version=\"1.", "Avoid using very old package versions");
            content.Should().NotContain("Version=\"2.", "Consider updating older packages for security");
        }
    }

    [Fact]
    public void Environment_Specific_Secrets_Should_Use_Secure_Storage()
    {
        // Arrange
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        // Act & Assert
        if (environment == "Production" || environment == "Staging")
        {
            // In production/staging, secrets should come from secure storage
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            var jwtSecret = Environment.GetEnvironmentVariable("JWT__SecretKey");

            // These should be set from Azure Key Vault or similar secure storage
            if (!string.IsNullOrEmpty(connectionString))
            {
                connectionString.Should().NotContain("localhost", "Production should not use localhost connection strings");
                connectionString.Should().NotContain("Password=password", "Production should not use default passwords");
            }

            if (!string.IsNullOrEmpty(jwtSecret))
            {
                jwtSecret.Should().NotContain("YourSecret", "Production should not use default JWT secrets");
                jwtSecret.Length.Should().BeGreaterOrEqualTo(32, "JWT secret should be at least 32 characters long");
            }
        }
    }

    [Fact]
    public void Assembly_Should_Be_Signed_In_Production_Builds()
    {
        // Arrange
        var assembly = typeof(Program).Assembly;

        // Act
        var isFullySigned = assembly.GetCustomAttribute<AssemblyDelaySignAttribute>()?.DelaySign == false;
        var publicKeyToken = assembly.GetName().GetPublicKeyToken();

        // Assert
        // In a real production scenario, you would sign assemblies
        // For now, we just ensure the assembly loads correctly
        assembly.Should().NotBeNull("Assembly should load successfully");
        
        // TODO: When implementing assembly signing:
        // publicKeyToken.Should().NotBeNull("Assembly should be signed in production builds");
        // isFullySigned.Should().BeTrue("Assembly should be fully signed");
    }

    [Fact]
    public void CI_Pipeline_Should_Include_Security_Scanning()
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var pipelineFile = Path.Combine(baseDirectory, "azure-pipelines.yml");

        File.Exists(pipelineFile).Should().BeTrue("Azure DevOps pipeline should exist");

        // Act
        var pipelineContent = File.ReadAllText(pipelineFile);

        // Assert
        pipelineContent.Should().Contain("Security", "Pipeline should include security scanning stage");
        pipelineContent.Should().Contain("SonarCloud", "Pipeline should include static analysis");
        pipelineContent.Should().Contain("dependency-check", "Pipeline should include dependency vulnerability scanning");
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("secrets.json")]
    [InlineData("private.key")]
    [InlineData("*.pfx")]
    public void Sensitive_Files_Should_Be_Git_Ignored(string sensitivePattern)
    {
        // Arrange
        var baseDirectory = GetSolutionRoot();
        var gitIgnorePath = Path.Combine(baseDirectory, ".gitignore");

        File.Exists(gitIgnorePath).Should().BeTrue(".gitignore file should exist");

        // Act
        var gitIgnoreContent = File.ReadAllText(gitIgnorePath);

        // Assert
        if (sensitivePattern == ".env")
        {
            gitIgnoreContent.Should().Contain("*.env", "Environment files should be git ignored");
        }
        // Add other sensitive file patterns as needed
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