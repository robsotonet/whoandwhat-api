using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Repositories;

/// <summary>
/// Implementation of IAppTaskRepository with advanced querying capabilities
/// </summary>
public class TaskRepository : Repository<DomainTask>, IAppTaskRepository
{
    private readonly ILogger<TaskRepository> _logger;

    public TaskRepository(ApplicationDbContext context, ILogger<TaskRepository> logger)
        : base(context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Enhanced Retrieval Methods

    public async Task<DomainTask?> GetTaskWithSubtasksAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Include(t => t.Subtasks.Where(st => !st.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task {TaskId} with subtasks for user {UserId}", taskId, userId);
            return null;
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksByProjectIdAsync(Guid projectId, Guid userId, bool includeCompleted = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet.Where(t => t.ProjectId == projectId && t.UserId == userId);

            if (!includeCompleted)
            {
                query = query.Where(t => t.Status != (int)DomainTaskStatus.Completed);
            }

            return await query
                .OrderBy(t => t.Status)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks for project {ProjectId} and user {UserId}", projectId, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<(IEnumerable<DomainTask> Tasks, int TotalCount)> GetTasksByUserIdAsync(Guid userId, TaskFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!filter.IsValid())
            {
                _logger.LogWarning("Invalid filter provided for user {UserId}", userId);
                return (Enumerable.Empty<DomainTask>(), 0);
            }

            var query = BuildFilteredQuery(userId, filter);

            var totalCount = await query.CountAsync(cancellationToken);

            var tasks = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            // Include related data if requested
            if (filter.IncludeSubtasks || filter.IncludeContacts || filter.IncludeProject)
            {
                tasks = await LoadRelatedDataAsync(tasks, filter, cancellationToken);
            }

            return (tasks, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving filtered tasks for user {UserId}", userId);
            return (Enumerable.Empty<DomainTask>(), 0);
        }
    }

    public async Task<IEnumerable<DomainTask>> GetOverdueTasksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            return await _dbSet
                .Where(t => t.UserId == userId
                           && t.DueDate.HasValue
                           && t.DueDate.Value.Date < today
                           && t.Status != (int)DomainTaskStatus.Completed
                           && t.Status != (int)DomainTaskStatus.Archived)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksForDateRangeAsync(Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId
                           && t.DueDate.HasValue
                           && t.DueDate.Value.Date >= fromDate.Date
                           && t.DueDate.Value.Date <= toDate.Date)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks for date range {FromDate}-{ToDate} for user {UserId}", fromDate, toDate, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetRecentTasksAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.UpdatedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Hierarchy and Relationship Methods

    public async Task<TaskHierarchyResult?> GetTaskHierarchyAsync(Guid rootTaskId, Guid userId, int? maxDepth = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var rootTask = await _dbSet
                .FirstOrDefaultAsync(t => t.Id == rootTaskId && t.UserId == userId, cancellationToken);

            if (rootTask == null)
            {
                return null;
            }

            var allRelatedTasks = await GetTaskDescendantsAsync(rootTaskId, userId, cancellationToken);
            var hierarchyNodes = BuildHierarchyNodes(allRelatedTasks.ToList(), rootTaskId, maxDepth);

            return new TaskHierarchyResult
            {
                RootTask = rootTask,
                Subtasks = hierarchyNodes,
                TotalTaskCount = 1 + allRelatedTasks.Count(),
                MaxDepth = CalculateMaxDepth(hierarchyNodes),
                CompletedTaskCount = CountCompletedTasks(rootTask, allRelatedTasks),
                CompletionPercentage = CalculateCompletionPercentage(rootTask, allRelatedTasks),
                HasOverdueTasks = HasOverdueTasks(rootTask, allRelatedTasks),
                AllTasks = new List<DomainTask> { rootTask }.Concat(allRelatedTasks).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building task hierarchy for root task {RootTaskId} and user {UserId}", rootTaskId, userId);
            return null;
        }
    }

    public async Task<int> GetActiveSubtasksCountAsync(Guid parentTaskId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.ProjectId == parentTaskId
                           && t.Status != (int)DomainTaskStatus.Completed
                           && t.Status != (int)DomainTaskStatus.Archived)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting active subtasks for parent task {ParentTaskId}", parentTaskId);
            return 0;
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTaskAncestorsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _dbSet
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);

            if (task?.ProjectId == null)
            {
                return Enumerable.Empty<DomainTask>();
            }

            var ancestors = new List<DomainTask>();
            var currentParentId = task.ProjectId;

            while (currentParentId.HasValue)
            {
                var parent = await _dbSet
                    .FirstOrDefaultAsync(t => t.Id == currentParentId.Value && t.UserId == userId, cancellationToken);

                if (parent == null) { break; }

                ancestors.Add(parent);
                currentParentId = parent.ProjectId;
            }

            ancestors.Reverse(); // Return from root to immediate parent
            return ancestors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ancestors for task {TaskId} and user {UserId}", taskId, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTaskDescendantsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use recursive CTE for efficient hierarchy traversal
            var sql = @"
                WITH RECURSIVE task_hierarchy AS (
                    SELECT * FROM ""Tasks"" 
                    WHERE ""ProjectId"" = @taskId AND ""UserId"" = @userId AND NOT ""IsDeleted""
                    
                    UNION ALL
                    
                    SELECT t.* FROM ""Tasks"" t
                    INNER JOIN task_hierarchy th ON t.""ProjectId"" = th.""Id""
                    WHERE t.""UserId"" = @userId AND NOT t.""IsDeleted""
                )
                SELECT * FROM task_hierarchy";

            return await _context.Tasks
                .FromSqlRaw(sql,
                    new NpgsqlParameter("@taskId", taskId),
                    new NpgsqlParameter("@userId", userId))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving descendants for task {TaskId} and user {UserId}", taskId, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetSubtasksAsync(Guid parentTaskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.ProjectId == parentTaskId && t.UserId == userId)
                .OrderBy(t => t.Status)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subtasks for parent {ParentTaskId} and user {UserId}", parentTaskId, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Advanced Filtering and Search

    public async Task<IEnumerable<DomainTask>> GetTasksByCategoryAsync(Guid userId, AppTaskCategory category, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId && t.Category == (int)category)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks by category {Category} for user {UserId}", category, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksByStatusAsync(Guid userId, DomainTaskStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId && t.Status == (int)status)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks by status {Status} for user {UserId}", status, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksByPriorityAsync(Guid userId, Priority priority, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId && t.Priority == (int)priority)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks by priority {Priority} for user {UserId}", priority, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksDueTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            return await _dbSet
                .Where(t => t.UserId == userId
                           && t.DueDate.HasValue
                           && t.DueDate.Value.Date == today
                           && t.Status != (int)DomainTaskStatus.Completed
                           && t.Status != (int)DomainTaskStatus.Archived)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks due today for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksDueSoonAsync(Guid userId, int days, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var endDate = today.AddDays(days);

            return await _dbSet
                .Where(t => t.UserId == userId
                           && t.DueDate.HasValue
                           && t.DueDate.Value.Date >= today
                           && t.DueDate.Value.Date <= endDate
                           && t.Status != (int)DomainTaskStatus.Completed
                           && t.Status != (int)DomainTaskStatus.Archived)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks due soon for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> SearchTasksAsync(Guid userId, string searchTerm, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Enumerable.Empty<DomainTask>();
            }

            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();

            return await _dbSet
                .Where(t => t.UserId == userId
                           && (EF.Functions.Like(t.Title.ToLower(), $"%{normalizedSearch}%") ||
                               (t.Description != null && EF.Functions.Like(t.Description.ToLower(), $"%{normalizedSearch}%"))))
                .OrderByDescending(t => EF.Functions.Like(t.Title.ToLower(), $"%{normalizedSearch}%") ? 2 : 1) // Title matches first
                .ThenByDescending(t => t.UpdatedAt)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks with term '{SearchTerm}' for user {UserId}", searchTerm, userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Private Helper Methods

    private IQueryable<DomainTask> BuildFilteredQuery(Guid userId, TaskFilter filter)
    {
        var query = _dbSet.Where(t => t.UserId == userId);

        // Apply soft delete filter
        if (!filter.IncludeDeleted)
        {
            query = query.Where(t => !t.IsDeleted);
        }

        // Status filtering
        if (filter.Status != null)
        {
            query = query.Where(t => t.Status == (int)filter.Status);
        }
        else if (filter.Statuses?.Any() == true)
        {
            var statusValues = filter.Statuses.Select(s => (int)s).ToList();
            query = query.Where(t => statusValues.Contains(t.Status));
        }

        if (filter.ExcludeCompleted)
        {
            query = query.Where(t => t.Status != (int)DomainTaskStatus.Completed);
        }

        if (filter.ExcludeArchived)
        {
            query = query.Where(t => t.Status != (int)DomainTaskStatus.Archived);
        }

        // Category filtering
        if (filter.Category != null)
        {
            query = query.Where(t => t.Category == (int)filter.Category);
        }
        else if (filter.Categories?.Any() == true)
        {
            var categoryValues = filter.Categories.Select(c => (int)c).ToList();
            query = query.Where(t => categoryValues.Contains(t.Category));
        }

        // Priority filtering
        if (filter.Priority != null)
        {
            query = query.Where(t => t.Priority == (int)filter.Priority);
        }

        if (filter.MinPriority != null)
        {
            query = query.Where(t => t.Priority >= (int)filter.MinPriority);
        }

        if (filter.MaxPriority != null)
        {
            query = query.Where(t => t.Priority <= (int)filter.MaxPriority);
        }

        // Date filtering
        ApplyDateFilters(ref query, filter);

        // Hierarchy filtering
        ApplyHierarchyFilters(ref query, filter);

        // Text filtering
        ApplyTextFilters(ref query, filter);

        // Apply sorting
        query = ApplySorting(query, filter);

        return query;
    }

    private void ApplyDateFilters(ref IQueryable<DomainTask> query, TaskFilter filter)
    {
        if (filter.DueDateFrom.HasValue)
        {
            query = query.Where(t => t.DueDate >= filter.DueDateFrom.Value);
        }

        if (filter.DueDateTo.HasValue)
        {
            query = query.Where(t => t.DueDate <= filter.DueDateTo.Value);
        }

        if (filter.CreatedAfter.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= filter.CreatedBefore.Value);
        }

        if (filter.UpdatedAfter.HasValue)
        {
            query = query.Where(t => t.UpdatedAt >= filter.UpdatedAfter.Value);
        }

        if (filter.UpdatedBefore.HasValue)
        {
            query = query.Where(t => t.UpdatedAt <= filter.UpdatedBefore.Value);
        }

        if (filter.OverdueOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date < today);
        }

        if (filter.DueTodayOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == today);
        }
    }

