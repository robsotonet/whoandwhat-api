# Phase 3: Task Management Core 📋

## Overview
This phase implements the core task management functionality - the heart of the WhoAndWhat platform. It includes CRUD operations, categorization, project management, and advanced task features.

## Prerequisites
- Phase 2 (Authentication) completed and verified
- User authentication working properly
- Database schema supports task entities
- Redis cache available for performance optimization

## Phase Objectives
- [x] Task CRUD operations with full validation
- [x] Task categorization and priority management
- [x] Task-to-project conversion functionality
- [x] Advanced search and filtering
- [x] Performance optimization with caching
- [x] Batch operations support

## Business Rules
- Maximum 10,000 tasks per user
- Maximum project depth of 5 levels
- Task titles limited to 200 characters
- Task descriptions limited to 5,000 characters
- Due dates must be in the future when set
- Projects can contain unlimited tasks

## Developer A Tasks - Task Infrastructure

### Task P3.A.1: Set up task caching and performance optimization
**Duration**: 2 days | **Priority**: High | **Depends on**: P2 Complete

**Redis Caching Strategy**:
```csharp
public class TaskCacheService : ITaskCacheService
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30);

    public async Task<IEnumerable<TaskResponseDto>?> GetUserTasksCacheAsync(Guid userId, TaskFilter filter)
    {
        var cacheKey = GenerateCacheKey(userId, filter);
        var cachedData = await _cache.GetStringAsync(cacheKey);
        
        if (cachedData != null)
        {
            return JsonSerializer.Deserialize<IEnumerable<TaskResponseDto>>(cachedData);
        }
        
        return null;
    }

    public async Task SetUserTasksCacheAsync(Guid userId, TaskFilter filter, IEnumerable<TaskResponseDto> tasks)
    {
        var cacheKey = GenerateCacheKey(userId, filter);
        var serializedData = JsonSerializer.Serialize(tasks);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _defaultExpiry
        };
        
        await _cache.SetStringAsync(cacheKey, serializedData, options);
    }

    public async Task InvalidateUserTasksCacheAsync(Guid userId)
    {
        var pattern = $"tasks:user:{userId}:*";
        await InvalidateCachePattern(pattern);
    }

    private static string GenerateCacheKey(Guid userId, TaskFilter filter)
    {
        var filterHash = GenerateFilterHash(filter);
        return $"tasks:user:{userId}:filter:{filterHash}";
    }
}

public class TaskPerformanceService : ITaskPerformanceService
{
    public async Task<IEnumerable<Task>> GetTasksOptimizedAsync(Guid userId, TaskFilter filter)
    {
        var query = _context.Tasks
            .Where(t => t.UserId == userId)
            .AsQueryable();

        // Apply filters with optimal query construction
        if (filter.Categories?.Any() == true)
            query = query.Where(t => filter.Categories.Contains(t.Category.Value));

        if (filter.Priorities?.Any() == true)
            query = query.Where(t => filter.Priorities.Contains(t.Priority.Value));

        if (filter.Status != null)
            query = query.Where(t => t.Status == filter.Status);

        if (filter.DueDateFrom.HasValue)
            query = query.Where(t => t.DueDate >= filter.DueDateFrom);

        if (filter.DueDateTo.HasValue)
            query = query.Where(t => t.DueDate <= filter.DueDateTo);

        // Apply ordering
        query = filter.OrderBy switch
        {
            TaskOrderBy.DueDate => query.OrderBy(t => t.DueDate ?? DateTime.MaxValue),
            TaskOrderBy.Priority => query.OrderByDescending(t => t.Priority.Value),
            TaskOrderBy.Created => query.OrderByDescending(t => t.CreatedAt),
            TaskOrderBy.Updated => query.OrderByDescending(t => t.UpdatedAt),
            _ => query.OrderByDescending(t => t.UpdatedAt)
        };

        // Apply pagination
        if (filter.Skip.HasValue)
            query = query.Skip(filter.Skip.Value);

        if (filter.Take.HasValue)
            query = query.Take(filter.Take.Value);

        return await query
            .Include(t => t.Project)
            .Include(t => t.Contacts)
            .AsSplitQuery()
            .ToListAsync();
    }
}
```

**Deliverables**:
- [ ] Redis caching implementation for task data
- [ ] Cache invalidation strategies
- [ ] Performance-optimized queries
- [ ] Query result caching
- [ ] Performance monitoring
- [ ] Load testing validation

### Task P3.A.2: Implement task search and indexing
**Duration**: 3 days | **Priority**: Medium | **Depends on**: P3.A.1

**Full-text Search Implementation**:
```csharp
public class TaskSearchService : ITaskSearchService
{
    public async Task<SearchResult<TaskResponseDto>> SearchTasksAsync(Guid userId, TaskSearchQuery query)
    {
        var searchQuery = _context.Tasks
            .Where(t => t.UserId == userId)
            .AsQueryable();

        // Full-text search on title and description
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim().ToLower();
            searchQuery = searchQuery.Where(t => 
                EF.Functions.ToTsVector("english", t.Title + " " + (t.Description ?? ""))
                .Matches(EF.Functions.ToTsQuery("english", searchTerm)));
        }

        // Apply additional filters
        searchQuery = ApplyFilters(searchQuery, query.Filters);

        // Count total results
        var totalCount = await searchQuery.CountAsync();

        // Apply pagination and get results
        var tasks = await searchQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(t => TaskResponseDto.FromEntity(t))
            .ToListAsync();

        return new SearchResult<TaskResponseDto>
        {
            Items = tasks,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<string>> GetSearchSuggestionsAsync(Guid userId, string partialTerm)
    {
        var suggestions = await _context.Tasks
            .Where(t => t.UserId == userId)
            .Where(t => t.Title.Contains(partialTerm) || 
                       (t.Description != null && t.Description.Contains(partialTerm)))
            .Select(t => t.Title)
            .Distinct()
            .Take(10)
            .ToListAsync();

        return suggestions;
    }
}

// Database migration for full-text search
public partial class AddFullTextSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_fulltext 
            ON tasks USING gin(to_tsvector('english', title || ' ' || COALESCE(description, '')))
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_title_trigram 
            ON tasks USING gin(title gin_trgm_ops)
        ");
    }
}
```

