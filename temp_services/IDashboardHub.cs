namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Interface for dashboard real-time communication
/// </summary>
public interface IDashboardHub
{
    /// <summary>
    /// Sends motivational content to a specific user
    /// </summary>
    Task SendMotivationalContentAsync(Guid userId, object content, CancellationToken cancellationToken = default);
}