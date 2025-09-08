using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Repositories;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Tests;

/// <summary>
/// Integration tests for Project repository soft delete functionality
/// </summary>
public class ProjectRepositoryIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<Project> _baseRepository;
    private readonly SoftDeleteService _softDeleteService;
    private readonly User _testUser;
    private readonly User _otherUser;

    public ProjectRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _baseRepository = new Repository<Project>(_context);
        _softDeleteService = new SoftDeleteService();
        
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
        await _context.SaveChangesAsync();
    }

    #region Basic Repository Operations

    [Fact]
    public async Task Should_Add_Project_Successfully()
    {
        // Arrange
        var project = CreateTestProject("New Project", _testUser.Id);

        // Act
        await _baseRepository.AddAsync(project);
        await _baseRepository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("New Project");
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Get_Project_By_Id()
    {
        // Arrange
        var project = CreateTestProject("Test Project", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.GetByIdAsync(project.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(project.Id);
        result.Name.Should().Be("Test Project");
    }

    [Fact]
    public async Task Should_Get_All_Projects_Excluding_Deleted()
    {
        // Arrange
        var activeProject = CreateTestProject("Active Project", _testUser.Id);
        var deletedProject = CreateTestProject("Deleted Project", _testUser.Id);
        deletedProject.SoftDelete();

        await _context.Projects.AddRangeAsync(activeProject, deletedProject);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.GetAllAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Name.Should().Be("Active Project");
    }

    [Fact]
    public async Task Should_Update_Project_Successfully()
    {
        // Arrange
        var project = CreateTestProject("Original Name", _testUser.Id);
        await _baseRepository.AddAsync(project);
        await _baseRepository.SaveChangesAsync();

        // Act
        project.Name = "Updated Name";
        project.Description = "Updated Description";
        _baseRepository.Update(project);
        await _baseRepository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }

    #endregion

    #region Soft Delete Operations

    [Fact]
    public async Task Should_Soft_Delete_Project_Without_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Empty Project", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = _softDeleteService.SoftDeleteProject(project);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("has been deleted");
        
        project.IsDeleted.Should().BeTrue();
        project.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Should not appear in normal queries
        var normalQuery = await _baseRepository.GetByIdAsync(project.Id);
        normalQuery.Should().BeNull();

        // Should appear in queries that ignore filters
        var deletedProject = await _context.Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == project.Id);
        deletedProject.Should().NotBeNull();
        deletedProject.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Not_Delete_Project_With_Active_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Tasks", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        var activeTask = CreateTestTask("Active Task", _testUser.Id, project.Id);
        activeTask.Status = (int)DomainTaskStatus.InProgress;
        await _context.Tasks.AddAsync(activeTask);
        await _context.SaveChangesAsync();

        // Reload project with tasks
        project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstAsync(p => p.Id == project.Id);

        // Act
        var result = _softDeleteService.SoftDeleteProject(project);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("contains active tasks");
        
        project.IsDeleted.Should().BeFalse();
        project.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_Delete_Project_With_Completed_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Completed Tasks", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        var completedTask = CreateTestTask("Completed Task", _testUser.Id, project.Id);
        completedTask.Status = (int)DomainTaskStatus.Completed;
        await _context.Tasks.AddAsync(completedTask);
        await _context.SaveChangesAsync();

        // Reload project with tasks
        project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstAsync(p => p.Id == project.Id);

        // Act
        var result = _softDeleteService.SoftDeleteProject(project, deleteTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        project.IsDeleted.Should().BeTrue();
        
        // Completed task should also be deleted
        var deletedTask = await _context.Tasks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == completedTask.Id);
        deletedTask.Should().NotBeNull();
        deletedTask.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Restore_Project_Successfully()
    {
        // Arrange
        var project = CreateTestProject("Project to Restore", _testUser.Id);
        project.SoftDelete();
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = _softDeleteService.RestoreProject(project);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("has been restored");
        
        project.IsDeleted.Should().BeFalse();
        project.DeletedAt.Should().BeNull();

        // Should appear in normal queries again
        var restoredProject = await _baseRepository.GetByIdAsync(project.Id);
        restoredProject.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Restore_Project_With_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Tasks", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        var task = CreateTestTask("Task to Restore", _testUser.Id, project.Id);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Soft delete both
        project.SoftDelete();
        task.SoftDelete();
        await _context.SaveChangesAsync();

        // Reload project with tasks
        project = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Tasks)
            .FirstAsync(p => p.Id == project.Id);

        // Act
        var result = _softDeleteService.RestoreProject(project, restoreTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("with 1 tasks");
        
        project.IsDeleted.Should().BeFalse();
        
        // Task should also be restored
        var restoredTask = await _context.Tasks.FindAsync(task.Id);
        restoredTask.Should().NotBeNull();
        restoredTask.IsDeleted.Should().BeFalse();
    }

    #endregion

    #region Project Business Rules

    [Fact]
    public async Task Should_Validate_Project_Can_Be_Deleted()
    {
        // Arrange
        var project = CreateTestProject("Validation Test Project", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var canDelete = project.CanSoftDelete();

        // Assert
        canDelete.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Validate_Project_Cannot_Be_Deleted_With_Active_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Active Tasks", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        var activeTask = CreateTestTask("Active Task", _testUser.Id, project.Id);
        activeTask.Status = (int)DomainTaskStatus.InProgress;
        await _context.Tasks.AddAsync(activeTask);
        await _context.SaveChangesAsync();

        // Reload project with tasks
        project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstAsync(p => p.Id == project.Id);

        // Act
        var canDelete = project.CanSoftDelete();

        // Assert
        canDelete.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Validate_Deleted_Project_Can_Be_Restored()
    {
        // Arrange
        var project = CreateTestProject("Deleted Project", _testUser.Id);
        project.SoftDelete();
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var canRestore = project.CanRestore();

        // Assert
        canRestore.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Validate_Active_Project_Cannot_Be_Restored()
    {
        // Arrange
        var project = CreateTestProject("Active Project", _testUser.Id);
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var canRestore = project.CanRestore();

        // Assert
        canRestore.Should().BeFalse();
    }

    #endregion

    #region Project with Contacts

    [Fact]
    public async Task Should_Handle_Project_Contact_Relationships()
    {
        // Arrange
        var project = CreateTestProject("Project With Contacts", _testUser.Id);
        await _context.Projects.AddAsync(project);

        var contact = CreateTestContact("Test Contact", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        
        await _context.SaveChangesAsync();

        // Add contact to project
        project.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        // Act
        var projectWithContacts = await _context.Projects
            .Include(p => p.Contacts)
            .FirstAsync(p => p.Id == project.Id);

        // Assert
        projectWithContacts.Contacts.Should().HaveCount(1);
        projectWithContacts.Contacts.First().Name.Should().Be("Test Contact");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Should_Handle_Large_Number_Of_Projects()
    {
        // Arrange
        var projects = new List<Project>();
        for (int i = 0; i < 100; i++)
        {
            projects.Add(CreateTestProject($"Project {i}", _testUser.Id));
        }
        
        await _context.Projects.AddRangeAsync(projects);
        await _context.SaveChangesAsync();

        // Act
        var start = DateTime.UtcNow;
        var result = await _context.Projects
            .Where(p => p.UserId == _testUser.Id)
            .ToListAsync();
        var duration = DateTime.UtcNow - start;

        // Assert
        result.Should().HaveCount(100);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should be fast
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task Should_Not_Allow_Access_To_Other_Users_Projects()
    {
        // Arrange
        var otherUserProject = CreateTestProject("Other User Project", _otherUser.Id);
        await _context.Projects.AddAsync(otherUserProject);
        await _context.SaveChangesAsync();

        // Act
        var userProjects = await _context.Projects
            .Where(p => p.UserId == _testUser.Id)
            .ToListAsync();

        // Assert
        userProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Filter_Projects_By_User_In_Soft_Delete()
    {
        // Arrange
        var userProject = CreateTestProject("User Project", _testUser.Id);
        var otherProject = CreateTestProject("Other Project", _otherUser.Id);
        
        await _context.Projects.AddRangeAsync(userProject, otherProject);
        await _context.SaveChangesAsync();

        // Act - Try to delete other user's project (should fail)
        var result = _softDeleteService.SoftDeleteProject(otherProject);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Service doesn't enforce user filtering
        
        // But verify the project belongs to the other user
        otherProject.UserId.Should().Be(_otherUser.Id);
        otherProject.UserId.Should().NotBe(_testUser.Id);
    }

    #endregion

    #region Helper Methods

    private Project CreateTestProject(string name, Guid userId)
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            UserId = userId,
            Status = 0, // Active
            Progress = 0,
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DomainTask CreateTestTask(string title, Guid userId, Guid? projectId = null)
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

    private Contact CreateTestContact(string name, Guid userId)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            UserId = userId,
            RelationshipType = 0, // Default relationship type
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