**Deliverables**:
- [ ] Full-text search implementation
- [ ] Search indexing with PostgreSQL
- [ ] Auto-complete/suggestions functionality
- [ ] Search performance optimization
- [ ] Search analytics tracking
- [ ] Unit and integration tests

### Task P3.A.3: Set up task data backup and archiving
**Duration**: 2 days | **Priority**: Low | **Depends on**: P3.A.1

**Data Archiving Service**:
```csharp
public class TaskArchiveService : ITaskArchiveService
{
    public async Task ArchiveCompletedTasksAsync(TimeSpan olderThan)
    {
        var cutoffDate = DateTime.UtcNow - olderThan;
        var tasksToArchive = await _context.Tasks
            .Where(t => t.Status == TaskStatus.Completed && 
                       t.CompletedAt < cutoffDate)
            .ToListAsync();

        if (!tasksToArchive.Any()) return;

        // Move to archive table
        var archivedTasks = tasksToArchive.Select(t => new ArchivedTask
        {
            OriginalId = t.Id,
            UserId = t.UserId,
            Title = t.Title,
            Description = t.Description,
            Category = t.Category,
            Priority = t.Priority,
            Status = t.Status,
            DueDate = t.DueDate,
            CreatedAt = t.CreatedAt,
            CompletedAt = t.CompletedAt,
            ArchivedAt = DateTime.UtcNow
        }).ToList();

        await _context.ArchivedTasks.AddRangeAsync(archivedTasks);
        _context.Tasks.RemoveRange(tasksToArchive);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Archived {tasksToArchive.Count} completed tasks");
    }

    public async Task BackupUserDataAsync(Guid userId, string backupPath)
    {
        var userData = await _context.Tasks
            .Where(t => t.UserId == userId)
            .Select(t => new TaskBackupDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Category = t.Category.Value,
                Priority = t.Priority.Value,
                Status = t.Status.ToString(),
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        var json = JsonSerializer.Serialize(userData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(Path.Combine(backupPath, $"tasks_backup_{userId}_{DateTime.UtcNow:yyyyMMdd}.json"), json);
    }
}
```

**Deliverables**:
- [ ] Task archiving system
- [ ] Data backup functionality
- [ ] Automated cleanup jobs
- [ ] Data retention policies
- [ ] Recovery procedures
- [ ] Archive performance tests

## Developer B Tasks - Task Domain & Data

### Task P3.B.1: Implement complete Task domain model
**Duration**: 3 days | **Priority**: Critical | **Blocks**: All task operations

