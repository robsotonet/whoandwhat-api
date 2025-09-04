using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDatabaseAsync(ApplicationDbContext context)
    {
        if (await context.Users.AnyAsync())
        {
            return;
        }

        var userDomainService = new UserDomainService();

        var (passwordHash, salt) = userDomainService.CreatePasswordHash("Password123!");

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "devuser",
            Email = "dev@example.com",
            PasswordHash = passwordHash,
            Salt = salt,
            PreferredLanguage = Language.en,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsVerified = true
        };

        var (passwordHash2, salt2) = userDomainService.CreatePasswordHash("Password456!");

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = passwordHash2,
            Salt = salt2,
            PreferredLanguage = Language.es,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsVerified = true
        };

        await context.Users.AddRangeAsync(user1, user2);
        await context.SaveChangesAsync();
    }
}