using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
// TODO: IDomainEventDispatcher registration will be added when domain event functionality is implemented
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(WhoAndWhat.Application.AssemblyReference).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(WhoAndWhat.Infrastructure.AssemblyReference).Assembly);
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); // Disabled for Docker development
app.MapControllers();

// Seed the database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DataSeeder.SeedDatabaseAsync(context);
}

app.Run();

/// <summary>
/// Program entry point for WhoAndWhat API
/// </summary>
public partial class Program { }