using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.API.Tests.Helpers;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers;

/// <summary>
/// Integration tests for Contacts controller endpoints
/// </summary>
public class ContactsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed = false;

    public ContactsControllerTests(WebApplicationFactory<Program> factory)
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

                // Register missing services required for contact management
                services.AddScoped<IContactRepository, ContactRepository>();
                services.AddScoped<ContactValidator>();

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

    private async Task<ContactDto> CreateTestContactAsync(string token, string name = "John Doe", string email = "john@example.com")
    {
        var createRequest = new CreateContactRequest
        {
            Name = name,
            Email = email,
            Phone = "+1234567890",
            RelationshipType = 1 // Friend
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

    #region GET /api/v1/contacts Tests

    [Fact]
    public async Task GetContacts_Should_Return_Ok_With_Valid_Token()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactSearchResult>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Contacts.Should().NotBeNull();
    }

    [Fact]
    public async Task GetContacts_Should_Return_Unauthorized_Without_Token()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetContacts_Should_Support_Pagination()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        
        // Create multiple contacts
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestContactAsync(token, $"Contact {i}", $"contact{i}@example.com");
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/contacts?pageSize=2&pageNumber=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactSearchResult>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Contacts.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetContacts_Should_Support_Search()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        
        // Create test contacts
        await CreateTestContactAsync(token, "John Smith", "john.smith@example.com");
        await CreateTestContactAsync(token, "Jane Doe", "jane.doe@example.com");
        await CreateTestContactAsync(token, "John Johnson", "john.johnson@example.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Search by name
        var response = await _client.GetAsync("/api/v1/contacts?search=John");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactSearchResult>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Contacts.Should().HaveCount(2);
        result.Contacts.Should().OnlyContain(c => c.Name.Contains("John"));
    }

    #endregion

    #region GET /api/v1/contacts/{id} Tests

    [Fact]
    public async Task GetContact_Should_Return_Ok_For_Valid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/contacts/{contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactDto>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(contact.Id);
        result.Name.Should().Be("John Doe");
        result.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task GetContact_Should_Return_NotFound_For_Invalid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/contacts/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetContact_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var contactId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/contacts/{contactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region POST /api/v1/contacts Tests

    [Fact]
    public async Task CreateContact_Should_Return_Created_For_Valid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var createRequest = new CreateContactRequest
        {
            Name = "Alice Johnson",
            Email = "alice.johnson@example.com",
            Phone = "+1987654321",
            RelationshipType = 2 // Family
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/v1/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactDto>(responseContent, _jsonOptions);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice Johnson");
        result.Email.Should().Be("alice.johnson@example.com");
        result.Phone.Should().Be("+1987654321");
        result.RelationshipType.Should().Be(2);
    }

    [Fact]
    public async Task CreateContact_Should_Return_BadRequest_For_Invalid_Request()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var createRequest = new CreateContactRequest
        {
            Name = "", // Invalid - empty name
            Email = "invalid-email", // Invalid email format
            RelationshipType = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/v1/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateContact_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var createRequest = new CreateContactRequest
        {
            Name = "Test Contact",
            Email = "test@example.com",
            RelationshipType = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateContact_Should_Prevent_Duplicate_Emails()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        
        // Create first contact
        await CreateTestContactAsync(token, "John First", "duplicate@example.com");

        // Try to create second contact with same email
        var duplicateRequest = new CreateContactRequest
        {
            Name = "John Second",
            Email = "duplicate@example.com",
            RelationshipType = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(duplicateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/v1/contacts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("email already exists");
    }

    #endregion

    #region PUT /api/v1/contacts/{id} Tests

    [Fact]
    public async Task UpdateContact_Should_Return_Ok_For_Valid_Update()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);

        var updateRequest = new UpdateContactRequest
        {
            Name = "John Smith Updated",
            Email = "john.updated@example.com",
            Phone = "+1111111111",
            RelationshipType = 3 // Colleague
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsync($"/api/v1/contacts/{contact.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactDto>(responseContent, _jsonOptions);
        result.Should().NotBeNull();
        result!.Name.Should().Be("John Smith Updated");
        result.Email.Should().Be("john.updated@example.com");
        result.Phone.Should().Be("+1111111111");
        result.RelationshipType.Should().Be(3);
    }

    [Fact]
    public async Task UpdateContact_Should_Return_NotFound_For_Invalid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var invalidId = Guid.NewGuid();

        var updateRequest = new UpdateContactRequest
        {
            Name = "Updated Name",
            Email = "updated@example.com",
            RelationshipType = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsync($"/api/v1/contacts/{invalidId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/v1/contacts/{id} Tests

    [Fact]
    public async Task DeleteContact_Should_Return_NoContent_For_Valid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/contacts/{contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify contact is soft deleted
        var getResponse = await _client.GetAsync($"/api/v1/contacts/{contact.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteContact_Should_Return_NotFound_For_Invalid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/contacts/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/v1/contacts/deleted Tests

    [Fact]
    public async Task GetDeletedContacts_Should_Return_Ok_With_Deleted_Contacts()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        
        // Create and delete contacts
        var contact1 = await CreateTestContactAsync(token, "Deleted 1", "deleted1@example.com");
        var contact2 = await CreateTestContactAsync(token, "Deleted 2", "deleted2@example.com");
        await CreateTestContactAsync(token, "Active Contact", "active@example.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Delete contacts
        await _client.DeleteAsync($"/api/v1/contacts/{contact1.Id}");
        await _client.DeleteAsync($"/api/v1/contacts/{contact2.Id}");

        // Act
        var response = await _client.GetAsync("/api/v1/contacts/deleted");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactSearchResult>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Contacts.Should().HaveCount(2);
        result.Contacts.Should().OnlyContain(c => c.IsDeleted);
    }

    #endregion

    #region GET /api/v1/contacts/{id}/tasks Tests

    [Fact]
    public async Task GetContactTasks_Should_Return_Ok_For_Valid_Contact()
    {
        // Arrange
        var token = await AuthenticationTestHelper.GetAuthTokenAsync(_client);
        var contact = await CreateTestContactAsync(token);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/contacts/{contact.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ContactDto>(content, _jsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(contact.Id);
        result.AssociatedTasks.Should().NotBeNull();
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _factory?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}