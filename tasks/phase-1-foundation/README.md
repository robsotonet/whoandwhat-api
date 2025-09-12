# Phase 1: Foundation & Project Setup 🏗️

## Overview
This phase establishes the fundamental architecture and infrastructure for the WhoAndWhat API. All developers must coordinate closely during this phase as it sets up the foundation that all subsequent phases will build upon.

## Prerequisites
- Visual Studio 2022 or VS Code with C# Dev Kit
- .NET 9.0 SDK installed
- Docker Desktop installed and running
- PostgreSQL client tools (pgAdmin or similar)
- Git configured with project repository access

## Phase Objectives
- [x] Clean Architecture solution structure
- [x] Docker development environment
- [x] Database schema and EF Core setup
- [x] ASP.NET Core API foundation
- [x] Swagger documentation framework
- [x] Testing infrastructure
- [x] CI/CD pipeline foundation

## Developer A Tasks - Infrastructure & Security

### Task P1.A.1: Set up solution structure with Clean Architecture layers
**Duration**: 2 days | **Priority**: Critical | **Blocks**: All other tasks

**Acceptance Criteria**:
- Solution structure follows Clean Architecture principles
- All project dependencies are correctly configured
- Projects compile successfully with no warnings
- Solution includes proper folder structure and naming conventions

**Technical Specifications**:
```
WhoAndWhat/
├── src/
│   ├── WhoAndWhat.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Services/
│   │   └── Events/
│   ├── WhoAndWhat.Application/
│   │   ├── UseCases/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── Services/
│   ├── WhoAndWhat.Infrastructure/
│   │   ├── Data/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   └── Configuration/
│   └── WhoAndWhat.API/
│       ├── Controllers/
│       ├── Middleware/
│       ├── Hubs/
│       └── Configuration/
├── tests/
│   ├── WhoAndWhat.Domain.Tests/
│   ├── WhoAndWhat.Application.Tests/
│   ├── WhoAndWhat.Infrastructure.Tests/
│   └── WhoAndWhat.API.Tests/
└── docs/
```

**Project References**:
- Domain: No external dependencies
- Application: References Domain only
- Infrastructure: References Domain and Application
- API: References all layers
- Tests: Reference corresponding layers

**Deliverables**:
- [ ] Visual Studio solution file (.sln)
- [ ] All project files (.csproj) with correct references
- [ ] EditorConfig file for code formatting
- [ ] Directory.Build.props for shared properties
- [ ] Unit tests validating project structure and dependencies

**Code Quality Requirements**:
- All projects must build without warnings
- EditorConfig rules must be enforced
- Nullable reference types enabled
- Implicit usings configured appropriately

---

### Task P1.A.2: Configure Docker development environment
**Duration**: 2 days | **Priority**: High | **Depends on**: P1.A.1

**Acceptance Criteria**:
- Docker containers start successfully with docker-compose up
- Database connection established and migrations run automatically
- API accessible at http://localhost:5000 and https://localhost:5001
- Hot reload works for development changes
- Environment variables properly configured

**Technical Specifications**:

**Dockerfile** (Multi-stage build):
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/WhoAndWhat.API/WhoAndWhat.API.csproj", "src/WhoAndWhat.API/"]
# ... copy other project files
RUN dotnet restore "src/WhoAndWhat.API/WhoAndWhat.API.csproj"
COPY . .
RUN dotnet build "src/WhoAndWhat.API/WhoAndWhat.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/WhoAndWhat.API/WhoAndWhat.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WhoAndWhat.API.dll"]
```

**docker-compose.yml**:
```yaml
version: '3.8'
services:
  api:
    build: .
    ports:
      - "5000:5000"
      - "5001:5001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=db;Database=WhoAndWhat;User Id=postgres;Password=postgres;
    depends_on:
      - db
    volumes:
      - ./src:/app/src
    
  db:
    image: postgres:15
    environment:
      POSTGRES_DB: WhoAndWhat
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      
volumes:
  postgres_data:
