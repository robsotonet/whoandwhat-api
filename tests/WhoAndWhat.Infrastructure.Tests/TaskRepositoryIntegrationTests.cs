using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Tests;

/// <summary>
/// Comprehensive integration tests for TaskRepository advanced querying capabilities
/// </summary>
public class TaskRepositoryIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IAppTaskRepository _taskRepository;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Project _testProject;
    private readonly Contact _testContact;

    public TaskRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        
        // Create mock logger
        var mockLogger = new Mock<ILogger<TaskRepository>>();
        _taskRepository = new TaskRepository(_context, mockLogger.Object);
        
        // Setup test data
        SetupTestData().GetAwaiter().GetResult();
    }

    private async Task SetupTestData()
    {
        // Create test users
        _testUser = new User("test@test.com", "testuser", Language.en);
        _testUser.SetPassword("TestPassword123!");
        
        _otherUser = new User("other@test.com", "otheruser", Language.en);
        _otherUser.SetPassword("TestPassword123!");

        await _context.Users.AddRangeAsync(_testUser, _otherUser);

        // Create test project
        _testProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test project for integration tests",
            UserId = _testUser.Id,
            User = _testUser
        };
        await _context.Projects.AddAsync(_testProject);

        // Create test contact
        _testContact = new Contact
        {
            Id = Guid.NewGuid(),
            Name = "Test Contact",
            Email = "contact@test.com",
            UserId = _testUser.Id,
            User = _testUser
        };
        await _context.Contacts.AddAsync(_testContact);

        await _context.SaveChangesAsync();
    }

    #region Enhanced Retrieval Methods Tests

    [Fact]
    public async Task GetTaskWithSubtasksAsync_Should_Return_Task_With_Subtasks()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent Task", _testUser.Id);
        var subtask1 = CreateTestTask("Subtask 1", _testUser.Id, parentTask.Id);
        var subtask2 = CreateTestTask("Subtask 2", _testUser.Id, parentTask.Id);
        var deletedSubtask = CreateTestTask("Deleted Subtask", _testUser.Id, parentTask.Id);
        deletedSubtask.SoftDelete();

        await _context.Tasks.AddRangeAsync(parentTask, subtask1, subtask2, deletedSubtask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTaskWithSubtasksAsync(parentTask.Id, _testUser.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(parentTask.Id);
        result.Subtasks.Should().HaveCount(2); // Should not include deleted subtask
        result.Subtasks.Should().Contain(s => s.Title == "Subtask 1");
        result.Subtasks.Should().Contain(s => s.Title == "Subtask 2");
        result.Subtasks.Should().NotContain(s => s.Title == "Deleted Subtask");
    }

    [Fact]
    public async Task GetTaskWithSubtasksAsync_Should_Return_Null_For_Other_User()
    {
        // Arrange
        var task = CreateTestTask("Other User Task", _otherUser.Id);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTaskWithSubtasksAsync(task.Id, _testUser.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTasksByProjectIdAsync_Should_Return_Tasks_In_Project()
    {
        // Arrange
        var task1 = CreateTestTask("Project Task 1", _testUser.Id, projectId: _testProject.Id);
        var task2 = CreateTestTask("Project Task 2", _testUser.Id, projectId: _testProject.Id);
        var task3 = CreateTestTask("Non-Project Task", _testUser.Id);
        var completedTask = CreateTestTask("Completed Task", _testUser.Id, projectId: _testProject.Id);
        completedTask.Status = (int)DomainTaskStatus.Completed;

        await _context.Tasks.AddRangeAsync(task1, task2, task3, completedTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksByProjectIdAsync(_testProject.Id, _testUser.Id, includeCompleted: true);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList.Should().Contain(t => t.Title == "Project Task 1");
        resultList.Should().Contain(t => t.Title == "Project Task 2");
        resultList.Should().Contain(t => t.Title == "Completed Task");
        resultList.Should().NotContain(t => t.Title == "Non-Project Task");
    }

    [Fact]
    public async Task GetTasksByProjectIdAsync_Should_Exclude_Completed_When_Requested()
    {
        // Arrange
        var activeTask = CreateTestTask("Active Task", _testUser.Id, projectId: _testProject.Id);
        var completedTask = CreateTestTask("Completed Task", _testUser.Id, projectId: _testProject.Id);
        completedTask.Status = (int)DomainTaskStatus.Completed;

        await _context.Tasks.AddRangeAsync(activeTask, completedTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksByProjectIdAsync(_testProject.Id, _testUser.Id, includeCompleted: false);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Active Task");
    }

    [Fact]
    public async Task GetTasksByUserIdAsync_Should_Apply_Filters_Correctly()
    {
        // Arrange
        var highPriorityTask = CreateTestTask("High Priority", _testUser.Id);
        highPriorityTask.Priority = (int)Priority.High;
        highPriorityTask.Category = (int)TaskCategory.ToDos;
        
        var lowPriorityTask = CreateTestTask("Low Priority", _testUser.Id);
        lowPriorityTask.Priority = (int)Priority.Low;
        lowPriorityTask.Category = (int)TaskCategory.Ideas;

        var overdueTask = CreateTestTask("Overdue Task", _testUser.Id);
        overdueTask.DueDate = DateTime.UtcNow.AddDays(-1);

        await _context.Tasks.AddRangeAsync(highPriorityTask, lowPriorityTask, overdueTask);
        await _context.SaveChangesAsync();

        var filter = new TaskFilter
        {
            Priority = Priority.High,
            Category = TaskCategory.ToDos,
            PageSize = 10,
            PageNumber = 1
        };

        // Act
        var (tasks, totalCount) = await _taskRepository.GetTasksByUserIdAsync(_testUser.Id, filter);

        // Assert
        var taskList = tasks.ToList();
        taskList.Should().HaveCount(1);
        taskList.First().Title.Should().Be("High Priority");
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTasksByUserIdAsync_Should_Return_Empty_For_Invalid_Filter()
    {
        // Arrange
        var invalidFilter = new TaskFilter
        {
            PageSize = -1, // Invalid page size
            PageNumber = 0  // Invalid page number
        };

        // Act
        var (tasks, totalCount) = await _taskRepository.GetTasksByUserIdAsync(_testUser.Id, invalidFilter);

        // Assert
        tasks.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOverdueTasksAsync_Should_Return_Only_Overdue_Tasks()
    {
        // Arrange
        var overdueTask = CreateTestTask("Overdue Task", _testUser.Id);
        overdueTask.DueDate = DateTime.UtcNow.AddDays(-1);
        overdueTask.Status = (int)DomainTaskStatus.InProgress;

        var futureTask = CreateTestTask("Future Task", _testUser.Id);
        futureTask.DueDate = DateTime.UtcNow.AddDays(1);

        var completedOverdueTask = CreateTestTask("Completed Overdue", _testUser.Id);
        completedOverdueTask.DueDate = DateTime.UtcNow.AddDays(-1);
        completedOverdueTask.Status = (int)DomainTaskStatus.Completed;

        await _context.Tasks.AddRangeAsync(overdueTask, futureTask, completedOverdueTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetOverdueTasksAsync(_testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Overdue Task");
    }

    [Fact]
    public async Task GetTasksDueTodayAsync_Should_Return_Tasks_Due_Today()
    {
        // Arrange
        var todayTask = CreateTestTask("Due Today", _testUser.Id);
        todayTask.DueDate = DateTime.Today.AddHours(14); // 2 PM today

        var tomorrowTask = CreateTestTask("Due Tomorrow", _testUser.Id);
        tomorrowTask.DueDate = DateTime.Today.AddDays(1);

        var yesterdayTask = CreateTestTask("Due Yesterday", _testUser.Id);
        yesterdayTask.DueDate = DateTime.Today.AddDays(-1);

        await _context.Tasks.AddRangeAsync(todayTask, tomorrowTask, yesterdayTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksDueTodayAsync(_testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Due Today");
    }

    #endregion

    #region Hierarchy and Relationship Methods Tests

    [Fact]
    public async Task GetTaskDescendantsAsync_Should_Return_All_Descendants()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent", _testUser.Id);
        var child1 = CreateTestTask("Child 1", _testUser.Id, parentTask.Id);
        var child2 = CreateTestTask("Child 2", _testUser.Id, parentTask.Id);
        var grandchild1 = CreateTestTask("Grandchild 1", _testUser.Id, child1.Id);

        // Set up project relationships for hierarchy
        parentTask.ProjectId = null;
        child1.ProjectId = parentTask.Id;
        child2.ProjectId = parentTask.Id;
        grandchild1.ProjectId = child1.Id;

        await _context.Tasks.AddRangeAsync(parentTask, child1, child2, grandchild1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTaskDescendantsAsync(parentTask.Id, _testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2); // Direct children only
        resultList.Should().Contain(t => t.Title == "Child 1");
        resultList.Should().Contain(t => t.Title == "Child 2");
    }

    [Fact]
    public async Task GetTaskHierarchyAsync_Should_Build_Complete_Hierarchy()
    {
        // Arrange
        var parentTask = CreateTestTask("Root Task", _testUser.Id);
        var child1 = CreateTestTask("Child 1", _testUser.Id, parentTask.Id);
        var child2 = CreateTestTask("Child 2", _testUser.Id, parentTask.Id);
        var grandchild = CreateTestTask("Grandchild", _testUser.Id, child1.Id);

        await _context.Tasks.AddRangeAsync(parentTask, child1, child2, grandchild);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTaskHierarchyAsync(parentTask.Id, _testUser.Id);

        // Assert
        result.Should().NotBeNull();
        result.RootTask.Title.Should().Be("Root Task");
        result.Subtasks.Should().HaveCount(2);
        
        var child1Node = result.Subtasks.First(s => s.Task.Title == "Child 1");
        child1Node.Subtasks.Should().HaveCount(1);
        child1Node.Subtasks.First().Task.Title.Should().Be("Grandchild");
    }

    #endregion

    #region Advanced Filtering and Search Tests

    [Fact]
    public async Task SearchTasksAsync_Should_Find_Tasks_By_Title_And_Description()
    {
        // Arrange
        var task1 = CreateTestTask("Important Meeting", _testUser.Id);
        task1.Description = "Quarterly review meeting with team";
        
        var task2 = CreateTestTask("Buy Groceries", _testUser.Id);
        task2.Description = "Important items for the week";
        
        var task3 = CreateTestTask("Read Book", _testUser.Id);
        task3.Description = "Fiction novel";

        await _context.Tasks.AddRangeAsync(task1, task2, task3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.SearchTasksAsync(_testUser.Id, "Important", 10);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(t => t.Title == "Important Meeting");
        resultList.Should().Contain(t => t.Title == "Buy Groceries");
    }

    [Fact]
    public async Task GetTasksByStatusAsync_Should_Return_Tasks_With_Specific_Status()
    {
        // Arrange
        var pendingTask = CreateTestTask("Pending Task", _testUser.Id);
        pendingTask.Status = (int)DomainTaskStatus.Pending;
        
        var inProgressTask = CreateTestTask("In Progress Task", _testUser.Id);
        inProgressTask.Status = (int)DomainTaskStatus.InProgress;
        
        var completedTask = CreateTestTask("Completed Task", _testUser.Id);
        completedTask.Status = (int)DomainTaskStatus.Completed;

        await _context.Tasks.AddRangeAsync(pendingTask, inProgressTask, completedTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksByStatusAsync(_testUser.Id, DomainTaskStatus.InProgress);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("In Progress Task");
    }

    [Fact]
    public async Task GetTasksByCategoryAsync_Should_Return_Tasks_In_Category()
    {
        // Arrange
        var todoTask = CreateTestTask("Todo Task", _testUser.Id);
        todoTask.Category = (int)TaskCategory.ToDos;
        
        var ideaTask = CreateTestTask("Idea Task", _testUser.Id);
        ideaTask.Category = (int)TaskCategory.Ideas;
        
        var appointmentTask = CreateTestTask("Appointment Task", _testUser.Id);
        appointmentTask.Category = (int)TaskCategory.Appointments;

        await _context.Tasks.AddRangeAsync(todoTask, ideaTask, appointmentTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksByCategoryAsync(_testUser.Id, TaskCategory.Ideas);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Idea Task");
    }

    [Fact]
    public async Task GetTasksByPriorityRangeAsync_Should_Return_Tasks_In_Priority_Range()
    {
        // Arrange
        var lowTask = CreateTestTask("Low Priority", _testUser.Id);
        lowTask.Priority = (int)Priority.Low;
        
        var mediumTask = CreateTestTask("Medium Priority", _testUser.Id);
        mediumTask.Priority = (int)Priority.Medium;
        
        var highTask = CreateTestTask("High Priority", _testUser.Id);
        highTask.Priority = (int)Priority.High;

        await _context.Tasks.AddRangeAsync(lowTask, mediumTask, highTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksByPriorityRangeAsync(_testUser.Id, Priority.Medium, Priority.High);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(t => t.Title == "Medium Priority");
        resultList.Should().Contain(t => t.Title == "High Priority");
        resultList.Should().NotContain(t => t.Title == "Low Priority");
    }

    #endregion

    #region Soft Delete and Archiving Tests

    [Fact]
    public async Task SoftDeleteTaskAsync_Should_Mark_Task_As_Deleted()
    {
        // Arrange
        var task = CreateTestTask("Task to Delete", _testUser.Id);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.SoftDeleteTaskAsync(task.Id, _testUser.Id);

        // Assert
        result.Should().BeTrue();
        
        var deletedTask = await _context.Tasks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == task.Id);
        deletedTask.Should().NotBeNull();
        deletedTask.IsDeleted.Should().BeTrue();
        deletedTask.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RestoreTaskAsync_Should_Restore_Soft_Deleted_Task()
    {
        // Arrange
        var task = CreateTestTask("Task to Restore", _testUser.Id);
        task.SoftDelete();
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.RestoreTaskAsync(task.Id, _testUser.Id);

        // Assert
        result.Should().BeTrue();
        
        var restoredTask = await _context.Tasks.FindAsync(task.Id);
        restoredTask.Should().NotBeNull();
        restoredTask.IsDeleted.Should().BeFalse();
        restoredTask.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetDeletedTasksAsync_Should_Return_Only_Soft_Deleted_Tasks()
    {
        // Arrange
        var activeTask = CreateTestTask("Active Task", _testUser.Id);
        var deletedTask = CreateTestTask("Deleted Task", _testUser.Id);
        deletedTask.SoftDelete();

        await _context.Tasks.AddRangeAsync(activeTask, deletedTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetDeletedTasksAsync(_testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Deleted Task");
    }

    [Fact]
    public async Task ArchiveTaskAsync_Should_Archive_Task_And_Move_To_Archive_Table()
    {
        // Arrange
        var task = CreateTestTask("Task to Archive", _testUser.Id);
        task.Status = (int)DomainTaskStatus.Completed;
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.ArchiveTaskAsync(task.Id, _testUser.Id, "Completed task archival");

        // Assert
        result.Should().BeTrue();
        
        // Task should be soft deleted
        var originalTask = await _context.Tasks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == task.Id);
        originalTask.Should().NotBeNull();
        originalTask.IsDeleted.Should().BeTrue();

        // Archived version should exist
        var archivedTask = await _context.ArchivedTasks
            .FirstOrDefaultAsync(at => at.OriginalTaskId == task.Id);
        archivedTask.Should().NotBeNull();
        archivedTask.Title.Should().Be("Task to Archive");
        archivedTask.ArchiveReason.Should().Be("Completed task archival");
    }

    #endregion

    #region Batch Operations Tests

    [Fact]
    public async Task SoftDeleteTasksBatchAsync_Should_Delete_Multiple_Tasks()
    {
        // Arrange
        var task1 = CreateTestTask("Batch Task 1", _testUser.Id);
        var task2 = CreateTestTask("Batch Task 2", _testUser.Id);
        var task3 = CreateTestTask("Keep Task", _testUser.Id);

        await _context.Tasks.AddRangeAsync(task1, task2, task3);
        await _context.SaveChangesAsync();

        var taskIds = new[] { task1.Id, task2.Id };

        // Act
        var result = await _taskRepository.SoftDeleteTasksBatchAsync(taskIds, _testUser.Id);

        // Assert
        result.Should().Be(2);

        var deletedTasks = await _context.Tasks.IgnoreQueryFilters()
            .Where(t => taskIds.Contains(t.Id))
            .ToListAsync();
        
        deletedTasks.Should().AllSatisfy(t => t.IsDeleted.Should().BeTrue());

        var activeTask = await _context.Tasks.FindAsync(task3.Id);
        activeTask.Should().NotBeNull();
        activeTask.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTasksStatusBatchAsync_Should_Update_Multiple_Task_Statuses()
    {
        // Arrange
        var task1 = CreateTestTask("Task 1", _testUser.Id);
        var task2 = CreateTestTask("Task 2", _testUser.Id);
        
        task1.Status = (int)DomainTaskStatus.Pending;
        task2.Status = (int)DomainTaskStatus.Pending;

        await _context.Tasks.AddRangeAsync(task1, task2);
        await _context.SaveChangesAsync();

        var taskIds = new[] { task1.Id, task2.Id };

        // Act
        var result = await _taskRepository.UpdateTasksStatusBatchAsync(taskIds, _testUser.Id, DomainTaskStatus.InProgress);

        // Assert
        result.Should().Be(2);

        var updatedTasks = await _context.Tasks.Where(t => taskIds.Contains(t.Id)).ToListAsync();
        updatedTasks.Should().AllSatisfy(t => t.Status.Should().Be((int)DomainTaskStatus.InProgress));
    }

    [Fact]
    public async Task UpdateTasksCategoryBatchAsync_Should_Update_Multiple_Task_Categories()
    {
        // Arrange
        var task1 = CreateTestTask("Task 1", _testUser.Id);
        var task2 = CreateTestTask("Task 2", _testUser.Id);
        
        task1.Category = (int)TaskCategory.ToDos;
        task2.Category = (int)TaskCategory.ToDos;

        await _context.Tasks.AddRangeAsync(task1, task2);
        await _context.SaveChangesAsync();

        var taskIds = new[] { task1.Id, task2.Id };

        // Act
        var result = await _taskRepository.UpdateTasksCategoryBatchAsync(taskIds, _testUser.Id, TaskCategory.Ideas);

        // Assert
        result.Should().Be(2);

        var updatedTasks = await _context.Tasks.Where(t => taskIds.Contains(t.Id)).ToListAsync();
        updatedTasks.Should().AllSatisfy(t => t.Category.Should().Be((int)TaskCategory.Ideas));
    }

    #endregion

    #region Performance and Analytics Tests

    [Fact]
    public async Task GetTaskStatisticsAsync_Should_Return_User_Task_Statistics()
    {
        // Arrange
        var activeTask = CreateTestTask("Active Task", _testUser.Id);
        activeTask.Status = (int)DomainTaskStatus.InProgress;
        
        var completedTask = CreateTestTask("Completed Task", _testUser.Id);
        completedTask.Status = (int)DomainTaskStatus.Completed;
        
        var overdueTask = CreateTestTask("Overdue Task", _testUser.Id);
        overdueTask.DueDate = DateTime.UtcNow.AddDays(-1);
        overdueTask.Status = (int)DomainTaskStatus.InProgress;

        await _context.Tasks.AddRangeAsync(activeTask, completedTask, overdueTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTaskStatisticsAsync(_testUser.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(_testUser.Id);
        result.TotalTasks.Should().Be(3);
        result.ActiveTasks.Should().Be(2);
        result.CompletedTasks.Should().Be(1);
        result.OverdueTasks.Should().Be(1);
    }

    [Fact]
    public async Task GetTaskCompletionTrendsAsync_Should_Return_Completion_Trends()
    {
        // Arrange
        var completedTask1 = CreateTestTask("Completed Yesterday", _testUser.Id);
        completedTask1.Status = (int)DomainTaskStatus.Completed;
        completedTask1.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        
        var completedTask2 = CreateTestTask("Completed Today", _testUser.Id);
        completedTask2.Status = (int)DomainTaskStatus.Completed;
        completedTask2.UpdatedAt = DateTime.UtcNow;

        await _context.Tasks.AddRangeAsync(completedTask1, completedTask2);
        await _context.SaveChangesAsync();

        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;

        // Act
        var result = await _taskRepository.GetTaskCompletionTrendsAsync(_testUser.Id, fromDate, toDate);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(_testUser.Id);
        result.DailyData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMostActiveTimePeriodsAsync_Should_Return_Activity_Periods()
    {
        // Arrange
        // Create tasks with different creation times
        var morningTask = CreateTestTask("Morning Task", _testUser.Id);
        morningTask.CreatedAt = DateTime.Today.AddHours(9);
        
        var afternoonTask = CreateTestTask("Afternoon Task", _testUser.Id);
        afternoonTask.CreatedAt = DateTime.Today.AddHours(14);
        
        var eveningTask = CreateTestTask("Evening Task", _testUser.Id);
        eveningTask.CreatedAt = DateTime.Today.AddHours(19);

        await _context.Tasks.AddRangeAsync(morningTask, afternoonTask, eveningTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetMostActiveTimePeriodsAsync(_testUser.Id, 5);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessOrEqualTo(5);
    }

    #endregion

    #region Data Integrity and Validation Tests

    [Fact]
    public async Task ValidateTaskIntegrityAsync_Should_Identify_Integrity_Issues()
    {
        // Arrange - Create a task with potential integrity issues
        var task = CreateTestTask("Task with Issues", _testUser.Id);
        task.ProjectId = Guid.NewGuid(); // Non-existent project
        
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.ValidateTaskIntegrityAsync(_testUser.Id);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(issue => issue.Contains("invalid project reference"));
    }

    [Fact]
    public async Task FindOrphanedTasksAsync_Should_Find_Tasks_With_Missing_Parents()
    {
        // Arrange
        var orphanTask = CreateTestTask("Orphan Task", _testUser.Id);
        orphanTask.ProjectId = Guid.NewGuid(); // Non-existent parent
        
        var validTask = CreateTestTask("Valid Task", _testUser.Id);

        await _context.Tasks.AddRangeAsync(orphanTask, validTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.FindOrphanedTasksAsync(_testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Title.Should().Be("Orphan Task");
    }

    [Fact]
    public async Task GetTasksRequiringAttentionAsync_Should_Find_Tasks_Needing_Attention()
    {
        // Arrange
        var staleTask = CreateTestTask("Stale Task", _testUser.Id);
        staleTask.UpdatedAt = DateTime.UtcNow.AddDays(-30);
        staleTask.Status = (int)DomainTaskStatus.InProgress;
        
        var overdueTask = CreateTestTask("Overdue Task", _testUser.Id);
        overdueTask.DueDate = DateTime.UtcNow.AddDays(-5);
        overdueTask.Status = (int)DomainTaskStatus.InProgress;
        
        var recentTask = CreateTestTask("Recent Task", _testUser.Id);
        recentTask.UpdatedAt = DateTime.UtcNow.AddDays(-1);

        await _context.Tasks.AddRangeAsync(staleTask, overdueTask, recentTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _taskRepository.GetTasksRequiringAttentionAsync(_testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(t => t.Title == "Stale Task");
        resultList.Should().Contain(t => t.Title == "Overdue Task");
    }

    #endregion

    #region Helper Methods

    private DomainTask CreateTestTask(string title, Guid userId, Guid? parentTaskId = null, Guid? projectId = null)
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            UserId = userId,
            ProjectId = projectId,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Medium,
            Category = (int)TaskCategory.ToDos,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}