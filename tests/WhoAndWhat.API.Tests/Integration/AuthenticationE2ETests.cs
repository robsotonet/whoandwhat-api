using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace WhoAndWhat.API.Tests.Integration;

/// <summary>
/// End-to-End integration tests for complete authentication workflows
/// Tests complete user journeys from registration to logout
/// </summary>
public class AuthenticationE2ETests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private ApplicationDbContext _dbContext = null!;

    public AuthenticationE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
        
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async SystemTask InitializeAsync()
    {
        // Initialize database context for test setup and verification
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async SystemTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
        _client.Dispose();
    }

    #region Complete User Registration and Login Flow

    [Fact]
    public async SystemTask CompleteUserJourney_Register_Verify_Login_Should_Work_End_To_End()
    {
        // Step 1: Register a new user
        var registerRequest = new RegisterRequest
        {
            Email = "journey@example.com",
            Username = "journeyuser",
            Password = "JourneyPassword123!",
            ConfirmPassword = "JourneyPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var registerResponse = await PostJsonAsync("/api/v1/auth/register", registerRequest);
        
        // Temporary debugging: capture error details if registration fails
        if (registerResponse.StatusCode != HttpStatusCode.Created)
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Registration failed with status {registerResponse.StatusCode}. Error: {errorContent}");
        }
        
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerResult = await DeserializeAsync<RegisterResponse>(registerResponse);
        registerResult.Should().NotBeNull();
        registerResult!.Email.Should().Be(registerRequest.Email);
        registerResult.Username.Should().Be(registerRequest.Username);

        // Step 2: Verify the user was created in database (but not verified yet)
        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == registerRequest.Email);
        userInDb.Should().NotBeNull();
        userInDb!.IsEmailVerified.Should().BeFalse();

        // Step 3: Simulate account verification (in a real scenario, this would be done via email link)
        // For E2E testing, we'll manually verify the account
        userInDb.VerifyEmail();
        await _dbContext.SaveChangesAsync();

        // Step 4: Login with the verified account
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var loginResponse = await PostJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await DeserializeAsync<LoginResponse>(loginResponse);
        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();
        loginResult.Email.Should().Be(registerRequest.Email);

        // Step 5: Use the access token to make an authenticated request
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

        // This would be a protected endpoint - for now we'll test the token is valid format
        loginResult.AccessToken.Split('.').Should().HaveCount(3); // JWT has 3 parts
    }

    [Fact]
    public async SystemTask RegisteredUser_Should_Not_Login_Without_Email_Verification()
    {
        // Step 1: Register a user
        var registerRequest = new RegisterRequest
        {
            Email = "unverified@example.com",
            Username = "unverifieduser",
            Password = "UnverifiedPassword123!",
            ConfirmPassword = "UnverifiedPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var registerResponse = await PostJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Try to login without verification
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var loginResponse = await PostJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await DeserializeAsync<ValidationErrorResponse>(loginResponse);
        error.Should().NotBeNull();
        error!.Message.Should().Contain("verified");
    }

    #endregion

    #region Token Refresh and Session Management Flow

    [Fact]
    public async SystemTask TokenRefresh_Flow_Should_Work_End_To_End()
    {
        // Step 1: Create and verify a user
        var user = await CreateAndVerifyUserAsync("refresh@example.com", "refreshuser", "RefreshPassword123!");

        // Step 2: Login to get tokens
        var loginRequest = new LoginRequest
        {
            Email = "refresh@example.com",
            Password = "RefreshPassword123!"
        };

        var loginResponse = await PostJsonAsync("/api/v1/auth/login", loginRequest);
        var loginResult = await DeserializeAsync<LoginResponse>(loginResponse);

        var originalAccessToken = loginResult!.AccessToken;
        var refreshToken = loginResult.RefreshToken;

        // Step 3: Use refresh token to get new access token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        var refreshResponse = await PostJsonAsync("/api/v1/auth/refresh", refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResult = await DeserializeAsync<RefreshTokenResponse>(refreshResponse);
        refreshResult.Should().NotBeNull();
        refreshResult!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.AccessToken.Should().NotBe(originalAccessToken); // Should be a new token
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();

        // Step 4: Verify old refresh token is no longer valid
        var oldRefreshResponse = await PostJsonAsync("/api/v1/auth/refresh", refreshRequest);
        oldRefreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async SystemTask InvalidRefreshToken_Should_Fail_Gracefully()
    {
        // Arrange
        var invalidRefreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await PostJsonAsync("/api/v1/auth/refresh", invalidRefreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeAsync<ValidationErrorResponse>(response);
        error.Should().NotBeNull();
    }

    #endregion

    #region Complete Logout Flow

    [Fact]
    public async SystemTask CompleteLogout_Flow_Should_Invalidate_Tokens()
    {
        // Step 1: Create verified user and login
        var user = await CreateAndVerifyUserAsync("logout@example.com", "logoutuser", "LogoutPassword123!");
        var authTokens = await LoginUserAsync("logout@example.com", "LogoutPassword123!");

        // Step 2: Logout
        var logoutRequest = new LogoutRequest
        {
            RefreshToken = authTokens.RefreshToken
        };

        var logoutResponse = await PostJsonAsync("/api/v1/auth/logout", logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Verify refresh token is no longer valid
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = authTokens.RefreshToken
        };

        var refreshResponse = await PostJsonAsync("/api/v1/auth/refresh", refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Authentication State Validation

    [Fact]
    public async SystemTask Authentication_State_Should_Persist_Across_Multiple_Requests()
    {
        // Step 1: Create verified user and login
        var user = await CreateAndVerifyUserAsync("persistent@example.com", "persistentuser", "PersistentPassword123!");
        var authTokens = await LoginUserAsync("persistent@example.com", "PersistentPassword123!");

        // Step 2: Set authorization header
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", authTokens.AccessToken);

        // Step 3: Make multiple requests that would require authentication
        // Note: Since we don't have protected endpoints yet, we'll validate token format and structure
        for (int i = 0; i < 3; i++)
        {
            // Verify token structure remains valid
            authTokens.AccessToken.Split('.').Should().HaveCount(3);
            
            // In a real scenario, we would call protected endpoints here
            // For now, we verify the token is consistently formatted
            authTokens.AccessToken.Should().NotBeNullOrEmpty();
        }

        // Step 4: Test token refresh maintains authentication state
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = authTokens.RefreshToken
        };

        var refreshResponse = await PostJsonAsync("/api/v1/auth/refresh", refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newTokens = await DeserializeAsync<RefreshTokenResponse>(refreshResponse);
        newTokens!.AccessToken.Should().NotBeNullOrEmpty();
        newTokens.AccessToken.Should().NotBe(authTokens.AccessToken);
    }

    [Fact]
    public async SystemTask Concurrent_Authentication_Requests_Should_Handle_Correctly()
    {
        // Step 1: Create verified user
        var user = await CreateAndVerifyUserAsync("concurrent@example.com", "concurrentuser", "ConcurrentPassword123!");
        
        // Step 2: Make multiple concurrent login requests
        var loginRequest = new LoginRequest
        {
            Email = "concurrent@example.com",
            Password = "ConcurrentPassword123!"
        };

        var loginTasks = Enumerable.Range(0, 5)
            .Select(_ => PostJsonAsync("/api/v1/auth/login", loginRequest))
            .ToList();

        var responses = await SystemTask.WhenAll(loginTasks);

        // Step 3: All should succeed (or handle gracefully)
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
            
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await DeserializeAsync<LoginResponse>(response);
                result!.AccessToken.Should().NotBeNullOrEmpty();
                result.RefreshToken.Should().NotBeNullOrEmpty();
            }
        }
    }

    #endregion

    #region Error Handling and Edge Cases

    [Fact]
    public async SystemTask Authentication_Should_Handle_Malformed_Requests_Gracefully()
    {
        // Test various malformed requests
        var testCases = new[]
        {
            // Empty login request
            (endpoint: "/api/v1/auth/login", payload: "{}"),
            // Invalid JSON
            (endpoint: "/api/v1/auth/login", payload: "{invalid json}"),
            // Missing required fields
            (endpoint: "/api/v1/auth/register", payload: @"{""email"":""test@test.com""}")
        };

        foreach (var testCase in testCases)
        {
            var content = new StringContent(testCase.payload, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(testCase.endpoint, content);
            
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest, 
                HttpStatusCode.UnprocessableEntity,
                HttpStatusCode.UnsupportedMediaType);
        }
    }

    [Fact]
    public async SystemTask Authentication_Should_Prevent_Duplicate_User_Registration()
    {
        // Step 1: Register first user
        var registerRequest = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Username = "duplicateuser",
            Password = "DuplicatePassword123!",
            ConfirmPassword = "DuplicatePassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var firstResponse = await PostJsonAsync("/api/v1/auth/register", registerRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Try to register same email again
        var duplicateResponse = await PostJsonAsync("/api/v1/auth/register", registerRequest);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 3: Try to register same username with different email
        var duplicateUsernameRequest = new RegisterRequest
        {
            Email = "different@example.com",
            Username = "duplicateuser", // Same username
            Password = "DuplicatePassword123!",
            ConfirmPassword = "DuplicatePassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var duplicateUsernameResponse = await PostJsonAsync("/api/v1/auth/register", duplicateUsernameRequest);
        duplicateUsernameResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private async System.Threading.Tasks.Task<User> CreateAndVerifyUserAsync(string email, string username, string password)
    {
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Username = username,
            Password = password,
            ConfirmPassword = password,
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var response = await PostJsonAsync("/api/v1/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        // Manually verify the user for testing purposes
        var user = await _dbContext.Users.FirstAsync(u => u.Email == email);
        user.VerifyEmail();
        await _dbContext.SaveChangesAsync();

        return user;
    }

    private async System.Threading.Tasks.Task<LoginResponse> LoginUserAsync(string email, string password)
    {
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var response = await PostJsonAsync("/api/v1/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        return await DeserializeAsync<LoginResponse>(response) ?? 
            throw new InvalidOperationException("Failed to deserialize login response");
    }

    private async System.Threading.Tasks.Task<HttpResponseMessage> PostJsonAsync<T>(string endpoint, T data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(endpoint, content);
    }

    private async System.Threading.Tasks.Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    #endregion
}

/// <summary>
/// DTO for validation error responses
/// </summary>
public class ValidationErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
}