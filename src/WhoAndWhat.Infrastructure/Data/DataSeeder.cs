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
            return; // Database already seeded
        }

        var userDomainService = new UserDomainService();

        // Create dev user
        var user1 = userDomainService.CreateUser("dev@example.com", "devuser", "Password123!", Language.en);
        user1.VerifyEmail(); // Mark as verified for dev environment

        // Create test user
        var user2 = userDomainService.CreateUser("test@example.com", "testuser", "Password456!", Language.es);
        user2.VerifyEmail(); // Mark as verified for dev environment

        await context.Users.AddRangeAsync(user1, user2);
        await context.SaveChangesAsync();
    }
}