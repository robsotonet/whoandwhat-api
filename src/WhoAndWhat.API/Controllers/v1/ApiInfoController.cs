using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// API information and health endpoints for version 1
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ApiInfoController : ControllerBase
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the ApiInfoController
    /// </summary>
    /// <param name="configuration">Configuration service</param>
    public ApiInfoController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Get API information and status
    /// </summary>
    /// <returns>API information including version, status, and available endpoints</returns>
    /// <response code="200">Returns API information</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiInfoResponse), StatusCodes.Status200OK)]
    public ActionResult<ApiInfoResponse> GetApiInfo()
    {
        var apiInfo = new ApiInfoResponse
        {
            Name = _configuration["ApiSettings:Title"] ?? "WhoAndWhat Task Management API",
            Version = _configuration["ApiSettings:Version"] ?? "v1",
            Description = _configuration["ApiSettings:Description"] ?? "A comprehensive task management platform",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            AvailableEndpoints = new[]
            {
                "/api/v1/apiinfo",
                "/health",
                "/health/live",
                "/health/ready",
                "/swagger"
            }
        };

        return Ok(apiInfo);
    }

    /// <summary>
    /// Get API version information
    /// </summary>
    /// <returns>Version details</returns>
    /// <response code="200">Returns version information</response>
    [HttpGet("version")]
    [ProducesResponseType(typeof(VersionInfo), StatusCodes.Status200OK)]
    public ActionResult<VersionInfo> GetVersion()
    {
        var version = new VersionInfo
        {
            ApiVersion = "1.0",
            BuildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "local",
            BuildDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Framework = ".NET 9.0",
            SupportedVersions = new[] { "1.0" }
        };

        return Ok(version);
    }
}

/// <summary>
/// API information response model
/// </summary>
public class ApiInfoResponse
{
    /// <summary>
    /// API name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current API version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// API description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current API status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Response timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Current environment
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// List of available endpoints
    /// </summary>
    public string[] AvailableEndpoints { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Version information response model
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// API version
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// Build number
    /// </summary>
    public string BuildNumber { get; set; } = string.Empty;

    /// <summary>
    /// Build date
    /// </summary>
    public string BuildDate { get; set; } = string.Empty;

    /// <summary>
    /// Target framework
    /// </summary>
    public string Framework { get; set; } = string.Empty;

    /// <summary>
    /// Supported API versions
    /// </summary>
    public string[] SupportedVersions { get; set; } = Array.Empty<string>();
}