**Enhanced Task Entity**:
```csharp
public class Task : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public TaskCategory Category { get; private set; }
    public Priority Priority { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? ParentTaskId { get; private set; }

    // Navigation properties
    public User User { get; private set; } = null!;
    public Project? Project { get; private set; }
    public Task? ParentTask { get; private set; }

    private readonly List<Task> _subtasks = new();
    public IReadOnlyList<Task> Subtasks => _subtasks.AsReadOnly();

    private readonly List<Contact> _contacts = new();
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    private readonly List<TaskAttachment> _attachments = new();
    public IReadOnlyList<TaskAttachment> Attachments => _attachments.AsReadOnly();

    public Task(Guid userId, string title, TaskCategory category, Priority? priority = null)
    {
        UserId = Guard.Against.Default(userId, nameof(userId));
        Title = Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Category = Guard.Against.Null(category, nameof(category));
        Priority = priority ?? Priority.Medium;
        Status = TaskStatus.Pending;

        ValidateBusinessRules();
        AddDomainEvent(new TaskCreatedEvent(Id, UserId, Title, Category));
    }

    public void UpdateTitle(string title)
    {
        Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Guard.Against.StringTooLong(title, 200, nameof(title));

        if (Title == title) return;

        Title = title;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskUpdatedEvent(Id, UserId));
    }

    public void UpdateDescription(string? description)
    {
        if (description != null)
            Guard.Against.StringTooLong(description, 5000, nameof(description));

        if (Description == description) return;

        Description = description;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskUpdatedEvent(Id, UserId));
    }

    public void SetDueDate(DateTime? dueDate)
    {
        if (dueDate.HasValue && dueDate.Value <= DateTime.UtcNow)
            throw new DomainException("Due date must be in the future");

        if (DueDate == dueDate) return;

        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskDueDateChangedEvent(Id, UserId, dueDate));
    }

    public void ChangePriority(Priority priority)
    {
        Guard.Against.Null(priority, nameof(priority));

        if (Priority.Equals(priority)) return;

        Priority = priority;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskPriorityChangedEvent(Id, UserId, priority));
    }

    public void ChangeCategory(TaskCategory category)
    {
        Guard.Against.Null(category, nameof(category));

        if (Category.Equals(category)) return;

        var oldCategory = Category;
        Category = category;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskCategoryChangedEvent(Id, UserId, oldCategory, category));
    }

    public void Complete()
    {
        if (Status == TaskStatus.Completed) return;

        // Complete all subtasks first
        foreach (var subtask in _subtasks.Where(s => s.Status != TaskStatus.Completed))
        {
            subtask.Complete();
        }

        Status = TaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskCompletedEvent(Id, UserId, CompletedAt.Value));
    }

    public void Reopen()
    {
        if (Status != TaskStatus.Completed)
            throw new DomainException("Only completed tasks can be reopened");

        Status = TaskStatus.Pending;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskReopenedEvent(Id, UserId));
    }

    public Project ConvertToProject(string projectName, string? projectDescription = null)
    {
        if (Status == TaskStatus.Completed)
            throw new DomainException("Cannot convert completed task to project");

        var project = new Project(UserId, projectName, projectDescription);
        
        // Move this task to the project
        ProjectId = project.Id;
        Category = TaskCategory.Project;
        
        // Move subtasks to project
        foreach (var subtask in _subtasks)
        {
            subtask.ProjectId = project.Id;
        }

        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskConvertedToProjectEvent(Id, project.Id, UserId));
        
        return project;
    }

    public void AssignToProject(Guid projectId)
    {
        if (ProjectId == projectId) return;

        ProjectId = projectId;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskAssignedToProjectEvent(Id, projectId, UserId));
    }

    public void AddSubtask(Task subtask)
    {
        Guard.Against.Null(subtask, nameof(subtask));
        
        if (subtask.UserId != UserId)
            throw new DomainException("Subtask must belong to the same user");

        if (_subtasks.Any(s => s.Id == subtask.Id))
            return;

        subtask.ParentTaskId = Id;
        if (ProjectId.HasValue)
            subtask.ProjectId = ProjectId;

        _subtasks.Add(subtask);
        AddDomainEvent(new SubtaskAddedEvent(Id, subtask.Id, UserId));
    }

    public void RemoveSubtask(Guid subtaskId)
    {
        var subtask = _subtasks.FirstOrDefault(s => s.Id == subtaskId);
        if (subtask == null) return;

        subtask.ParentTaskId = null;
        _subtasks.Remove(subtask);
        AddDomainEvent(new SubtaskRemovedEvent(Id, subtaskId, UserId));
    }

    private void ValidateBusinessRules()
    {
        if (Title.Length > 200)
            throw new DomainException("Task title cannot exceed 200 characters");

        if (Description?.Length > 5000)
            throw new DomainException("Task description cannot exceed 5000 characters");

        if (DueDate.HasValue && DueDate.Value <= DateTime.UtcNow)
            throw new DomainException("Due date must be in the future");
    }
}

public enum TaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    OnHold = 5
}
```

**Task Domain Services**:
```csharp
public class TaskDomainService : ITaskDomainService
{
    public async Task<Result<Task>> CreateTaskAsync(Guid userId, CreateTaskCommand command)
    {
        // Validate user task limit
        var userTaskCount = await _taskRepository.GetUserTaskCountAsync(userId);
        if (userTaskCount >= 10000)
            return Result<Task>.Failure("Maximum task limit reached (10,000 tasks per user)");

        var category = TaskCategory.FromString(command.Category);
        var priority = command.Priority.HasValue ? Priority.FromValue(command.Priority.Value) : Priority.Medium;

        var task = new Task(userId, command.Title, category, priority);
        
        if (!string.IsNullOrWhiteSpace(command.Description))
            task.UpdateDescription(command.Description);

        if (command.DueDate.HasValue)
            task.SetDueDate(command.DueDate);

        return Result<Task>.Success(task);
    }

    public async Task<Result> ValidateTaskUpdateAsync(Guid taskId, Guid userId, UpdateTaskCommand command)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return Result.Failure("Task not found");

        if (task.UserId != userId)
            return Result.Failure("Access denied");

        if (task.Status == TaskStatus.Completed && command.Status != TaskStatus.Completed)
        {
            // Allow reopening only if no dependent tasks
            var hasActiveDependents = await _taskRepository.HasActiveDependentTasksAsync(taskId);
            if (hasActiveDependents)
                return Result.Failure("Cannot reopen task with active dependent tasks");
        }

        return Result.Success();
    }

    public async Task<bool> CanDeleteTaskAsync(Guid taskId, Guid userId)
    {
        var task = await _taskRepository.GetByIdWithSubtasksAsync(taskId);
        if (task == null || task.UserId != userId)
            return false;

        // Cannot delete if has incomplete subtasks
        return !task.Subtasks.Any(s => s.Status != TaskStatus.Completed && s.Status != TaskStatus.Cancelled);
    }
}
```

**Deliverables**:
- [ ] Complete Task entity with all business rules
- [ ] Task status management and transitions
- [ ] Task-to-project conversion logic
- [ ] Subtask management functionality
- [ ] Task validation services
- [ ] Unit tests (90%+ coverage)

### Task P3.B.2: Create task data access layer
**Duration**: 3 days | **Priority**: Critical | **Depends on**: P3.B.1

