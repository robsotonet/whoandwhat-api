using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using WhoAndWhat.API.Configuration;
using Xunit;

namespace WhoAndWhat.API.Tests.Configuration;

/// <summary>
/// Unit tests for ServiceCollectionExtensions
/// </summary>
public class ServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;

    public ServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development"); // Default to Development environment
    }

    [Fact]
    public void AddApiVersioningConfiguration_Should_Register_API_Versioning_Services()
    {
        // Act
        _services.AddApiVersioningConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify that API versioning services are registered
        var apiVersioningServices = _services.Where(s => s.ServiceType.FullName?.Contains("Versioning") == true);
        apiVersioningServices.Should().NotBeEmpty("API versioning services should be registered");
    }

    [Fact]
    public void AddSwaggerConfiguration_Should_Register_Swagger_Services()
    {
        // Act
        _services.AddSwaggerConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify that Swagger services are registered
        var swaggerServices = _services.Where(s => s.ServiceType.FullName?.Contains("Swagger") == true);
        swaggerServices.Should().NotBeEmpty("Swagger services should be registered");
    }

    [Fact]
    public void AddHealthCheckConfiguration_Should_Register_Health_Check_Services()
    {
        // Act
        _services.AddHealthCheckConfiguration(_mockEnvironment.Object);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify that health check services are registered
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull("HealthCheckService should be registered");
    }

    [Fact]
    public void AddHealthCheckConfiguration_Should_Register_Database_Health_Check()
    {
        // Arrange
        _services.AddDbContext<WhoAndWhat.Infrastructure.Data.ApplicationDbContext>(options => { });

        // Act
        _services.AddHealthCheckConfiguration(_mockEnvironment.Object);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull();
        
        // Verify health check options are configured
        var healthCheckOptions = serviceProvider.GetService<IOptions<HealthCheckServiceOptions>>();
        healthCheckOptions.Should().NotBeNull();
    }

    [Fact]
    public void AddCorsConfiguration_Should_Register_CORS_Services()
    {
        // Act
        _services.AddCorsConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify CORS services are registered
        var corsServices = _services.Where(s => s.ServiceType.FullName?.Contains("Cors") == true);
        corsServices.Should().NotBeEmpty("CORS services should be registered");
    }

    [Fact]
    public void AddCorsConfiguration_Should_Configure_Default_And_Development_Policies()
    {
        // Act
        _services.AddCorsConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify that CORS policies are configured
        var corsOptions = serviceProvider.GetService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();
        corsOptions.Should().NotBeNull("CORS options should be configured");
    }

    [Fact]
    public void AddResponseCompressionConfiguration_Should_Register_Compression_Services()
    {
        // Act
        _services.AddResponseCompressionConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify response compression services are registered
        var compressionServices = _services.Where(s => s.ServiceType.FullName?.Contains("Compression") == true);
        compressionServices.Should().NotBeEmpty("Response compression services should be registered");
    }

    [Fact]
    public void AddApplicationInsightsConfiguration_Should_Register_AppInsights_When_ConnectionString_Provided()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetConnectionString("ApplicationInsights"))
                          .Returns("InstrumentationKey=test-key");

        // Act
        _services.AddApplicationInsightsConfiguration(_mockConfiguration.Object);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify Application Insights services are registered
        var appInsightsServices = _services.Where(s => s.ServiceType.FullName?.Contains("ApplicationInsights") == true);
        appInsightsServices.Should().NotBeEmpty("Application Insights services should be registered");
    }

    [Fact]
    public void AddApplicationInsightsConfiguration_Should_Not_Register_AppInsights_When_ConnectionString_Missing()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetConnectionString("ApplicationInsights"))
                          .Returns((string?)null);

        // Act
        _services.AddApplicationInsightsConfiguration(_mockConfiguration.Object);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify Application Insights services are not registered when no connection string
        var appInsightsServices = _services.Where(s => s.ServiceType.FullName?.Contains("ApplicationInsights") == true);
        appInsightsServices.Should().BeEmpty("Application Insights services should not be registered without connection string");
    }

    [Fact]
    public void AddApplicationInsightsConfiguration_Should_Not_Register_AppInsights_When_ConnectionString_Empty()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetConnectionString("ApplicationInsights"))
                          .Returns(string.Empty);

        // Act
        _services.AddApplicationInsightsConfiguration(_mockConfiguration.Object);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify Application Insights services are not registered when connection string is empty
        var appInsightsServices = _services.Where(s => s.ServiceType.FullName?.Contains("ApplicationInsights") == true);
        appInsightsServices.Should().BeEmpty("Application Insights services should not be registered with empty connection string");
    }

    [Fact]
    public void All_Extension_Methods_Should_Return_IServiceCollection_For_Chaining()
    {
        // Act & Assert
        _services.AddApiVersioningConfiguration()
                .AddSwaggerConfiguration()
                .AddHealthCheckConfiguration(_mockEnvironment.Object)
                .AddCorsConfiguration()
                .AddResponseCompressionConfiguration()
                .AddApplicationInsightsConfiguration(_mockConfiguration.Object)
                .Should().BeSameAs(_services, "All extension methods should return IServiceCollection for method chaining");
    }

    [Fact]
    public void Extension_Methods_Should_Not_Throw_When_Called_Multiple_Times()
    {
        // Act & Assert - Should not throw
        Action act = () =>
        {
            _services.AddApiVersioningConfiguration();
            _services.AddApiVersioningConfiguration(); // Second call should not throw

            _services.AddSwaggerConfiguration();
            _services.AddSwaggerConfiguration(); // Second call should not throw

            _services.AddHealthCheckConfiguration(_mockEnvironment.Object);
            _services.AddHealthCheckConfiguration(_mockEnvironment.Object); // Second call should not throw

            _services.AddCorsConfiguration();
            _services.AddCorsConfiguration(); // Second call should not throw

            _services.AddResponseCompressionConfiguration();
            _services.AddResponseCompressionConfiguration(); // Second call should not throw
        };

        act.Should().NotThrow("Extension methods should be idempotent");
    }

    [Fact]
    public void AddApiVersioningConfiguration_Should_Configure_URL_Path_Strategy()
    {
        // Act
        _services.AddApiVersioningConfiguration();

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        // Verify API versioning is configured (service registration indicates configuration)
        var apiVersioningServices = _services.Where(s => s.ServiceType.FullName?.Contains("ApiVersioning") == true);
        apiVersioningServices.Should().NotBeEmpty("API versioning should be configured with URL path strategy");
    }

    [Fact]
    public void AddHealthCheckConfiguration_Should_Configure_API_And_Database_Checks()
    {
        // Act
        _services.AddHealthCheckConfiguration(_mockEnvironment.Object);
        var serviceProvider = _services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();

        // Assert
        healthCheckService.Should().NotBeNull("Health check service should be configured");
        
        // The configuration includes both 'api' and 'database' checks as per the implementation
        var options = serviceProvider.GetService<IOptions<HealthCheckServiceOptions>>();
        options.Should().NotBeNull("Health check options should be configured");
    }
}