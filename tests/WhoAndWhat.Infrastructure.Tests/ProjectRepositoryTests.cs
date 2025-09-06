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

namespace WhoAndWhat.Infrastructure.Tests;

public class ProjectRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<Project> _repository;
    private readonly User _testUser;

    public ProjectRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<Project>(_context);
        
        // Create test user for foreign key relationships
        _testUser = new User("test@test.com", "testuser", Language.en);
        _testUser.SetPassword("TestPassword123!");

        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Should_Add_Project()
    {
        // Arrange
        var project = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Project", 
            Description = "Test Description",
            Progress = 25,
            UserId = _testUser.Id
        };

        // Act
        await _repository.AddAsync(project);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Project");
        result.Description.Should().Be("Test Description");
        result.Progress.Should().Be(25);
    }

    [Fact]
    public async Task Should_Get_All_Projects()
    {
        // Arrange
        var project1 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Project 1", 
            UserId = _testUser.Id 
        };
        var project2 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Project 2", 
            UserId = _testUser.Id 
        };
        await _context.Projects.AddRangeAsync(project1, project2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Get_Project_By_Id()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project 
        { 
            Id = projectId, 
            Name = "Test Project", 
            UserId = _testUser.Id 
        };
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
        result.Name.Should().Be("Test Project");
    }

    [Fact]
    public async Task Should_Find_Projects_By_Name()
    {
        // Arrange
        var project1 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Important Project", 
            UserId = _testUser.Id 
        };
        var project2 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Regular Project", 
            UserId = _testUser.Id 
        };
        await _context.Projects.AddRangeAsync(project1, project2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(p => p.Name.Contains("Important"));

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Important Project");
    }

    [Fact]
    public async Task Should_Find_Projects_By_Status()
    {
        // Arrange
        var project1 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Active Project", 
            Status = 1, // Active
            UserId = _testUser.Id 
        };
        var project2 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Completed Project", 
            Status = 2, // Completed
            UserId = _testUser.Id 
        };
        await _context.Projects.AddRangeAsync(project1, project2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(p => p.Status == 1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active Project");
    }

    [Fact]
    public async Task Should_Update_Project()
    {
        // Arrange
        var project = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Original Name", 
            Progress = 10,
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(project);
        await _repository.SaveChangesAsync();

        // Act
        project.Name = "Updated Name";
        project.Progress = 75;
        _repository.Update(project);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Progress.Should().Be(75);
    }

    [Fact]
    public async Task Should_Remove_Project()
    {
        // Arrange
        var project = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Project to Remove", 
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(project);
        await _repository.SaveChangesAsync();

        // Act
        _repository.Remove(project);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Add_Project_With_Dates()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(30);
        var project = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Project with Dates", 
            StartDate = startDate,
            EndDate = endDate,
            UserId = _testUser.Id 
        };

        // Act
        await _repository.AddAsync(project);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Projects.FindAsync(project.Id);
        result.Should().NotBeNull();
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task Should_Find_Projects_By_Progress_Range()
    {
        // Arrange
        var project1 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "Low Progress", 
            Progress = 25,
            UserId = _testUser.Id 
        };
        var project2 = new Project 
        { 
            Id = Guid.NewGuid(), 
            Name = "High Progress", 
            Progress = 85,
            UserId = _testUser.Id 
        };
        await _context.Projects.AddRangeAsync(project1, project2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(p => p.Progress >= 50);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("High Progress");
    }
}