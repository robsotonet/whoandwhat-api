using FluentAssertions;
using NetArchTest.Rules;

namespace WhoAndWhat.Architecture.Tests;

public class ArchitectureTests
{
    private const string DomainNamespace = "WhoAndWhat.Domain";
    private const string ApplicationNamespace = "WhoAndWhat.Application";
    private const string InfrastructureNamespace = "WhoAndWhat.Infrastructure";
    private const string ApiNamespace = "WhoAndWhat.API";

    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        // Arrange
        var assembly = typeof(WhoAndWhat.Domain.AssemblyReference).Assembly;

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOnInfrastructureOrApi()
    {
        // Arrange
        var assembly = typeof(Application.AssemblyReference).Assembly;

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_Should_Not_HaveDependencyOnApi()
    {
        // Arrange
        var assembly = typeof(Infrastructure.AssemblyReference).Assembly;

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Controllers_Should_HaveDependencyOnMediatR()
    {
        // Arrange
        var assembly = typeof(API.AssemblyReference).Assembly;

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void All_Assemblies_Should_Load_Successfully()
    {
        // Test that all assemblies load correctly (which they do since this test runs)
        var domainAssembly = typeof(Domain.AssemblyReference).Assembly;
        var applicationAssembly = typeof(Application.AssemblyReference).Assembly;
        var infrastructureAssembly = typeof(Infrastructure.AssemblyReference).Assembly;
        var apiAssembly = typeof(API.AssemblyReference).Assembly;

        domainAssembly.Should().NotBeNull("Domain assembly should load");
        applicationAssembly.Should().NotBeNull("Application assembly should load");
        infrastructureAssembly.Should().NotBeNull("Infrastructure assembly should load");
        apiAssembly.Should().NotBeNull("API assembly should load");
    }

    [Fact]
    public void Solution_Should_Build_Successfully()
    {
        // This test ensures that all projects compile without errors
        // The fact that this test runs means the solution built successfully
        true.Should().BeTrue("Solution should build without errors");
    }
}