**Task Repository Implementation**:
```csharp
public class TaskRepository : Repository<Task>, ITaskRepository
{
    public async Task<IEnumerable<Task>> GetUserTasksAsync(Guid userId, TaskFilter filter)
    {
        var query = _dbSet
            .Where(t => t.UserId == userId)
            .AsQueryable();

        query = ApplyFilters(query, filter);
        query = ApplyOrdering(query, filter.OrderBy);

        if (filter.Skip.HasValue)
            query = query.Skip(filter.Skip.Value);

        if (filter.Take.HasValue)
            query = query.Take(filter.Take.Value);

        return await query
            .Include(t => t.Project)
            .Include(t => t.Subtasks)
            .Include(t => t.Contacts)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Task?> GetByIdWithDetailsAsync(Guid id, Guid userId)
    {
        return await _dbSet
            .Where(t => t.Id == id && t.UserId == userId)
            .Include(t => t.Project)
            .Include(t => t.ParentTask)
            .Include(t => t.Subtasks)
                .ThenInclude(st => st.Contacts)
            .Include(t => t.Contacts)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetUserTaskCountAsync(Guid userId)
    {
        return await _dbSet.CountAsync(t => t.UserId == userId);
    }

    public async Task<IEnumerable<Task>> GetTasksDueSoonAsync(Guid userId, TimeSpan timespan)
    {
        var cutoffDate = DateTime.UtcNow.Add(timespan);
        return await _dbSet
            .Where(t => t.UserId == userId && 
                       t.Status != TaskStatus.Completed && 
                       t.DueDate.HasValue && 
                       t.DueDate <= cutoffDate)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Task>> GetOverdueTasksAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(t => t.UserId == userId && 
                       t.Status != TaskStatus.Completed && 
                       t.DueDate.HasValue && 
                       t.DueDate < now)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<bool> HasActiveDependentTasksAsync(Guid taskId)
    {
        return await _dbSet.AnyAsync(t => t.ParentTaskId == taskId && 
                                         t.Status != TaskStatus.Completed && 
                                         t.Status != TaskStatus.Cancelled);
    }

    public async Task<IEnumerable<Task>> GetSubtasksAsync(Guid parentTaskId, Guid userId)
    {
        return await _dbSet
            .Where(t => t.ParentTaskId == parentTaskId && t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dictionary<TaskStatus, int>> GetTaskStatusCountsAsync(Guid userId)
    {
        return await _dbSet
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetTaskCategoryCountsAsync(Guid userId)
    {
        return await _dbSet
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Category.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<IEnumerable<Task>> GetRecentTasksAsync(Guid userId, int count = 10)
    {
        return await _dbSet
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(count)
            .Include(t => t.Project)
            .ToListAsync();
    }

    private static IQueryable<Task> ApplyFilters(IQueryable<Task> query, TaskFilter filter)
    {
        if (filter.Categories?.Any() == true)
            query = query.Where(t => filter.Categories.Contains(t.Category.Value));

        if (filter.Priorities?.Any() == true)
            query = query.Where(t => filter.Priorities.Contains(t.Priority.Value));

        if (filter.Statuses?.Any() == true)
            query = query.Where(t => filter.Statuses.Contains(t.Status));

        if (filter.DueDateFrom.HasValue)
            query = query.Where(t => t.DueDate >= filter.DueDateFrom);

        if (filter.DueDateTo.HasValue)
            query = query.Where(t => t.DueDate <= filter.DueDateTo);

        if (filter.ProjectId.HasValue)
            query = query.Where(t => t.ProjectId == filter.ProjectId);

        if (filter.HasDueDate.HasValue)
        {
            query = filter.HasDueDate.Value
                ? query.Where(t => t.DueDate.HasValue)
                : query.Where(t => !t.DueDate.HasValue);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(searchTerm) ||
                                    (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
        }

        return query;
    }

    private static IQueryable<Task> ApplyOrdering(IQueryable<Task> query, TaskOrderBy orderBy)
    {
        return orderBy switch
        {
            TaskOrderBy.Title => query.OrderBy(t => t.Title),
            TaskOrderBy.DueDate => query.OrderBy(t => t.DueDate ?? DateTime.MaxValue),
            TaskOrderBy.Priority => query.OrderByDescending(t => t.Priority.Value),
            TaskOrderBy.Status => query.OrderBy(t => t.Status),
            TaskOrderBy.Created => query.OrderByDescending(t => t.CreatedAt),
            TaskOrderBy.Updated => query.OrderByDescending(t => t.UpdatedAt),
            _ => query.OrderByDescending(t => t.UpdatedAt)
        };
    }
}

public class TaskFilter
{
    public string[]? Categories { get; set; }
    public int[]? Priorities { get; set; }
    public TaskStatus[]? Statuses { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public Guid? ProjectId { get; set; }
    public bool? HasDueDate { get; set; }
    public string? SearchTerm { get; set; }
    public TaskOrderBy OrderBy { get; set; } = TaskOrderBy.Updated;
    public int? Skip { get; set; }
    public int? Take { get; set; }
}

public enum TaskOrderBy
{
    Title,
    DueDate, 
    Priority,
    Status,
    Created,
    Updated
}
```

**Database Optimizations**:
```sql
-- Performance indexes for task queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_user_status 
ON tasks(user_id, status) WHERE status != 'Completed';

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_user_due_date 
ON tasks(user_id, due_date) WHERE due_date IS NOT NULL AND status != 'Completed';

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_user_category 
ON tasks(user_id, category);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_user_priority 
ON tasks(user_id, priority) WHERE status != 'Completed';

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_user_updated 
ON tasks(user_id, updated_at);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_project_id 
ON tasks(project_id) WHERE project_id IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tasks_parent_task 
ON tasks(parent_task_id) WHERE parent_task_id IS NOT NULL;
```

**Deliverables**:
- [ ] Complete task repository with optimized queries
- [ ] Advanced filtering and sorting capabilities
- [ ] Efficient data loading with proper includes
- [ ] Database performance optimization
- [ ] Integration tests for all repository methods
- [ ] Query performance benchmarks

