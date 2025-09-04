using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Tests;

public class EventRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<Event> _repository;
    private readonly User _testUser;

    public EventRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<Event>(_context);
        
        // Create test user for foreign key relationships
        _testUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = "testuser", 
            Email = "test@test.com" 
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Should_Add_Event()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(1);
        var endDate = DateTime.UtcNow.AddDays(1).AddHours(2);
        var @event = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Test Event", 
            Description = "Test Description",
            StartDate = startDate,
            EndDate = endDate,
            Location = "Conference Room A",
            Type = "Meeting",
            UserId = _testUser.Id
        };

        // Act
        await _repository.AddAsync(@event);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Events.FindAsync(@event.Id);
        result.Should().NotBeNull();
        result.Title.Should().Be("Test Event");
        result.Description.Should().Be("Test Description");
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
        result.Location.Should().Be("Conference Room A");
        result.Type.Should().Be("Meeting");
    }

    [Fact]
    public async Task Should_Get_All_Events()
    {
        // Arrange
        var event1 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Event 1",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        var event2 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Event 2",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Conference",
            UserId = _testUser.Id 
        };
        await _context.Events.AddRangeAsync(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Get_Event_By_Id()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var @event = new Event 
        { 
            Id = eventId, 
            Title = "Test Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        await _context.Events.AddAsync(@event);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(eventId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(eventId);
        result.Title.Should().Be("Test Event");
    }

    [Fact]
    public async Task Should_Find_Events_By_Title()
    {
        // Arrange
        var event1 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Important Meeting",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        var event2 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Regular Conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(2),
            Type = "Conference",
            UserId = _testUser.Id 
        };
        await _context.Events.AddRangeAsync(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(e => e.Title.Contains("Important"));

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Important Meeting");
    }

    [Fact]
    public async Task Should_Find_Events_By_Type()
    {
        // Arrange
        var event1 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Team Meeting",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        var event2 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Annual Conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(8),
            Type = "Conference",
            UserId = _testUser.Id 
        };
        await _context.Events.AddRangeAsync(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(e => e.Type == "Meeting");

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Team Meeting");
    }

    [Fact]
    public async Task Should_Find_Events_By_Date_Range()
    {
        // Arrange
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var nextWeek = DateTime.UtcNow.AddDays(7);
        
        var event1 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Tomorrow Event",
            StartDate = tomorrow,
            EndDate = tomorrow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        var event2 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Next Week Event",
            StartDate = nextWeek,
            EndDate = nextWeek.AddHours(1),
            Type = "Conference",
            UserId = _testUser.Id 
        };
        await _context.Events.AddRangeAsync(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(e => e.StartDate >= tomorrow && e.StartDate < tomorrow.AddDays(2));

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Tomorrow Event");
    }

    [Fact]
    public async Task Should_Update_Event()
    {
        // Arrange
        var @event = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Original Title",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(@event);
        await _repository.SaveChangesAsync();

        // Act
        @event.Title = "Updated Title";
        @event.Location = "New Location";
        _repository.Update(@event);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Events.FindAsync(@event.Id);
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
        result.Location.Should().Be("New Location");
    }

    [Fact]
    public async Task Should_Remove_Event()
    {
        // Arrange
        var @event = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Event to Remove",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(@event);
        await _repository.SaveChangesAsync();

        // Act
        _repository.Remove(@event);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Events.FindAsync(@event.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Find_Events_By_Location()
    {
        // Arrange
        var event1 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Room A Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Location = "Conference Room A",
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        var event2 = new Event 
        { 
            Id = Guid.NewGuid(), 
            Title = "Room B Event",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Location = "Conference Room B",
            Type = "Meeting",
            UserId = _testUser.Id 
        };
        await _context.Events.AddRangeAsync(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(e => e.Location == "Conference Room A");

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Room A Event");
    }
}