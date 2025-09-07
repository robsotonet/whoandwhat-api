namespace WhoAndWhat.Application.DTOs.Tasks;

public class TaskActionDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class TaskWorkflowStateDto
{
    public int CurrentStatus { get; set; }
    public string CurrentStatusName { get; set; } = string.Empty;
    public int? RecommendedNextStatus { get; set; }
    public string? RecommendedNextStatusName { get; set; }
    public List<TaskActionDto> AvailableActions { get; set; } = new();
    public List<string> Blockers { get; set; } = new();
    public bool CanComplete { get; set; }
    public bool CanReopen { get; set; }
    public string? WorkflowStage { get; set; }
}

public class TaskSchedulingSuggestionDto
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public DateTime RecommendedDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class TaskSchedulingResponse
{
    public List<TaskSchedulingSuggestionDto> Suggestions { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public TimeSpan TotalEstimatedTime { get; set; }
}

public class TaskMetricsDto
{
    public List<CategoryMetricsDto> CategoryMetrics { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
    public int TotalTasksAnalyzed { get; set; }
}

public class CategoryMetricsDto
{
    public TaskCategoryDto Category { get; set; } = new();
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public decimal CompletionPercentage { get; set; }
    public TimeSpan AverageCompletionTime { get; set; }
    public List<string> CommonPatterns { get; set; } = new();
}

public class TaskCategoryDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresDueDate { get; set; }
    public bool AllowsSubtasks { get; set; }
    public List<string> AllowedConversions { get; set; } = new();
}