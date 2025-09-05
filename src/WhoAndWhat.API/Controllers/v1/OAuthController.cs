using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Asp.Versioning;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// OAuth authentication controller handling provider callbacks and token generation
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/oauth")]
[Tags("OAuth Authentication")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly OAuthSettings _oauthSettings;
    private readonly ILogger<OAuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the OAuth controller
    /// </summary>
    /// <param name="oauthService">OAuth service for authentication operations</param>
    /// <param name="jwtTokenService">JWT token service for token generation</param>
    /// <param name="oauthSettings">OAuth configuration settings</param>
    /// <param name="logger">Logger for OAuth controller</param>
    public OAuthController(
        IOAuthService oauthService,
        IJwtTokenService jwtTokenService,
        IOptions<OAuthSettings> oauthSettings,
        ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _jwtTokenService = jwtTokenService;
        _oauthSettings = oauthSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initiate Google OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>Redirect to Google OAuth</returns>
    [HttpGet("google/login")]
    [ProducesResponseType(typeof(void), StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "OAuth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handle Google OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>JWT token or redirect</returns>
    [HttpGet("google/callback")]
    [ProducesResponseType(typeof(OAuthCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        return await HandleOAuthCallback(GoogleDefaults.AuthenticationScheme, OAuthProviders.Google, returnUrl);
    }

    /// <summary>
    /// Initiate Facebook OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>Redirect to Facebook OAuth</returns>
    [HttpGet("facebook/login")]
    [ProducesResponseType(typeof(void), StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult FacebookLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(FacebookCallback), "OAuth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        return Challenge(properties, FacebookDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handle Facebook OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>JWT token or redirect</returns>
    [HttpGet("facebook/callback")]
    [ProducesResponseType(typeof(OAuthCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> FacebookCallback(string? returnUrl = null)
    {
        return await HandleOAuthCallback(FacebookDefaults.AuthenticationScheme, OAuthProviders.Facebook, returnUrl);
    }

    /// <summary>
    /// Initiate Microsoft OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>Redirect to Microsoft OAuth</returns>
    [HttpGet("microsoft/login")]
    [ProducesResponseType(typeof(void), StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult MicrosoftLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(MicrosoftCallback), "OAuth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        return Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handle Microsoft OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after authentication</param>
    /// <returns>JWT token or redirect</returns>
    [HttpGet("microsoft/callback")]
    [ProducesResponseType(typeof(OAuthCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MicrosoftCallback(string? returnUrl = null)
    {
        return await HandleOAuthCallback(MicrosoftAccountDefaults.AuthenticationScheme, OAuthProviders.Microsoft, returnUrl);
    }

    /// <summary>
    /// Link OAuth account to existing authenticated user
    /// </summary>
    /// <param name="request">OAuth linking request</param>
    /// <returns>Success response</returns>
    [HttpPost("link")]
    [Authorize]
    [ProducesResponseType(typeof(OAuthLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkOAuthAccount([FromBody] OAuthLinkRequest request)
    {
        if (!OAuthProviders.IsSupported(request.Provider))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported Provider",
                Detail = $"OAuth provider '{request.Provider}' is not supported",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _oauthService.LinkOAuthAccountAsync(
                userId, 
                request.Provider, 
                request.ExternalId, 
                request.Email, 
                request.Name, 
                request.ProfileImageUrl);

            _logger.LogInformation("OAuth account linked successfully. UserId: {UserId}, Provider: {Provider}", 
                userId, request.Provider);

            return Ok(new OAuthLinkResponse
            {
                Success = true,
                Message = $"{request.Provider} account linked successfully",
                Provider = request.Provider
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("OAuth linking failed: {Message}", ex.Message);
            
            return Conflict(new ProblemDetails
            {
                Title = "OAuth Linking Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    /// <summary>
    /// Unlink OAuth account from authenticated user
    /// </summary>
    /// <param name="provider">OAuth provider name</param>
    /// <returns>Success response</returns>
    [HttpDelete("unlink/{provider}")]
    [Authorize]
    [ProducesResponseType(typeof(OAuthUnlinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnlinkOAuthAccount(string provider)
    {
        if (!OAuthProviders.IsSupported(provider))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported Provider",
                Detail = $"OAuth provider '{provider}' is not supported",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        await _oauthService.UnlinkOAuthAccountAsync(userId, provider);

        _logger.LogInformation("OAuth account unlinked successfully. UserId: {UserId}, Provider: {Provider}", 
            userId, provider);

        return Ok(new OAuthUnlinkResponse
        {
            Success = true,
            Message = $"{provider} account unlinked successfully",
            Provider = provider
        });
    }

    /// <summary>
    /// Get user's linked OAuth accounts
    /// </summary>
    /// <returns>List of linked OAuth accounts</returns>
    [HttpGet("accounts")]
    [Authorize]
    [ProducesResponseType(typeof(UserOAuthAccountsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserOAuthAccounts()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var accounts = await _oauthService.GetUserOAuthAccountsAsync(userId);

        return Ok(new UserOAuthAccountsResponse
        {
            Accounts = accounts.Select(a => new OAuthAccountInfo
            {
                Provider = a.Provider,
                Email = a.Email,
                Name = a.Name,
                ProfileImageUrl = a.ProfileImageUrl,
                LastLoginAt = a.LastLoginAt,
                IsActive = a.IsActive
            }).ToList()
        });
    }

    private async Task<IActionResult> HandleOAuthCallback(string authenticationScheme, string provider, string? returnUrl)
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(authenticationScheme);
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("OAuth authentication failed for provider: {Provider}", provider);
                return Unauthorized(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Authentication with the OAuth provider failed",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var claims = result.Principal?.Claims;
            var externalId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var profileImageUrl = claims?.FirstOrDefault(c => c.Type == "picture")?.Value;

            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("OAuth callback missing external ID for provider: {Provider}", provider);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid OAuth Response",
                    Detail = "OAuth provider did not return a valid user identifier",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var user = await _oauthService.AuthenticateWithOAuthAsync(provider, externalId, email, name, profileImageUrl);
            
            if (user == null)
            {
                _logger.LogWarning("OAuth user creation/authentication failed for provider: {Provider}, ExternalId: {ExternalId}", 
                    provider, externalId);
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Unable to authenticate or create user account",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var tokenResult = await _jwtTokenService.GenerateTokensAsync(user);

            _logger.LogInformation("OAuth authentication successful. UserId: {UserId}, Provider: {Provider}", 
                user.Id, provider);

            var response = new OAuthCallbackResponse
            {
                Success = true,
                User = new OAuthUserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    IsEmailVerified = user.IsEmailVerified,
                    Language = user.PreferredLanguage.ToString()
                },
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                Provider = provider
            };

            if (!string.IsNullOrEmpty(returnUrl))
            {
                // For web applications, redirect with tokens as query parameters
                var redirectUri = $"{returnUrl}?accessToken={tokenResult.AccessToken}&refreshToken={tokenResult.RefreshToken}";
                return Redirect(redirectUri);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OAuth callback for provider: {Provider}", provider);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "OAuth Authentication Error",
                Detail = "An unexpected error occurred during OAuth authentication",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}

// Response DTOs
/// <summary>
/// Response model for OAuth callback operations
/// </summary>
public class OAuthCallbackResponse
{
    /// <summary>
    /// Indicates if the OAuth callback was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// User information from OAuth authentication
    /// </summary>
    public OAuthUserInfo User { get; set; } = null!;
    
    /// <summary>
    /// JWT access token for authenticated user
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// JWT refresh token for token renewal
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
    
    /// <summary>
    /// OAuth provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// User information from OAuth authentication
/// </summary>
public class OAuthUserInfo
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// User's username
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if the user's email is verified
    /// </summary>
    public bool IsEmailVerified { get; set; }
    
    /// <summary>
    /// User's preferred language
    /// </summary>
    public string Language { get; set; } = string.Empty;
}

/// <summary>
/// Request model for linking OAuth account to existing user
/// </summary>
public class OAuthLinkRequest
{
    /// <summary>
    /// OAuth provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// External user identifier from OAuth provider
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;
    
    /// <summary>
    /// User's email address from OAuth provider
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// User's name from OAuth provider
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// User's profile image URL from OAuth provider
    /// </summary>
    public string? ProfileImageUrl { get; set; }
}

/// <summary>
/// Response model for OAuth account linking operations
/// </summary>
public class OAuthLinkResponse
{
    /// <summary>
    /// Indicates if the linking operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Success or error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// OAuth provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Response model for OAuth account unlinking operations
/// </summary>
public class OAuthUnlinkResponse
{
    /// <summary>
    /// Indicates if the unlinking operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Success or error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// OAuth provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Response model containing user's linked OAuth accounts
/// </summary>
public class UserOAuthAccountsResponse
{
    /// <summary>
    /// List of user's linked OAuth accounts
    /// </summary>
    public List<OAuthAccountInfo> Accounts { get; set; } = new();
}

/// <summary>
/// Information about a user's linked OAuth account
/// </summary>
public class OAuthAccountInfo
{
    /// <summary>
    /// OAuth provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// User's email address from OAuth provider
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// User's name from OAuth provider
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// User's profile image URL from OAuth provider
    /// </summary>
    public string? ProfileImageUrl { get; set; }
    
    /// <summary>
    /// Last login timestamp for this OAuth account
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// Indicates if this OAuth account is active
    /// </summary>
    public bool IsActive { get; set; }
}