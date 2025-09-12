using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainAppTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Advanced repository interface for Task-specific operations
/// Extends the generic repository with domain-specific functionality
/// </summary>
public interface IAppTaskRepository : IRepository<DomainAppTask>
{
    #region Enhanced Retrieval Methods

    /// <summary>
    /// Gets a task with all its subtasks loaded
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AppTask with subtasks or null if not found</returns>
    public Task<DomainAppTask?> GetTaskWithSubtasksAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a task by ID with subtasks loaded (alternative method name for compatibility)
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AppTask with subtasks or null if not found</returns>
    public Task<DomainAppTask?> GetByIdWithSubtasksAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tasks belonging to a specific project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="includeCompleted">Include completed tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks in the project</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksByProjectIdAsync(Guid projectId, Guid userId, bool includeCompleted = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks for a user with advanced filtering
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="filter">Advanced filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered tasks with total count</returns>
    public Task<(IEnumerable<DomainAppTask> Tasks, int TotalCount)> GetTasksByUserIdAsync(Guid userId, TaskFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all overdue tasks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Overdue tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetOverdueTasksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks within a specific date range
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="fromDate">Start date (inclusive)</param>
    /// <param name="toDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks in date range</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksForDateRangeAsync(Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recently created or updated tasks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="count">Maximum number of tasks to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recent tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetRecentTasksAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default);

    #endregion

    #region Hierarchy and Relationship Methods

    /// <summary>
    /// Gets the complete task hierarchy starting from a root task
    /// </summary>
    /// <param name="rootTaskId">The root task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="maxDepth">Maximum depth to traverse (null for unlimited)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete task hierarchy</returns>
    public Task<TaskHierarchyResult?> GetTaskHierarchyAsync(Guid rootTaskId, Guid userId, int? maxDepth = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active subtasks for a parent task
    /// </summary>
    /// <param name="parentTaskId">The parent task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of active subtasks</returns>
    public Task<int> GetActiveSubtasksCountAsync(Guid parentTaskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ancestry path of a task (from root to task)
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ancestor tasks from root to parent</returns>
    public Task<IEnumerable<DomainAppTask>> GetTaskAncestorsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all descendant tasks (children and their children)
    /// </summary>
    /// <param name="taskId">The parent task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All descendant tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetTaskDescendantsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct subtasks of a parent task
    /// </summary>
    /// <param name="parentTaskId">The parent task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Direct subtasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetSubtasksAsync(Guid parentTaskId, Guid userId, CancellationToken cancellationToken = default);

    #endregion

    #region Advanced Filtering and Search

    /// <summary>
    /// Gets tasks by specific category
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="category">The task category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks in the category</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksByCategoryAsync(Guid userId, AppTaskCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks by specific status
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="status">The task status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks with the status</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksByStatusAsync(Guid userId, DomainTaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks by specific priority
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="priority">The priority level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks with the priority</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksByPriorityAsync(Guid userId, Priority priority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks due today for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks due today</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksDueTodayAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks due within the next specified days
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="days">Number of days to look ahead</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks due soon</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksDueSoonAsync(Guid userId, int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches tasks by title or description content
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="searchTerm">The search term</param>
    /// <param name="maxResults">Maximum results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching tasks</returns>
    public Task<IEnumerable<DomainAppTask>> SearchTasksAsync(Guid userId, string searchTerm, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches tasks using advanced criteria with pagination
    /// </summary>
    /// <param name="searchCriteria">Search criteria</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="sortBy">Sort field name</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged search results</returns>
    public Task<PagedResult<DomainAppTask>> SearchAsync(AppTaskSearchCriteria searchCriteria, int pageNumber, int pageSize, string sortBy, bool sortDescending, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks with pagination support for testing compatibility
    /// </summary>
    /// <param name="searchCriteria">Search criteria</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="sortBy">Sort field name</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks with total count</returns>
    public Task<(IList<DomainAppTask>, int)> GetPagedAsync(AppTaskSearchCriteria searchCriteria, int pageSize, int pageNumber, string sortBy, bool sortDescending, CancellationToken cancellationToken = default);

    #endregion

    #region Soft Delete and Archiving

    /// <summary>
    /// Soft deletes a task (marks as deleted without removing from database)
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully deleted</returns>
    public Task<bool> SoftDeleteTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted task
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully restored</returns>
    public Task<bool> RestoreTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all soft-deleted tasks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Soft-deleted tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetDeletedTasksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks that are eligible for archiving
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks ready for archiving</returns>
    public Task<IEnumerable<DomainAppTask>> GetArchivableTasksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets archived tasks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archived tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetArchivedTasksAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Batch Operations

    /// <summary>
    /// Updates the status of multiple tasks in a single operation
    /// </summary>
    /// <param name="taskIds">The task IDs to update</param>
    /// <param name="newStatus">The new status</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks successfully updated</returns>
    public Task<int> BulkUpdateStatusAsync(IEnumerable<Guid> taskIds, DomainTaskStatus newStatus, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives multiple tasks in a single operation
    /// </summary>
    /// <param name="taskIds">The task IDs to archive</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks successfully archived</returns>
    public Task<int> BulkArchiveTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores multiple soft-deleted tasks in a single operation
    /// </summary>
    /// <param name="taskIds">The task IDs to restore</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks successfully restored</returns>
    public Task<int> BulkRestoreTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes multiple tasks in a single operation
    /// </summary>
    /// <param name="taskIds">The task IDs to delete</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks successfully deleted</returns>
    public Task<int> BulkSoftDeleteTasksAsync(IEnumerable<Guid> taskIds, Guid userId, CancellationToken cancellationToken = default);

    #endregion

    #region Performance and Analytics

    /// <summary>
    /// Gets comprehensive task statistics for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AppTask statistics</returns>
    public Task<TaskStatistics> GetStatisticsAsync(AppTaskSearchCriteria searchCriteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets task completion trends for a user over a specified period
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="period">The time period to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AppTask completion trends</returns>
    public Task<TaskCompletionTrends> GetTaskCompletionTrendsAsync(Guid userId, TimeSpan period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most frequently updated tasks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="count">Number of tasks to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Most active tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetMostActiveTasksAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks that haven't been updated for a specified period
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="inactivePeriod">Period of inactivity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stale tasks</returns>
    public Task<IEnumerable<DomainAppTask>> GetStaleTasksAsync(Guid userId, TimeSpan inactivePeriod, CancellationToken cancellationToken = default);

    #endregion

    #region Data Integrity and Validation

    /// <summary>
    /// Validates that a task belongs to the specified user
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the task belongs to the user</returns>
    public Task<bool> TaskBelongsToUserAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a task exists and is not soft-deleted
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the task exists and is active</returns>
    public Task<bool> TaskExistsAndActiveAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks that may have data integrity issues
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks with potential issues</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksWithIntegrityIssuesAsync(Guid userId, CancellationToken cancellationToken = default);

    #endregion

    #region Specialized Queries

    /// <summary>
    /// Gets tasks that are good candidates for project conversion
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks suitable for project conversion</returns>
    public Task<IEnumerable<DomainAppTask>> GetProjectConversionCandidatesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks that need priority escalation based on due dates and age
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tasks needing priority escalation</returns>
    public Task<IEnumerable<DomainAppTask>> GetTasksNeedingPriorityEscalationAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user's productivity patterns by analyzing task creation and completion times
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity patterns</returns>
    public Task<Dictionary<DayOfWeek, TimeSpan>> GetProductivityPatternsAsync(Guid userId, CancellationToken cancellationToken = default);

    #endregion

    #region Contact-Enhanced Retrieval Methods

    /// <summary>
    /// Gets a task by ID including contact information
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task with contact information or null if not found</returns>
    public Task<DomainAppTask?> GetTaskWithContactsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a task by ID with subtasks and contact information
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task with subtasks and contact information or null if not found</returns>
    public Task<DomainAppTask?> GetTaskWithSubtasksAndContactsAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    #endregion
}