### Task P3.B.3: Implement task category and priority management
**Duration**: 2 days | **Priority**: Medium | **Depends on**: P3.B.2

**Value Objects Implementation**:
```csharp
public class TaskCategory : ValueObject
{
    public static TaskCategory ToDo => new("ToDo");
    public static TaskCategory Idea => new("Idea");
    public static TaskCategory Appointment => new("Appointment");
    public static TaskCategory BillReminder => new("BillReminder");
    public static TaskCategory Project => new("Project");

    public string Value { get; }

    private TaskCategory(string value)
    {
        Value = Guard.Against.NullOrWhiteSpace(value, nameof(value));
    }

    public static TaskCategory FromString(string value)
    {
        return value?.ToLower() switch
        {
            "todo" => ToDo,
            "idea" => Idea,
            "appointment" => Appointment,
            "billreminder" => BillReminder,
            "project" => Project,
            _ => throw new ArgumentException($"Invalid task category: {value}", nameof(value))
        };
    }

    public static IEnumerable<TaskCategory> GetAll()
    {
        return new[] { ToDo, Idea, Appointment, BillReminder, Project };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}

public class Priority : ValueObject
{
    public static Priority VeryLow => new(1, "Very Low");
    public static Priority Low => new(2, "Low");
    public static Priority Medium => new(3, "Medium");
    public static Priority High => new(4, "High");
    public static Priority VeryHigh => new(5, "Very High");

    public int Value { get; }
    public string Name { get; }

    private Priority(int value, string name)
    {
        Value = Guard.Against.OutOfRange(value, nameof(value), 1, 5);
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));
    }

    public static Priority FromValue(int value)
    {
        return value switch
        {
            1 => VeryLow,
            2 => Low,
            3 => Medium,
            4 => High,
            5 => VeryHigh,
            _ => throw new ArgumentException($"Invalid priority value: {value}", nameof(value))
        };
    }

    public static Priority FromName(string name)
    {
        return name?.ToLower() switch
        {
            "very low" => VeryLow,
            "low" => Low,
            "medium" => Medium,
            "high" => High,
            "very high" => VeryHigh,
            _ => throw new ArgumentException($"Invalid priority name: {name}", nameof(name))
        };
    }

    public static IEnumerable<Priority> GetAll()
    {
        return new[] { VeryLow, Low, Medium, High, VeryHigh };
    }

    public bool IsHigherThan(Priority other) => Value > other.Value;
    public bool IsLowerThan(Priority other) => Value < other.Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return Name;
    }
}

public class TaskCategoryService : ITaskCategoryService
{
    public IEnumerable<TaskCategoryDto> GetAllCategories()
    {
        return TaskCategory.GetAll().Select(c => new TaskCategoryDto
        {
            Value = c.Value,
            DisplayName = c.Value.Humanize(),
            Description = GetCategoryDescription(c)
        });
    }

    public IEnumerable<PriorityDto> GetAllPriorities()
    {
        return Priority.GetAll().Select(p => new PriorityDto
        {
            Value = p.Value,
            Name = p.Name,
            DisplayName = p.Name,
            Color = GetPriorityColor(p)
        });
    }

    public async Task<Dictionary<string, int>> GetUserCategoryStatsAsync(Guid userId)
    {
        return await _taskRepository.GetTaskCategoryCountsAsync(userId);
    }

    public async Task<Dictionary<int, int>> GetUserPriorityStatsAsync(Guid userId)
    {
        var tasks = await _taskRepository.GetActiveUserTasksAsync(userId);
        return tasks.GroupBy(t => t.Priority.Value)
                   .ToDictionary(g => g.Key, g => g.Count());
    }

    private static string GetCategoryDescription(TaskCategory category) => category.Value switch
    {
        "ToDo" => "General tasks and to-do items",
        "Idea" => "Ideas and future considerations",
        "Appointment" => "Scheduled appointments and meetings",
        "BillReminder" => "Bill payments and financial reminders",
        "Project" => "Complex projects with multiple tasks",
        _ => string.Empty
    };

    private static string GetPriorityColor(Priority priority) => priority.Value switch
    {
        1 => "#9CA3AF", // Gray
        2 => "#3B82F6", // Blue
        3 => "#10B981", // Green
        4 => "#F59E0B", // Yellow
        5 => "#EF4444", // Red
        _ => "#6B7280"  // Default gray
    };
}
```

**Deliverables**:
- [ ] Task category value object with validation
- [ ] Priority value object with comparison methods
- [ ] Category and priority management services
- [ ] User statistics for categories and priorities
- [ ] Unit tests for all value objects
- [ ] Category migration and transition logic

## Developer C Tasks - Task Management APIs

### Task P3.C.1: Create core task CRUD endpoints
**Duration**: 4 days | **Priority**: Critical | **Depends on**: P3.B.1, P3.B.2

