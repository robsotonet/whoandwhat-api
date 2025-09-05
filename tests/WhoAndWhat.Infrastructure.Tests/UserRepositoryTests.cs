
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Tests;

public class UserRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IUserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            Salt = "salt"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserByEmailAsync(user.Email);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var result = await _repository.GetUserByEmailAsync(email);

        // Assert
        result.Should().BeNull();
    }
}
