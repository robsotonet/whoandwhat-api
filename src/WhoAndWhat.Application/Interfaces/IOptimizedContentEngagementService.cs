using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service interface for optimized content engagement and personalization
/// </summary>
public interface IOptimizedContentEngagementService
{
    /// <summary>
    /// Gets personalized motivational content for a user based on their preferences and engagement history
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="count">Number of content items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of personalized content ordered by relevance</returns>
    public Task<List<MotivationalContent>> GetPersonalizedContentAsync(
        Guid userId, 
        int count = 3, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes content for engagement based on user interaction data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of content items optimized</returns>
    public Task<int> OptimizeContentForEngagementAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records user interaction with content for future optimization
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contentId">The content ID</param>
    /// <param name="interactionType">Type of interaction (view, click, share, dismiss)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if interaction was recorded successfully</returns>
    public Task<bool> RecordUserInteractionAsync(
        Guid userId,
        Guid contentId,
        string interactionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes user behavior patterns and updates preferences automatically
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if preferences were updated</returns>
    public Task<bool> UpdateUserPreferencesFromBehaviorAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content performance analytics
    /// </summary>
    /// <param name="startDate">Start date for analytics period</param>
    /// <param name="endDate">End date for analytics period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of content performance metrics</returns>
    public Task<Dictionary<string, object>> GetContentPerformanceAnalyticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}