using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Data transfer object representing search results for tasks with pagination and metadata
/// </summary>
public class TaskSearchResult
{
    public IEnumerable<TaskSearchItem> Tasks { get; init; } = new List<TaskSearchItem>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public TimeSpan SearchDuration { get; init; }
    public string SearchQuery { get; init; } = string.Empty;
    public SearchResultMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Creates a TaskSearchResult from a collection of tasks and search criteria
    /// </summary>
    /// <param name="tasks">Search result tasks with relevance scores</param>
    /// <param name="totalCount">Total number of matching tasks</param>
    /// <param name="pageNumber">Current page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="searchDuration">Time taken to execute the search</param>
    /// <param name="searchQuery">Original search query</param>
    /// <param name="metadata">Additional search metadata</param>
    /// <returns>Configured TaskSearchResult</returns>
    public static TaskSearchResult Create(
        IEnumerable<TaskSearchItem> tasks,
        int totalCount,
        int pageNumber,
        int pageSize,
        TimeSpan searchDuration,
        string searchQuery = "",
        SearchResultMetadata? metadata = null)
    {
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new TaskSearchResult
        {
            Tasks = tasks,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPrevious = pageNumber > 1,
            HasNext = pageNumber < totalPages,
            SearchDuration = searchDuration,
            SearchQuery = searchQuery,
            Metadata = metadata ?? new SearchResultMetadata()
        };
    }

    /// <summary>
    /// Creates an empty search result
    /// </summary>
    /// <param name="searchQuery">Original search query</param>
    /// <param name="searchDuration">Time taken to execute the search</param>
    /// <returns>Empty TaskSearchResult</returns>
    public static TaskSearchResult Empty(string searchQuery = "", TimeSpan searchDuration = default)
    {
        return new TaskSearchResult
        {
            Tasks = new List<TaskSearchItem>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 0,
            HasPrevious = false,
            HasNext = false,
            SearchDuration = searchDuration,
            SearchQuery = searchQuery,
            Metadata = new SearchResultMetadata()
        };
    }
}

/// <summary>
/// Individual task search result item with relevance scoring
/// </summary>
public class TaskSearchItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? DueDate { get; init; }
    public int Priority { get; init; }
    public int Category { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public double RelevanceScore { get; init; }
    public IEnumerable<string> MatchedTerms { get; init; } = new List<string>();
    public SearchMatchInfo MatchInfo { get; init; } = new();

    /// <summary>
    /// Creates a TaskSearchItem from a Task entity
    /// </summary>
    /// <param name="task">Source task entity</param>
    /// <param name="relevanceScore">Search relevance score</param>
    /// <param name="matchedTerms">Terms that matched in the search</param>
    /// <param name="matchInfo">Detailed match information</param>
    /// <returns>Configured TaskSearchItem</returns>
    public static TaskSearchItem FromTask(
        Domain.Entities.AppTask task,
        double relevanceScore = 0.0,
        IEnumerable<string>? matchedTerms = null,
        SearchMatchInfo? matchInfo = null)
    {
        return new TaskSearchItem
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            DueDate = task.DueDate,
            Priority = task.Priority,
            Category = task.Category,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            UserId = task.UserId,
            ProjectId = task.ProjectId,
            RelevanceScore = relevanceScore,
            MatchedTerms = matchedTerms ?? new List<string>(),
            MatchInfo = matchInfo ?? new SearchMatchInfo()
        };
    }
}

/// <summary>
/// Information about where and how search terms matched
/// </summary>
public class SearchMatchInfo
{
    public bool TitleMatch { get; init; }
    public bool DescriptionMatch { get; init; }
    public int TitleMatches { get; init; }
    public int DescriptionMatches { get; init; }
    public IEnumerable<string> HighlightedTitle { get; init; } = new List<string>();
    public IEnumerable<string> HighlightedDescription { get; init; } = new List<string>();
}

/// <summary>
/// Additional metadata about the search results
/// </summary>
public class SearchResultMetadata
{
    public bool FromCache { get; init; }
    public DateTime SearchExecutedAt { get; init; } = DateTime.UtcNow;
    public string DatabaseQueryPlan { get; init; } = string.Empty;
    public int DatabaseHits { get; init; }
    public TimeSpan DatabaseDuration { get; init; }
    public TimeSpan CacheDuration { get; init; }
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}
