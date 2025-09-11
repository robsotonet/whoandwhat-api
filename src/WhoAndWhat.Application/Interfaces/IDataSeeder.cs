namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Interface for data seeding operations
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seeds initial data into the database
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}