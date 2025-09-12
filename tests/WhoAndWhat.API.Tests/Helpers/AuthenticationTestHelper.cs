using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.API.Tests.Helpers;

/// <summary>
/// Helper class for authentication-related test operations
/// </summary>
public static class AuthenticationTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a test user and returns an authentication token
    /// </summary>
    public static async Task<string> GetAuthTokenAsync(HttpClient client)
    {
        // Generate unique credentials for test isolation
        var uniqueEmail = $"test_{Guid.NewGuid():N}@example.com";
        var uniqueUsername = $"user_{Guid.NewGuid():N}";

        // Register the test user
        var registerRequest = new RegisterRequest
        {
            Email = uniqueEmail,
            Username = uniqueUsername,
            Password = "TestPass123!",
            AcceptTerms = true
        };

        var registerResponse = await RegisterUserAsync(client, registerRequest);

        if (!IsSuccessStatusCode(registerResponse.StatusCode))
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to register test user: {registerResponse.StatusCode} - {errorContent}");
        }

        // Login to get token
        var loginRequest = new LoginRequest
        {
            Email = uniqueEmail,
            Password = "TestPass123!"
        };

        var loginResponse = await LoginUserAsync(client, loginRequest);

        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await loginResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to login test user: {loginResponse.StatusCode} - {errorContent}");
        }

        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<LoginResponse>(loginContent, JsonOptions);

        if (string.IsNullOrEmpty(loginResult?.AccessToken))
        {
            throw new Exception("Login successful but no access token received");
        }

        return loginResult.AccessToken;
    }

    /// <summary>
    /// Creates a test user with specific credentials and returns an authentication token
    /// </summary>
    public static async Task<string> GetAuthTokenAsync(HttpClient client, string email, string username, string password)
    {
        // Register the test user
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Username = username,
            Password = password,
            AcceptTerms = true
        };

        var registerResponse = await RegisterUserAsync(client, registerRequest);

        // If user already exists, just try to login
        if (registerResponse.StatusCode == HttpStatusCode.Conflict)
        {
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var loginResponse = await LoginUserAsync(client, loginRequest);

            if (loginResponse.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await loginResponse.Content.ReadAsStringAsync();
                throw new Exception($"Failed to login existing user: {loginResponse.StatusCode} - {errorContent}");
            }

            var loginContent = await loginResponse.Content.ReadAsStringAsync();
            var loginResult = JsonSerializer.Deserialize<LoginResponse>(loginContent, JsonOptions);

            return loginResult!.AccessToken;
        }

        if (!IsSuccessStatusCode(registerResponse.StatusCode))
        {
            var errorContent = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to register test user: {registerResponse.StatusCode} - {errorContent}");
        }

        // Login after successful registration
        var newLoginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var newLoginResponse = await LoginUserAsync(client, newLoginRequest);
        var newLoginContent = await newLoginResponse.Content.ReadAsStringAsync();
        var newLoginResult = JsonSerializer.Deserialize<LoginResponse>(newLoginContent, JsonOptions);

        return newLoginResult!.AccessToken;
    }

    /// <summary>
    /// Sets the authorization header on the HttpClient
    /// </summary>
    public static void SetAuthorizationHeader(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clears the authorization header from the HttpClient
    /// </summary>
    public static void ClearAuthorizationHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Creates and authenticates a test user, then sets the authorization header
    /// </summary>
    public static async Task<string> AuthenticateClientAsync(HttpClient client)
    {
        var token = await GetAuthTokenAsync(client);
        SetAuthorizationHeader(client, token);
        return token;
    }

    private static async Task<HttpResponseMessage> RegisterUserAsync(HttpClient client, RegisterRequest request)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        return await client.PostAsync("/api/v1/auth/register", content);
    }

    private static async Task<HttpResponseMessage> LoginUserAsync(HttpClient client, LoginRequest request)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        return await client.PostAsync("/api/v1/auth/login", content);
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.OK ||
               statusCode == HttpStatusCode.Created ||
               statusCode == HttpStatusCode.NoContent;
    }
}
