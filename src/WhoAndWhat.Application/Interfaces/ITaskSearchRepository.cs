using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for task search operations with PostgreSQL full-text search
/// </summary>
public interface ITaskSearchRepository
{
    /// <summary>
    /// Performs a full-text search across tasks with filtering and pagination
    /// </summary>
    /// <param name="userId">User identifier to scope the search</param>
    /// <param name="criteria">Search criteria including query, filters, and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with relevance scoring and pagination metadata</returns>
    public Task<TaskSearchResult> SearchTasksAsync(Guid userId, AppTaskSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search suggestions and auto-completion for a partial query
    /// </summary>
    /// <param name="userId">User identifier to scope suggestions</param>
    /// <param name="query">Partial search query</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of search suggestions</returns>
    public Task<IEnumerable<string>> GetSearchSuggestionsAsync(Guid userId, string query, int maxSuggestions = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance metrics for search operations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search performance metrics</returns>
    public Task<SearchPerformanceMetrics> GetSearchMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search analytics for a specific user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="period">Time period for analytics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search analytics data</returns>
    public Task<SearchAnalytics> GetSearchAnalyticsAsync(Guid userId, TimeSpan period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a search query for analytics and performance tracking
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="criteria">Search criteria that was executed</param>
    /// <param name="resultCount">Number of results returned</param>
    /// <param name="duration">Time taken to execute the search</param>
    /// <param name="fromCache">Whether results came from cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public Task RecordSearchQueryAsync(Guid userId, AppTaskSearchCriteria criteria, int resultCount, TimeSpan duration, bool fromCache, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the search infrastructure is properly configured
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if search is properly configured, false otherwise</returns>
    public Task<bool> ValidateSearchConfigurationAsync(CancellationToken cancellationToken = default);
}
