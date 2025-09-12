namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Performance metrics for task search operations
/// </summary>
public class SearchPerformanceMetrics
{
    public long TotalSearchRequests { get; init; }
    public long CachedSearchHits { get; init; }
    public long DatabaseSearchHits { get; init; }
    public double CacheHitRatio => TotalSearchRequests > 0 ? (double)CachedSearchHits / TotalSearchRequests : 0.0;
    public TimeSpan AverageSearchDuration { get; init; }
    public TimeSpan AverageDatabaseQueryDuration { get; init; }
    public TimeSpan AverageCacheRetrievalDuration { get; init; }
    public DateTime MetricsStartTime { get; init; }
    public DateTime LastResetTime { get; init; }
    public SearchQueryStatistics QueryStatistics { get; init; } = new();
    public Dictionary<string, long> PopularSearchTerms { get; init; } = new();
    public Dictionary<string, long> ZeroResultQueries { get; init; } = new();
}

/// <summary>
/// Statistics about search query patterns
/// </summary>
public class SearchQueryStatistics
{
    public long FullTextSearchCount { get; init; }
    public long FilterOnlySearchCount { get; init; }
    public long CombinedSearchCount { get; init; }
    public double AverageResultsPerQuery { get; init; }
    public long EmptyResultCount { get; init; }
    public Dictionary<string, long> CategoryFilterUsage { get; init; } = new();
    public Dictionary<string, long> StatusFilterUsage { get; init; } = new();
    public Dictionary<string, long> PriorityFilterUsage { get; init; } = new();
    public Dictionary<string, long> SortingPreferences { get; init; } = new();
}

/// <summary>
/// Search analytics data for business intelligence
/// </summary>
public class SearchAnalytics
{
    public Guid UserId { get; init; }
    public SearchPerformanceMetrics Performance { get; init; } = new();
    public IEnumerable<PopularSearch> UserPopularSearches { get; init; } = new List<PopularSearch>();
    public IEnumerable<SearchTrend> SearchTrends { get; init; } = new List<SearchTrend>();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan AnalyticsPeriod { get; init; }
}

/// <summary>
/// Popular search query with usage statistics
/// </summary>
public class PopularSearch
{
    public string Query { get; init; } = string.Empty;
    public long SearchCount { get; init; }
    public double AverageResultCount { get; init; }
    public DateTime FirstSearched { get; init; }
    public DateTime LastSearched { get; init; }
    public TimeSpan AverageResponseTime { get; init; }
}

/// <summary>
/// Search trend data over time
/// </summary>
public class SearchTrend
{
    public DateTime Date { get; init; }
    public long SearchCount { get; init; }
    public long UniqueQueries { get; init; }
    public double AverageResultsPerSearch { get; init; }
    public TimeSpan AverageResponseTime { get; init; }
}
