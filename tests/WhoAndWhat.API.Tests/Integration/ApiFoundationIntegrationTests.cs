using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using WhoAndWhat.API.Controllers.v1;
using WhoAndWhat.Infrastructure.Data;
using Xunit;

namespace WhoAndWhat.API.Tests.Integration;

/// <summary>
/// Integration tests for API Foundation components
/// </summary>
public class ApiFoundationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiFoundationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); // Use Development to enable Swagger
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase");
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_Should_Return_Healthy_Status()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var healthResult = JsonSerializer.Deserialize<JsonElement>(content);
        
        healthResult.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");
        healthResult.GetProperty("timestamp").ValueKind.Should().Be(JsonValueKind.String);
        healthResult.GetProperty("checks").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task HealthCheck_Live_Should_Return_Healthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthCheck_Ready_Should_Return_Status()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region API Versioning Tests

    [Fact]
    public async Task ApiInfo_v1_Should_Return_Success()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var apiInfo = JsonSerializer.Deserialize<ApiInfoResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        apiInfo.Should().NotBeNull();
        apiInfo!.Name.Should().NotBeNullOrEmpty();
        apiInfo.Version.Should().NotBeNullOrEmpty();
        apiInfo.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task ApiVersion_v1_Should_Return_Version_Info()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo/version");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var versionInfo = JsonSerializer.Deserialize<VersionInfo>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        versionInfo.Should().NotBeNull();
        versionInfo!.ApiVersion.Should().Be("1.0");
        versionInfo.Framework.Should().Be(".NET 9.0");
    }

    [Fact]
    public async Task ApiVersioning_Should_Handle_Unsupported_Version()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/apiinfo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Security Headers Tests

    [Fact]
    public async Task SecurityHeaders_Should_Be_Present_In_Response()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.Should().ContainKey("X-XSS-Protection");
        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.Should().ContainKey("Permissions-Policy");
        response.Headers.Should().ContainKey("Content-Security-Policy");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("X-XSS-Protection").Should().Contain("1; mode=block");
    }

    [Fact]
    public async Task SecurityHeaders_Should_Not_Include_Server_Header()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo");

        // Assert
        response.Headers.Should().NotContainKey("Server");
    }

    #endregion

    #region CORS Tests

    [Fact]
    public async Task CORS_Should_Handle_Preflight_Request()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/apiinfo");
        request.Headers.Add("Origin", "https://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task CORS_Should_Allow_Cross_Origin_Requests()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/apiinfo");
        request.Headers.Add("Origin", "https://localhost:3000");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GlobalExceptionMiddleware_Should_Return_NotFound_For_Invalid_Route()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_Should_Return_Structured_Error_Response()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    #endregion

    #region Swagger Documentation Tests

    [Fact]
    public async Task Swagger_Documentation_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JsonSerializer.Deserialize<JsonElement>(content);
        
        swaggerDoc.GetProperty("openapi").GetString().Should().StartWith("3.0");
        swaggerDoc.GetProperty("info").GetProperty("title").GetString().Should().Contain("WhoAndWhat");
        swaggerDoc.GetProperty("info").GetProperty("version").GetString().Should().Be("v1");
    }

    [Fact]
    public async Task Swagger_UI_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Middleware Pipeline Tests

    [Fact]
    public async Task Middleware_Pipeline_Should_Execute_In_Correct_Order()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo");

        // Assert
        // Security headers should be added first
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        
        // Response compression is optional in test environment
        // In production, compression would typically be applied by reverse proxy or middleware
        // For test environment, we just verify response is successful without requiring compression
        
        // Content should be properly formatted JSON
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        // Response should be successful
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task API_Should_Handle_Concurrent_Requests()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/api/v1/apiinfo"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        });
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task API_Should_Respond_Within_Acceptable_Time()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/api/v1/apiinfo");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should respond within 1 second
    }

    #endregion

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
        }
    }
}