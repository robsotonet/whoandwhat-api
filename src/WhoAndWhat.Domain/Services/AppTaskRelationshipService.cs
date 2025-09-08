using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for managing task relationships, hierarchies, and dependencies
/// </summary>
public class AppTaskRelationshipService
{
    /// <summary>
    /// Establishes parent-child relationship between tasks with validation
    /// </summary>
    /// <param name="parentTask">Parent task</param>
    /// <param name="childTask">Child task to add</param>
    /// <returns>Validation result</returns>
    public ValidationResult EstablishParentChildRelationship(DomainAppTask parentTask, DomainAppTask childTask)
    {
        var errors = new List<string>();
        var parentCategory = AppTaskCategory.FromValue(parentTask.Category);
        var childCategory = AppTaskCategory.FromValue(childTask.Category);

        // Validate parent can have subtasks
        if (!parentCategory.AllowsSubtasks)
        {
            errors.Add($"{parentCategory.GetDisplayName()} tasks cannot have subtasks");
        }

        // Validate child is not already part of a project
        if (childTask.ProjectId.HasValue)
        {
            errors.Add("AppTask is already part of another project");
        }

        // Validate child is not a project itself (projects cannot be nested)
        if (childCategory == AppTaskCategory.Project)
        {
            errors.Add("Projects cannot be subtasks of other tasks");
        }

        // Validate circular dependency
        if (WouldCreateCircularDependency(parentTask, childTask))
        {
            errors.Add("Cannot create circular dependency between tasks");
        }

        // Validate business rules consistency
        var consistencyValidation = ValidateTaskConsistency(parentTask, childTask);
        if (!consistencyValidation.IsValid)
        {
            errors.AddRange(consistencyValidation.Errors);
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Removes parent-child relationship with validation
    /// </summary>
    /// <param name="parentTask">Parent task</param>
    /// <param name="childTask">Child task to remove</param>
    /// <returns>Validation result</returns>
    public ValidationResult RemoveParentChildRelationship(DomainAppTask parentTask, DomainAppTask childTask)
    {
        var errors = new List<string>();
        var parentStatus = DomainAppTaskStatus.FromValue(parentTask.Status);
        var childStatus = DomainAppTaskStatus.FromValue(childTask.Status);

        // Validate removal doesn't leave parent in invalid state
        if (parentStatus == DomainAppTaskStatus.Completed)
        {
            // If parent is completed, child should also be completed or archived
            if (childStatus.IsActive())
            {
                errors.Add("Cannot remove active subtask from completed parent task");
            }
        }

        // Validate orphaned task will be valid
        if (childTask.ProjectId == parentTask.Id)
        {
            // Child will become standalone - validate this is acceptable
            var childCategory = AppTaskCategory.FromValue(childTask.Category);
            if (childCategory == AppTaskCategory.Project)
            {
                errors.Add("Project subtasks cannot become standalone tasks");
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates consistency between parent and child tasks
    /// </summary>
    private ValidationResult ValidateTaskConsistency(DomainAppTask parentTask, DomainAppTask childTask)
    {
        var errors = new List<string>();
        var parentPriority = Priority.FromValue(parentTask.Priority);
        var childPriority = Priority.FromValue(childTask.Priority);

        // Due date consistency
        if (parentTask.DueDate.HasValue && childTask.DueDate.HasValue)
        {
            if (childTask.DueDate.Value > parentTask.DueDate.Value)
            {
                errors.Add($"Subtask due date ({childTask.DueDate:yyyy-MM-dd}) cannot be later than parent due date ({parentTask.DueDate:yyyy-MM-dd})");
            }
        }

        // Priority consistency warning (not an error, but a recommendation)
        if (childPriority.IsHigherThan(parentPriority))
        {
            // This is allowed but we could log a warning
            // For now, just validate it's not extremely inconsistent
            if (parentPriority == Priority.Low && childPriority == Priority.Urgent)
            {
                errors.Add("Subtask priority should not be drastically higher than parent priority");
            }
        }

        // User consistency
        if (parentTask.UserId != childTask.UserId)
        {
            errors.Add("Parent and child tasks must belong to the same user");
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Checks if establishing a relationship would create circular dependency
    /// </summary>
    private bool WouldCreateCircularDependency(DomainAppTask potentialParent, DomainAppTask potentialChild)
    {
        // Simple check: if potential parent is already a child of potential child
        return potentialParent.ProjectId == potentialChild.Id;
    }

    /// <summary>
    /// Gets the complete task hierarchy starting from a root task
    /// </summary>
    /// <param name="rootTask">Root task</param>
    /// <param name="allTasks">Complete collection of tasks to build hierarchy from</param>
    /// <returns>Hierarchical task structure</returns>
    public TaskHierarchy BuildTaskHierarchy(DomainAppTask rootTask, IEnumerable<DomainTask> allTasks)
    {
        var taskLookup = allTasks.ToLookup(t => t.ProjectId);
        
        return new TaskHierarchy
        {
            Root = rootTask,
            Children = BuildChildHierarchies(rootTask.Id, taskLookup)
        };
    }

    /// <summary>
    /// Recursively builds child hierarchies
    /// </summary>
    private List<TaskHierarchy> BuildChildHierarchies(Guid parentId, ILookup<Guid?, DomainTask> taskLookup)
    {
        var children = new List<TaskHierarchy>();
        
        foreach (var childAppTask in taskLookup[parentId])
        {
            children.Add(new TaskHierarchy
            {
                Root = childTask,
                Children = BuildChildHierarchies(childTask.Id, taskLookup)
            });
        }

        return children;
    }

    /// <summary>
    /// Calculates aggregate metrics for a task hierarchy
    /// </summary>
    /// <param name="hierarchy">AppTask hierarchy</param>
    /// <returns>Aggregate metrics</returns>
    public TaskHierarchyMetrics CalculateHierarchyMetrics(TaskHierarchy hierarchy)
    {
        var metrics = new TaskHierarchyMetrics();
        
        CollectMetrics(hierarchy, metrics);
        
        // Calculate derived metrics
        metrics.CompletionPercentage = metrics.TotalTasks > 0 
            ? (decimal)metrics.CompletedTasks / metrics.TotalTasks * 100 
            : 0;
            
        metrics.IsOverdue = metrics.OverdueTasks > 0;
        metrics.HighestPriority = metrics.Priorities.Any() 
            ? metrics.Priorities.OrderBy(p => p.SortOrder).First()
            : Priority.Low;
            
        return metrics;
    }

    /// <summary>
    /// Recursively collects metrics from task hierarchy
    /// </summary>
    private void CollectMetrics(TaskHierarchy hierarchy, TaskHierarchyMetrics metrics)
    {
        var task = hierarchy.Root;
        var status = DomainAppTaskStatus.FromValue(task.Status);
        var priority = Priority.FromValue(task.Priority);

        // Count tasks by status
        metrics.TotalTasks++;
        if (status == DomainAppTaskStatus.Completed)
        {
            metrics.CompletedTasks++;
        }
        if (status == DomainAppTaskStatus.InProgress)
        {
            metrics.InProgressTasks++;
        }
        if (status == DomainAppTaskStatus.Pending)
        {
            metrics.PendingTasks++;
        }
        if (status == DomainAppTaskStatus.Archived)
        {
            metrics.ArchivedTasks++;
        }

        // Track overdue tasks
        if (task.IsOverdue)
        {
            metrics.OverdueTasks++;
        }

        // Track priorities
        metrics.Priorities.Add(priority);

        // Track due dates
        if (task.DueDate.HasValue)
        {
            if (!metrics.EarliestDueDate.HasValue || task.DueDate.Value < metrics.EarliestDueDate.Value)
            {
                metrics.EarliestDueDate = task.DueDate.Value;
            }
                
            if (!metrics.LatestDueDate.HasValue || task.DueDate.Value > metrics.LatestDueDate.Value)
            {
                metrics.LatestDueDate = task.DueDate.Value;
            }
        }

        // Process children
        foreach (var child in hierarchy.Children)
        {
            CollectMetrics(child, metrics);
        }
    }

    /// <summary>
    /// Reorders subtasks within a parent task based on priority and due date
    /// </summary>
    /// <param name="parentTask">Parent task containing subtasks</param>
    /// <param name="subtasks">Collection of subtasks to reorder</param>
    /// <returns>Reordered subtasks</returns>
    public IEnumerable<DomainTask> ReorderSubtasks(DomainAppTask parentTask, IEnumerable<DomainTask> subtasks)
    {
        return subtasks.OrderBy(task => 
        {
            var priority = Priority.FromValue(task.Priority);
            var status = DomainAppTaskStatus.FromValue(task.Status);
            
            // Primary sort: Status (active tasks first)
            var statusOrder = status switch
            {
                _ when status == DomainAppTaskStatus.InProgress => 0,
                _ when status == DomainAppTaskStatus.Pending => 1,
                _ when status == DomainAppTaskStatus.Completed => 2,
                _ when status == DomainAppTaskStatus.Archived => 3,
                _ => 4
            };

            return (statusOrder * 1000) + priority.SortOrder;
        })
        .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
        .ThenBy(task => task.CreatedAt);
    }

    /// <summary>
    /// Suggests task breakdown for complex tasks
    /// </summary>
    /// <param name="complexTask">AppTask to analyze for breakdown</param>
    /// <returns>Suggested subtask breakdown</returns>
    public IEnumerable<SubtaskSuggestion> SuggestTaskBreakdown(DomainAppTask complexTask)
    {
        var suggestions = new List<SubtaskSuggestion>();
        var category = AppTaskCategory.FromValue(complexTask.Category);
        var description = complexTask.Description ?? "";

        // Category-specific breakdown suggestions
        switch (category.Name)
        {
            case "Project":
                suggestions.AddRange(SuggestProjectBreakdown(complexTask));
                break;
                
            case "Idea":
                if (description.Length > 100) // Substantial ideas
                {
                    suggestions.AddRange(SuggestIdeaBreakdown(complexTask));
                }
                break;
                
            case "ToDo":
                if (description.Contains("and") || description.Contains(","))
                {
                    suggestions.AddRange(SuggestTodoBreakdown(complexTask));
                }
                break;
        }

        return suggestions;
    }

    /// <summary>
    /// Suggests breakdown for project tasks
    /// </summary>
    private IEnumerable<SubtaskSuggestion> SuggestProjectBreakdown(DomainAppTask project)
    {
        var suggestions = new List<SubtaskSuggestion>
        {
            new("Planning Phase", "Define project scope and requirements", Priority.High),
            new("Research Phase", "Gather information and resources", Priority.Medium),
            new("Implementation Phase", "Execute the main project work", Priority.High),
            new("Review Phase", "Test and review project outcomes", Priority.Medium),
            new("Documentation Phase", "Document results and lessons learned", Priority.Low)
        };

        // Adjust due dates based on project due date
        if (project.DueDate.HasValue)
        {
            var projectDuration = project.DueDate.Value - DateTime.UtcNow;
            var phaseDuration = projectDuration.TotalDays / 5; // 5 phases
            
            for (int i = 0; i < suggestions.Count; i++)
            {
                suggestions[i].SuggestedDueDate = DateTime.UtcNow.AddDays((i + 1) * phaseDuration);
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Suggests breakdown for idea tasks
    /// </summary>
    private IEnumerable<SubtaskSuggestion> SuggestIdeaBreakdown(DomainAppTask idea)
    {
        return new List<SubtaskSuggestion>
        {
            new("Research the Idea", "Investigate feasibility and requirements", Priority.Medium),
            new("Create Action Plan", "Define steps to implement the idea", Priority.High),
            new("Prototype/Test", "Create a basic version or test concept", Priority.Medium)
        };
    }

    /// <summary>
    /// Suggests breakdown for complex todo tasks
    /// </summary>
    private IEnumerable<SubtaskSuggestion> SuggestTodoBreakdown(DomainAppTask todo)
    {
        var description = todo.Description ?? "";
        var suggestions = new List<SubtaskSuggestion>();

        // Simple heuristic: split on "and" or comma
        var parts = description.Split(new[] { " and ", ", " }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length > 1)
        {
            for (int i = 0; i < Math.Min(parts.Length, 5); i++) // Max 5 subtasks
            {
                var part = parts[i].Trim();
                if (part.Length > 5) // Reasonable length
                {
                    suggestions.Add(new SubtaskSuggestion(
                        char.ToUpper(part[0]) + part[1..], 
                        $"Complete: {part}",
                        Priority.Medium));
                }
            }
        }

        return suggestions;
    }
}

/// <summary>
/// Represents a hierarchical structure of tasks
/// </summary>
public class TaskHierarchy
{
    public DomainAppTask Root { get; set; } = null!;
    public List<TaskHierarchy> Children { get; set; } = new();
    
    /// <summary>
    /// Gets the total depth of the hierarchy
    /// </summary>
    public int Depth => 1 + (Children.Any() ? Children.Max(c => c.Depth) : 0);
    
    /// <summary>
    /// Gets all tasks in the hierarchy flattened
    /// </summary>
    public IEnumerable<DomainTask> AllTasks
    {
        get
        {
            yield return Root;
            foreach (var child in Children)
            {
                foreach (var task in child.AllTasks)
                {
                    yield return task;
                }
            }
        }
    }
}

/// <summary>
/// Aggregate metrics for a task hierarchy
/// </summary>
public class TaskHierarchyMetrics
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public int ArchivedTasks { get; set; }
    public int OverdueTasks { get; set; }
    
    public decimal CompletionPercentage { get; set; }
    public bool IsOverdue { get; set; }
    public Priority HighestPriority { get; set; } = Priority.Low;
    
    public DateTime? EarliestDueDate { get; set; }
    public DateTime? LatestDueDate { get; set; }
    
    public List<Priority> Priorities { get; set; } = new();
}

/// <summary>
/// Represents a suggested subtask breakdown
/// </summary>
public class SubtaskSuggestion
{
    public string Title { get; set; }
    public string? Description { get; set; }
    public Priority Priority { get; set; }
    public DateTime? SuggestedDueDate { get; set; }

    public SubtaskSuggestion(string title, string? description, Priority priority)
    {
        Title = title;
        Description = description;
        Priority = priority;
    }
}