**Task Controller Implementation**:
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    /// <summary>
    /// Get user's tasks with filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskResponseDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<ActionResult<PagedResult<TaskResponseDto>>> GetTasks(
        [FromQuery] GetTasksQueryDto query)
    {
        var userId = User.GetUserId();
        var filter = query.ToTaskFilter();
        
        // Try cache first
        var cachedTasks = await _taskCacheService.GetUserTasksCacheAsync(userId, filter);
        if (cachedTasks != null)
        {
            return Ok(cachedTasks);
        }

        var tasks = await _taskService.GetUserTasksAsync(userId, filter);
        var totalCount = await _taskService.GetUserTaskCountAsync(userId, filter);
        
        var result = new PagedResult<TaskResponseDto>
        {
            Items = tasks.Select(TaskResponseDto.FromEntity).ToList(),
            TotalCount = totalCount,
            Page = query.Page ?? 1,
            PageSize = query.PageSize ?? 20
        };

        // Cache the result
        await _taskCacheService.SetUserTasksCacheAsync(userId, filter, result.Items);

        return Ok(result);
    }

    /// <summary>
    /// Get a specific task by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaskDetailResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<ActionResult<TaskDetailResponseDto>> GetTask(Guid id)
    {
        var userId = User.GetUserId();
        var task = await _taskService.GetTaskByIdAsync(id, userId);
        
        if (task == null)
            return NotFound(new ErrorResponse { Message = "Task not found" });

        return Ok(TaskDetailResponseDto.FromEntity(task));
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDetailResponseDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<ActionResult<TaskDetailResponseDto>> CreateTask([FromBody] CreateTaskRequestDto request)
    {
        var userId = User.GetUserId();
        var command = new CreateTaskCommand
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            DueDate = request.DueDate,
            ProjectId = request.ProjectId
        };

        var result = await _taskService.CreateTaskAsync(command);
        if (!result.IsSuccess)
            return BadRequest(new ErrorResponse { Message = result.Error });

        var response = TaskDetailResponseDto.FromEntity(result.Value);
        
        // Invalidate cache
        await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

        return CreatedAtAction(nameof(GetTask), new { version = "1.0", id = result.Value.Id }, response);
    }

    /// <summary>
    /// Update an existing task
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TaskDetailResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<ActionResult<TaskDetailResponseDto>> UpdateTask(Guid id, [FromBody] UpdateTaskRequestDto request)
    {
        var userId = User.GetUserId();
        var command = new UpdateTaskCommand
        {
            TaskId = id,
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            Status = request.Status,
            DueDate = request.DueDate
        };

        var result = await _taskService.UpdateTaskAsync(command);
        if (!result.IsSuccess)
        {
            if (result.Error.Contains("not found"))
                return NotFound(new ErrorResponse { Message = result.Error });
            return BadRequest(new ErrorResponse { Message = result.Error });
        }

        var response = TaskDetailResponseDto.FromEntity(result.Value);
        
        // Invalidate cache
        await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

        return Ok(response);
    }

    /// <summary>
    /// Delete a task
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _taskService.DeleteTaskAsync(id, userId);
        
        if (!result.IsSuccess)
        {
            if (result.Error.Contains("not found"))
                return NotFound(new ErrorResponse { Message = result.Error });
            return BadRequest(new ErrorResponse { Message = result.Error });
        }

        // Invalidate cache
        await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

        return NoContent();
    }
}
```

**DTOs**:
```csharp
public record CreateTaskRequestDto
{
    [Required]
    [Length(1, 200)]
    public string Title { get; init; } = string.Empty;

    [Length(0, 5000)]
    public string? Description { get; init; }

    [Required]
    public string Category { get; init; } = string.Empty;

    [Range(1, 5)]
    public int? Priority { get; init; }

    public DateTime? DueDate { get; init; }

    public Guid? ProjectId { get; init; }
}

public record UpdateTaskRequestDto
{
    [Length(1, 200)]
    public string? Title { get; init; }

    [Length(0, 5000)]
    public string? Description { get; init; }

    public string? Category { get; init; }

    [Range(1, 5)]
    public int? Priority { get; init; }

    public TaskStatus? Status { get; init; }

    public DateTime? DueDate { get; init; }
}

public record TaskResponseDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public int SubtaskCount { get; init; }
    public int ContactCount { get; init; }

    public static TaskResponseDto FromEntity(Task task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Category = task.Category.Value,
        Priority = task.Priority.Value,
        Status = task.Status.ToString(),
        DueDate = task.DueDate,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        CompletedAt = task.CompletedAt,
        ProjectId = task.ProjectId,
        ProjectName = task.Project?.Name,
        SubtaskCount = task.Subtasks.Count,
        ContactCount = task.Contacts.Count
    };
}

public record TaskDetailResponseDto : TaskResponseDto
{
    public IEnumerable<TaskResponseDto> Subtasks { get; init; } = Array.Empty<TaskResponseDto>();
    public IEnumerable<ContactResponseDto> Contacts { get; init; } = Array.Empty<ContactResponseDto>();
    public TaskResponseDto? ParentTask { get; init; }

