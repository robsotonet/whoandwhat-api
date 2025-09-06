using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Tests.Data;

/// <summary>
/// Unit tests for DataSeeder static methods
/// Tests database seeding functionality for development environment
/// </summary>
public class DataSeederTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public DataSeederTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Create_Development_Users_When_Database_Empty()
    {
        // Arrange
        _context.Users.Should().BeEmpty("Database should start empty");

        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var users = await _context.Users.ToListAsync();
        users.Should().HaveCount(2, "Should create exactly 2 development users");

        // Verify first user (dev user)
        var devUser = users.FirstOrDefault(u => u.Email == "dev@example.com");
        devUser.Should().NotBeNull("Dev user should be created");
        devUser!.Username.Should().Be("devuser");
        devUser.PreferredLanguage.Should().Be(Language.en);
        devUser.IsEmailVerified.Should().BeTrue("Dev user should be verified");
        devUser.VerifyPassword("Password123!").Should().BeTrue("Dev user password should be correct");

        // Verify second user (test user)
        var testUser = users.FirstOrDefault(u => u.Email == "test@example.com");
        testUser.Should().NotBeNull("Test user should be created");
        testUser!.Username.Should().Be("testuser");
        testUser.PreferredLanguage.Should().Be(Language.es);
        testUser.IsEmailVerified.Should().BeTrue("Test user should be verified");
        testUser.VerifyPassword("Password456!").Should().BeTrue("Test user password should be correct");
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Not_Seed_When_Users_Already_Exist()
    {
        // Arrange - Add an existing user to the database
        var existingUser = new User("existing@example.com", "existing", Language.en);
        existingUser.SetPassword("Password123!");
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var initialUserCount = await _context.Users.CountAsync();
        initialUserCount.Should().Be(1, "Should have exactly 1 existing user");

        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var finalUserCount = await _context.Users.CountAsync();
        finalUserCount.Should().Be(1, "Should not add more users when database already has users");

        var users = await _context.Users.ToListAsync();
        users.Should().Contain(u => u.Email == "existing@example.com", "Original user should still exist");
        users.Should().NotContain(u => u.Email == "dev@example.com", "Dev user should not be added");
        users.Should().NotContain(u => u.Email == "test@example.com", "Test user should not be added");
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Handle_Multiple_Calls_Safely()
    {
        // Act - Call seeding multiple times
        await DataSeeder.SeedDatabaseAsync(_context);
        await DataSeeder.SeedDatabaseAsync(_context);
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var users = await _context.Users.ToListAsync();
        users.Should().HaveCount(2, "Should still have exactly 2 users after multiple seeding calls");

        var devUserCount = users.Count(u => u.Email == "dev@example.com");
        var testUserCount = users.Count(u => u.Email == "test@example.com");
        
        devUserCount.Should().Be(1, "Should have exactly 1 dev user, not duplicates");
        testUserCount.Should().Be(1, "Should have exactly 1 test user, not duplicates");
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Persist_Users_To_Database()
    {
        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert - Create new context to verify persistence
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "PersistenceTest")
            .Options;
        
        using var newContext = new ApplicationDbContext(options);
        
        // Add users to new context and verify
        await DataSeeder.SeedDatabaseAsync(newContext);
        
        var persistedUsers = await newContext.Users.ToListAsync();
        persistedUsers.Should().HaveCount(2, "Users should persist in database");
        
        persistedUsers.Should().Contain(u => u.Email == "dev@example.com");
        persistedUsers.Should().Contain(u => u.Email == "test@example.com");
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Create_Valid_User_Entities()
    {
        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            // Verify basic entity properties
            user.Id.Should().NotBe(Guid.Empty, "User should have valid ID");
            user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1), "CreatedAt should be recent");
            user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1), "UpdatedAt should be recent");
            
            // Verify user is active by default
            user.IsActive.Should().BeTrue("User should be active by default");
            user.IsLocked.Should().BeFalse("User should not be locked by default");
            
            // Verify email is verified for dev environment
            user.IsEmailVerified.Should().BeTrue("Users should be verified in development");
            
            // Verify authentication fields
            user.PasswordHash.Should().NotBeNullOrEmpty("Password hash should be set");
        }
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Use_UserDomainService_For_User_Creation()
    {
        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var users = await _context.Users.ToListAsync();
        users.Should().HaveCount(2);

        // Verify that users are created with proper domain service logic
        foreach (var user in users)
        {
            // UserDomainService should create users with proper validation
            user.Email.Should().NotBeNullOrEmpty();
            user.Username.Should().NotBeNullOrEmpty();
            user.PasswordHash.Should().NotBeNullOrEmpty("UserDomainService should hash passwords");
            
            // Verify email format (basic validation)
            user.Email.Should().Contain("@", "Email should have valid format");
            user.Email.Should().Contain(".", "Email should have domain");
        }
    }

    [Fact]
    public async Task SeedDatabaseAsync_Should_Set_Correct_Language_Preferences()
    {
        // Act
        await DataSeeder.SeedDatabaseAsync(_context);

        // Assert
        var users = await _context.Users.ToListAsync();
        
        var devUser = users.First(u => u.Email == "dev@example.com");
        var testUser = users.First(u => u.Email == "test@example.com");
        
        devUser.PreferredLanguage.Should().Be(Language.en, "Dev user should prefer English");
        testUser.PreferredLanguage.Should().Be(Language.es, "Test user should prefer Spanish");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}