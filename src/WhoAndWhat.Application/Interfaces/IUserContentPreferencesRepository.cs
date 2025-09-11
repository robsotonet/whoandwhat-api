using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for UserContentPreferences entities
/// </summary>
public interface IUserContentPreferencesRepository : IRepository<UserContentPreferences>
{
    /// <summary>
    /// Gets user content preferences by user ID
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User preferences or null if not found</returns>
    Task<UserContentPreferences?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates delivery count for today
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="deliveryCount">New delivery count</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateDeliveryCountAsync(
        Guid userId, 
        int deliveryCount, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets daily delivery count for all users (typically called daily)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of users reset</returns>
    Task<int> ResetDailyDeliveryCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users who can receive content at the current time
    /// </summary>
    /// <param name="currentHour">Current hour (0-23)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of users who can receive content</returns>
    Task<IEnumerable<UserContentPreferences>> GetUsersEligibleForContentAsync(
        int currentHour,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user engagement metrics
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="interactionType">Type of interaction (view, click, share, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateEngagementMetricsAsync(
        Guid userId,
        string interactionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users with high engagement scores for personalized content testing
    /// </summary>
    /// <param name="minScore">Minimum engagement score</param>
    /// <param name="count">Number of users to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of highly engaged users</returns>
    Task<IEnumerable<UserContentPreferences>> GetHighEngagementUsersAsync(
        double minScore = 0.7,
        int count = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates optimal delivery hours for a user based on engagement data
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="optimalHours">List of optimal hours (0-23)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateOptimalDeliveryHoursAsync(
        Guid userId,
        List<int> optimalHours,
        CancellationToken cancellationToken = default);
}