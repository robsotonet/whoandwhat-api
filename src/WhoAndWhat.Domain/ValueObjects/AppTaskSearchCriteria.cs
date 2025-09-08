using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing search criteria for tasks with validation and defaults
/// </summary>
public class AppTaskSearchCriteria
{
    public Guid? UserId { get; init; }
    public string Query { get; init; } = string.Empty;
    public string SearchText { get; init; } = string.Empty;
    public string SearchTerm => !string.IsNullOrWhiteSpace(SearchText) ? SearchText : Query;
    public AppTaskCategory? Category { get; init; }
    public List<int>? Categories { get; init; }
    public AppTaskStatus? Status { get; init; }
    public List<int>? Statuses { get; init; }
    public Priority? Priority { get; init; }
    public List<int>? Priorities { get; init; }
    public DateTime? DueDateFrom { get; init; }
    public DateTime? DueDateTo { get; init; }
    public DateTime? DueDateStart => DueDateFrom;
    public DateTime? DueDateEnd => DueDateTo;
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public List<Guid>? ContactIds { get; init; }
    public bool? HasDueDate { get; init; }
    public bool? IsOverdue { get; init; }
    public bool? HasSubtasks { get; init; }
    public Guid? ParentTaskId { get; init; }
    public Guid? ProjectId { get; init; }
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

        var searchText = !string.IsNullOrWhiteSpace(SearchText) ? SearchText : Query;

        if (!string.IsNullOrWhiteSpace(searchText) && searchText.Length < MinQueryLength)
        {
            errors.Add($"Search query must be at least {MinQueryLength} characters long");
        }

        if (searchText?.Length > 200)
        {
            errors.Add("Search query cannot exceed 200 characters");
        }

        if (Categories?.Any() == true && Categories.Any(c => c < 0))
        {
            errors.Add("All category values must be non-negative");
        }

        if (Statuses?.Any() == true && Statuses.Any(s => s < 0))
        {
            errors.Add("All status values must be non-negative");
        }

        if (Priorities?.Any() == true && Priorities.Any(p => p < 0))
        {
            errors.Add("All priority values must be non-negative");
        }

        if (ContactIds?.Count > 50)
        {
            errors.Add("Cannot filter by more than 50 contact IDs");
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

        if (CreatedFrom.HasValue && CreatedTo.HasValue && CreatedFrom.Value > CreatedTo.Value)
        {
            errors.Add("Created 'from' date cannot be later than created 'to' date");
        }

        return errors;
    }

    /// <summary>
    /// Creates a copy with normalized and sanitized values
    /// </summary>
    /// <returns>Normalized search criteria</returns>
    public AppTaskSearchCriteria Normalize()
    {
        var normalizedQuery = Query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            normalizedQuery = string.Empty;
        }

        var normalizedSearchText = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            normalizedSearchText = string.Empty;
        }

        var clampedPageSize = Math.Min(Math.Max(PageSize, 1), MaxPageSize);
        var clampedPageNumber = Math.Max(PageNumber, 1);

        return new AppTaskSearchCriteria
        {
            UserId = UserId,
            Query = normalizedQuery,
            SearchText = normalizedSearchText,
            Category = Category,
            Categories = Categories?.Where(c => c >= 0).ToList(),
            Status = Status,
            Statuses = Statuses?.Where(s => s >= 0).ToList(),
            Priority = Priority,
            Priorities = Priorities?.Where(p => p >= 0).ToList(),
            DueDateFrom = DueDateFrom,
            DueDateTo = DueDateTo,
            CreatedAfter = CreatedAfter,
            CreatedBefore = CreatedBefore,
            CreatedFrom = CreatedFrom,
            CreatedTo = CreatedTo,
            ContactIds = ContactIds?.Take(50).ToList(),
            HasDueDate = HasDueDate,
            IsOverdue = IsOverdue,
            HasSubtasks = HasSubtasks,
            ParentTaskId = ParentTaskId,
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
    public bool IsFullTextSearch
    {
        get
        {
            var searchText = !string.IsNullOrWhiteSpace(SearchText) ? SearchText : Query;
            return !string.IsNullOrWhiteSpace(searchText) && searchText.Length >= MinQueryLength;
        }
    }

    /// <summary>
    /// Determines if any filters are applied
    /// </summary>
    public bool HasFilters => Category != null || Status != null || Priority != null ||
                             Categories?.Any() == true || Statuses?.Any() == true || Priorities?.Any() == true ||
                             DueDateFrom.HasValue || DueDateTo.HasValue ||
                             CreatedAfter.HasValue || CreatedBefore.HasValue ||
                             CreatedFrom.HasValue || CreatedTo.HasValue ||
                             ContactIds?.Any() == true || HasDueDate.HasValue || IsOverdue.HasValue ||
                             HasSubtasks.HasValue || ParentTaskId.HasValue ||
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
    public string GetCacheKey(Guid? userId = null)
    {
        var actualUserId = userId ?? UserId ?? Guid.Empty;
        var searchText = !string.IsNullOrWhiteSpace(SearchText) ? SearchText : Query;

        var keyComponents = new[]
        {
            $"search:{actualUserId}",
            $"q:{searchText?.GetHashCode() ?? 0}",
            $"cat:{Category?.Value ?? -1}",
            $"cats:{string.Join(",", Categories ?? new List<int>())}",
            $"st:{Status?.Value ?? -1}",
            $"sts:{string.Join(",", Statuses ?? new List<int>())}",
            $"pr:{Priority?.Value ?? -1}",
            $"prs:{string.Join(",", Priorities ?? new List<int>())}",
            $"df:{DueDateFrom?.Ticks ?? 0}",
            $"dt:{DueDateTo?.Ticks ?? 0}",
            $"ca:{CreatedAfter?.Ticks ?? 0}",
            $"cb:{CreatedBefore?.Ticks ?? 0}",
            $"cf:{CreatedFrom?.Ticks ?? 0}",
            $"ct:{CreatedTo?.Ticks ?? 0}",
            $"cids:{string.Join(",", ContactIds ?? new List<Guid>())}",
            $"hd:{HasDueDate?.ToString() ?? "null"}",
            $"io:{IsOverdue?.ToString() ?? "null"}",
            $"hs:{HasSubtasks?.ToString() ?? "null"}",
            $"ptid:{ParentTaskId?.ToString() ?? "null"}",
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
