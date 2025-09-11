using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Infrastructure.Data;
using Xunit;

namespace WhoAndWhat.API.Tests.Controllers;

/// <summary>
/// Integration tests for Authentication controller endpoints
/// </summary>
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // No service configuration needed - environment-conditional logic in Program.cs handles database setup
        });

        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region User Registration Tests

    [Fact]
    public async Task Register_Should_Return_Created_With_Valid_Request()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var responseContent = await response.Content.ReadAsStringAsync();
        var registerResponse = JsonSerializer.Deserialize<RegisterResponse>(responseContent, _jsonOptions);

        registerResponse.Should().NotBeNull();
        registerResponse!.UserId.Should().NotBeEmpty();
        registerResponse.Email.Should().Be(request.Email);
        registerResponse.Username.Should().Be(request.Username);
        registerResponse.RequiresEmailVerification.Should().BeTrue();
        registerResponse.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_Should_Return_BadRequest_With_Invalid_Email()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Email");
    }

    [Fact]
    public async Task Register_Should_Return_BadRequest_With_Weak_Password()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "weak",
            ConfirmPassword = "weak",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_BadRequest_With_Password_Mismatch()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "DifferentPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_BadRequest_With_Terms_Not_Accepted()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = false
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_Conflict_With_Duplicate_Email()
    {
        // Arrange - First registration
        var firstRequest = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Username = "firstuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json1 = JsonSerializer.Serialize(firstRequest, _jsonOptions);
        var content1 = new StringContent(json1, Encoding.UTF8, "application/json");

        await _client.PostAsync("/api/v1/auth/register", content1);

        // Arrange - Duplicate email registration
        var duplicateRequest = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Username = "seconduser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json2 = JsonSerializer.Serialize(duplicateRequest, _jsonOptions);
        var content2 = new StringContent(json2, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_Should_Return_Conflict_With_Duplicate_Username()
    {
        // Arrange - First registration
        var firstRequest = new RegisterRequest
        {
            Email = "first@example.com",
            Username = "duplicateuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json1 = JsonSerializer.Serialize(firstRequest, _jsonOptions);
        var content1 = new StringContent(json1, Encoding.UTF8, "application/json");

        await _client.PostAsync("/api/v1/auth/register", content1);

        // Arrange - Duplicate username registration
        var duplicateRequest = new RegisterRequest
        {
            Email = "second@example.com",
            Username = "duplicateuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json2 = JsonSerializer.Serialize(duplicateRequest, _jsonOptions);
        var content2 = new StringContent(json2, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/register", content2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region User Login Tests

    [Fact]
    public async Task Login_Should_Return_Ok_With_Valid_Credentials()
    {
        // Arrange - Register a user first
        await RegisterTestUser("login@example.com", "loginuser", "TestPassword123!");

        var loginRequest = new LoginRequest
        {
            Email = "login@example.com",
            Password = "TestPassword123!",
            RememberMe = false
        };

        var json = JsonSerializer.Serialize(loginRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var responseContent = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, _jsonOptions);

        loginResponse.Should().NotBeNull();
        loginResponse!.UserId.Should().NotBeEmpty();
        loginResponse.Email.Should().Be(loginRequest.Email);
        loginResponse.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.RefreshToken.Should().NotBeNullOrEmpty();
        loginResponse.TokenType.Should().Be("Bearer");
        loginResponse.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_With_Invalid_Email()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "TestPassword123!"
        };

        var json = JsonSerializer.Serialize(loginRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_With_Invalid_Password()
    {
        // Arrange - Register a user first
        await RegisterTestUser("wrongpwd@example.com", "wrongpwduser", "TestPassword123!");

        var loginRequest = new LoginRequest
        {
            Email = "wrongpwd@example.com",
            Password = "WrongPassword123!"
        };

        var json = JsonSerializer.Serialize(loginRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_BadRequest_With_Invalid_Email_Format()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "invalid-email",
            Password = "TestPassword123!"
        };

        var json = JsonSerializer.Serialize(loginRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Token Refresh Tests

    [Fact]
    public async Task RefreshToken_Should_Return_Ok_With_Valid_Refresh_Token()
    {
        // Arrange - Register and login to get tokens
        await RegisterTestUser("refresh@example.com", "refreshuser", "TestPassword123!");
        var loginResponse = await LoginTestUser("refresh@example.com", "TestPassword123!");

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        };

        var json = JsonSerializer.Serialize(refreshRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var responseContent = await response.Content.ReadAsStringAsync();
        var refreshResponse = JsonSerializer.Deserialize<RefreshTokenResponse>(responseContent, _jsonOptions);

        refreshResponse.Should().NotBeNull();
        refreshResponse!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResponse.RefreshToken.Should().NotBeNullOrEmpty();
        refreshResponse.TokenType.Should().Be("Bearer");
        refreshResponse.ExpiresIn.Should().BeGreaterThan(0);

        // New tokens should be different from original
        refreshResponse.AccessToken.Should().NotBe(loginResponse.AccessToken);
        refreshResponse.RefreshToken.Should().NotBe(loginResponse.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_Return_BadRequest_With_Invalid_Token()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        var json = JsonSerializer.Serialize(refreshRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_Should_Return_BadRequest_With_Revoked_Token()
    {
        // Arrange - Register, login, then logout to revoke tokens
        await RegisterTestUser("revoked@example.com", "revokeduser", "TestPassword123!");
        var loginResponse = await LoginTestUser("revoked@example.com", "TestPassword123!");

        // Logout to revoke all tokens
        await LogoutTestUser(loginResponse.AccessToken);

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        };

        var json = JsonSerializer.Serialize(refreshRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region User Logout Tests

    [Fact]
    public async Task Logout_Should_Return_Ok_With_Valid_Token()
    {
        // Arrange - Register and login to get access token
        await RegisterTestUser("logout@example.com", "logoutuser", "TestPassword123!");
        var loginResponse = await LoginTestUser("logout@example.com", "TestPassword123!");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        // Act
        var response = await _client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var responseContent = await response.Content.ReadAsStringAsync();
        var logoutResponse = JsonSerializer.Deserialize<LogoutResponse>(responseContent, _jsonOptions);

        logoutResponse.Should().NotBeNull();
        logoutResponse!.Message.Should().NotBeNullOrEmpty();
        logoutResponse.TokensRevoked.Should().BeGreaterThan(0);
        logoutResponse.LogoutAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Logout_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange - Clear any authorization headers
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Return_Unauthorized_With_Invalid_Token()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Invalidate_Refresh_Token()
    {
        // Arrange - Register and login to get tokens
        await RegisterTestUser("invalidate@example.com", "invalidateuser", "TestPassword123!");
        var loginResponse = await LoginTestUser("invalidate@example.com", "TestPassword123!");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        // Act - Logout
        await _client.PostAsync("/api/v1/auth/logout", null);

        // Try to use refresh token after logout
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        };

        var json = JsonSerializer.Serialize(refreshRequest, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var refreshResponse = await _client.PostAsync("/api/v1/auth/refresh", content);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Current User Tests

    [Fact]
    public async Task GetCurrentUser_Should_Return_Ok_With_Valid_Token()
    {
        // Arrange - Register and login to get access token
        await RegisterTestUser("currentuser@example.com", "currentuser", "TestPassword123!");
        var loginResponse = await LoginTestUser("currentuser@example.com", "TestPassword123!");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var responseContent = await response.Content.ReadAsStringAsync();
        var currentUserResponse = JsonSerializer.Deserialize<CurrentUserResponse>(responseContent, _jsonOptions);

        currentUserResponse.Should().NotBeNull();
        currentUserResponse!.UserId.Should().NotBeEmpty();
        currentUserResponse.Email.Should().Be("currentuser@example.com");
        currentUserResponse.Username.Should().Be("currentuser");
    }

    [Fact]
    public async Task GetCurrentUser_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange - Clear any authorization headers
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_Should_Return_Unauthorized_With_Invalid_Token()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task Auth_Endpoints_Should_Have_Security_Headers()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.Should().ContainKey("X-XSS-Protection");
        response.Headers.Should().ContainKey("Referrer-Policy");
    }

    [Fact]
    public async Task Auth_Endpoints_Should_Not_Expose_Server_Information()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.Headers.Should().NotContainKey("Server");
        response.Headers.Should().NotContainKey("X-Powered-By");
        response.Headers.Should().NotContainKey("X-AspNet-Version");
        response.Headers.Should().NotContainKey("X-AspNetMvc-Version");
    }

    [Fact]
    public async Task Auth_Endpoints_Should_Handle_Malformed_JSON()
    {
        // Arrange
        var malformedJson = "{ invalid json }";
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private async Task<RegisterResponse> RegisterTestUser(string email, string username, string password)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Username = username,
            Password = password,
            ConfirmPassword = password,
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/register", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RegisterResponse>(responseContent, _jsonOptions)!;
    }

    private async Task<LoginResponse> LoginTestUser(string email, string password)
    {
        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/login", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResponse>(responseContent, _jsonOptions)!;
    }

    private async Task LogoutTestUser(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.PostAsync("/api/v1/auth/logout", null);
        response.EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion
}
