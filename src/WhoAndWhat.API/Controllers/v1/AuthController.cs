using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;
using Asp.Versioning;
using WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;
using WhoAndWhat.Application.Features.Auth.Commands.LoginUser;
using WhoAndWhat.Application.Features.Auth.Commands.RefreshToken;
using WhoAndWhat.Application.Features.Auth.Commands.LogoutUser;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Authentication controller handling core auth operations like register, login, refresh and logout
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Tags("Core Authentication")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the Auth controller
    /// </summary>
    /// <param name="mediator">MediatR mediator for command handling</param>
    /// <param name="logger">Logger for Auth controller</param>
    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration response with user details and tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Username,
                request.Password,
                request.PreferredLanguage,
                request.AcceptTerms
            );

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("User registration failed for email: {Email}. Error: {Error}", 
                    request.Email, result.Error);

                return result.Error.Contains("already exists") || result.Error.Contains("already taken")
                    ? Conflict(new ProblemDetails
                    {
                        Title = "Registration Failed",
                        Detail = result.Error,
                        Status = StatusCodes.Status409Conflict
                    })
                    : BadRequest(new ProblemDetails
                    {
                        Title = "Registration Failed",
                        Detail = result.Error,
                        Status = StatusCodes.Status400BadRequest
                    });
            }

            _logger.LogInformation("User registered successfully: {Email}", request.Email);

            return CreatedAtAction(
                actionName: nameof(Register),
                value: result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during user registration for email: {Email}", request.Email);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Registration Error",
                Detail = "An unexpected error occurred during registration. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Authenticate user and generate access tokens
    /// </summary>
    /// <param name="request">User login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with user details and tokens</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new LoginUserCommand(request.Email, request.Password, false);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Login failed for email: {Email}. Error: {Error}", 
                    request.Email, result.Error);

                if (result.Error.Contains("locked"))
                {
                    return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
                    {
                        Title = "Account Locked",
                        Detail = result.Error,
                        Status = StatusCodes.Status423Locked
                    });
                }

                if (result.Error.Contains("Invalid email or password"))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Authentication Failed",
                        Detail = result.Error,
                        Status = StatusCodes.Status401Unauthorized
                    });
                }

                return BadRequest(new ProblemDetails
                {
                    Title = "Login Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("User logged in successfully: {Email}", request.Email);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during user login for email: {Email}", request.Email);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Login Error",
                Detail = "An unexpected error occurred during login. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Token refresh request with refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Token refresh failed. Error: {Error}", result.Error);

                return result.Error.Contains("expired") || result.Error.Contains("invalid")
                    ? Unauthorized(new ProblemDetails
                    {
                        Title = "Token Refresh Failed",
                        Detail = result.Error,
                        Status = StatusCodes.Status401Unauthorized
                    })
                    : BadRequest(new ProblemDetails
                    {
                        Title = "Token Refresh Failed",
                        Detail = result.Error,
                        Status = StatusCodes.Status400BadRequest
                    });
            }

            _logger.LogInformation("Token refreshed successfully");
            
            // Map TokenResult to RefreshTokenResponse
            var response = new RefreshTokenResponse
            {
                UserId = Guid.Empty, // Will need to get this from the token
                AccessToken = result.Value.AccessToken,
                RefreshToken = result.Value.RefreshToken,
                TokenType = result.Value.TokenType,
                ExpiresIn = result.Value.ExpiresIn,
                IssuedAt = result.Value.IssuedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Token Refresh Error",
                Detail = "An unexpected error occurred during token refresh. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Logout user and invalidate tokens
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Logout confirmation</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Authentication Required",
                    Detail = "Valid authentication is required to logout",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var command = new LogoutUserCommand(userId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Logout failed for user: {UserId}. Error: {Error}", 
                    userId, result.Error);

                return BadRequest(new ProblemDetails
                {
                    Title = "Logout Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("User logged out successfully: {UserId}", userId);
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Logout Error",
                Detail = "An unexpected error occurred during logout. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get current user information from authenticated session
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Task.FromResult<IActionResult>(Unauthorized(new ProblemDetails
                {
                    Title = "Authentication Required",
                    Detail = "Valid authentication is required",
                    Status = StatusCodes.Status401Unauthorized
                }));
            }

            var response = new CurrentUserResponse
            {
                UserId = userId,
                Email = emailClaim ?? string.Empty,
                Username = usernameClaim ?? string.Empty
            };

            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting current user information");
            
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "User Information Error",
                Detail = "An unexpected error occurred retrieving user information. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            }));
        }
    }
}