```

**Environment Variables**:
- `ASPNETCORE_ENVIRONMENT`: Development/Staging/Production
- `ConnectionStrings__DefaultConnection`: Database connection string
- `JWT__SecretKey`: JWT signing key (development only)
- `JWT__Issuer`: JWT issuer
- `JWT__Audience`: JWT audience

**Deliverables**:
- [ ] Dockerfile with multi-stage build
- [ ] docker-compose.yml for development environment
- [ ] docker-compose.override.yml for development settings
- [ ] .dockerignore file
- [ ] Environment variable configuration
- [ ] Unit tests for Docker configuration validation
- [ ] Development setup documentation

---

### Task P1.A.3: Set up CI/CD pipeline foundation
**Duration**: 3 days | **Priority**: Medium | **Depends on**: P1.A.1, P1.A.2

**Acceptance Criteria**:
- Azure DevOps pipeline builds successfully
- All tests run automatically on PR creation
- Code coverage report generated (minimum 80%)
- Security scanning integrated
- Automated deployment to staging environment

**Azure DevOps Pipeline (azure-pipelines.yml)**:
```yaml
trigger:
- main
- develop

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

stages:
- stage: Build
  jobs:
  - job: Build
    steps:
    - task: UseDotNet@2
      inputs:
        packageType: 'sdk'
        version: '9.x'
    
    - task: DotNetCoreCLI@2
      displayName: 'Restore packages'
      inputs:
        command: 'restore'
        projects: '**/*.csproj'
    
    - task: DotNetCoreCLI@2
      displayName: 'Build solution'
      inputs:
        command: 'build'
        projects: '**/*.csproj'
        arguments: '--configuration $(buildConfiguration) --no-restore'

- stage: Test
  jobs:
  - job: Test
    steps:
    - task: DotNetCoreCLI@2
      displayName: 'Run tests'
      inputs:
        command: 'test'
        projects: '**/*Tests.csproj'
        arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage" --logger trx'
    
    - task: PublishCodeCoverageResults@1
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'

- stage: SecurityScan
  jobs:
  - job: SecurityScan
    steps:
    - task: WhiteSource@21
      inputs:
        cwd: '$(System.DefaultWorkingDirectory)'

- stage: Deploy
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/develop'))
  jobs:
  - job: DeployToStaging
    steps:
    - task: AzureWebApp@1
      inputs:
        azureSubscription: 'Azure-Subscription'
        appName: 'whoandwhat-api-staging'
        package: '$(System.DefaultWorkingDirectory)/**/*.zip'
```

**Deliverables**:
- [ ] Azure DevOps pipeline configuration
- [ ] Build and test automation
- [ ] Code coverage configuration
- [ ] Security scanning integration
- [ ] Staging deployment automation
- [ ] Integration tests for CI/CD pipeline
- [ ] Pipeline monitoring and alerting setup

---

## Developer B Tasks - Database Foundation

### Task P1.B.1: Design and implement database schema
**Duration**: 3 days | **Priority**: Critical | **Blocks**: All data-related tasks

**Acceptance Criteria**:
- PostgreSQL schema supports all planned entities
- All relationships properly defined with foreign keys
- Indexes created for performance optimization
- Database constraints enforce business rules
- Schema documentation complete

**Entity Relationship Design**:

```sql
-- Users table
CREATE TABLE Users (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Email VARCHAR(255) UNIQUE NOT NULL,
    Username VARCHAR(50) UNIQUE NOT NULL,
    PasswordHash VARCHAR(255),
    PreferredLanguage VARCHAR(5) DEFAULT 'en',
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    LastLoginAt TIMESTAMP WITH TIME ZONE,
    IsEmailVerified BOOLEAN DEFAULT FALSE,
    IsActive BOOLEAN DEFAULT TRUE
);

-- Tasks table
CREATE TABLE Tasks (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Title VARCHAR(200) NOT NULL,
    Description TEXT,
    Category VARCHAR(20) NOT NULL CHECK (Category IN ('ToDo', 'Idea', 'Appointment', 'BillReminder', 'Project')),
    Priority INTEGER DEFAULT 3 CHECK (Priority BETWEEN 1 AND 5),
    Status VARCHAR(20) DEFAULT 'Pending' CHECK (Status IN ('Pending', 'InProgress', 'Completed', 'Cancelled')),
    DueDate TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CompletedAt TIMESTAMP WITH TIME ZONE,
    ProjectId UUID REFERENCES Projects(Id) ON DELETE SET NULL
);

