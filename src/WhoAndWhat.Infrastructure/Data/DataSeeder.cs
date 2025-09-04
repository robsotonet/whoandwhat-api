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
            var user = new User("dev@example.com", "devuser", Language.en);
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }
    }
}