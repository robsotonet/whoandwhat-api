using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for UserSchedulingPreference entities
/// Provides data access methods for user scheduling preferences
/// </summary>
public interface IUserSchedulingPreferenceRepository : IRepository<UserSchedulingPreference>
{
    /// <summary>
    /// Gets scheduling preferences for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User scheduling preferences or null if not found</returns>
    public Task<UserSchedulingPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates default preferences for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User scheduling preferences (existing or newly created)</returns>
    public Task<UserSchedulingPreference> GetOrCreateDefaultAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user preferences with new values
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="preferences">Updated preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences</returns>
    public Task<UserSchedulingPreference> UpdatePreferencesAsync(Guid userId, UserSchedulingPreference preferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users who have scheduling preferences that need reanalysis
    /// </summary>
    /// <param name="threshold">Reanalysis threshold (default: 7 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User preferences needing reanalysis</returns>
    public Task<IEnumerable<UserSchedulingPreference>> GetPreferencesNeedingReanalysisAsync(TimeSpan? threshold = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users with similar productivity patterns for machine learning
    /// </summary>
    /// <param name="userId">The reference user ID</param>
    /// <param name="similarityThreshold">Minimum similarity score (0.0 to 1.0)</param>
    /// <param name="maxResults">Maximum number of similar users to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Users with similar patterns</returns>
    public Task<IEnumerable<UserSchedulingPreference>> GetSimilarUsersAsync(Guid userId, double similarityThreshold = 0.7, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates productivity scores for multiple users in batch
    /// </summary>
    /// <param name="updates">Dictionary of user ID to productivity score</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of preferences updated</returns>
    public Task<int> BulkUpdateProductivityScoresAsync(Dictionary<Guid, double> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets preferences for users within specific working hours range
    /// </summary>
    /// <param name="startTimeRange">Start time range (inclusive)</param>
    /// <param name="endTimeRange">End time range (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preferences within working hours range</returns>
    public Task<IEnumerable<UserSchedulingPreference>> GetByWorkingHoursRangeAsync(TimeSpan startTimeRange, TimeSpan endTimeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets preferences for users with specific productivity patterns
    /// </summary>
    /// <param name="productivityPattern">The productivity pattern to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preferences with matching productivity pattern</returns>
    public Task<IEnumerable<UserSchedulingPreference>> GetByProductivityPatternAsync(int productivityPattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if user has custom scheduling preferences (not default)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has customized preferences</returns>
    public Task<bool> HasCustomPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated statistics about user preferences
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preference statistics</returns>
    public Task<Dictionary<string, object>> GetPreferenceStatisticsAsync(CancellationToken cancellationToken = default);
}