-- Projects table
CREATE TABLE Projects (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Name VARCHAR(200) NOT NULL,
    Description TEXT,
    StartDate TIMESTAMP WITH TIME ZONE,
    EndDate TIMESTAMP WITH TIME ZONE,
    Status VARCHAR(20) DEFAULT 'Active' CHECK (Status IN ('Active', 'Completed', 'OnHold', 'Cancelled')),
    Progress INTEGER DEFAULT 0 CHECK (Progress BETWEEN 0 AND 100),
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Contacts table
CREATE TABLE Contacts (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(255),
    Phone VARCHAR(20),
    QRCode VARCHAR(500),
    InviteCode VARCHAR(50) UNIQUE,
    RelationshipType VARCHAR(20) DEFAULT 'Friend' CHECK (RelationshipType IN ('Friend', 'Family', 'Colleague', 'Other')),
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Task-Contact relationships (many-to-many)
CREATE TABLE TaskContacts (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    TaskId UUID NOT NULL REFERENCES Tasks(Id) ON DELETE CASCADE,
    ContactId UUID NOT NULL REFERENCES Contacts(Id) ON DELETE CASCADE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(TaskId, ContactId)
);

-- Dashboard settings
CREATE TABLE DashboardSettings (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID UNIQUE NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ShowProductivityStreak BOOLEAN DEFAULT TRUE,
    ShowOverdueTasks BOOLEAN DEFAULT TRUE,
    ShowMotivationalContent BOOLEAN DEFAULT TRUE,
    PreferredMetrics JSONB DEFAULT '["completionRate", "productivityStreak"]',
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Indexes for performance
CREATE INDEX idx_tasks_user_id ON Tasks(UserId);
CREATE INDEX idx_tasks_due_date ON Tasks(DueDate) WHERE DueDate IS NOT NULL;
CREATE INDEX idx_tasks_status ON Tasks(Status);
CREATE INDEX idx_tasks_category ON Tasks(Category);
CREATE INDEX idx_tasks_project_id ON Tasks(ProjectId) WHERE ProjectId IS NOT NULL;
CREATE INDEX idx_projects_user_id ON Projects(UserId);
CREATE INDEX idx_contacts_user_id ON Contacts(UserId);
CREATE INDEX idx_task_contacts_task_id ON TaskContacts(TaskId);
CREATE INDEX idx_task_contacts_contact_id ON TaskContacts(ContactId);
```

**Deliverables**:
- [ ] Complete database schema SQL scripts
- [ ] Entity Framework Core model classes
- [ ] Database migration files
- [ ] Seed data for development
- [ ] Unit tests for entity validation
- [ ] Database performance optimization
- [ ] Schema documentation

---

### Task P1.B.2: Set up Entity Framework Core infrastructure
**Duration**: 2 days | **Priority**: High | **Depends on**: P1.B.1

**Acceptance Criteria**:
- DbContext configured with PostgreSQL provider
- Repository pattern base classes implemented
- Database migrations working correctly
- Connection pooling and performance optimized
- Development data seeding functional

**DbContext Configuration**:
```csharp
public class WhoAndWhatDbContext : DbContext
{
    public WhoAndWhatDbContext(DbContextOptions<WhoAndWhatDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Task> Tasks { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<TaskContact> TaskContacts { get; set; }
    public DbSet<DashboardSettings> DashboardSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WhoAndWhatDbContext).Assembly);
        
        // Configure PostgreSQL specific features
        modelBuilder.HasPostgresExtension("uuid-ossp");
        
        base.OnModelCreating(modelBuilder);
    }
}
```

**Repository Pattern Base Classes**:
```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(Guid id);
}

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly WhoAndWhatDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(WhoAndWhatDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // Implementation of interface methods...
}
```

**Deliverables**:
- [ ] DbContext with proper configuration
- [ ] Repository pattern base classes
- [ ] Entity configurations (Fluent API)
- [ ] Migration management system
- [ ] Connection string management
- [ ] Integration tests for database operations
- [ ] Performance monitoring setup

---

### Task P1.B.3: Create domain entities and value objects
**Duration**: 3 days | **Priority**: Critical | **Depends on**: P1.B.2

**Acceptance Criteria**:
- All domain entities implement proper validation
- Value objects enforce business constraints
- Domain events infrastructure in place
- Entity relationships correctly configured
- Business rules enforced at domain level

**Domain Entity Examples**:
```csharp
public class User : BaseEntity
{
    public string Email { get; private set; }
    public string Username { get; private set; }
    public string? PasswordHash { get; private set; }
    public Language PreferredLanguage { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Task> _tasks = new();
    public IReadOnlyList<Task> Tasks => _tasks.AsReadOnly();

    private readonly List<Project> _projects = new();
    public IReadOnlyList<Project> Projects => _projects.AsReadOnly();

    public User(string email, string username, Language preferredLanguage)
    {
        Email = Guard.Against.NullOrWhiteSpace(email, nameof(email));
        Username = Guard.Against.NullOrWhiteSpace(username, nameof(username));
        PreferredLanguage = Guard.Against.Null(preferredLanguage, nameof(preferredLanguage));
        IsActive = true;
        IsEmailVerified = false;

        AddDomainEvent(new UserCreatedEvent(Id, Email, Username));
    }

    // Business methods...
    public void VerifyEmail()
    {
        if (IsEmailVerified) return;
        
        IsEmailVerified = true;
        AddDomainEvent(new UserEmailVerifiedEvent(Id));
    }
}

public class Task : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public TaskCategory Category { get; private set; }
    public Priority Priority { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? ProjectId { get; private set; }

    private readonly List<Contact> _contacts = new();
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    public Task(Guid userId, string title, TaskCategory category)
    {
        UserId = Guard.Against.Default(userId, nameof(userId));
        Title = Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Category = Guard.Against.Null(category, nameof(category));
        Priority = Priority.Medium;
        Status = TaskStatus.Pending;

        AddDomainEvent(new TaskCreatedEvent(Id, UserId, Title, Category));
    }

    // Business methods...
    public void Complete()
    {
        if (Status == TaskStatus.Completed) return;

        Status = TaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new TaskCompletedEvent(Id, UserId));
    }
}
```

**Value Object Examples**:
```csharp
public class Priority : ValueObject
{
    public static Priority VeryLow => new(1);
    public static Priority Low => new(2);
    public static Priority Medium => new(3);
    public static Priority High => new(4);
    public static Priority VeryHigh => new(5);

    public int Value { get; }

    private Priority(int value)
    {
        if (value < 1 || value > 5)
            throw new ArgumentException("Priority must be between 1 and 5", nameof(value));
        
        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}

public class TaskCategory : ValueObject
{
    public static TaskCategory ToDo => new(nameof(ToDo));
    public static TaskCategory Idea => new(nameof(Idea));
    public static TaskCategory Appointment => new(nameof(Appointment));
    public static TaskCategory BillReminder => new(nameof(BillReminder));
    public static TaskCategory Project => new(nameof(Project));

    public string Value { get; }

    private TaskCategory(string value)
    {
        Value = Guard.Against.NullOrWhiteSpace(value, nameof(value));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Deliverables**:
- [ ] All domain entities with proper encapsulation
- [ ] Value objects for business concepts
- [ ] Domain events infrastructure
- [ ] Entity validation and business rules
- [ ] Unit tests for all domain logic (80%+ coverage)
- [ ] Domain service interfaces
- [ ] Complete domain model documentation

---

## Developer C Tasks - API Foundation

### Task P1.C.1: Set up ASP.NET Core Web API foundation
**Duration**: 2 days | **Priority**: Critical | **Blocks**: All API tasks

**Acceptance Criteria**:
- ASP.NET Core 9.0 Web API project properly configured
- Dependency injection container set up with all services
- Middleware pipeline configured correctly
- API versioning implemented
- Global exception handling functional

**Program.cs Configuration**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver"));
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// Add database
builder.Services.AddDbContext<WhoAndWhatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Add application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        // JWT configuration
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebAndMobile", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // Web client
              .WithOrigins("capacitor://localhost", "ionic://localhost") // Mobile client
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhoAndWhat API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowWebAndMobile");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();

app.Run();
```

**Global Exception Middleware**:
```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ValidationException => new { error = "Validation failed", message = exception.Message, statusCode = 400 },
            NotFoundException => new { error = "Resource not found", message = exception.Message, statusCode = 404 },
            UnauthorizedException => new { error = "Unauthorized", message = exception.Message, statusCode = 401 },
            _ => new { error = "Internal server error", message = "An unexpected error occurred", statusCode = 500 }
        };

        context.Response.StatusCode = response.statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

**Deliverables**:
- [ ] ASP.NET Core Web API project with proper configuration
- [ ] Dependency injection container setup
- [ ] Middleware pipeline configuration
- [ ] API versioning implementation
- [ ] Global exception handling
- [ ] Integration tests for API foundation
- [ ] API configuration documentation

---

### Task P1.C.2: Configure Swagger/OpenAPI documentation
**Duration**: 2 days | **Priority**: High | **Depends on**: P1.C.1

**Acceptance Criteria**:
- Swagger UI accessible and functional
- All endpoints documented with examples
- Authentication documented
- API versioning reflected in documentation
- Request/response schemas accurate

**Swagger Configuration**:
```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WhoAndWhat API",
        Version = "v1",
        Description = "Smart Task Management API with Social & AI Integration",
        Contact = new OpenApiContact
        {
            Name = "WhoAndWhat Team",
            Email = "api@whoandwhat.com"
        }
    });

    // Add JWT authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Add examples
    c.EnableAnnotations();
    c.ExampleFilters();
});
```

**Controller Documentation Example**:
```csharp
/// <summary>
/// Authentication endpoints for user registration and login
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration details</param>
    /// <returns>User registration result</returns>
    /// <response code="201">User successfully registered</response>
    /// <response code="400">Invalid registration data</response>
    /// <response code="409">User with email already exists</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        // Implementation
    }
}
```

**Deliverables**:
- [ ] Comprehensive Swagger UI setup
- [ ] All endpoints documented with XML comments
- [ ] Request/response DTOs with examples
- [ ] Authentication flow documentation
- [ ] API versioning in Swagger
- [ ] Documentation validation tests
- [ ] API client generation templates

---

### Task P1.C.3: Set up logging and monitoring infrastructure
**Duration**: 2 days | **Priority**: Medium | **Depends on**: P1.C.1

**Acceptance Criteria**:
- Structured logging with Serilog configured
- Application Insights integration working
- Health check endpoints functional
- Performance metrics collection active
- Log correlation and tracing enabled

**Serilog Configuration**:
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.ApplicationInsights(TelemetryConfiguration.CreateDefault(), TelemetryConverter.Traces)
    .CreateLogger();

builder.Host.UseSerilog();
```

**Health Checks Configuration**:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WhoAndWhatDbContext>("database")
    .AddCheck("redis", () => 
    {
        // Redis health check implementation
        return HealthCheckResult.Healthy();
    })
    .AddApplicationInsightsPublisher();

// In pipeline
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

**Application Insights Configuration**:
```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
});

builder.Services.AddSingleton<ITelemetryInitializer, CloudRoleNameTelemetryInitializer>();
```

**Deliverables**:
- [ ] Serilog structured logging setup
- [ ] Application Insights integration
- [ ] Health check endpoints implementation
- [ ] Performance counter collection
- [ ] Request/response logging middleware
- [ ] Unit tests for logging components
- [ ] Monitoring dashboard configuration

---

## Phase Completion Criteria

### All Tasks Must Be Completed Before Phase 2
- [ ] Solution compiles without warnings
- [ ] All unit tests pass with 80%+ coverage
- [ ] Docker environment starts successfully
- [ ] Database migrations run without errors
- [ ] API responds to requests at all endpoints
- [ ] Swagger documentation accessible and accurate
- [ ] CI/CD pipeline builds and deploys successfully
- [ ] Health checks return healthy status

### Documentation Requirements
- [ ] Architecture decision records (ADRs) created
- [ ] Development setup guide updated
- [ ] API documentation in Swagger complete
- [ ] Database schema documented
- [ ] Code review checklist established

### Integration Points
- [ ] All three developers can run the complete solution locally
- [ ] Database schema supports all planned features
- [ ] API endpoints return proper HTTP status codes
- [ ] Error handling works consistently across all layers
- [ ] Authentication infrastructure ready for Phase 2

---

## Risk Assessment & Mitigation

### High Risk Items
1. **Docker Setup Issues**: Test on multiple developer machines
2. **Database Performance**: Monitor query performance during development
3. **CI/CD Pipeline Failures**: Implement comprehensive testing
4. **Team Coordination**: Daily standups and integration meetings

### Dependencies
- External: Docker, PostgreSQL, Azure DevOps
- Internal: All developers must complete their tasks before Phase 2
- Shared: Database schema affects all developers

### Success Metrics
- 100% task completion rate
- Zero critical bugs in foundation
- < 2 second API response times
- 80%+ unit test coverage achieved
- All developers can develop locally without issues

---

*Last Updated: September 3, 2025*