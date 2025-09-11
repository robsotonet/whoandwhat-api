using System.Net;
using System.Text.Json;

namespace WhoAndWhat.API.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>
    /// Initializes the global exception middleware
    /// </summary>
    /// <param name="next">Next middleware in pipeline</param>
    /// <param name="logger">Logger instance</param>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware
    /// </summary>
    /// <param name="context">HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ArgumentException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INVALID_ARGUMENT",
                    Message = "The provided argument is invalid.",
                    Details = exception.Message
                };
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "UNAUTHORIZED",
                    Message = "Access denied. Please authenticate.",
                    Details = "You must be authenticated to access this resource."
                };
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = "The requested resource was not found.",
                    Details = exception.Message
                };
                break;

            case InvalidOperationException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INVALID_OPERATION",
                    Message = "The requested operation is not valid in the current state.",
                    Details = exception.Message
                };
                break;

            case TimeoutException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "REQUEST_TIMEOUT",
                    Message = "The request timed out.",
                    Details = "Please try again later."
                };
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INTERNAL_SERVER_ERROR",
                    Message = "An unexpected error occurred.",
                    Details = "Please contact support if this problem persists."
                };
                break;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var result = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await response.WriteAsync(result);
    }
}

/// <summary>
/// Structured error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Trace identifier for request correlation
    /// </summary>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Timestamp when error occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// Error detail information
    /// </summary>
    public ErrorDetail Error { get; set; } = new();
}

/// <summary>
/// Error detail information
/// </summary>
public class ErrorDetail
{
    /// <summary>
    /// Error code identifier
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// User-friendly error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Detailed error information
    /// </summary>
    public string Details { get; set; } = string.Empty;
}
