using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Events;
using WhoAndWhat.Infrastructure;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using WhoAndWhat.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyReference).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly);
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
    await DataSeeder.SeedDatabaseAsync(app);
}

app.Run();

/// <summary>
/// Program entry point for WhoAndWhat API
/// </summary>
public partial class Program { }