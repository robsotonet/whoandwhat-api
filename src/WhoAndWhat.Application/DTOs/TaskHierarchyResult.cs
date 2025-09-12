using WhoAndWhat.Domain.Entities;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Result of a task hierarchy query
/// </summary>
public class TaskHierarchyResult
{
    /// <summary>
    /// The root task of the hierarchy
    /// </summary>
    public DomainTask RootTask { get; set; } = null!;

    /// <summary>
    /// All subtasks organized hierarchically
    /// </summary>
    public List<TaskHierarchyNode> Subtasks { get; set; } = new();

    /// <summary>
    /// Total number of tasks in the entire hierarchy
    /// </summary>
    public int TotalTaskCount { get; set; }

    /// <summary>
    /// Maximum depth of the hierarchy
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Number of completed tasks in the hierarchy
    /// </summary>
    public int CompletedTaskCount { get; set; }

    /// <summary>
    /// Overall completion percentage for the hierarchy
    /// </summary>
    public decimal CompletionPercentage { get; set; }

    /// <summary>
    /// Whether the entire hierarchy has any overdue tasks
    /// </summary>
    public bool HasOverdueTasks { get; set; }

    /// <summary>
    /// Flattened list of all tasks in the hierarchy for easy iteration
    /// </summary>
    public List<DomainTask> AllTasks { get; set; } = new();

    /// <summary>
    /// Gets all tasks at a specific depth level (0 = root, 1 = direct children, etc.)
    /// </summary>
    /// <param name="depth">The depth level to retrieve</param>
    /// <returns>Tasks at the specified depth</returns>
    public IEnumerable<DomainTask> GetTasksAtDepth(int depth)
    {
        if (depth == 0)
        {
            return new[] { RootTask };
        }

        return GetTasksAtDepthRecursive(Subtasks, depth - 1);
    }

    /// <summary>
    /// Gets the path from root to a specific task
    /// </summary>
    /// <param name="taskId">The task ID to find the path to</param>
    /// <returns>Path from root to the task, or empty if not found</returns>
    public List<DomainTask> GetPathToTask(Guid taskId)
    {
        if (RootTask.Id == taskId)
        {
            return new List<DomainTask> { RootTask };
        }

        var path = new List<DomainTask> { RootTask };
        if (FindTaskPathRecursive(Subtasks, taskId, path))
        {
            return path;
        }

        return new List<DomainTask>();
    }

    /// <summary>
    /// Gets all leaf tasks (tasks with no subtasks) in the hierarchy
    /// </summary>
    /// <returns>All leaf tasks</returns>
    public IEnumerable<DomainTask> GetLeafTasks()
    {
        var leafTasks = new List<DomainTask>();

        if (!Subtasks.Any())
        {
            leafTasks.Add(RootTask);
        }
        else
        {
            CollectLeafTasksRecursive(Subtasks, leafTasks);
        }

        return leafTasks;
    }

    /// <summary>
    /// Creates a simple hierarchy result with just the root task
    /// </summary>
    /// <param name="rootTask">The root task</param>
    /// <returns>Simple hierarchy result</returns>
    public static TaskHierarchyResult CreateSimple(DomainTask rootTask)
    {
        return new TaskHierarchyResult
        {
            RootTask = rootTask,
            Subtasks = new List<TaskHierarchyNode>(),
            TotalTaskCount = 1,
            MaxDepth = 0,
            CompletedTaskCount = rootTask.Status == (int)Domain.ValueObjects.AppTaskStatus.Completed ? 1 : 0,
            CompletionPercentage = rootTask.Status == (int)Domain.ValueObjects.AppTaskStatus.Completed ? 100m : 0m,
            HasOverdueTasks = rootTask.IsOverdue,
            AllTasks = new List<DomainTask> { rootTask }
        };
    }

    private IEnumerable<DomainTask> GetTasksAtDepthRecursive(IEnumerable<TaskHierarchyNode> nodes, int targetDepth)
    {
        if (targetDepth == 0)
        {
            return nodes.Select(n => n.Task);
        }

        return nodes.SelectMany(n => GetTasksAtDepthRecursive(n.Subtasks, targetDepth - 1));
    }

