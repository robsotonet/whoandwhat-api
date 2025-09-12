using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using TaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of task search with full-text search capabilities
/// </summary>
public class TaskSearchRepository : ITaskSearchRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TaskSearchRepository> _logger;

    // Performance tracking
    private long _totalSearchRequests = 0;
    private long _cachedSearchHits = 0;
    private long _databaseSearchHits = 0;
    private readonly List<long> _searchResponseTimes = new();
    private readonly Dictionary<string, long> _popularSearchTerms = new();
    private readonly Dictionary<string, long> _zeroResultQueries = new();
    private readonly Dictionary<string, long> _categoryFilterUsage = new();
    private readonly Dictionary<string, long> _statusFilterUsage = new();
    private readonly Dictionary<string, long> _priorityFilterUsage = new();
    private readonly Dictionary<string, long> _sortingPreferences = new();
    private readonly DateTime _metricsStartTime = DateTime.UtcNow;

    public TaskSearchRepository(ApplicationDbContext context, ILogger<TaskSearchRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskSearchResult> SearchTasksAsync(Guid userId, AppTaskSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var normalizedCriteria = criteria.Normalize();

        try
        {
            _logger.LogDebug("Starting task search for user {UserId} with query: {Query}", userId, normalizedCriteria.Query);

            // Build the base query
            var query = _context.Tasks.Where(t => t.UserId == userId);

            // Apply full-text search if query is provided
            if (normalizedCriteria.IsFullTextSearch)
            {
                var language = GetSearchLanguage(normalizedCriteria.SearchTerm);
                query = ApplyFullTextSearch(query, normalizedCriteria.SearchTerm, language);
            }

            // Apply filters
            query = ApplyFilters(query, normalizedCriteria);

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting and pagination
            var sortedQuery = ApplySorting(query, normalizedCriteria);
            var paginatedQuery = sortedQuery
                .Skip(normalizedCriteria.Offset)
                .Take(normalizedCriteria.PageSize);

            // Execute query with relevance scoring if full-text search
            List<TaskSearchItem> searchItems;
            if (normalizedCriteria.IsFullTextSearch)
            {
                searchItems = await ExecuteFullTextSearchQuery(paginatedQuery, normalizedCriteria.SearchTerm, cancellationToken);
            }
            else
            {
                var tasks = await paginatedQuery.ToListAsync(cancellationToken);
                searchItems = tasks.Select(t => TaskSearchItem.FromTask(t, 1.0)).ToList();
            }

            stopwatch.Stop();

            // Record analytics
            await RecordSearchQueryAsync(userId, normalizedCriteria, totalCount, stopwatch.Elapsed, false, cancellationToken);

            var result = TaskSearchResult.Create(
                searchItems,
                totalCount,
                normalizedCriteria.PageNumber,
                normalizedCriteria.PageSize,
                stopwatch.Elapsed,
                normalizedCriteria.SearchTerm,
                new SearchResultMetadata
                {
                    FromCache = false,
                    DatabaseHits = 1,
                    DatabaseDuration = stopwatch.Elapsed,
                    SearchExecutedAt = DateTime.UtcNow
                });

            _logger.LogDebug("Task search completed for user {UserId}. Found {TotalCount} results in {Duration}ms",
                userId, totalCount, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing task search for user {UserId} with query: {Query}", userId, normalizedCriteria.SearchTerm);
            return TaskSearchResult.Empty(normalizedCriteria.SearchTerm, stopwatch.Elapsed);
        }
    }

    public async Task<IEnumerable<string>> GetSearchSuggestionsAsync(Guid userId, string query, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return new List<string>();
        }

        try
        {
            var sanitizedQuery = query.Trim().ToLowerInvariant();

            // Use trigram similarity search for suggestions
            var suggestions = await _context.Database
                .SqlQuery<string>($@"
                    SELECT DISTINCT ""Title""
                    FROM ""Tasks""
                    WHERE ""UserId"" = {userId}
                    AND similarity(lower(""Title""), {sanitizedQuery}) > 0.3
                    ORDER BY similarity(lower(""Title""), {sanitizedQuery}) DESC
                    LIMIT {maxSuggestions}")
                .ToListAsync(cancellationToken);

            // Also get suggestions from descriptions if we need more
            if (suggestions.Count < maxSuggestions)
            {
                var descriptionSuggestions = await _context.Database
                    .SqlQuery<string>($@"
                        SELECT DISTINCT ""Description""
                        FROM ""Tasks""
                        WHERE ""UserId"" = {userId}
                        AND ""Description"" IS NOT NULL
                        AND ""Description"" != ''
                        AND similarity(lower(""Description""), {sanitizedQuery}) > 0.2
                        ORDER BY similarity(lower(""Description""), {sanitizedQuery}) DESC
                        LIMIT {maxSuggestions - suggestions.Count}")
                    .ToListAsync(cancellationToken);

                suggestions.AddRange(descriptionSuggestions.Where(d => !string.IsNullOrWhiteSpace(d)));
            }

            return suggestions.Take(maxSuggestions).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search suggestions for user {UserId} with query: {Query}", userId, query);
            return new List<string>();
        }
    }

    public async Task<SearchPerformanceMetrics> GetSearchMetricsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Async method for future extensibility

        return new SearchPerformanceMetrics
        {
            TotalSearchRequests = Interlocked.Read(ref _totalSearchRequests),
            CachedSearchHits = Interlocked.Read(ref _cachedSearchHits),
            DatabaseSearchHits = Interlocked.Read(ref _databaseSearchHits),
            AverageSearchDuration = CalculateAverageSearchDuration(),
            AverageDatabaseQueryDuration = CalculateAverageSearchDuration(), // Same for now
            AverageCacheRetrievalDuration = TimeSpan.Zero, // Not implemented yet
            MetricsStartTime = _metricsStartTime,
            LastResetTime = _metricsStartTime,
            QueryStatistics = GetQueryStatistics(),
            PopularSearchTerms = new Dictionary<string, long>(_popularSearchTerms),
            ZeroResultQueries = new Dictionary<string, long>(_zeroResultQueries)
        };
    }

    public async Task<SearchAnalytics> GetSearchAnalyticsAsync(Guid userId, TimeSpan period, CancellationToken cancellationToken = default)
    {
        // This would typically query a search analytics table
        // For now, return basic analytics
        await Task.CompletedTask;

        return new SearchAnalytics
        {
            UserId = userId,
            Performance = await GetSearchMetricsAsync(cancellationToken),
            UserPopularSearches = new List<PopularSearch>(),
            SearchTrends = new List<SearchTrend>(),
            GeneratedAt = DateTime.UtcNow,
            AnalyticsPeriod = period
        };
    }

    public async Task RecordSearchQueryAsync(Guid userId, AppTaskSearchCriteria criteria, int resultCount, TimeSpan duration, bool fromCache, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Async for future database logging

        // Increment counters
        Interlocked.Increment(ref _totalSearchRequests);

        if (fromCache)
        {
            Interlocked.Increment(ref _cachedSearchHits);
        }
        else
        {
            Interlocked.Increment(ref _databaseSearchHits);
        }

        // Record response time
        lock (_searchResponseTimes)
        {
            _searchResponseTimes.Add(duration.Ticks);
            if (_searchResponseTimes.Count > 1000)
            {
                _searchResponseTimes.RemoveRange(0, _searchResponseTimes.Count - 1000);
            }
        }

        // Track popular search terms
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var normalizedQuery = criteria.Query.ToLowerInvariant().Trim();
            lock (_popularSearchTerms)
            {
                _popularSearchTerms.TryGetValue(normalizedQuery, out var count);
                _popularSearchTerms[normalizedQuery] = count + 1;
            }

            // Track zero result queries
            if (resultCount == 0)
            {
                lock (_zeroResultQueries)
                {
                    _zeroResultQueries.TryGetValue(normalizedQuery, out var zeroCount);
                    _zeroResultQueries[normalizedQuery] = zeroCount + 1;
                }
            }
        }

        // Track filter usage
        TrackFilterUsage(criteria);
    }

    public async Task<bool> ValidateSearchConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if pg_trgm extension is available
            var trgmCheck = await _context.Database
                .SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm')")
                .FirstOrDefaultAsync(cancellationToken);

            // Check if full-text search indexes exist
            var indexCheck = await _context.Database
                .SqlQuery<long>($@"
                    SELECT COUNT(*) FROM pg_indexes 
                    WHERE tablename = 'Tasks' 
                    AND indexname IN ('idx_tasks_fulltext_search_english', 'idx_tasks_fulltext_search_spanish')")
                .FirstOrDefaultAsync(cancellationToken);

            var isValid = trgmCheck && indexCheck >= 2;

            _logger.LogInformation("Search configuration validation: pg_trgm={PgTrgm}, Indexes={IndexCount}, Valid={IsValid}",
                trgmCheck, indexCheck, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating search configuration");
            return false;
        }
    }

    private IQueryable<Domain.Entities.AppTask> ApplyFullTextSearch(IQueryable<Domain.Entities.AppTask> query, string searchQuery, string language)
    {
        var sanitizedQuery = searchQuery.Replace("'", "''"); // Basic SQL injection protection

        // Use raw SQL for full-text search with ranking
        return query.Where(t =>
            EF.Functions.ToTsVector(language, t.Title + " " + (t.Description ?? ""))
                .Matches(EF.Functions.ToTsQuery(language, sanitizedQuery)));
    }

    private IQueryable<Domain.Entities.AppTask> ApplyFilters(IQueryable<Domain.Entities.AppTask> query, AppTaskSearchCriteria criteria)
    {
        if (criteria.Category != null)
        {
            query = query.Where(t => t.Category == (int)criteria.Category);
        }

        if (criteria.Status != null)
        {
            query = query.Where(t => t.Status == (int)criteria.Status);
        }

        if (criteria.Priority != null)
        {
            query = query.Where(t => t.Priority == (int)criteria.Priority);
        }

        if (criteria.DueDateFrom.HasValue)
        {
            query = query.Where(t => t.DueDate >= criteria.DueDateFrom.Value);
        }

        if (criteria.DueDateTo.HasValue)
        {
            query = query.Where(t => t.DueDate <= criteria.DueDateTo.Value);
        }

        if (criteria.CreatedAfter.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= criteria.CreatedAfter.Value);
        }

        if (criteria.CreatedBefore.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= criteria.CreatedBefore.Value);
        }

        if (!criteria.IncludeCompleted)
        {
            query = query.Where(t => t.Status != (int)TaskStatus.Completed);
        }

        // Note: IncludeArchived filter would be implemented when archiving is added

        return query;
    }

    private IQueryable<Domain.Entities.AppTask> ApplySorting(IQueryable<Domain.Entities.AppTask> query, AppTaskSearchCriteria criteria)
    {
        return criteria.SortBy switch
        {
            TaskSearchSortBy.Relevance => criteria.IsFullTextSearch
                ? query // Relevance sorting is handled in the full-text search query
                : query.OrderByDescending(t => t.UpdatedAt),
            TaskSearchSortBy.CreatedAt => criteria.SortDescending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),
            TaskSearchSortBy.UpdatedAt => criteria.SortDescending
                ? query.OrderByDescending(t => t.UpdatedAt)
                : query.OrderBy(t => t.UpdatedAt),
            TaskSearchSortBy.DueDate => criteria.SortDescending
                ? query.OrderByDescending(t => t.DueDate)
                : query.OrderBy(t => t.DueDate),
            TaskSearchSortBy.Priority => criteria.SortDescending
                ? query.OrderByDescending(t => t.Priority)
                : query.OrderBy(t => t.Priority),
            TaskSearchSortBy.Title => criteria.SortDescending
                ? query.OrderByDescending(t => t.Title)
                : query.OrderBy(t => t.Title),
            _ => query.OrderByDescending(t => t.UpdatedAt)
        };
    }

    private async Task<List<TaskSearchItem>> ExecuteFullTextSearchQuery(IQueryable<Domain.Entities.AppTask> query, string searchQuery, CancellationToken cancellationToken)
    {
        var tasks = await query.ToListAsync(cancellationToken);

        // For now, return with default relevance score
        // In a more advanced implementation, we'd extract the ts_rank from the query
        return tasks.Select(t => TaskSearchItem.FromTask(t, 1.0, ExtractMatchedTerms(t, searchQuery))).ToList();
    }

    private IEnumerable<string> ExtractMatchedTerms(Domain.Entities.AppTask task, string searchQuery)
    {
        var searchTerms = searchQuery.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchedTerms = new List<string>();

        foreach (var term in searchTerms)
        {
            if (task.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (task.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                matchedTerms.Add(term);
            }
        }

        return matchedTerms;
    }

    private string GetSearchLanguage(string query)
    {
        // Simple language detection - in a real implementation, you might use a more sophisticated approach
        var spanishWords = new[] { "el", "la", "de", "que", "y", "en", "un", "es", "se", "no", "te", "lo", "le", "da", "su", "por", "son", "con", "para", "al", "una", "del", "todo", "está", "muy", "fue", "han", "era", "sobre", "ser", "tiene", "hasta", "sin", "entre", "está" };
        var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var spanishMatches = words.Count(word => spanishWords.Contains(word));
        var spanishRatio = words.Length > 0 ? (double)spanishMatches / words.Length : 0;

        return spanishRatio > 0.3 ? "spanish" : "english";
    }

    private TimeSpan CalculateAverageSearchDuration()
    {
        lock (_searchResponseTimes)
        {
            if (_searchResponseTimes.Count == 0)
            {
                return TimeSpan.Zero;
            }

            var averageTicks = _searchResponseTimes.Sum() / _searchResponseTimes.Count;
            return new TimeSpan(averageTicks);
        }
    }

    private SearchQueryStatistics GetQueryStatistics()
    {
        return new SearchQueryStatistics
        {
            FullTextSearchCount = _totalSearchRequests, // Simplified for now
            CategoryFilterUsage = new Dictionary<string, long>(_categoryFilterUsage),
            StatusFilterUsage = new Dictionary<string, long>(_statusFilterUsage),
            PriorityFilterUsage = new Dictionary<string, long>(_priorityFilterUsage),
            SortingPreferences = new Dictionary<string, long>(_sortingPreferences)
        };
    }

    private void TrackFilterUsage(AppTaskSearchCriteria criteria)
    {
        if (criteria.Category != null)
        {
            var category = criteria.Category.ToString();
            lock (_categoryFilterUsage)
            {
                _categoryFilterUsage.TryGetValue(category, out var count);
                _categoryFilterUsage[category] = count + 1;
            }
        }

        if (criteria.Status != null)
        {
            var status = criteria.Status.ToString();
            lock (_statusFilterUsage)
            {
                _statusFilterUsage.TryGetValue(status, out var count);
                _statusFilterUsage[status] = count + 1;
            }
        }

        if (criteria.Priority != null)
        {
            var priority = criteria.Priority.ToString();
            lock (_priorityFilterUsage)
            {
                _priorityFilterUsage.TryGetValue(priority, out var count);
                _priorityFilterUsage[priority] = count + 1;
            }
        }

        var sortBy = criteria.SortBy.ToString();
        lock (_sortingPreferences)
        {
            _sortingPreferences.TryGetValue(sortBy, out var count);
            _sortingPreferences[sortBy] = count + 1;
        }
    }
}