    private void ApplyHierarchyFilters(ref IQueryable<DomainTask> query, TaskFilter filter)
    {
        if (filter.ParentTasksOnly)
        {
            query = query.Where(t => t.ProjectId == null);
        }

        if (filter.SubtasksOnly)
        {
            query = query.Where(t => t.ProjectId != null);
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == filter.ProjectId.Value);
        }

        if (filter.StandaloneTasksOnly)
        {
            query = query.Where(t => t.ProjectId == null);
        }
    }

    private void ApplyTextFilters(ref IQueryable<DomainTask> query, TaskFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            var titleSearch = filter.TitleContains.ToLowerInvariant();
            query = query.Where(t => EF.Functions.Like(t.Title.ToLower(), $"%{titleSearch}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.DescriptionContains))
        {
            var descriptionSearch = filter.DescriptionContains.ToLowerInvariant();
            query = query.Where(t => t.Description != null && EF.Functions.Like(t.Description.ToLower(), $"%{descriptionSearch}%"));
        }
    }

    private IQueryable<DomainTask> ApplySorting(IQueryable<DomainTask> query, TaskFilter filter)
    {
        return filter.SortBy switch
        {
            TaskSortBy.CreatedAt => filter.SortDescending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),

            TaskSortBy.UpdatedAt => filter.SortDescending
                ? query.OrderByDescending(t => t.UpdatedAt)
                : query.OrderBy(t => t.UpdatedAt),

            TaskSortBy.DueDate => filter.SortDescending
                ? query.OrderByDescending(t => t.DueDate)
                : query.OrderBy(t => t.DueDate),

            TaskSortBy.Priority => filter.SortDescending
                ? query.OrderByDescending(t => t.Priority)
                : query.OrderBy(t => t.Priority),

            TaskSortBy.Title => filter.SortDescending
                ? query.OrderByDescending(t => t.Title)
                : query.OrderBy(t => t.Title),

            TaskSortBy.Status => filter.SortDescending
                ? query.OrderByDescending(t => t.Status)
                : query.OrderBy(t => t.Status),

            TaskSortBy.Category => filter.SortDescending
                ? query.OrderByDescending(t => t.Category)
                : query.OrderBy(t => t.Category),

            _ => query.OrderByDescending(t => t.UpdatedAt)
        };
    }

    private async Task<List<DomainTask>> LoadRelatedDataAsync(List<DomainTask> tasks, TaskFilter filter, CancellationToken cancellationToken)
    {
        var taskIds = tasks.Select(t => t.Id).ToList();

        var query = _context.Tasks.Where(t => taskIds.Contains(t.Id));

        if (filter.IncludeSubtasks)
        {
            query = query.Include(t => t.Subtasks.Where(st => !st.IsDeleted));
        }

        if (filter.IncludeContacts)
        {
            query = query.Include(t => t.Contacts);
        }

        if (filter.IncludeProject)
        {
            query = query.Include(t => t.Project);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private List<TaskHierarchyNode> BuildHierarchyNodes(List<DomainTask> allTasks, Guid parentId, int? maxDepth, int currentDepth = 0)
    {
        if (maxDepth.HasValue && currentDepth >= maxDepth.Value)
        {
            return new List<TaskHierarchyNode>();
        }

        var children = allTasks.Where(t => t.ProjectId == parentId).ToList();
        var nodes = new List<TaskHierarchyNode>();

        foreach (var child in children)
        {
            var childNodes = BuildHierarchyNodes(allTasks, child.Id, maxDepth, currentDepth + 1);
            var node = TaskHierarchyNode.Create(child, currentDepth, childNodes);
            nodes.Add(node);
        }

        return nodes;
    }

    private int CalculateMaxDepth(List<TaskHierarchyNode> nodes)
    {
        if (!nodes.Any()) { return 0; }
        return 1 + nodes.Max(n => CalculateMaxDepth(n.Subtasks));
    }

    private int CountCompletedTasks(DomainTask root, IEnumerable<DomainTask> descendants)
    {
        var count = root.Status == (int)DomainTaskStatus.Completed ? 1 : 0;
        count += descendants.Count(t => t.Status == (int)DomainTaskStatus.Completed);
        return count;
    }

    private decimal CalculateCompletionPercentage(DomainTask root, IEnumerable<DomainTask> descendants)
    {
        var total = 1 + descendants.Count();
        var completed = CountCompletedTasks(root, descendants);
        return total > 0 ? (decimal)completed / total * 100 : 0;
    }

    private bool HasOverdueTasks(DomainTask root, IEnumerable<DomainTask> descendants)
    {
        var today = DateTime.UtcNow.Date;

        if (root.DueDate.HasValue && root.DueDate.Value.Date < today &&
            root.Status != (int)DomainTaskStatus.Completed &&
            root.Status != (int)DomainTaskStatus.Archived)
        {
            return true;
        }

        return descendants.Any(t => t.DueDate.HasValue &&
                                  t.DueDate.Value.Date < today &&
                                  t.Status != (int)DomainTaskStatus.Completed &&
                                  t.Status != (int)DomainTaskStatus.Archived);
    }

    #endregion

    #region Soft Delete and Archiving

    public async Task<bool> SoftDeleteTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _dbSet
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId && !t.IsDeleted, cancellationToken);

            if (task == null)
            {
                return false;
            }

            task.SoftDelete();
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Task {TaskId} soft deleted for user {UserId}", taskId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting task {TaskId} for user {UserId}", taskId, userId);
            return false;
        }
    }

    public async Task<bool> RestoreTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use IgnoreQueryFilters to find soft-deleted tasks
            var task = await _context.Tasks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId && t.IsDeleted, cancellationToken);

            if (task == null)
            {
                return false;
            }

            task.Restore();
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Task {TaskId} restored for user {UserId}", taskId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring task {TaskId} for user {UserId}", taskId, userId);
            return false;
        }
    }

    public async Task<IEnumerable<DomainTask>> GetDeletedTasksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Tasks
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId && t.IsDeleted)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deleted tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetArchivableTasksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);

            return await _dbSet
                .Where(t => t.UserId == userId &&
                           (t.Status == (int)DomainTaskStatus.Completed ||
                            (t.Status == (int)DomainTaskStatus.Pending && t.CreatedAt < sixMonthsAgo)))
                .Where(t => t.Status != (int)DomainTaskStatus.Archived)
                .OrderBy(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving archivable tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetArchivedTasksAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId && t.Status == (int)DomainTaskStatus.Archived)
                .OrderByDescending(t => t.UpdatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving archived tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Batch Operations

    public async Task<int> BulkUpdateStatusAsync(IEnumerable<Guid> taskIds, DomainTaskStatus newStatus, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var taskIdsList = taskIds.ToList();
            if (!taskIdsList.Any())
            {
                return 0;
            }

            var tasks = await _dbSet
                .Where(t => taskIdsList.Contains(t.Id) && t.UserId == userId)
                .ToListAsync(cancellationToken);

            var updatedCount = 0;
            foreach (var task in tasks)
            {
                // Validate status transition using domain logic
                if (CanTransitionToStatus(task, newStatus))
                {
                    task.Status = (int)newStatus;
                    task.UpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Bulk updated {UpdatedCount} tasks to status {Status} for user {UserId}",
                    updatedCount, newStatus, userId);
            }

            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating task status to {Status} for user {UserId}", newStatus, userId);
            return 0;
        }
    }

    public async Task<int> BulkArchiveTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var taskIdsList = taskIds.ToList();
            if (!taskIdsList.Any())
            {
                return 0;
            }

            var tasks = await _dbSet
                .Where(t => taskIdsList.Contains(t.Id) && t.UserId == userId)
                .ToListAsync(cancellationToken);

            var archivedCount = 0;
            foreach (var task in tasks)
            {
                if (task.CanBeArchived())
                {
                    task.MarkArchived();
                    archivedCount++;
                }
            }

            if (archivedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Bulk archived {ArchivedCount} tasks for user {UserId}", archivedCount, userId);
            }

            return archivedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk archiving tasks for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> BulkRestoreTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var taskIdsList = taskIds.ToList();
            if (!taskIdsList.Any())
            {
                return 0;
            }

            var tasks = await _context.Tasks
                .IgnoreQueryFilters()
                .Where(t => taskIdsList.Contains(t.Id) && t.UserId == userId && t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var task in tasks)
            {
                task.Restore();
            }

            if (tasks.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Bulk restored {RestoredCount} tasks for user {UserId}", tasks.Count, userId);
            }

            return tasks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk restoring tasks for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> BulkSoftDeleteTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var taskIdsList = taskIds.ToList();
            if (!taskIdsList.Any())
            {
                return 0;
            }

            var tasks = await _dbSet
                .Where(t => taskIdsList.Contains(t.Id) && t.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var task in tasks)
            {
                task.SoftDelete();
            }

            if (tasks.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Bulk soft deleted {DeletedCount} tasks for user {UserId}", tasks.Count, userId);
            }

            return tasks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk soft deleting tasks for user {UserId}", userId);
            return 0;
        }
    }

    #endregion

    #region Performance and Analytics

    public async Task<TaskStatistics> GetTaskStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _dbSet
                .Where(t => t.UserId == userId)
                .ToListAsync(cancellationToken);

            var statistics = new TaskStatistics
            {
                UserId = userId,
                TotalTasks = tasks.Count,
                ActiveTasks = tasks.Count(t => t.Status != (int)DomainTaskStatus.Completed &&
                                               t.Status != (int)DomainTaskStatus.Archived),
                CompletedTasks = tasks.Count(t => t.Status == (int)DomainTaskStatus.Completed),
                ArchivedTasks = tasks.Count(t => t.Status == (int)DomainTaskStatus.Archived),
                OverdueTasks = tasks.Count(t => t.IsOverdue),
                TasksDueToday = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == DateTime.UtcNow.Date),
                GeneratedAt = DateTime.UtcNow,
                Period = TimeSpan.FromDays(365) // Default to last year
            };

            statistics.CompletionPercentage = statistics.TotalTasks > 0
                ? (decimal)statistics.CompletedTasks / statistics.TotalTasks * 100
                : 0;

            // Calculate category statistics
            statistics.CategoryStats = tasks
                .GroupBy(t => (AppTaskCategory)t.Category)
                .ToDictionary(g => g.Key, g => new CategoryStatistics
                {
                    Category = g.Key,
                    TotalTasks = g.Count(),
                    CompletedTasks = g.Count(t => t.Status == (int)DomainTaskStatus.Completed),
                    ActiveTasks = g.Count(t => t.Status != (int)DomainTaskStatus.Completed &&
                                               t.Status != (int)DomainTaskStatus.Archived),
                    CompletionPercentage = g.Any() ? (decimal)g.Count(t => t.Status == (int)DomainTaskStatus.Completed) / g.Count() * 100 : 0
                });

            // Calculate priority statistics
            statistics.PriorityStats = tasks
                .GroupBy(t => (Priority)t.Priority)
                .ToDictionary(g => g.Key, g => new PriorityStatistics
                {
                    Priority = g.Key,
                    TotalTasks = g.Count(),
                    CompletedTasks = g.Count(t => t.Status == (int)DomainTaskStatus.Completed),
                    ActiveTasks = g.Count(t => t.Status != (int)DomainTaskStatus.Completed &&
                                               t.Status != (int)DomainTaskStatus.Archived),
                    OverdueTasks = g.Count(t => t.IsOverdue)
                });

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating task statistics for user {UserId}", userId);
            return new TaskStatistics { UserId = userId, GeneratedAt = DateTime.UtcNow };
        }
    }

    public async Task<TaskCompletionTrends> GetTaskCompletionTrendsAsync(Guid userId, TimeSpan period, CancellationToken cancellationToken = default)
    {
        try
        {
            var startDate = DateTime.UtcNow.Subtract(period).Date;
            var tasks = await _dbSet
                .Where(t => t.UserId == userId && t.UpdatedAt >= startDate)
                .ToListAsync(cancellationToken);

            var trends = new TaskCompletionTrends
            {
                UserId = userId,
                Period = period,
                GeneratedAt = DateTime.UtcNow
            };

            // Generate daily data
            var dailyGroups = tasks
                .Where(t => t.Status == (int)DomainTaskStatus.Completed)
                .GroupBy(t => t.UpdatedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                trends.DailyData.Add(new DailyCompletionData
                {
                    Date = date,
                    CompletedTasks = dailyGroups.GetValueOrDefault(date, 0),
                    CreatedTasks = tasks.Count(t => t.CreatedAt.Date == date),
                    ActiveTasks = tasks.Count(t => t.CreatedAt.Date <= date &&
                                                   (t.Status != (int)DomainTaskStatus.Completed || t.UpdatedAt.Date > date))
                });
            }

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating task completion trends for user {UserId}", userId);
            return new TaskCompletionTrends { UserId = userId, GeneratedAt = DateTime.UtcNow, Period = period };
        }
    }

    public async Task<IEnumerable<DomainTask>> GetMostActiveTasksAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a simplified version - in a real scenario, you'd track task update frequency
            return await _dbSet
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.UpdatedAt)
                .ThenByDescending(t => (t.UpdatedAt - t.CreatedAt).TotalDays)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving most active tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetStaleTasksAsync(Guid userId, TimeSpan inactivePeriod, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(inactivePeriod);
            return await _dbSet
                .Where(t => t.UserId == userId &&
                           t.UpdatedAt < cutoffDate &&
                           t.Status != (int)DomainTaskStatus.Completed &&
                           t.Status != (int)DomainTaskStatus.Archived)
                .OrderBy(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stale tasks for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Data Integrity and Validation

    public async Task<bool> TaskBelongsToUserAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AnyAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking task ownership for task {TaskId} and user {UserId}", taskId, userId);
            return false;
        }
    }

    public async Task<bool> TaskExistsAndActiveAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AnyAsync(t => t.Id == taskId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking task existence for task {TaskId}", taskId);
            return false;
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksWithIntegrityIssuesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find tasks with potential integrity issues
            return await _dbSet
                .Where(t => t.UserId == userId && (
                    // Tasks with invalid project references
                    (t.ProjectId != null && !_context.Tasks.Any(p => p.Id == t.ProjectId.Value)) ||
                    // Tasks with future created dates
                    t.CreatedAt > DateTime.UtcNow ||
                    // Tasks with updated date before created date
                    t.UpdatedAt < t.CreatedAt ||
                    // Tasks with due dates way in the past but still pending
                    (t.Status == (int)DomainTaskStatus.Pending &&
                     t.DueDate.HasValue &&
                     t.DueDate.Value < DateTime.UtcNow.AddYears(-1))
                ))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking task integrity for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    #endregion

    #region Specialized Queries

    public async Task<IEnumerable<DomainTask>> GetProjectConversionCandidatesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Where(t => t.UserId == userId)
                .Where(t => t.CanConvertToProject())
                .OrderByDescending(t => t.Description != null ? t.Description.Length : 0)
                .ThenByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project conversion candidates for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<IEnumerable<DomainTask>> GetTasksNeedingPriorityEscalationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            return await _dbSet
                .Where(t => t.UserId == userId &&
                           t.Status != (int)DomainTaskStatus.Completed &&
                           t.Status != (int)DomainTaskStatus.Archived &&
                           ((t.DueDate.HasValue && t.DueDate.Value.Date <= tomorrow && t.Priority < (int)Priority.High) ||
                            (t.CreatedAt < DateTime.UtcNow.AddDays(-30) && t.Priority == (int)Priority.Low)))
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Priority)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks needing priority escalation for user {UserId}", userId);
            return Enumerable.Empty<DomainTask>();
        }
    }

    public async Task<Dictionary<DayOfWeek, TimeSpan>> GetProductivityPatternsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var completedTasks = await _dbSet
                .Where(t => t.UserId == userId &&
                           t.Status == (int)DomainTaskStatus.Completed &&
                           t.UpdatedAt >= thirtyDaysAgo)
                .Select(t => new { t.CreatedAt, t.UpdatedAt })
                .ToListAsync(cancellationToken);

            var patterns = new Dictionary<DayOfWeek, TimeSpan>();

            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
            {
                var dayTasks = completedTasks.Where(t => t.UpdatedAt.DayOfWeek == day).ToList();
                if (dayTasks.Any())
                {
                    var averageCompletionTime = dayTasks.Average(t => (t.UpdatedAt - t.CreatedAt).TotalMinutes);
                    patterns[day] = TimeSpan.FromMinutes(averageCompletionTime);
                }
                else
                {
                    patterns[day] = TimeSpan.Zero;
                }
            }

            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating productivity patterns for user {UserId}", userId);
            return Enum.GetValues<DayOfWeek>().ToDictionary(d => d, d => TimeSpan.Zero);
        }
    }

    #endregion

    #region Additional Helper Methods

    private bool CanTransitionToStatus(DomainTask task, DomainTaskStatus newStatus)
    {
        var currentStatus = (DomainTaskStatus)task.Status;

        // Basic transition validation
        return newStatus.Value switch
        {
            var val when val == DomainTaskStatus.Pending.Value => currentStatus == DomainTaskStatus.InProgress,
            var val when val == DomainTaskStatus.InProgress.Value => currentStatus != DomainTaskStatus.Archived,
            var val when val == DomainTaskStatus.Completed.Value => task.CanBeCompleted(),
            var val when val == DomainTaskStatus.Archived.Value => task.CanBeArchived(),
            _ => false
        };
    }

    #endregion

    #region Interface Implementation - Missing Methods

    public async Task<DomainTask?> GetByIdWithSubtasksAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await GetTaskWithSubtasksAsync(taskId, Guid.Empty, cancellationToken); // This method exists but needs user filtering fixed
    }

    public async Task<PagedResult<DomainTask>> SearchAsync(AppTaskSearchCriteria searchCriteria, int pageNumber, int pageSize, string sortBy, bool sortDescending, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet.Where(t => t.UserId == (searchCriteria.UserId ?? Guid.Empty));

            // Apply search criteria filters
            if (!string.IsNullOrEmpty(searchCriteria.SearchTerm))
            {
                query = query.Where(t => t.Title.Contains(searchCriteria.SearchTerm) ||
                                       (t.Description != null && t.Description.Contains(searchCriteria.SearchTerm)));
            }

            if (searchCriteria.Category != null)
            {
                query = query.Where(t => t.Category == (int)searchCriteria.Category);
            }

            if (searchCriteria.Status != null)
            {
                query = query.Where(t => t.Status == (int)searchCriteria.Status);
            }

            if (searchCriteria.Priority != null)
            {
                query = query.Where(t => t.Priority == (int)searchCriteria.Priority);
            }

            if (searchCriteria.DueDateStart.HasValue)
            {
                query = query.Where(t => t.DueDate >= searchCriteria.DueDateStart.Value);
            }

            if (searchCriteria.DueDateEnd.HasValue)
            {
                query = query.Where(t => t.DueDate <= searchCriteria.DueDateEnd.Value);
            }

            if (searchCriteria.ProjectId.HasValue)
            {
                query = query.Where(t => t.ProjectId == searchCriteria.ProjectId.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "title" => sortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "duedate" => sortDescending ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
                "priority" => sortDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "createdat" => sortDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
                _ => sortDescending ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt)
            };

            // Apply pagination
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return PagedResult<DomainTask>.Create(items, totalCount, pageNumber, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks with criteria: {@SearchCriteria}", searchCriteria);
            return PagedResult<DomainTask>.Empty(pageNumber, pageSize);
        }
    }

    public async Task<TaskStatistics> GetStatisticsAsync(AppTaskSearchCriteria searchCriteria, CancellationToken cancellationToken = default)
    {
        return await GetTaskStatisticsAsync(searchCriteria.UserId ?? Guid.Empty, cancellationToken); // Method already exists
    }

    #endregion

    #region Contact-Enhanced Retrieval Methods

    public async Task<DomainTask?> GetTaskWithContactsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Include(t => t.TaskContacts)
                    .ThenInclude(tc => tc.Contact)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId && !t.IsDeleted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task {TaskId} with contacts for user {UserId}", taskId, userId);
            return null;
        }
    }

    public async Task<DomainTask?> GetTaskWithSubtasksAndContactsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Include(t => t.Subtasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.TaskContacts)
                        .ThenInclude(tc => tc.Contact)
                .Include(t => t.TaskContacts)
                    .ThenInclude(tc => tc.Contact)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId && !t.IsDeleted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task {TaskId} with subtasks and contacts for user {UserId}", taskId, userId);
            return null;
        }
    }

    /// <summary>
    /// Gets tasks with pagination support for testing compatibility
    /// </summary>
    public async Task<(IList<DomainTask>, int)> GetPagedAsync(AppTaskSearchCriteria searchCriteria, int pageSize, int pageNumber, string sortBy, bool sortDescending, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet.Where(t => !t.IsDeleted);

            // Apply search criteria filters
            if (searchCriteria.UserId.HasValue)
            {
                query = query.Where(t => t.UserId == searchCriteria.UserId.Value);
            }

            if (searchCriteria.Categories?.Any() == true)
            {
                query = query.Where(t => searchCriteria.Categories.Contains(t.Category));
            }

            if (searchCriteria.Statuses?.Any() == true)
            {
                query = query.Where(t => searchCriteria.Statuses.Contains(t.Status));
            }

            if (searchCriteria.Priorities?.Any() == true)
            {
                query = query.Where(t => searchCriteria.Priorities.Contains(t.Priority));
            }

            if (!string.IsNullOrEmpty(searchCriteria.SearchTerm))
            {
                query = query.Where(t => EF.Functions.ILike(t.Title, $"%{searchCriteria.SearchTerm}%") ||
                                        EF.Functions.ILike(t.Description ?? "", $"%{searchCriteria.SearchTerm}%"));
            }

            if (searchCriteria.DueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate >= searchCriteria.DueDateFrom.Value);
            }

            if (searchCriteria.DueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate <= searchCriteria.DueDateTo.Value);
            }

            // Get total count
            int totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting
            query = sortBy.ToLowerInvariant() switch
            {
                "title" => sortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "duedate" => sortDescending ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
                "priority" => sortDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "createdat" => sortDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
                _ => sortDescending ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt)
            };

            // Apply pagination
            var tasks = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (tasks, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged tasks with criteria");
            return (new List<DomainTask>(), 0);
        }
    }

    #endregion
}
