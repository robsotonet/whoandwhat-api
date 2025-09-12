using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Infrastructure.Data.Seeding;

/// <summary>
/// Main database seeder that orchestrates all seeding operations
/// </summary>
public class DatabaseSeeder : IDataSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(IServiceProvider serviceProvider, ILogger<DatabaseSeeder> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seeds all initial data into the database
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting database seeding process...");

            using var scope = _serviceProvider.CreateScope();

            // Seed motivational content
            var contentSeeder = scope.ServiceProvider.GetRequiredService<MotivationalContentSeeder>();
            await contentSeeder.SeedAsync(cancellationToken);

            // TODO: Add other seeders as needed (user roles, default settings, etc.)

            _logger.LogInformation("Database seeding process completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding");
            throw;
        }
    }
}
