using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Controller for OAuth authentication flows (Google, Facebook)
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/oauth")]
[Tags("OAuth Authentication")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oAuthService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        IOAuthService oAuthService,
        IJwtTokenService jwtTokenService,
        ILogger<OAuthController> logger)
    {
        _oAuthService = oAuthService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Initiate Google OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <returns>Redirect to Google OAuth</returns>
    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        try
        {
            var redirectUrl = Url.Action(nameof(GoogleCallback), "OAuth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            _logger.LogInformation("Initiating Google OAuth authentication");
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Google OAuth authentication");
            return BadRequest(new ProblemDetails
            {
                Title = "OAuth Error",
                Detail = "Failed to initiate Google authentication",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Handle Google OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with JWT tokens</returns>
    [HttpGet("google/callback")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Google OAuth authentication failed");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Google authentication was not successful",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var principal = authenticateResult.Principal!;
            var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value;
            var profileImage = principal.FindFirst("picture")?.Value;

            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Google OAuth callback missing external ID");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Data Missing",
                    Detail = "Required user information not provided by Google",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var user = await _oAuthService.AuthenticateWithOAuthAsync(
                OAuthProviders.Google,
                externalId,
                email,
                name,
                profileImage,
                cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Failed to authenticate user via Google OAuth");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Unable to authenticate user with Google account",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Generate JWT tokens
            var tokenResult = await _jwtTokenService.GenerateTokensAsync(user);

            // Record OAuth login
            await _oAuthService.RecordOAuthLoginAsync(OAuthProviders.Google, externalId, cancellationToken);

            _logger.LogInformation("Google OAuth authentication successful for user: {UserId}", user.Id);

            var response = new LoginResponse
            {
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresIn = tokenResult.ExpiresIn,
                TokenType = tokenResult.TokenType,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    IsEmailVerified = user.IsEmailVerified,
                    PreferredLanguage = user.PreferredLanguage.ToString()
                }
            };

            // If returnUrl is provided, redirect to it with tokens
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var redirectUrlWithTokens = $"{returnUrl}?access_token={tokenResult.AccessToken}&refresh_token={tokenResult.RefreshToken}";
                return Redirect(redirectUrlWithTokens);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google OAuth callback");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "OAuth Callback Error",
                Detail = "An error occurred processing the OAuth callback",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Initiate Facebook OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <returns>Redirect to Facebook OAuth</returns>
    [HttpGet("facebook")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult FacebookLogin([FromQuery] string? returnUrl = null)
    {
        try
        {
            var redirectUrl = Url.Action(nameof(FacebookCallback), "OAuth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            _logger.LogInformation("Initiating Facebook OAuth authentication");
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Facebook OAuth authentication");
            return BadRequest(new ProblemDetails
            {
                Title = "OAuth Error",
                Detail = "Failed to initiate Facebook authentication",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Handle Facebook OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with JWT tokens</returns>
    [HttpGet("facebook/callback")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FacebookCallback([FromQuery] string? returnUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(FacebookDefaults.AuthenticationScheme);
            
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Facebook OAuth authentication failed");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Facebook authentication was not successful",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var principal = authenticateResult.Principal!;
            var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value;
            var profileImage = principal.FindFirst("picture")?.Value;

            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Facebook OAuth callback missing external ID");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Data Missing",
                    Detail = "Required user information not provided by Facebook",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var user = await _oAuthService.AuthenticateWithOAuthAsync(
                OAuthProviders.Facebook,
                externalId,
                email,
                name,
                profileImage,
                cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Failed to authenticate user via Facebook OAuth");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Unable to authenticate user with Facebook account",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Generate JWT tokens
            var tokenResult = await _jwtTokenService.GenerateTokensAsync(user);

            // Record OAuth login
            await _oAuthService.RecordOAuthLoginAsync(OAuthProviders.Facebook, externalId, cancellationToken);

            _logger.LogInformation("Facebook OAuth authentication successful for user: {UserId}", user.Id);

            var response = new LoginResponse
            {
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresIn = tokenResult.ExpiresIn,
                TokenType = tokenResult.TokenType,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    IsEmailVerified = user.IsEmailVerified,
                    PreferredLanguage = user.PreferredLanguage.ToString()
                }
            };

            // If returnUrl is provided, redirect to it with tokens
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var redirectUrlWithTokens = $"{returnUrl}?access_token={tokenResult.AccessToken}&refresh_token={tokenResult.RefreshToken}";
                return Redirect(redirectUrlWithTokens);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Facebook OAuth callback");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "OAuth Callback Error",
                Detail = "An error occurred processing the OAuth callback",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Initiate Apple OAuth authentication
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <returns>Redirect to Apple OAuth</returns>
    [HttpGet("apple")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult AppleLogin([FromQuery] string? returnUrl = null)
    {
        try
        {
            var redirectUrl = Url.Action(nameof(AppleCallback), "OAuth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            _logger.LogInformation("Initiating Apple OAuth authentication");
            return Challenge(properties, AppleAuthenticationDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Apple OAuth authentication");
            return BadRequest(new ProblemDetails
            {
                Title = "OAuth Error",
                Detail = "Failed to initiate Apple authentication",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Handle Apple OAuth callback
    /// </summary>
    /// <param name="returnUrl">URL to redirect after authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with JWT tokens</returns>
    [HttpGet("apple/callback")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AppleCallback([FromQuery] string? returnUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(AppleAuthenticationDefaults.AuthenticationScheme);
            
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Apple OAuth authentication failed");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Apple authentication was not successful",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var principal = authenticateResult.Principal!;
            var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value;
            var profileImage = principal.FindFirst("picture")?.Value;

            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Apple OAuth callback missing external ID");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Data Missing",
                    Detail = "Required user information not provided by Apple",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var user = await _oAuthService.AuthenticateWithOAuthAsync(
                OAuthProviders.Apple,
                externalId,
                email,
                name,
                profileImage,
                cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Failed to authenticate user via Apple OAuth");
                return BadRequest(new ProblemDetails
                {
                    Title = "OAuth Authentication Failed",
                    Detail = "Unable to authenticate user with Apple account",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Generate JWT tokens
            var tokenResult = await _jwtTokenService.GenerateTokensAsync(user);

            // Record OAuth login
            await _oAuthService.RecordOAuthLoginAsync(OAuthProviders.Apple, externalId, cancellationToken);

            _logger.LogInformation("Apple OAuth authentication successful for user: {UserId}", user.Id);

            var response = new LoginResponse
            {
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresIn = tokenResult.ExpiresIn,
                TokenType = tokenResult.TokenType,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    IsEmailVerified = user.IsEmailVerified,
                    PreferredLanguage = user.PreferredLanguage.ToString()
                }
            };

            // If returnUrl is provided, redirect to it with tokens
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var redirectUrlWithTokens = $"{returnUrl}?access_token={tokenResult.AccessToken}&refresh_token={tokenResult.RefreshToken}";
                return Redirect(redirectUrlWithTokens);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Apple OAuth callback");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "OAuth Callback Error",
                Detail = "An error occurred processing the OAuth callback",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}