using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for MotivationalContent entities
/// </summary>
public interface IMotivationalContentRepository : IRepository<MotivationalContent>
{
    /// <summary>
    /// Gets the count of active (non-deactivated) motivational content
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of active content items</returns>
    public Task<int> GetActiveContentCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active motivational content filtered by type and category
    /// </summary>
    /// <param name="contentType">Optional content type filter</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of filtered active content</returns>
    public Task<IEnumerable<MotivationalContent>> GetActiveContentAsync(
        MotivationalContentType? contentType = null,
        ContentCategory? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content that matches specific targeting conditions
    /// </summary>
    /// <param name="targetConditions">Dictionary of targeting conditions to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching content</returns>
    public Task<IEnumerable<MotivationalContent>> GetContentByTargetingAsync(
        Dictionary<string, object> targetConditions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content scheduled for a specific time range
    /// </summary>
    /// <param name="startTime">Start of time range</param>
    /// <param name="endTime">End of time range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of scheduled content</returns>
    public Task<IEnumerable<MotivationalContent>> GetScheduledContentAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content with A/B testing enabled
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of A/B testing content</returns>
    public Task<IEnumerable<MotivationalContent>> GetABTestingContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content ordered by priority (highest first)
    /// </summary>
    /// <param name="count">Number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of high-priority content</returns>
    public Task<IEnumerable<MotivationalContent>> GetHighPriorityContentAsync(
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates content priority
    /// </summary>
    /// <param name="contentId">Content ID</param>
    /// <param name="newPriority">New priority value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update was successful</returns>
    public Task<bool> UpdateContentPriorityAsync(
        Guid contentId,
        int newPriority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates content (soft delete)
    /// </summary>
    /// <param name="contentId">Content ID</param>
    /// <param name="reason">Reason for deactivation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deactivation was successful</returns>
    public Task<bool> DeactivateContentAsync(
        Guid contentId,
        string reason,
        CancellationToken cancellationToken = default);
}
