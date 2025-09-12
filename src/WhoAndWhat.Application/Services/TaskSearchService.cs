using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Configuration;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using TaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.Services;

/// <summary>
/// Application service for task search with Redis caching and analytics
/// </summary>
public class TaskSearchService : ITaskSearchService
{
    private readonly ITaskSearchRepository _searchRepository;
    private readonly ITaskCacheService _cacheService;
    private readonly IDistributedCache _distributedCache;
    private readonly ICacheSettings _cacheSettings;
    private readonly ILogger<TaskSearchService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Cache key prefixes
    private const string SearchResultsCachePrefix = "search:results";
    private const string SearchSuggestionsCachePrefix = "search:suggestions";
    private const string SearchAnalyticsCachePrefix = "search:analytics";

    public TaskSearchService(
        ITaskSearchRepository searchRepository,
        ITaskCacheService cacheService,
        IDistributedCache distributedCache,
        IOptions<ICacheSettings> cacheSettings,
        ILogger<TaskSearchService> logger)
    {
        _searchRepository = searchRepository ?? throw new ArgumentNullException(nameof(searchRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _cacheSettings = cacheSettings.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<TaskSearchResult> SearchTasksAsync(Guid userId, AppTaskSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate criteria
            var validation = ValidateSearchCriteria(criteria);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid search criteria for user {UserId}: {Errors}",
                    userId, string.Join(", ", validation.Errors));
                return TaskSearchResult.Empty(criteria.SearchTerm, stopwatch.Elapsed);
            }

            var normalizedCriteria = validation.NormalizedCriteria!;
            var cacheKey = GetSearchResultsCacheKey(userId, normalizedCriteria);

            // Try to get from cache first
            var cachedResult = await GetCachedSearchResultAsync(cacheKey, cancellationToken);
            if (cachedResult != null)
            {
                stopwatch.Stop();
                _logger.LogDebug("Search cache hit for user {UserId}, key: {CacheKey}", userId, cacheKey);

                // Record cache hit in analytics
                await _searchRepository.RecordSearchQueryAsync(userId, normalizedCriteria,
                    cachedResult.TotalCount, stopwatch.Elapsed, true, cancellationToken);

                // Update metadata
                cachedResult = UpdateResultMetadata(cachedResult, stopwatch.Elapsed, true);
                return cachedResult;
            }

            // Execute search from database
            _logger.LogDebug("Search cache miss for user {UserId}, executing database search", userId);
            var searchResult = await _searchRepository.SearchTasksAsync(userId, normalizedCriteria, cancellationToken);

            // Cache the result
            await CacheSearchResultAsync(cacheKey, searchResult, cancellationToken);

            stopwatch.Stop();
            return UpdateResultMetadata(searchResult, stopwatch.Elapsed, false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing search for user {UserId} with query: {Query}", userId, criteria.SearchTerm);
            return TaskSearchResult.Empty(criteria.SearchTerm, stopwatch.Elapsed);
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
            var cacheKey = GetSearchSuggestionsCacheKey(userId, query, maxSuggestions);

            // Try to get from cache
            var cachedSuggestions = await GetCachedSuggestionsAsync(cacheKey, cancellationToken);
            if (cachedSuggestions != null)
            {
                _logger.LogDebug("Search suggestions cache hit for user {UserId}", userId);
                return cachedSuggestions;
            }

            // Get from repository
            var suggestions = await _searchRepository.GetSearchSuggestionsAsync(userId, query, maxSuggestions, cancellationToken);
            var suggestionsList = suggestions.ToList();

            // Cache the suggestions
            await CacheSuggestionsAsync(cacheKey, suggestionsList, cancellationToken);

            return suggestionsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search suggestions for user {UserId} with query: {Query}", userId, query);
            return new List<string>();
        }
    }

    public async Task<SearchAnalytics> GetSearchAnalyticsAsync(Guid userId, TimeSpan? period = null, CancellationToken cancellationToken = default)
    {
        var analyticsPeriod = period ?? TimeSpan.FromDays(30);

        try
        {
            var cacheKey = GetSearchAnalyticsCacheKey(userId, analyticsPeriod);

            // Try to get from cache (analytics can be cached longer)
            var cachedAnalytics = await GetCachedAnalyticsAsync(cacheKey, cancellationToken);
            if (cachedAnalytics != null)
            {
                return cachedAnalytics;
            }

            // Get from repository
            var analytics = await _searchRepository.GetSearchAnalyticsAsync(userId, analyticsPeriod, cancellationToken);

            // Cache analytics for longer period
            await CacheAnalyticsAsync(cacheKey, analytics, cancellationToken);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search analytics for user {UserId}", userId);
            return new SearchAnalytics
            {
                UserId = userId,
                AnalyticsPeriod = analyticsPeriod,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<SearchPerformanceMetrics> GetSearchPerformanceMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _searchRepository.GetSearchMetricsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search performance metrics");
            return new SearchPerformanceMetrics();
        }
    }

    public Task<bool> ClearUserSearchCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_cacheSettings.KeyPrefix}:{SearchResultsCachePrefix}:{userId}:*";

            // Note: This is a simplified implementation. In a production system,
            // you'd want to use Redis SCAN with pattern matching to find and delete keys
            _logger.LogInformation("Clearing search cache for user {UserId}", userId);

            // For now, we'll rely on cache expiration
            // In a full implementation, you'd iterate through matching keys and delete them

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing search cache for user {UserId}", userId);
            return Task.FromResult(false);
        }
    }

    public SearchValidationResult ValidateSearchCriteria(AppTaskSearchCriteria criteria)
    {
        var errors = criteria.Validate().ToList();

        if (errors.Any())
        {
            return SearchValidationResult.Invalid(errors);
        }

        return SearchValidationResult.Valid(criteria.Normalize());
    }

    public async Task<int> WarmSearchCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting search cache warming for user {UserId}", userId);

            // Common search patterns to warm up
            var commonSearches = new[]
            {
                new AppTaskSearchCriteria { PageSize = 20, SortBy = TaskSearchSortBy.UpdatedAt },
                new AppTaskSearchCriteria { PageSize = 20, SortBy = TaskSearchSortBy.DueDate },
                new AppTaskSearchCriteria { Status = TaskStatus.InProgress, PageSize = 20 },
                new AppTaskSearchCriteria { Status = TaskStatus.Pending, PageSize = 20 },
                new AppTaskSearchCriteria { Priority = Priority.High, PageSize = 20 },
                new AppTaskSearchCriteria { Priority = Priority.Urgent, PageSize = 20 }
            };

            var warmedCount = 0;
            foreach (var searchCriteria in commonSearches)
            {
                try
                {
                    await SearchTasksAsync(userId, searchCriteria, cancellationToken);
                    warmedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to warm search cache for criteria: {@SearchCriteria}", searchCriteria);
                }
            }

            _logger.LogInformation("Search cache warming completed for user {UserId}. Warmed {Count} searches", userId, warmedCount);
            return warmedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search cache warming for user {UserId}", userId);
            return 0;
        }
    }

    private string GetSearchResultsCacheKey(Guid userId, AppTaskSearchCriteria criteria)
    {
        return $"{_cacheSettings.KeyPrefix}:{SearchResultsCachePrefix}:{criteria.GetCacheKey(userId)}";
    }

    private string GetSearchSuggestionsCacheKey(Guid userId, string query, int maxSuggestions)
    {
        var queryHash = query.ToLowerInvariant().GetHashCode();
        return $"{_cacheSettings.KeyPrefix}:{SearchSuggestionsCachePrefix}:{userId}:{queryHash}:{maxSuggestions}";
    }

    private string GetSearchAnalyticsCacheKey(Guid userId, TimeSpan period)
    {
        return $"{_cacheSettings.KeyPrefix}:{SearchAnalyticsCachePrefix}:{userId}:{period.TotalDays}";
    }

    private async Task<TaskSearchResult?> GetCachedSearchResultAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<TaskSearchResult>(cachedData, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached search result for key: {CacheKey}", cacheKey);
        }

        return null;
    }

    private async Task CacheSearchResultAsync(string cacheKey, TaskSearchResult result, CancellationToken cancellationToken)
    {
        try
        {
            var serializedResult = JsonSerializer.Serialize(result, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.TaskListCacheExpirationMinutes)
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedResult, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching search result for key: {CacheKey}", cacheKey);
        }
    }

    private async Task<IEnumerable<string>?> GetCachedSuggestionsAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<IEnumerable<string>>(cachedData, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached suggestions for key: {CacheKey}", cacheKey);
        }

        return null;
    }

    private async Task CacheSuggestionsAsync(string cacheKey, IEnumerable<string> suggestions, CancellationToken cancellationToken)
    {
        try
        {
            var serializedSuggestions = JsonSerializer.Serialize(suggestions, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.DefaultExpirationMinutes)
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedSuggestions, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching suggestions for key: {CacheKey}", cacheKey);
        }
    }

    private async Task<SearchAnalytics?> GetCachedAnalyticsAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<SearchAnalytics>(cachedData, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached analytics for key: {CacheKey}", cacheKey);
        }

        return null;
    }

    private async Task CacheAnalyticsAsync(string cacheKey, SearchAnalytics analytics, CancellationToken cancellationToken)
    {
        try
        {
            var serializedAnalytics = JsonSerializer.Serialize(analytics, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Cache analytics for 1 hour
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedAnalytics, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching analytics for key: {CacheKey}", cacheKey);
        }
    }

    private TaskSearchResult UpdateResultMetadata(TaskSearchResult result, TimeSpan totalDuration, bool fromCache)
    {
        var updatedMetadata = new SearchResultMetadata
        {
            FromCache = fromCache,
            SearchExecutedAt = DateTime.UtcNow,
            DatabaseHits = fromCache ? 0 : result.Metadata.DatabaseHits,
            DatabaseDuration = fromCache ? TimeSpan.Zero : result.Metadata.DatabaseDuration,
            CacheDuration = fromCache ? totalDuration : TimeSpan.Zero,
            AdditionalData = result.Metadata.AdditionalData
        };

        return TaskSearchResult.Create(
            result.Tasks,
            result.TotalCount,
            result.PageNumber,
            result.PageSize,
            totalDuration,
            result.SearchQuery,
            updatedMetadata);
    }
}
