namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Generic paged result container for API responses
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public record PagedResult<T>
{
    /// <summary>
    /// List of items for the current page
    /// </summary>
    public IEnumerable<T> Items { get; init; } = new List<T>();

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Number of items on current page
    /// </summary>
    public int CurrentPageCount => Items.Count();

    /// <summary>
    /// Creates a new paged result
    /// </summary>
    /// <param name="items">Items for current page</param>
    /// <param name="totalCount">Total count across all pages</param>
    /// <param name="page">Current page number</param>
    /// <param name="pageSize">Page size</param>
    public static PagedResult<T> Create(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Creates an empty paged result
    /// </summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
    {
        return new PagedResult<T>
        {
            Items = new List<T>(),
            TotalCount = 0,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Maps items to a different type while preserving pagination info
    /// </summary>
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new PagedResult<TResult>
        {
            Items = Items.Select(mapper),
            TotalCount = TotalCount,
            Page = Page,
            PageSize = PageSize
        };
    }
}
