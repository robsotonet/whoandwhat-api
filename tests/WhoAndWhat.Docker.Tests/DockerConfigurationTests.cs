using FluentAssertions;
using Testcontainers.PostgreSql;

namespace WhoAndWhat.Docker.Tests;

public class DockerConfigurationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;

    public DockerConfigurationTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("WhoAndWhat")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:15-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
    }

    [Fact]
    public void Dockerfile_Should_Exist()
    {
        // Arrange
        var dockerfilePath = Path.Combine(GetSolutionRoot(), "Dockerfile");

        // Act & Assert
        File.Exists(dockerfilePath).Should().BeTrue("Dockerfile should exist in solution root");
    }

    [Fact]
    public void DockerCompose_Files_Should_Exist()
    {
        // Arrange
        var solutionRoot = GetSolutionRoot();
        var dockerComposeFile = Path.Combine(solutionRoot, "docker-compose.yml");
        var dockerComposeOverrideFile = Path.Combine(solutionRoot, "docker-compose.override.yml");
        var dockerComposeProdFile = Path.Combine(solutionRoot, "docker-compose.prod.yml");

        // Act & Assert
        File.Exists(dockerComposeFile).Should().BeTrue("docker-compose.yml should exist");
        File.Exists(dockerComposeOverrideFile).Should().BeTrue("docker-compose.override.yml should exist");
        File.Exists(dockerComposeProdFile).Should().BeTrue("docker-compose.prod.yml should exist");
    }

    [Fact]
    public void DockerIgnore_Should_Exist()
    {
        // Arrange
        var dockerIgnorePath = Path.Combine(GetSolutionRoot(), ".dockerignore");

        // Act & Assert
        File.Exists(dockerIgnorePath).Should().BeTrue(".dockerignore should exist");
    }

    [Fact]
    public void Environment_Files_Should_Exist()
    {
        // Arrange
        var solutionRoot = GetSolutionRoot();
        var envExampleFile = Path.Combine(solutionRoot, ".env.example");
        var envDockerFile = Path.Combine(solutionRoot, ".env.docker");

        // Act & Assert
        File.Exists(envExampleFile).Should().BeTrue(".env.example should exist");
        File.Exists(envDockerFile).Should().BeTrue(".env.docker should exist");
    }

    [Fact]
    public void Database_Init_Scripts_Should_Exist()
    {
        // Arrange
        var solutionRoot = GetSolutionRoot();
        var initScriptPath = Path.Combine(solutionRoot, "database", "init", "01-create-extensions.sql");
        var devInitScriptPath = Path.Combine(solutionRoot, "database", "dev-init", "02-create-dev-data.sql");

        // Act & Assert
        File.Exists(initScriptPath).Should().BeTrue("Database init script should exist");
        File.Exists(devInitScriptPath).Should().BeTrue("Development init script should exist");
    }

    [Fact]
    public void PostgreSQL_Container_Should_Start_Successfully()
    {
        // Act & Assert
        // If we reach this point, the container started successfully in InitializeAsync
        true.Should().BeTrue("Container should start successfully");
    }

    [Fact]
    public void PostgreSQL_Container_Should_Accept_Connections()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();

        // Act & Assert
        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain("Database=WhoAndWhat");
        connectionString.Should().Contain("Username=postgres");
    }

    [Fact]
    public void DockerCompose_Should_Contain_Required_Services()
    {
        // Arrange
        var dockerComposeContent = File.ReadAllText(Path.Combine(GetSolutionRoot(), "docker-compose.yml"));

        // Act & Assert
        dockerComposeContent.Should().Contain("api:");
        dockerComposeContent.Should().Contain("db:");
        dockerComposeContent.Should().Contain("redis:");
        dockerComposeContent.Should().Contain("pgadmin:");
        dockerComposeContent.Should().Contain("postgres:15-alpine");
        dockerComposeContent.Should().Contain("redis:7-alpine");
    }

    [Fact]
    public void DockerCompose_Should_Have_Proper_Port_Mapping()
    {
        // Arrange
        var dockerComposeContent = File.ReadAllText(Path.Combine(GetSolutionRoot(), "docker-compose.yml"));

        // Act & Assert
        dockerComposeContent.Should().Contain("\"5000:8080\"");  // API HTTP
        dockerComposeContent.Should().Contain("\"5432:5432\"");  // PostgreSQL
        dockerComposeContent.Should().Contain("\"6379:6379\"");  // Redis
        dockerComposeContent.Should().Contain("\"8080:80\"");    // pgAdmin
    }

    [Fact]
    public void DockerCompose_Should_Have_Health_Checks()
    {
        // Arrange
        var dockerComposeContent = File.ReadAllText(Path.Combine(GetSolutionRoot(), "docker-compose.yml"));

        // Act & Assert
        dockerComposeContent.Should().Contain("healthcheck:");
        dockerComposeContent.Should().Contain("pg_isready");
        dockerComposeContent.Should().Contain("redis-cli\", \"ping");
    }

    [Fact]
    public void Dockerfile_Should_Use_Multi_Stage_Build()
    {
        // Arrange
        var dockerfileContent = File.ReadAllText(Path.Combine(GetSolutionRoot(), "Dockerfile"));

        // Act & Assert
        dockerfileContent.Should().Contain("FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build");
        dockerfileContent.Should().Contain("FROM build AS publish");
        dockerfileContent.Should().Contain("FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final");
    }

    [Fact]
    public void Dockerfile_Should_Have_Security_Configurations()
    {
        // Arrange
        var dockerfileContent = File.ReadAllText(Path.Combine(GetSolutionRoot(), "Dockerfile"));

        // Act & Assert
        dockerfileContent.Should().Contain("RUN groupadd -r dotnet");
        dockerfileContent.Should().Contain("USER dotnet");
        dockerfileContent.Should().Contain("HEALTHCHECK");
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
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        return directory.FullName;
    }
}
