using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Repositories;

/// <summary>
/// Repository interface for Project entities with soft delete support
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Gets a project by ID including soft deleted projects
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The project including if soft deleted, or null if not found</returns>
    public Task<Project?> GetProjectIncludingDeletedAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all soft deleted projects for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of soft deleted projects</returns>
    public Task<IEnumerable<Project>> GetDeletedProjectsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a project with all its tasks (including soft deleted if specified)
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="includeDeletedTasks">Whether to include soft deleted tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The project with tasks, or null if not found</returns>
    public Task<Project?> GetProjectWithTasksAsync(Guid projectId, Guid userId, bool includeDeletedTasks = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the project was soft deleted, false if not found or already deleted</returns>
    public Task<bool> SoftDeleteProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft deleted project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="restoreTasks">Whether to also restore associated tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the project was restored, false if not found or not deleted</returns>
    public Task<bool> RestoreProjectAsync(Guid projectId, Guid userId, bool restoreTasks = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a soft deleted project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the project was permanently deleted, false if not found or not soft deleted</returns>
    public Task<bool> PermanentlyDeleteProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets projects that can be safely deleted (no active tasks)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects that can be safely deleted</returns>
    public Task<IEnumerable<Project>> GetProjectsSafeToDeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active tasks in a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID for security filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of active tasks in the project</returns>
    public Task<int> CountActiveTasksInProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
