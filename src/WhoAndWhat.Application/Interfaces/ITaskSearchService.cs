using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Application service interface for task search operations with caching and analytics
/// </summary>
public interface ITaskSearchService
{
    /// <summary>
    /// Performs a comprehensive task search with caching and analytics tracking
    /// </summary>
    /// <param name="userId">User identifier to scope the search</param>
    /// <param name="criteria">Search criteria including query, filters, and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with caching metadata and performance information</returns>
    public Task<TaskSearchResult> SearchTasksAsync(Guid userId, TaskSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached search suggestions with auto-completion
    /// </summary>
    /// <param name="userId">User identifier to scope suggestions</param>
    /// <param name="query">Partial search query</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of search suggestions</returns>
    public Task<IEnumerable<string>> GetSearchSuggestionsAsync(Guid userId, string query, int maxSuggestions = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive search analytics for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="period">Time period for analytics (defaults to last 30 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search analytics including trends and popular queries</returns>
    public Task<SearchAnalytics> GetSearchAnalyticsAsync(Guid userId, TimeSpan? period = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system-wide search performance metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search performance metrics</returns>
    public Task<SearchPerformanceMetrics> GetSearchPerformanceMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears search cache for a specific user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache was cleared successfully</returns>
    public Task<bool> ClearUserSearchCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates search criteria and returns validation results
    /// </summary>
    /// <param name="criteria">Search criteria to validate</param>
    /// <returns>Validation result with any errors</returns>
    public SearchValidationResult ValidateSearchCriteria(TaskSearchCriteria criteria);

    /// <summary>
    /// Preloads search cache with popular queries for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of cache entries preloaded</returns>
    public Task<int> WarmSearchCacheAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of search criteria validation
/// </summary>
public class SearchValidationResult
{
    public bool IsValid { get; init; }
    public IEnumerable<string> Errors { get; init; } = new List<string>();
    public TaskSearchCriteria? NormalizedCriteria { get; init; }

    public static SearchValidationResult Valid(TaskSearchCriteria normalizedCriteria)
    {
        return new SearchValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            NormalizedCriteria = normalizedCriteria
        };
    }

    public static SearchValidationResult Invalid(IEnumerable<string> errors)
    {
        return new SearchValidationResult
        {
            IsValid = false,
            Errors = errors,
            NormalizedCriteria = null
        };
    }
}