using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing search criteria for tasks with validation and defaults
/// </summary>
public class TaskSearchCriteria
{
    public string Query { get; init; } = string.Empty;
    public TaskCategory? Category { get; init; }
    public TaskStatus? Status { get; init; }
    public Priority? Priority { get; init; }
    public DateTime? DueDateFrom { get; init; }
    public DateTime? DueDateTo { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public TaskSearchSortBy SortBy { get; init; } = TaskSearchSortBy.Relevance;
    public bool SortDescending { get; init; } = true;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool IncludeCompleted { get; init; } = true;
    public bool IncludeArchived { get; init; } = false;

    /// <summary>
    /// Gets the maximum allowed page size to prevent performance issues
    /// </summary>
    public static int MaxPageSize => 100;

    /// <summary>
    /// Gets the minimum search query length for full-text search
    /// </summary>
    public static int MinQueryLength => 2;

    /// <summary>
    /// Validates the search criteria and returns validation errors
    /// </summary>
    /// <returns>Collection of validation error messages</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(Query) && Query.Length < MinQueryLength)
        {
            errors.Add($"Search query must be at least {MinQueryLength} characters long");
        }

        if (Query?.Length > 200)
        {
            errors.Add("Search query cannot exceed 200 characters");
        }

        if (PageNumber < 1)
        {
            errors.Add("Page number must be greater than 0");
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors.Add($"Page size must be between 1 and {MaxPageSize}");
        }

        if (DueDateFrom.HasValue && DueDateTo.HasValue && DueDateFrom.Value > DueDateTo.Value)
        {
            errors.Add("Due date 'from' cannot be later than due date 'to'");
        }

        if (CreatedAfter.HasValue && CreatedBefore.HasValue && CreatedAfter.Value > CreatedBefore.Value)
        {
            errors.Add("Created 'after' date cannot be later than created 'before' date");
        }

        return errors;
    }

    /// <summary>
    /// Creates a copy with normalized and sanitized values
    /// </summary>
    /// <returns>Normalized search criteria</returns>
    public TaskSearchCriteria Normalize()
    {
        var normalizedQuery = Query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            normalizedQuery = string.Empty;
        }

        var clampedPageSize = Math.Min(Math.Max(PageSize, 1), MaxPageSize);
        var clampedPageNumber = Math.Max(PageNumber, 1);

        return new TaskSearchCriteria
        {
            Query = normalizedQuery,
            Category = Category,
            Status = Status,
            Priority = Priority,
            DueDateFrom = DueDateFrom,
            DueDateTo = DueDateTo,
            CreatedAfter = CreatedAfter,
            CreatedBefore = CreatedBefore,
            SortBy = SortBy,
            SortDescending = SortDescending,
            PageNumber = clampedPageNumber,
            PageSize = clampedPageSize,
            IncludeCompleted = IncludeCompleted,
            IncludeArchived = IncludeArchived
        };
    }

    /// <summary>
    /// Determines if this is a full-text search query
    /// </summary>
    public bool IsFullTextSearch => !string.IsNullOrWhiteSpace(Query) && Query.Length >= MinQueryLength;

    /// <summary>
    /// Determines if any filters are applied
    /// </summary>
    public bool HasFilters => Category != null || Status != null || Priority != null || 
                             DueDateFrom.HasValue || DueDateTo.HasValue ||
                             CreatedAfter.HasValue || CreatedBefore.HasValue ||
                             !IncludeCompleted || IncludeArchived;

    /// <summary>
    /// Gets the offset for pagination
    /// </summary>
    public int Offset => (PageNumber - 1) * PageSize;

    /// <summary>
    /// Creates a cache key for this search criteria
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Cache key string</returns>
    public string GetCacheKey(Guid userId)
    {
        var keyComponents = new[]
        {
            $"search:{userId}",
            $"q:{Query?.GetHashCode() ?? 0}",
            $"cat:{Category?.Value ?? -1}",
            $"st:{Status?.Value ?? -1}",
            $"pr:{Priority?.Value ?? -1}",
            $"df:{DueDateFrom?.Ticks ?? 0}",
            $"dt:{DueDateTo?.Ticks ?? 0}",
            $"ca:{CreatedAfter?.Ticks ?? 0}",
            $"cb:{CreatedBefore?.Ticks ?? 0}",
            $"sb:{(int)SortBy}",
            $"sd:{SortDescending}",
            $"pn:{PageNumber}",
            $"ps:{PageSize}",
            $"ic:{IncludeCompleted}",
            $"ia:{IncludeArchived}"
        };

        return string.Join(":", keyComponents);
    }
}

/// <summary>
/// Enumeration of available search sorting options
/// </summary>
public enum TaskSearchSortBy
{
    Relevance = 0,
    CreatedAt = 1,
    UpdatedAt = 2,
    DueDate = 3,
    Priority = 4,
    Title = 5
}