    private bool FindTaskPathRecursive(IEnumerable<TaskHierarchyNode> nodes, Guid taskId, List<DomainTask> path)
    {
        foreach (var node in nodes)
        {
            path.Add(node.Task);

            if (node.Task.Id == taskId)
            {
                return true;
            }

            if (FindTaskPathRecursive(node.Subtasks, taskId, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    private void CollectLeafTasksRecursive(IEnumerable<TaskHierarchyNode> nodes, List<DomainTask> leafTasks)
    {
        foreach (var node in nodes)
        {
            if (!node.Subtasks.Any())
            {
                leafTasks.Add(node.Task);
            }
            else
            {
                CollectLeafTasksRecursive(node.Subtasks, leafTasks);
            }
        }
    }
}

/// <summary>
/// A node in the task hierarchy tree
/// </summary>
public class TaskHierarchyNode
{
    /// <summary>
    /// The task at this node
    /// </summary>
    public DomainTask Task { get; set; } = null!;

    /// <summary>
    /// The depth of this node (0 = direct child of root)
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Direct subtasks of this task
    /// </summary>
    public List<TaskHierarchyNode> Subtasks { get; set; } = new();

    /// <summary>
    /// Whether this is a leaf node (has no subtasks)
    /// </summary>
    public bool IsLeaf => !Subtasks.Any();

    /// <summary>
    /// Total number of descendants (including indirect subtasks)
    /// </summary>
    public int DescendantCount { get; set; }

    /// <summary>
    /// Number of completed descendants
    /// </summary>
    public int CompletedDescendantCount { get; set; }

    /// <summary>
    /// Completion percentage for this subtree
    /// </summary>
    public decimal SubtreeCompletionPercentage { get; set; }

    /// <summary>
    /// Creates a hierarchy node with calculated metrics
    /// </summary>
    /// <param name="task">The task</param>
    /// <param name="depth">The depth in the hierarchy</param>
    /// <param name="subtasks">Direct subtasks</param>
    /// <returns>Hierarchy node with calculated metrics</returns>
    public static TaskHierarchyNode Create(DomainTask task, int depth, List<TaskHierarchyNode> subtasks)
    {
        var node = new TaskHierarchyNode
        {
            Task = task,
            Depth = depth,
            Subtasks = subtasks
        };

        // Calculate metrics
        node.CalculateMetrics();

        return node;
    }

    /// <summary>
    /// Calculates descendant counts and completion percentages
    /// </summary>
    public void CalculateMetrics()
    {
        if (!Subtasks.Any())
        {
            DescendantCount = 0;
            CompletedDescendantCount = 0;
            SubtreeCompletionPercentage = Task.Status == (int)Domain.ValueObjects.AppTaskStatus.Completed ? 100m : 0m;
            return;
        }

        // Calculate for all subtasks first
        foreach (var subtask in Subtasks)
        {
            subtask.CalculateMetrics();
        }

        // Calculate our metrics
        DescendantCount = Subtasks.Sum(s => s.DescendantCount + 1); // +1 for the subtask itself
        CompletedDescendantCount = Subtasks.Sum(s => s.CompletedDescendantCount +
            (s.Task.Status == (int)Domain.ValueObjects.AppTaskStatus.Completed ? 1 : 0));

        var totalTasks = DescendantCount + 1; // +1 for this task
        var completedTasks = CompletedDescendantCount +
            (Task.Status == (int)Domain.ValueObjects.AppTaskStatus.Completed ? 1 : 0);

        SubtreeCompletionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100m : 0m;
    }

    /// <summary>
    /// Gets all tasks in this subtree (including this task)
    /// </summary>
    /// <returns>All tasks in the subtree</returns>
    public IEnumerable<DomainTask> GetAllTasks()
    {
        yield return Task;

        foreach (var subtask in Subtasks)
        {
            foreach (var task in subtask.GetAllTasks())
            {
                yield return task;
            }
        }
    }
}
