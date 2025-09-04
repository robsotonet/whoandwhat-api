using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDatabaseAsync(ApplicationDbContext context)
    {
        if (!await context.Users.AnyAsync())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "devuser",
                Email = "dev@example.com",
                PreferredLanguage = Language.en,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }
    }
}