    public static new TaskDetailResponseDto FromEntity(Task task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Category = task.Category.Value,
        Priority = task.Priority.Value,
        Status = task.Status.ToString(),
        DueDate = task.DueDate,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        CompletedAt = task.CompletedAt,
        ProjectId = task.ProjectId,
        ProjectName = task.Project?.Name,
        SubtaskCount = task.Subtasks.Count,
        ContactCount = task.Contacts.Count,
        Subtasks = task.Subtasks.Select(TaskResponseDto.FromEntity),
        Contacts = task.Contacts.Select(ContactResponseDto.FromEntity),
        ParentTask = task.ParentTask != null ? TaskResponseDto.FromEntity(task.ParentTask) : null
    };
}
```

**Deliverables**:
- [ ] Complete CRUD endpoints for tasks
- [ ] Comprehensive input validation
- [ ] Proper error handling and status codes
- [ ] Response caching implementation
- [ ] Integration tests for all endpoints
- [ ] Updated Swagger documentation with examples

### Task P3.C.2: Create advanced task management endpoints
**Duration**: 3 days | **Priority**: High | **Depends on**: P3.C.1

**Advanced Task Operations**:
```csharp
/// <summary>
/// Convert task to project
/// </summary>
[HttpPost("{id}/convert-to-project")]
[ProducesResponseType(typeof(ProjectResponseDto), 201)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
public async Task<ActionResult<ProjectResponseDto>> ConvertTaskToProject(Guid id, [FromBody] ConvertToProjectRequestDto request)
{
    var userId = User.GetUserId();
    var result = await _taskService.ConvertTaskToProjectAsync(id, userId, request.ProjectName, request.ProjectDescription);
    
    if (!result.IsSuccess)
    {
        if (result.Error.Contains("not found"))
            return NotFound(new ErrorResponse { Message = result.Error });
        return BadRequest(new ErrorResponse { Message = result.Error });
    }

    var response = ProjectResponseDto.FromEntity(result.Value);
    
    // Invalidate task cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return CreatedAtAction("GetProject", "Projects", new { version = "1.0", id = result.Value.Id }, response);
}

/// <summary>
/// Get available task categories
/// </summary>
[HttpGet("categories")]
[ProducesResponseType(typeof(IEnumerable<TaskCategoryDto>), 200)]
public ActionResult<IEnumerable<TaskCategoryDto>> GetTaskCategories()
{
    var categories = _taskCategoryService.GetAllCategories();
    return Ok(categories);
}

/// <summary>
/// Search tasks with advanced filtering
/// </summary>
[HttpGet("search")]
[ProducesResponseType(typeof(SearchResult<TaskResponseDto>), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
public async Task<ActionResult<SearchResult<TaskResponseDto>>> SearchTasks([FromQuery] TaskSearchQueryDto query)
{
    var userId = User.GetUserId();
    var searchQuery = new TaskSearchQuery
    {
        UserId = userId,
        SearchTerm = query.Q,
        Filters = query.ToTaskFilter(),
        Page = query.Page ?? 1,
        PageSize = Math.Min(query.PageSize ?? 20, 100) // Limit max page size
    };

    var result = await _taskSearchService.SearchTasksAsync(searchQuery);
    return Ok(result);
}

/// <summary>
/// Update task status
/// </summary>
[HttpPatch("{id}/status")]
[ProducesResponseType(typeof(TaskResponseDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
public async Task<ActionResult<TaskResponseDto>> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequestDto request)
{
    var userId = User.GetUserId();
    var result = await _taskService.UpdateTaskStatusAsync(id, userId, request.Status);
    
    if (!result.IsSuccess)
    {
        if (result.Error.Contains("not found"))
            return NotFound(new ErrorResponse { Message = result.Error });
        return BadRequest(new ErrorResponse { Message = result.Error });
    }

    var response = TaskResponseDto.FromEntity(result.Value);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return Ok(response);
}

/// <summary>
/// Get task statistics
/// </summary>
[HttpGet("statistics")]
[ProducesResponseType(typeof(TaskStatisticsDto), 200)]
public async Task<ActionResult<TaskStatisticsDto>> GetTaskStatistics()
{
    var userId = User.GetUserId();
    var stats = await _taskService.GetTaskStatisticsAsync(userId);
    return Ok(stats);
}

/// <summary>
/// Get tasks due soon
/// </summary>
[HttpGet("due-soon")]
[ProducesResponseType(typeof(IEnumerable<TaskResponseDto>), 200)]
public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetTasksDueSoon([FromQuery] int hours = 24)
{
    var userId = User.GetUserId();
    var tasks = await _taskService.GetTasksDueSoonAsync(userId, TimeSpan.FromHours(hours));
    var response = tasks.Select(TaskResponseDto.FromEntity);
    return Ok(response);
}

/// <summary>
/// Get overdue tasks
/// </summary>
[HttpGet("overdue")]
[ProducesResponseType(typeof(IEnumerable<TaskResponseDto>), 200)]
public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetOverdueTasks()
{
    var userId = User.GetUserId();
    var tasks = await _taskService.GetOverdueTasksAsync(userId);
    var response = tasks.Select(TaskResponseDto.FromEntity);
    return Ok(response);
}
```

**Subtask Management Endpoints**:
```csharp
/// <summary>
/// Get task's subtasks
/// </summary>
[HttpGet("{id}/subtasks")]
[ProducesResponseType(typeof(IEnumerable<TaskResponseDto>), 200)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetSubtasks(Guid id)
{
    var userId = User.GetUserId();
    var subtasks = await _taskService.GetSubtasksAsync(id, userId);
    var response = subtasks.Select(TaskResponseDto.FromEntity);
    return Ok(response);
}

/// <summary>
/// Add a subtask
/// </summary>
[HttpPost("{id}/subtasks")]
[ProducesResponseType(typeof(TaskResponseDto), 201)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
public async Task<ActionResult<TaskResponseDto>> AddSubtask(Guid id, [FromBody] CreateTaskRequestDto request)
{
    var userId = User.GetUserId();
    var result = await _taskService.CreateSubtaskAsync(id, userId, request.Title, request.Description);
    
    if (!result.IsSuccess)
    {
        if (result.Error.Contains("not found"))
            return NotFound(new ErrorResponse { Message = result.Error });
        return BadRequest(new ErrorResponse { Message = result.Error });
    }

    var response = TaskResponseDto.FromEntity(result.Value);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return CreatedAtAction(nameof(GetTask), new { version = "1.0", id = result.Value.Id }, response);
}
```

**Deliverables**:
- [ ] Task-to-project conversion endpoint
- [ ] Advanced search and filtering
- [ ] Task status update endpoint
- [ ] Task statistics endpoint
- [ ] Due soon and overdue task endpoints
- [ ] Subtask management endpoints
- [ ] Integration tests and Swagger documentation

### Task P3.C.3: Implement task batch operations
**Duration**: 2 days | **Priority**: Medium | **Depends on**: P3.C.2

**Batch Operations Controller**:
```csharp
/// <summary>
/// Create multiple tasks in batch
/// </summary>
[HttpPost("batch")]
[ProducesResponseType(typeof(BatchTaskResponseDto), 201)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
public async Task<ActionResult<BatchTaskResponseDto>> CreateTasksBatch([FromBody] BatchCreateTasksRequestDto request)
{
    if (request.Tasks.Count > 100)
        return BadRequest(new ErrorResponse { Message = "Maximum 100 tasks allowed per batch operation" });

    var userId = User.GetUserId();
    var result = await _taskService.CreateTasksBatchAsync(userId, request.Tasks);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return CreatedAtAction(nameof(GetTasks), new { version = "1.0" }, result);
}

/// <summary>
/// Update multiple tasks in batch
/// </summary>
[HttpPut("batch")]
[ProducesResponseType(typeof(BatchTaskResponseDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
public async Task<ActionResult<BatchTaskResponseDto>> UpdateTasksBatch([FromBody] BatchUpdateTasksRequestDto request)
{
    if (request.Tasks.Count > 50)
        return BadRequest(new ErrorResponse { Message = "Maximum 50 tasks allowed per batch update" });

    var userId = User.GetUserId();
    var result = await _taskService.UpdateTasksBatchAsync(userId, request.Tasks);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return Ok(result);
}

/// <summary>
/// Delete multiple tasks in batch
/// </summary>
[HttpDelete("batch")]
[ProducesResponseType(typeof(BatchDeleteResponseDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
public async Task<ActionResult<BatchDeleteResponseDto>> DeleteTasksBatch([FromBody] BatchDeleteTasksRequestDto request)
{
    if (request.TaskIds.Count > 50)
        return BadRequest(new ErrorResponse { Message = "Maximum 50 tasks allowed per batch delete" });

    var userId = User.GetUserId();
    var result = await _taskService.DeleteTasksBatchAsync(userId, request.TaskIds);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return Ok(result);
}

/// <summary>
/// Bulk update task status
/// </summary>
[HttpPatch("batch/status")]
[ProducesResponseType(typeof(BatchUpdateResponseDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
public async Task<ActionResult<BatchUpdateResponseDto>> UpdateTasksStatusBatch([FromBody] BatchUpdateStatusRequestDto request)
{
    if (request.TaskIds.Count > 100)
        return BadRequest(new ErrorResponse { Message = "Maximum 100 tasks allowed per batch status update" });

    var userId = User.GetUserId();
    var result = await _taskService.UpdateTasksStatusBatchAsync(userId, request.TaskIds, request.Status);
    
    // Invalidate cache
    await _taskCacheService.InvalidateUserTasksCacheAsync(userId);

    return Ok(result);
}
```

**Batch Operation DTOs**:
```csharp
public record BatchCreateTasksRequestDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreateTaskRequestDto> Tasks { get; init; } = new();
}

public record BatchUpdateTasksRequestDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<BatchUpdateTaskItemDto> Tasks { get; init; } = new();
}

public record BatchUpdateTaskItemDto
{
    [Required]
    public Guid Id { get; init; }
    
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int? Priority { get; init; }
    public TaskStatus? Status { get; init; }
    public DateTime? DueDate { get; init; }
}

public record BatchTaskResponseDto
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public List<TaskResponseDto> SuccessfulTasks { get; init; } = new();
    public List<BatchErrorDto> Errors { get; init; } = new();
}

public record BatchErrorDto
{
    public int Index { get; init; }
    public Guid? TaskId { get; init; }
    public string Error { get; init; } = string.Empty;
}
```

**Deliverables**:
- [ ] Batch task creation endpoint
- [ ] Batch task update endpoint
- [ ] Batch task deletion endpoint
- [ ] Bulk status update endpoint
- [ ] Comprehensive error handling for batch operations
- [ ] Integration tests and Swagger documentation

## Phase Completion Criteria

### Functionality Requirements
- [ ] All CRUD operations working correctly
- [ ] Task categorization and priority management functional
- [ ] Task-to-project conversion working
- [ ] Search and filtering performant (< 200ms for 10k tasks)
- [ ] Batch operations handling up to 100 tasks efficiently
- [ ] Subtask management complete

### Performance Requirements
- [ ] Task list loading < 200ms for 1000 tasks
- [ ] Search results < 500ms with full-text search
- [ ] Cache hit ratio > 80% for frequently accessed data
- [ ] Database queries optimized with proper indexing
- [ ] Memory usage optimized for large task lists

### Testing Requirements
- [ ] Unit test coverage ≥ 85% for domain logic
- [ ] Integration tests for all API endpoints
- [ ] Performance tests for large data sets
- [ ] Cache invalidation tests
- [ ] Concurrent operation tests

### Client Integration
- [ ] Swagger documentation complete with examples
- [ ] All endpoints properly versioned
- [ ] Response DTOs optimized for client consumption
- [ ] Error responses consistent across all endpoints
- [ ] WebSocket events for real-time updates (Phase 7 prep)

---

*Last Updated: September 3, 2025*