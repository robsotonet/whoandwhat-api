using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Tasks;

public class TaskQueryRequest
{
    public string? Search { get; set; }

    public List<int>? Categories { get; set; }

    public List<int>? Statuses { get; set; }

    public List<int>? Priorities { get; set; }

    public DateTime? DueDateFrom { get; set; }

    public DateTime? DueDateTo { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    public List<Guid>? ContactIds { get; set; }

    public bool? HasDueDate { get; set; }

    public bool? IsOverdue { get; set; }

    public bool? HasSubtasks { get; set; }

    public Guid? ParentTaskId { get; set; }

    public string? SortBy { get; set; } = "UpdatedAt";

    public bool SortDescending { get; set; } = true;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    public bool IncludeArchived { get; set; } = false;
}

public class TaskStatisticsResponse
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int TasksDueToday { get; set; }
    public int TasksDueThisWeek { get; set; }
    public CategoryStatistics[] CategoryStats { get; set; } = Array.Empty<CategoryStatistics>();
    public PriorityStatistics[] PriorityStats { get; set; } = Array.Empty<PriorityStatistics>();
    public decimal CompletionRate { get; set; }
    public TimeSpan AverageCompletionTime { get; set; }
}

public class CategoryStatistics
{
    public int Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public decimal CompletionPercentage { get; set; }
    public TimeSpan AverageCompletionTime { get; set; }
}

public class PriorityStatistics
{
    public int Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
}
