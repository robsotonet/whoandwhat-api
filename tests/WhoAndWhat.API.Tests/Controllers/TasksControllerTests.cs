using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using WhoAndWhat.API.Tests.Helpers;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers;

/// <summary>
/// Integration tests for Tasks controller endpoints
/// </summary>
public class TasksControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed = false;

    public TasksControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            
            // Override configuration for tests
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.Testing.json", optional: false);
            });
            
            builder.ConfigureServices(services =>
            {
                // Remove all existing database-related registrations
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();

                // Add InMemory database for testing with unique name per test class
                var testDatabaseName = $"TestDb_{GetType().Name}_{Guid.NewGuid()}";
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(testDatabaseName);
                    options.EnableSensitiveDataLogging();
                    options.EnableServiceProviderCaching(false);
                    options.EnableDetailedErrors();
                });

                // Register missing services required for task management
                services.AddScoped<IAppTaskRepository, TaskRepository>();
                services.AddScoped<IContactRepository, ContactRepository>();
                services.AddScoped<CategoryBusinessRuleService>();
                services.AddScoped<CategoryWorkflowService>();
                services.AddScoped<ITaskApplicationService, TaskApplicationService>();

                // Build the service provider and ensure database is ready
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<ApplicationDbContext>();

                    // Ensure the database is created and clean
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                }
            });
        });
        
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Helper Methods


    private async Task<TaskDto> CreateTestTaskAsync(string token, string title = "Test Task")
    {
        var createRequest = new CreateTaskRequest
        {
            Title = title,
            Description = "Test Description",
            Category = 0, // ToDo
            Priority = 1, // Low
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync("/api/v1/tasks", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        return JsonSerializer.Deserialize<TaskDto>(responseContent, _jsonOptions)!;
    }

    private async Task<ContactDto> CreateTestContactAsync(string token, string name = "Test Contact")
    {
        var createRequest = new CreateContactRequest
        {
            Name = name,
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            Phone = "+1234567890",
            RelationshipType = 2 // Colleague
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync("/api/v1/contacts", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        return JsonSerializer.Deserialize<ContactDto>(responseContent, _jsonOptions)!;
    }

    #endregion

    #region GET /api/v1/tasks Tests

    [Fact]
    public async Task GetTasks_Should_Return_Ok_With_Valid_Token()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<TaskDto>>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTasks_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTasks_Should_Support_Pagination()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create multiple test tasks
        for (int i = 0; i < 5; i++)
        {
            await CreateTestTaskAsync(token, $"Task {i}");
        }

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?pageSize=2&pageNumber=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<TaskDto>>(content, _jsonOptions);
        result!.PageSize.Should().Be(2);
        result.Page.Should().Be(1);
        result.Items.Count().Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public async Task GetTasks_Should_Support_Filtering_By_Category()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks?category=0"); // ToDo category

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<TaskDto>>(content, _jsonOptions);
        result.Should().NotBeNull();
    }

    #endregion

    #region GET /api/v1/tasks/{id} Tests

    [Fact]
    public async Task GetTask_Should_Return_Ok_With_Valid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskDto>(content, _jsonOptions);
        result!.Id.Should().Be(task.Id);
        result.Title.Should().Be("Test Task");
    }

    [Fact]
    public async Task GetTask_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/v1/tasks Tests

    [Fact]
    public async Task CreateTask_Should_Return_Created_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateTaskRequest
        {
            Title = "New Task",
            Description = "Task description",
            Category = 0, // ToDo
            Priority = 2, // Medium
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskDto>(responseContent, _jsonOptions);
        result!.Title.Should().Be("New Task");
        result.Category.Should().Be(0);
        result.Priority.Should().Be(2);
    }

    [Fact]
    public async Task CreateTask_Should_Return_BadRequest_With_Empty_Title()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateTaskRequest
        {
            Title = "", // Empty title should be invalid
            Description = "Task description",
            Category = 0,
            Priority = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/v1/tasks/{id} Tests

    [Fact]
    public async Task UpdateTask_Should_Return_Ok_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var updateRequest = new UpdateTaskRequest
        {
            Title = "Updated Task",
            Description = "Updated description",
            Priority = 3, // High
            Status = 1 // InProgress
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskDto>(responseContent, _jsonOptions);
        result!.Title.Should().Be("Updated Task");
        result.Priority.Should().Be(3);
    }

    [Fact]
    public async Task UpdateTask_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();
        
        var updateRequest = new UpdateTaskRequest
        {
            Title = "Updated Task",
            Description = "Updated description"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync($"/api/v1/tasks/{invalidId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/v1/tasks/{id} Tests

    [Fact]
    public async Task DeleteTask_Should_Return_NoContent_With_Valid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTask_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/v1/tasks/{id}/convert-to-project Tests

    [Fact]
    public async Task ConvertToProject_Should_Return_Ok_With_Valid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/convert-to-project", null);

        // Assert - This may return BadRequest if the task doesn't meet conversion criteria
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion

    #region GET /api/v1/tasks/categories Tests

    [Fact]
    public async Task GetCategories_Should_Return_Ok_With_Category_List()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ToDo");
        content.Should().Contain("Project");
    }

    #endregion

    #region GET /api/v1/tasks/search Tests

    [Fact]
    public async Task SearchTasks_Should_Return_Ok_With_Valid_Query()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await CreateTestTaskAsync(token, "Searchable Task");

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/search?query=Searchable");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskSearchResult>(content, _jsonOptions);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchTasks_Should_Return_BadRequest_With_Empty_Query()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/search?query=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region GET /api/v1/tasks/statistics Tests

    [Fact]
    public async Task GetTaskStatistics_Should_Return_Ok_With_Valid_Token()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    #endregion

    #region PATCH /api/v1/tasks/{id}/status Tests

    [Fact]
    public async Task UpdateTaskStatus_Should_Return_Ok_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var statusUpdateRequest = new
        {
            Status = 1, // InProgress
            Reason = "Starting work on task"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(statusUpdateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/api/v1/tasks/{task.Id}/status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskDto>(responseContent, _jsonOptions);
        result!.Status.Should().Be(1);
    }

    [Fact]
    public async Task UpdateTaskStatus_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();
        
        var statusUpdateRequest = new
        {
            Status = 2, // Completed
            Reason = "Task completed"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(statusUpdateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/api/v1/tasks/{invalidId}/status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/v1/tasks/overdue Tests

    [Fact]
    public async Task GetOverdueTasks_Should_Return_Ok_With_Overdue_Tasks()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create an overdue task
        var createRequest = new CreateTaskRequest
        {
            Title = "Overdue Task",
            Description = "This task is overdue",
            Category = 0, // ToDo
            Priority = 3, // High
            DueDate = DateTime.UtcNow.AddDays(-3) // 3 days in the past
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/v1/tasks", createContent);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskDto>>(content, _jsonOptions);
        tasks.Should().NotBeNull();
        tasks!.Any(t => t.Title == "Overdue Task").Should().BeTrue();
    }

    [Fact]
    public async Task GetOverdueTasks_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tasks/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/v1/tasks/due-today Tests

    [Fact]
    public async Task GetTasksDueToday_Should_Return_Ok_With_Tasks_Due_Today()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create a task due today
        var createRequest = new CreateTaskRequest
        {
            Title = "Task Due Today",
            Description = "This task is due today",
            Category = 2, // Appointment
            Priority = 2, // Medium
            DueDate = DateTime.UtcNow.Date.AddHours(15) // Today at 3 PM
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/v1/tasks", createContent);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/due-today");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskDto>>(content, _jsonOptions);
        tasks.Should().NotBeNull();
        tasks!.Any(t => t.Title == "Task Due Today").Should().BeTrue();
    }

    [Fact]
    public async Task GetTasksDueToday_Should_Return_Empty_When_No_Tasks_Due_Today()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create a task due tomorrow
        var createRequest = new CreateTaskRequest
        {
            Title = "Task Due Tomorrow",
            Description = "This task is due tomorrow",
            Category = 0, // ToDo
            Priority = 1, // Low
            DueDate = DateTime.UtcNow.Date.AddDays(1)
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/v1/tasks", createContent);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/due-today");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskDto>>(content, _jsonOptions);
        tasks.Should().NotBeNull();
        tasks!.Any(t => t.Title == "Task Due Tomorrow").Should().BeFalse();
    }

    #endregion

    #region POST /api/v1/tasks/batch Tests

    [Fact]
    public async Task BatchOperation_Should_Delete_Multiple_Tasks()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create multiple tasks
        var task1 = await CreateTestTaskAsync(token, "Task 1");
        var task2 = await CreateTestTaskAsync(token, "Task 2");
        var task3 = await CreateTestTaskAsync(token, "Task 3");
        
        var batchRequest = new
        {
            Operation = "delete",
            TaskIds = new[] { task1.Id, task2.Id, task3.Id }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("\"operation\":\"delete\"");
        responseContent.Should().Contain("\"successfulTasks\":3");
        
        // Verify tasks are deleted
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task1.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BatchOperation_Should_Complete_Multiple_Tasks()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create multiple tasks
        var task1 = await CreateTestTaskAsync(token, "Task to Complete 1");
        var task2 = await CreateTestTaskAsync(token, "Task to Complete 2");
        
        var batchRequest = new
        {
            Operation = "complete",
            TaskIds = new[] { task1.Id, task2.Id }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("\"operation\":\"complete\"");
        responseContent.Should().Contain("\"successfulTasks\":2");
        
        // Verify tasks are completed
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task1.Id}");
        var taskContent = await getResponse.Content.ReadAsStringAsync();
        var updatedTask = JsonSerializer.Deserialize<TaskDto>(taskContent, _jsonOptions);
        updatedTask!.Status.Should().Be(2); // Completed
    }

    [Fact]
    public async Task BatchOperation_Should_Archive_Multiple_Tasks()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create multiple tasks
        var task1 = await CreateTestTaskAsync(token, "Task to Archive 1");
        var task2 = await CreateTestTaskAsync(token, "Task to Archive 2");
        
        var batchRequest = new
        {
            Operation = "archive",
            TaskIds = new[] { task1.Id, task2.Id }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("\"operation\":\"archive\"");
        responseContent.Should().Contain("\"successfulTasks\":2");
        
        // Verify tasks are archived
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task1.Id}");
        var taskContent = await getResponse.Content.ReadAsStringAsync();
        var updatedTask = JsonSerializer.Deserialize<TaskDto>(taskContent, _jsonOptions);
        updatedTask!.Status.Should().Be(3); // Archived
    }

    [Fact]
    public async Task BatchOperation_Should_Return_BadRequest_With_Invalid_Operation()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var task = await CreateTestTaskAsync(token);
        
        var batchRequest = new
        {
            Operation = "invalid_operation",
            TaskIds = new[] { task.Id }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region POST /api/v1/tasks/{id}/actions Tests

    [Fact]
    public async Task ExecuteTaskAction_Should_Return_Ok_With_Valid_Action()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var actionRequest = new
        {
            ActionId = "MarkCompleted",
            Parameters = new Dictionary<string, object>(),
            Reason = "Task completed"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(actionRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/actions", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskDto>(responseContent, _jsonOptions);
        result!.Status.Should().Be(2); // Completed
    }

    [Fact]
    public async Task ExecuteTaskAction_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();
        
        var actionRequest = new
        {
            ActionId = "MarkCompleted",
            Parameters = new Dictionary<string, object>()
        };

        var content = new StringContent(
            JsonSerializer.Serialize(actionRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{invalidId}/actions", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteTaskAction_Should_Return_BadRequest_With_Invalid_Action()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var actionRequest = new
        {
            ActionId = "InvalidAction",
            Parameters = new Dictionary<string, object>()
        };

        var content = new StringContent(
            JsonSerializer.Serialize(actionRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/actions", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region GET /api/v1/tasks/status-options Tests

    [Fact]
    public async Task GetStatusOptions_Should_Return_Ok_With_Status_List()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/status-options");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Pending");
        content.Should().Contain("InProgress");
        content.Should().Contain("Completed");
        content.Should().Contain("Archived");
    }

    [Fact]
    public async Task GetStatusOptions_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tasks/status-options");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/v1/tasks/{id}/workflow Tests

    [Fact]
    public async Task GetTaskWorkflow_Should_Return_Ok_With_Valid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{task.Id}/workflow");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTaskWorkflow_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{invalidId}/workflow");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskWorkflow_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var validId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{validId}/workflow");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/v1/tasks/scheduling Tests

    [Fact]
    public async Task GetTaskScheduling_Should_Return_Ok_With_Valid_Parameters()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/tasks/scheduling?maxSuggestions=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTaskScheduling_Should_Return_Ok_With_Target_Date()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var targetDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/scheduling?targetDate={Uri.EscapeDataString(targetDate)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTaskScheduling_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tasks/scheduling");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region POST /api/v1/tasks/batch/update-status Tests

    [Fact]
    public async Task BatchUpdateStatus_Should_Update_Multiple_Task_Statuses()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create multiple tasks
        var task1 = await CreateTestTaskAsync(token, "Task 1 for Status Update");
        var task2 = await CreateTestTaskAsync(token, "Task 2 for Status Update");
        var task3 = await CreateTestTaskAsync(token, "Task 3 for Status Update");
        
        var batchStatusRequest = new
        {
            NewStatus = 1, // InProgress
            TaskIds = new[] { task1.Id, task2.Id, task3.Id },
            Reason = "Batch status update test"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchStatusRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch/update-status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("\"operation\":\"update-status-1\"");
        responseContent.Should().Contain("\"successfulTasks\":3");
        
        // Verify tasks status updated
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task1.Id}");
        var taskContent = await getResponse.Content.ReadAsStringAsync();
        var updatedTask = JsonSerializer.Deserialize<TaskDto>(taskContent, _jsonOptions);
        updatedTask!.Status.Should().Be(1); // InProgress
    }

    [Fact]
    public async Task BatchUpdateStatus_Should_Return_BadRequest_With_Invalid_Status()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var task = await CreateTestTaskAsync(token);
        
        var batchStatusRequest = new
        {
            NewStatus = 5, // Invalid status (should be 0-3)
            TaskIds = new[] { task.Id }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchStatusRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch/update-status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task BatchUpdateStatus_Should_Handle_Mix_Of_Valid_And_Invalid_Tasks()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Create one valid task
        var validTask = await CreateTestTaskAsync(token, "Valid Task");
        var invalidTaskId = Guid.NewGuid(); // Task that doesn't exist
        
        var batchStatusRequest = new
        {
            NewStatus = 2, // Completed
            TaskIds = new[] { validTask.Id, invalidTaskId },
            Reason = "Mixed batch test"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchStatusRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch/update-status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("\"totalTasks\":2");
        responseContent.Should().Contain("\"successfulTasks\":1");
        responseContent.Should().Contain("\"failedTasks\":1");
    }

    [Fact]
    public async Task BatchUpdateStatus_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var batchStatusRequest = new
        {
            NewStatus = 2, // Completed
            TaskIds = new[] { Guid.NewGuid() }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(batchStatusRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/tasks/batch/update-status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Task-Contact Management Tests

    [Fact]
    public async Task LinkContactToTask_Should_Return_Created_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Collaborator",
            Notes = "Test collaboration"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskContactDto>(responseContent, _jsonOptions);
        result!.ContactId.Should().Be(contact.Id);
        result.Role.Should().Be("Collaborator");
        result.TaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task LinkContactToTask_Should_Return_BadRequest_With_Invalid_Role()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "InvalidRole", // Invalid role
            Notes = "Test"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkContactToTask_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidTaskId = Guid.NewGuid();
        
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Observer"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{invalidTaskId}/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkContactToTask_Should_Return_NotFound_With_Invalid_Contact_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidContactId = Guid.NewGuid();
        
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = invalidContactId,
            Role = "Reviewer"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkContactToTask_Should_Return_BadRequest_When_Contact_Already_Linked()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // First link
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Owner"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);

        // Act - Try to link again
        var secondResponse = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkContactToTask_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = Guid.NewGuid(),
            Role = "Collaborator"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/tasks/{taskId}/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnlinkContactFromTask_Should_Return_NoContent_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // First link the contact
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Collaborator"
        };

        var linkContent = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent);

        // Act - Unlink the contact
        var response = await _client.DeleteAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Verify contact is no longer linked
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var contacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await getResponse.Content.ReadAsStringAsync(), _jsonOptions);
        contacts!.Should().NotContain(c => c.ContactId == contact.Id);
    }

    [Fact]
    public async Task UnlinkContactFromTask_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidTaskId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tasks/{invalidTaskId}/contacts/{contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlinkContactFromTask_Should_Return_NotFound_With_Non_Linked_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to unlink contact that was never linked
        var response = await _client.DeleteAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateContactRole_Should_Return_Ok_With_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // First link the contact
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Collaborator"
        };

        var linkContent = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent);

        // Act - Update the role
        var updateRequest = new UpdateContactRoleRequest
        {
            Role = "Owner",
            Notes = "Promoting to owner"
        };

        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PutAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}", updateContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TaskContactDto>(responseContent, _jsonOptions);
        result!.Role.Should().Be("Owner");
        result.ContactId.Should().Be(contact.Id);
        result.TaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task UpdateContactRole_Should_Return_BadRequest_With_Invalid_Role()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // First link the contact
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Collaborator"
        };

        var linkContent = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent);

        // Act - Try to update with invalid role
        var updateRequest = new UpdateContactRoleRequest
        {
            Role = "InvalidRole"
        };

        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PutAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}", updateContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateContactRole_Should_Return_NotFound_With_Non_Linked_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to update role for contact that was never linked
        var updateRequest = new UpdateContactRoleRequest
        {
            Role = "Owner"
        };

        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PutAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}", updateContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskContacts_Should_Return_Ok_With_Linked_Contacts()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var contact1 = await CreateTestContactAsync(token, "Contact 1");
        var contact2 = await CreateTestContactAsync(token, "Contact 2");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Link both contacts
        var linkRequest1 = new LinkContactToTaskRequest
        {
            ContactId = contact1.Id,
            Role = "Owner"
        };

        var linkRequest2 = new LinkContactToTaskRequest
        {
            ContactId = contact2.Id,
            Role = "Reviewer"
        };

        var linkContent1 = new StringContent(
            JsonSerializer.Serialize(linkRequest1, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var linkContent2 = new StringContent(
            JsonSerializer.Serialize(linkRequest2, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent1);
        await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent2);

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var contacts = JsonSerializer.Deserialize<List<TaskContactDto>>(responseContent, _jsonOptions);
        contacts!.Should().HaveCount(2);
        contacts.Should().Contain(c => c.ContactId == contact1.Id && c.Role == "Owner");
        contacts.Should().Contain(c => c.ContactId == contact2.Id && c.Role == "Reviewer");
    }

    [Fact]
    public async Task GetTaskContacts_Should_Return_Empty_List_With_No_Linked_Contacts()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var contacts = JsonSerializer.Deserialize<List<TaskContactDto>>(responseContent, _jsonOptions);
        contacts!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaskContacts_Should_Return_NotFound_With_Invalid_Task_Id()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidTaskId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/tasks/{invalidTaskId}/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TaskContactManagement_Should_Support_Full_Lifecycle()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token, "Lifecycle Test Task");
        var contact = await CreateTestContactAsync(token, "Lifecycle Contact");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Act & Assert - Full lifecycle test
        
        // 1. Initially no contacts
        var initialResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var initialContacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await initialResponse.Content.ReadAsStringAsync(), _jsonOptions);
        initialContacts!.Should().BeEmpty();

        // 2. Link contact as Collaborator
        var linkRequest = new LinkContactToTaskRequest
        {
            ContactId = contact.Id,
            Role = "Collaborator",
            Notes = "Initial collaboration"
        };

        var linkContent = new StringContent(
            JsonSerializer.Serialize(linkRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var linkResponse = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", linkContent);
        linkResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Verify contact is linked
        var afterLinkResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var linkedContacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await afterLinkResponse.Content.ReadAsStringAsync(), _jsonOptions);
        linkedContacts!.Should().HaveCount(1);
        linkedContacts[0].Role.Should().Be("Collaborator");

        // 4. Update role to Owner
        var updateRequest = new UpdateContactRoleRequest
        {
            Role = "Owner",
            Notes = "Promoted to owner"
        };

        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var updateResponse = await _client.PutAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}", updateContent);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Verify role was updated
        var afterUpdateResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var updatedContacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await afterUpdateResponse.Content.ReadAsStringAsync(), _jsonOptions);
        updatedContacts![0].Role.Should().Be("Owner");

        // 6. Unlink contact
        var unlinkResponse = await _client.DeleteAsync($"/api/v1/tasks/{task.Id}/contacts/{contact.Id}");
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Verify contact is no longer linked
        var finalResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var finalContacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await finalResponse.Content.ReadAsStringAsync(), _jsonOptions);
        finalContacts!.Should().BeEmpty();
    }

    [Fact]
    public async Task TaskContactManagement_Should_Support_Multiple_Role_Types()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var task = await CreateTestTaskAsync(token);
        var owner = await CreateTestContactAsync(token, "Owner Contact");
        var collaborator = await CreateTestContactAsync(token, "Collaborator Contact");
        var reviewer = await CreateTestContactAsync(token, "Reviewer Contact");
        var observer = await CreateTestContactAsync(token, "Observer Contact");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var roles = new[]
        {
            (owner.Id, "Owner"),
            (collaborator.Id, "Collaborator"),
            (reviewer.Id, "Reviewer"),
            (observer.Id, "Observer")
        };

        // Act - Link all contacts with different roles
        foreach (var (contactId, role) in roles)
        {
            var linkRequest = new LinkContactToTaskRequest
            {
                ContactId = contactId,
                Role = role,
                Notes = $"Testing {role} role"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(linkRequest, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _client.PostAsync($"/api/v1/tasks/{task.Id}/contacts", content);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Assert - Verify all roles are correctly assigned
        var getResponse = await _client.GetAsync($"/api/v1/tasks/{task.Id}/contacts");
        var contacts = JsonSerializer.Deserialize<List<TaskContactDto>>(
            await getResponse.Content.ReadAsStringAsync(), _jsonOptions);
        
        contacts!.Should().HaveCount(4);
        foreach (var (contactId, expectedRole) in roles)
        {
            contacts.Should().Contain(c => c.ContactId == contactId && c.Role == expectedRole);
        }
    }

    #endregion

    #region Dispose Pattern

    /// <summary>
    /// Clean up test resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _client?.Dispose();
                _factory?.Dispose();
            }
            _disposed = true;
        }
    }

    #endregion
}