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

public class RepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<User> _repository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<User>(_context);
    }

    [Fact]
    public async Task Should_Add_User()
    {
        // Arrange
        var user = new User("test@test.com", "test", Language.en);

        // Act
        await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Users.FindAsync(user.Id);
        result.Should().NotBeNull();
        result.Username.Should().Be("test");
    }

    [Fact]
    public async Task Should_Get_All_Users()
    {
        // Arrange
        var user1 = new User("test1@test.com", "test1", Language.en);
        var user2 = new User("test2@test.com", "test2", Language.en);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Should_Get_User_By_Id()
    {
        // Arrange
        var user = new User("test@test.com", "test", Language.en);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
    }
    
    [Fact]
    public async Task Should_Find_User()
    {
        // Arrange
        var user = new User("findme@test.com", "findme", Language.en);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(u => u.Username == "findme");

        // Assert
        result.Should().HaveCount(1);
        result.First().Username.Should().Be("findme");
    }
    
    [Fact]
    public async Task Should_Update_User()
    {
        // Arrange
        var user = new User("test@test.com", "test", Language.en);
        await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();

        // Act
        user.UpdatePreferredLanguage(Language.es);
        _repository.Update(user);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Users.FindAsync(user.Id);
        result.Should().NotBeNull();
        result.PreferredLanguage.Should().Be(Language.es);
    }

    [Fact]
    public async Task Should_Remove_User()
    {
        // Arrange
        var user = new User("test@test.com", "test", Language.en);
        await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();

        // Act
        _repository.Remove(user);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Users.FindAsync(user.Id);
        result.Should().BeNull();
    }
}