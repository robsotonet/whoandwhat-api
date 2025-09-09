using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Data;
using AppTaskCategory = WhoAndWhat.Domain.ValueObjects.AppTaskCategory;
using Priority = WhoAndWhat.Domain.ValueObjects.Priority;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using TaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Service for managing task archiving operations
/// Handles moving completed tasks to archive storage for performance optimization
/// </summary>
public class TaskArchiveService : ITaskArchiveService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TaskArchiveService> _logger;
    private readonly ArchiveSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskArchiveService(
        ApplicationDbContext context,
        ILogger<TaskArchiveService> logger,
        IOptions<ArchiveSettings> settings)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<ArchiveOperationResult> ArchiveTasksAsync(ArchiveCriteria criteria, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting archive operation with criteria: {@Criteria}", criteria);

            if (!_settings.Enabled)
            {
                _logger.LogWarning("Archive operations are disabled in configuration");
                return ArchiveOperationResult.Failure("Archive operations are disabled", duration: stopwatch.Elapsed);
            }

            // Validate criteria
            var validation = ValidateArchiveCriteria(criteria);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid archive criteria: {Errors}", string.Join(", ", validation.Errors));
                return ArchiveOperationResult.Failure($"Invalid criteria: {string.Join(", ", validation.Errors)}", duration: stopwatch.Elapsed);
            }

            // Get eligible tasks
            var eligibleTasks = await GetEligibleTasksForArchivingAsync(criteria, cancellationToken);

            if (!eligibleTasks.Any())
            {
                _logger.LogInformation("No tasks found matching archive criteria");
                stopwatch.Stop();
                return ArchiveOperationResult.Success(0, 0, stopwatch.Elapsed);
            }

            _logger.LogInformation("Found {TaskCount} tasks eligible for archiving", eligibleTasks.Count);

            var archivedTaskIds = new List<Guid>();
            var errors = new List<string>();
            var tasksArchived = 0;

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var task in eligibleTasks)
                {
                    try
                    {
                        var archivedTask = await ArchiveTaskAsync(task, "Automatic", cancellationToken);
                        archivedTaskIds.Add(archivedTask.Id);
                        tasksArchived++;

                        // Remove from active tasks table
                        _context.Tasks.Remove(task);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to archive task {TaskId}: {Error}", task.Id, ex.Message);
                        errors.Add($"Task {task.Id}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Successfully archived {TasksArchived} tasks in {Duration}ms", tasksArchived, stopwatch.ElapsedMilliseconds);

                stopwatch.Stop();

                if (errors.Any())
                {
                    return ArchiveOperationResult.Partial(tasksArchived, errors.Count, 0, stopwatch.Elapsed, errors);
                }

                return ArchiveOperationResult.Success(tasksArchived, 0, stopwatch.Elapsed, archivedTaskIds);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Archive operation failed: {Error}", ex.Message);
            return ArchiveOperationResult.Failure($"Archive operation failed: {ex.Message}", duration: stopwatch.Elapsed);
        }
    }

    public async Task<ArchiveOperationResult> ArchiveUserTasksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var criteria = _settings.ToArchiveCriteria(userId);
        return await ArchiveTasksAsync(criteria, cancellationToken);
    }

    public async Task<ArchiveOperationPreview> PreviewArchiveAsync(ArchiveCriteria criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var eligibleTasks = await GetEligibleTasksForArchivingAsync(criteria, cancellationToken);

            var preview = new ArchiveOperationPreview
            {
                TasksToArchive = eligibleTasks.Count,
                EstimatedDataSize = CalculateEstimatedDataSize(eligibleTasks),
                TasksByCategory = eligibleTasks.GroupBy(t => ((AppTaskCategory)t.Category).ToString())
                                             .ToDictionary(g => g.Key, g => g.Count()),
                TasksByStatus = eligibleTasks.GroupBy(t => ((TaskStatus)t.Status).ToString())
                                           .ToDictionary(g => g.Key, g => g.Count()),
                TasksByPriority = eligibleTasks.GroupBy(t => ((Priority)t.Priority).ToString())
                                             .ToDictionary(g => g.Key, g => g.Count()),
                OldestTaskDate = eligibleTasks.Any() ? DateOnly.FromDateTime(eligibleTasks.Min(t => t.CreatedAt)) : null,
                NewestTaskDate = eligibleTasks.Any() ? DateOnly.FromDateTime(eligibleTasks.Max(t => t.CreatedAt)) : null
            };

            // Add warnings
            var warnings = new List<string>();
            if (preview.TasksToArchive > criteria.MaxArchiveBatchSize)
            {
                warnings.Add($"CRITICAL: Archive batch size ({preview.TasksToArchive}) exceeds maximum ({criteria.MaxArchiveBatchSize}). Consider using smaller batches.");
            }

            if (eligibleTasks.Any(t => t.UpdatedAt > DateTime.UtcNow.AddDays(-7)))
            {
                warnings.Add("WARNING: Some tasks were updated recently (within 7 days). Verify they should be archived.");
            }

            preview = preview with { Warnings = warnings };
            return preview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate archive preview: {Error}", ex.Message);
            return new ArchiveOperationPreview
            {
                Warnings = new List<string> { $"Preview generation failed: {ex.Message}" }
            };
        }
    }

    public async Task<RestoreOperationResult> RestoreArchivedAppTaskAsync(Guid archivedTaskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var archivedTask = await _context.ArchivedAppTasks
                .FirstOrDefaultAsync(at => at.Id == archivedTaskId && at.UserId == userId, cancellationToken);

            if (archivedTask == null)
            {
                return RestoreOperationResult.Failure("Archived task not found or access denied");
            }

            if (!archivedTask.CanBeRestored)
            {
                return RestoreOperationResult.Failure("Task cannot be restored (may be too old or in wrong status)");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Create restored task
                var restoredTask = new DomainTask
                {
                    Id = Guid.NewGuid(), // New ID to avoid conflicts
                    Title = archivedTask.Title,
                    Description = archivedTask.Description,
                    DueDate = archivedTask.DueDate,
                    Priority = archivedTask.Priority,
                    Category = archivedTask.Category,
                    Status = (int)TaskStatus.Pending, // Reset to pending status
                    CreatedAt = archivedTask.CreatedAt,
                    UpdatedAt = DateTime.UtcNow, // Update timestamp
                    UserId = archivedTask.UserId,
                    ProjectId = archivedTask.ProjectId
                };

                _context.Tasks.Add(restoredTask);

                // Remove from archives
                _context.ArchivedAppTasks.Remove(archivedTask);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Successfully restored archived task {ArchivedAppTaskId} as new task {TaskId} for user {UserId}",
                    archivedTaskId, restoredTask.Id, userId);

                return RestoreOperationResult.Success(restoredTask.Id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore archived task {ArchivedAppTaskId}: {Error}", archivedTaskId, ex.Message);
            return RestoreOperationResult.Failure($"Restore failed: {ex.Message}");
        }
    }

    public async Task<PagedResult<ArchivedAppTaskDto>> GetArchivedAppTasksAsync(Guid userId, ArchivedAppTaskFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new ArchivedAppTaskFilter();

        try
        {
            var query = _context.ArchivedAppTasks.Where(at => at.UserId == userId);

            // Apply filters
            query = ApplyFilters(query, filter);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting and pagination
            query = ApplySorting(query, filter);
            query = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize);

            // Execute query and map to DTOs
            var archivedTasks = await query.ToListAsync(cancellationToken);
            var items = archivedTasks.Select(ArchivedAppTaskDto.FromEntity);

            return PagedResult<ArchivedAppTaskDto>.Create(items, totalCount, filter.Page, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archived tasks for user {UserId}: {Error}", userId, ex.Message);
            return PagedResult<ArchivedAppTaskDto>.Empty(filter.Page, filter.PageSize);
        }
    }

    public async Task<ArchiveStatistics> GetArchiveStatisticsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var taskQuery = userId.HasValue
                ? _context.ArchivedAppTasks.Where(at => at.UserId == userId.Value)
                : _context.ArchivedAppTasks;

            var projectQuery = userId.HasValue
                ? _context.ArchivedProjects.Where(ap => ap.UserId == userId.Value)
                : _context.ArchivedProjects;

            var totalTasks = await taskQuery.CountAsync(cancellationToken);
            var totalProjects = await projectQuery.CountAsync(cancellationToken);
            var recentlyArchived = await taskQuery.CountAsync(at => at.ArchivedAt > DateTime.UtcNow.AddDays(-30), cancellationToken);

            var archiveReasons = await taskQuery
                .GroupBy(at => at.ArchiveReason)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

            var categories = await taskQuery
                .GroupBy(at => at.Category)
                .ToDictionaryAsync(g => ((AppTaskCategory)g.Key).ToString(), g => g.Count(), cancellationToken);

            var monthlyStats = await taskQuery
                .Where(at => at.ArchivedAt > DateTime.UtcNow.AddYears(-1))
                .GroupBy(at => new { at.ArchivedAt.Year, at.ArchivedAt.Month })
                .ToDictionaryAsync(g => $"{g.Key.Year}-{g.Key.Month:00}", g => g.Count(), cancellationToken);

            var oldestDate = await taskQuery.MinAsync(at => (DateTime?)at.ArchivedAt, cancellationToken);
            var latestDate = await taskQuery.MaxAsync(at => (DateTime?)at.ArchivedAt, cancellationToken);

            return new ArchiveStatistics
            {
                TotalArchivedAppTasks = totalTasks,
                TotalArchivedProjects = totalProjects,
                RecentlyArchivedAppTasks = recentlyArchived,
                ArchiveReasonBreakdown = archiveReasons,
                CategoryBreakdown = categories,
                MonthlyArchiveCount = monthlyStats,
                OldestArchivedDate = oldestDate,
                LatestArchivedDate = latestDate,
                TotalStorageUsed = await CalculateStorageUsageAsync(taskQuery, projectQuery, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive statistics: {Error}", ex.Message);
            return new ArchiveStatistics();
        }
    }

    public async Task<ArchiveOperationResult> ArchiveProjectAsync(Guid projectId, Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId, cancellationToken);

            if (project == null)
            {
                return ArchiveOperationResult.Failure("Project not found or access denied");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Archive all project tasks first
                var tasksArchived = 0;
                var archivedTaskIds = new List<Guid>();

                foreach (var task in project.Tasks)
                {
                    var archivedTask = await ArchiveTaskAsync(task, $"Project archived: {reason}", cancellationToken);
                    archivedTaskIds.Add(archivedTask.Id);
                    tasksArchived++;
                    _context.Tasks.Remove(task);
                }

                // Archive the project
                var archivedProject = await ArchiveProjectInternalAsync(project, reason, userId, cancellationToken);
                _context.Projects.Remove(project);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation("Successfully archived project {ProjectId} with {TaskCount} tasks",
                    projectId, tasksArchived);

                return ArchiveOperationResult.Success(tasksArchived, 1, stopwatch.Elapsed,
                    archivedTaskIds, new List<Guid> { archivedProject.Id });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to archive project {ProjectId}: {Error}", projectId, ex.Message);
            return ArchiveOperationResult.Failure($"Project archive failed: {ex.Message}", duration: stopwatch.Elapsed);
        }
    }

    public async Task<CleanupOperationResult> CleanupExpiredArchivesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cutoffDate = DateTime.UtcNow - retentionPeriod;

            var expiredTasks = await _context.ArchivedAppTasks
                .Where(at => at.ArchivedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            var expiredProjects = await _context.ArchivedProjects
                .Where(ap => ap.ArchivedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (!expiredTasks.Any() && !expiredProjects.Any())
            {
                stopwatch.Stop();
                return CleanupOperationResult.Success(0, 0, 0, stopwatch.Elapsed);
            }

            var estimatedSpaceFreed = CalculateEstimatedDataSize(expiredTasks) + CalculateEstimatedDataSize(expiredProjects);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                _context.ArchivedAppTasks.RemoveRange(expiredTasks);
                _context.ArchivedProjects.RemoveRange(expiredProjects);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation("Cleaned up {TaskCount} expired tasks and {ProjectCount} expired projects, freed ~{SpaceFreed} bytes",
                    expiredTasks.Count, expiredProjects.Count, estimatedSpaceFreed);

                return CleanupOperationResult.Success(expiredTasks.Count, expiredProjects.Count, estimatedSpaceFreed, stopwatch.Elapsed);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Cleanup operation failed: {Error}", ex.Message);
            return new CleanupOperationResult
            {
                IsSuccess = false,
                Errors = new List<string> { ex.Message },
                Duration = stopwatch.Elapsed
            };
        }
    }

    public ArchiveValidationResult ValidateArchiveCriteria(ArchiveCriteria criteria)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (criteria.MinimumCompletedAge <= TimeSpan.Zero)
        {
            errors.Add("Minimum completed age must be greater than zero");
        }

        if (criteria.MinimumCanceledAge <= TimeSpan.Zero)
        {
            errors.Add("Minimum canceled age must be greater than zero");
        }

        if (criteria.MaxArchiveBatchSize <= 0)
        {
            errors.Add("Maximum batch size must be greater than zero");
        }

        if (criteria.MaxArchiveBatchSize > _settings.MaxArchiveBatchSize)
        {
            errors.Add($"Batch size cannot exceed configured maximum of {_settings.MaxArchiveBatchSize}");
        }

        if (criteria.MinimumCompletedAge < TimeSpan.FromDays(1))
        {
            warnings.Add("Archiving tasks less than 1 day old may be premature");
        }

        if (criteria.MaxArchiveBatchSize > 5000)
        {
            warnings.Add("Large batch sizes may impact performance");
        }

        return errors.Any()
            ? ArchiveValidationResult.Invalid(errors, warnings)
            : ArchiveValidationResult.Valid() with { Warnings = warnings };
    }

    private async Task<List<DomainTask>> GetEligibleTasksForArchivingAsync(ArchiveCriteria criteria, CancellationToken cancellationToken)
    {
        var currentTime = DateTime.UtcNow;

        var query = _context.Tasks.AsQueryable();

        // Apply user filter if specified
        if (criteria.UserId.HasValue)
        {
            query = query.Where(t => t.UserId == criteria.UserId.Value);
        }

        // Apply status filter
        query = query.Where(t => criteria.ArchivableStatuses.Contains((TaskStatus)t.Status));

        // Apply age filters based on status
        var completedCutoff = currentTime - criteria.MinimumCompletedAge;
        var canceledCutoff = currentTime - criteria.MinimumCanceledAge;

        query = query.Where(t =>
            (t.Status == (int)TaskStatus.Completed && t.UpdatedAt < completedCutoff) ||
            (t.Status == (int)TaskStatus.Archived && t.UpdatedAt < canceledCutoff));

        // Apply priority filter if specified
        if (criteria.MaxPriorityToArchive != null)
        {
            query = query.Where(t => t.Priority <= (int)criteria.MaxPriorityToArchive);
        }

        // Include navigation properties for related data
        query = query.Include(t => t.Subtasks)
                    .Include(t => t.Contacts)
                    .Include(t => t.Project);

        // Apply batch size limit
        query = query.Take(criteria.MaxArchiveBatchSize);

        var tasks = await query.ToListAsync(cancellationToken);

        // Apply additional business rule filters that can't be done in SQL
        var eligibleTasks = new List<DomainTask>();
        foreach (var task in tasks)
        {
            if (criteria.ShouldArchiveTask(task, currentTime))
            {
                eligibleTasks.Add(task);
            }
        }

        return eligibleTasks;
    }

    private Task<ArchivedAppTask> ArchiveTaskAsync(DomainTask task, string reason, CancellationToken cancellationToken)
    {
        var archivedTask = new ArchivedAppTask
        {
            Id = Guid.NewGuid(),
            OriginalAppTaskId = task.Id,
            UserId = task.UserId,
            Title = task.Title,
            Description = task.Description,
            DueDate = task.DueDate,
            Priority = task.Priority,
            Category = task.Category,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            CompletedAt = null, // Task entity doesn't have CompletedAt
            ArchivedAt = DateTime.UtcNow,
            ArchiveReason = reason,
            ProjectId = task.ProjectId,
            ProjectName = task.Project?.Name
        };

        // Serialize related data
        if (task.Subtasks.Any())
        {
            var subtasksData = task.Subtasks.Select(st => new
            {
                st.Id,
                st.Title,
                st.Status,
                st.CreatedAt,
                st.UpdatedAt
            });
            archivedTask.SubtasksJson = JsonSerializer.Serialize(subtasksData, _jsonOptions);
        }

        if (task.Contacts.Any())
        {
            var contactsData = task.Contacts.Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.RelationshipType
            });
            archivedTask.ContactsJson = JsonSerializer.Serialize(contactsData, _jsonOptions);
        }

        _context.ArchivedAppTasks.Add(archivedTask);
        return System.Threading.Tasks.Task.FromResult(archivedTask);
    }

    private Task<ArchivedProject> ArchiveProjectInternalAsync(Project project, string reason, Guid? archivedByUserId, CancellationToken cancellationToken)
    {
        var archivedProject = new ArchivedProject
        {
            Id = Guid.NewGuid(),
            OriginalProjectId = project.Id,
            UserId = project.UserId,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Status = project.Status,
            Progress = project.Progress,
            CreatedAt = DateTime.UtcNow, // Project entity doesn't have CreatedAt, using current time
            UpdatedAt = DateTime.UtcNow, // Project entity doesn't have UpdatedAt, using current time
            CompletedAt = project.EndDate, // Using EndDate as completion date
            ArchivedAt = DateTime.UtcNow,
            ArchiveReason = reason,
            ArchivedByUserId = archivedByUserId,
            TotalTasksCount = project.Tasks.Count,
            CompletedTasksCount = project.Tasks.Count(t => t.Status == (int)TaskStatus.Completed)
        };

        if (project.StartDate.HasValue && project.EndDate.HasValue)
        {
            archivedProject.TotalDuration = project.EndDate.Value - project.StartDate.Value;
        }

        // Serialize tasks data
        if (project.Tasks.Any())
        {
            var tasksData = project.Tasks.Select(t => new
            {
                t.Id,
                t.Title,
                Status = ((TaskStatus)t.Status).ToString(),
                t.CreatedAt,
                t.UpdatedAt
            });
            archivedProject.TasksJson = JsonSerializer.Serialize(tasksData, _jsonOptions);
        }

        _context.ArchivedProjects.Add(archivedProject);
        return System.Threading.Tasks.Task.FromResult(archivedProject);
    }

    private IQueryable<ArchivedAppTask> ApplyFilters(IQueryable<ArchivedAppTask> query, ArchivedAppTaskFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Category))
        {
            if (AppTaskCategory.TryFromName(filter.Category, out var category) && category != null)
            {
                query = query.Where(at => at.Category == (int)category);
            }
        }

        if (!string.IsNullOrEmpty(filter.Status))
        {
            if (AppTaskStatus.TryFromName(filter.Status, out var status) && status != null)
            {
                query = query.Where(at => at.Status == (int)status);
            }
        }

        if (!string.IsNullOrEmpty(filter.Priority))
        {
            if (Priority.TryFromName(filter.Priority, out var priority) && priority != null)
            {
                query = query.Where(at => at.Priority == (int)priority);
            }
        }

        if (!string.IsNullOrEmpty(filter.ArchiveReason))
        {
            query = query.Where(at => at.ArchiveReason.Contains(filter.ArchiveReason));
        }

        if (filter.ArchivedAfter.HasValue)
        {
            query = query.Where(at => at.ArchivedAt >= filter.ArchivedAfter.Value);
        }

        if (filter.ArchivedBefore.HasValue)
        {
            query = query.Where(at => at.ArchivedAt <= filter.ArchivedBefore.Value);
        }

        if (filter.CreatedAfter.HasValue)
        {
            query = query.Where(at => at.CreatedAt >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            query = query.Where(at => at.CreatedAt <= filter.CreatedBefore.Value);
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(at => at.ProjectId == filter.ProjectId.Value);
        }

        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(at => at.Title.ToLower().Contains(searchTerm) ||
                                     (at.Description != null && at.Description.ToLower().Contains(searchTerm)));
        }

        if (filter.OnlyRestorable == true)
        {
            query = query.Where(at => at.CanBeRestored);
        }

        return query;
    }

    private IQueryable<ArchivedAppTask> ApplySorting(IQueryable<ArchivedAppTask> query, ArchivedAppTaskFilter filter)
    {
        return filter.SortBy switch
        {
            ArchivedAppTaskSortBy.Title => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.Title)
                : query.OrderByDescending(at => at.Title),
            ArchivedAppTaskSortBy.CreatedAt => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.CreatedAt)
                : query.OrderByDescending(at => at.CreatedAt),
            ArchivedAppTaskSortBy.UpdatedAt => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.UpdatedAt)
                : query.OrderByDescending(at => at.UpdatedAt),
            ArchivedAppTaskSortBy.ArchivedAt => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.ArchivedAt)
                : query.OrderByDescending(at => at.ArchivedAt),
            ArchivedAppTaskSortBy.DueDate => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.DueDate ?? DateTime.MaxValue)
                : query.OrderByDescending(at => at.DueDate ?? DateTime.MinValue),
            ArchivedAppTaskSortBy.Priority => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.Priority)
                : query.OrderByDescending(at => at.Priority),
            ArchivedAppTaskSortBy.Category => filter.SortDirection == SortDirection.Ascending
                ? query.OrderBy(at => at.Category)
                : query.OrderByDescending(at => at.Category),
            _ => query.OrderByDescending(at => at.ArchivedAt)
        };
    }

    private long CalculateEstimatedDataSize(IEnumerable<DomainTask> tasks)
    {
        // Rough estimation: base task size + JSON data
        return tasks.Sum(t =>
            (t.Title?.Length ?? 0) +
            (t.Description?.Length ?? 0) +
            500 + // Base entity size estimate
            (t.Subtasks.Count * 200) + // Estimated subtask JSON size
            (t.Contacts.Count * 150)); // Estimated contact JSON size
    }

    private long CalculateEstimatedDataSize(IEnumerable<ArchivedAppTask> tasks)
    {
        return tasks.Sum(at =>
            (at.Title?.Length ?? 0) +
            (at.Description?.Length ?? 0) +
            (at.SubtasksJson?.Length ?? 0) +
            (at.ContactsJson?.Length ?? 0) +
            (at.AttachmentsJson?.Length ?? 0) +
            800); // Base archived entity size estimate
    }

    private long CalculateEstimatedDataSize(IEnumerable<ArchivedProject> projects)
    {
        return projects.Sum(ap =>
            (ap.Name?.Length ?? 0) +
            (ap.Description?.Length ?? 0) +
            (ap.TasksJson?.Length ?? 0) +
            (ap.ContactsJson?.Length ?? 0) +
            (ap.MetadataJson?.Length ?? 0) +
            1000); // Base archived project size estimate
    }

    private async Task<long> CalculateStorageUsageAsync(IQueryable<ArchivedAppTask> taskQuery, IQueryable<ArchivedProject> projectQuery, CancellationToken cancellationToken)
    {
        // This is a rough estimate - in production you might want more accurate calculations
        var taskCount = await taskQuery.CountAsync(cancellationToken);
        var projectCount = await projectQuery.CountAsync(cancellationToken);

        return (taskCount * 2000L) + (projectCount * 5000L); // Estimated average sizes
    }
}
