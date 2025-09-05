using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;
using DomainTask = WhoAndWhat.Domain.Entities.Task;

namespace WhoAndWhat.Infrastructure.Tests;

public class TaskRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<DomainTask> _repository;
    private readonly User _testUser;

    public TaskRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<DomainTask>(_context);
        
        // Create test user for foreign key relationships

        _testUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = "testuser", 
            Email = "test@test.com",
            PasswordHash = "testhash",
            Salt = "testsalt"
        };

        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Should_Add_Task()
    {
        // Arrange
        var task = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Test Task", 
            Description = "Test Description",
            UserId = _testUser.Id
        };

        // Act
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Tasks.FindAsync(task.Id);
        result.Should().NotBeNull();
        result.Title.Should().Be("Test Task");
        result.Description.Should().Be("Test Description");
    }

    [Fact]
    public async Task Should_Get_All_Tasks()
    {
        // Arrange
        var task1 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Task 1", 
            UserId = _testUser.Id 
        };
        var task2 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Task 2", 
            UserId = _testUser.Id 
        };
        await _context.Tasks.AddRangeAsync(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Get_Task_By_Id()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new DomainTask 
        { 
            Id = taskId, 
            Title = "Test Task", 
            UserId = _testUser.Id 
        };
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(taskId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(taskId);
        result.Title.Should().Be("Test Task");
    }

    [Fact]
    public async Task Should_Find_Tasks_By_Title()
    {
        // Arrange
        var task1 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Find This Task", 
            UserId = _testUser.Id 
        };
        var task2 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Another Task", 
            UserId = _testUser.Id 
        };
        await _context.Tasks.AddRangeAsync(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(t => t.Title.Contains("Find This"));

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Find This Task");
    }

    [Fact]
    public async Task Should_Find_Tasks_By_User()
    {
        // Arrange

        var anotherUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = "anotheruser", 
            Email = "another@test.com",
            PasswordHash = "testhash",
            Salt = "testsalt"
        };

        await _context.Users.AddAsync(anotherUser);
        
        var task1 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "User Task", 
            UserId = _testUser.Id 
        };
        var task2 = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Another User Task", 
            UserId = anotherUser.Id 
        };
        await _context.Tasks.AddRangeAsync(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(t => t.UserId == _testUser.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("User Task");
    }

    [Fact]
    public async Task Should_Update_Task()
    {
        // Arrange
        var task = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Original Title", 
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        // Act
        task.Title = "Updated Title";
        _repository.Update(task);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Tasks.FindAsync(task.Id);
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Should_Remove_Task()
    {
        // Arrange
        var task = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Task to Remove", 
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        // Act
        _repository.Remove(task);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Tasks.FindAsync(task.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Add_Task_With_Due_Date()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(7);
        var task = new DomainTask 
        { 
            Id = Guid.NewGuid(), 
            Title = "Task with Due Date", 
            DueDate = dueDate,
            UserId = _testUser.Id 
        };

        // Act
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Tasks.FindAsync(task.Id);
        result.Should().NotBeNull();
        result.DueDate.Should().Be(dueDate);
    }
}