namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Simple message response for API operations
/// </summary>
public record MessageResponse
{
    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Additional data if needed
    /// </summary>
    public object? Data { get; init; }
}