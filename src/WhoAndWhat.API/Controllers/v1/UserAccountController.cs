using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Controller for user account management operations
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/account")]
[Tags("Account Management")]
[Authorize]
public class UserAccountController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserAccountController> _logger;

    public UserAccountController(
        IUserService userService,
        ILogger<UserAccountController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user profile information</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "User ID not found in token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var user = await _userService.GetUserByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = "User account not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var response = new CurrentUserResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
                PreferredLanguage = user.PreferredLanguage.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile for user ID: {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving user profile",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update user profile information
    /// </summary>
    /// <param name="request">Profile update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated profile information</returns>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UpdateProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "User ID not found in token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var user = await _userService.GetUserByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = "User account not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Update profile fields if provided
            var updateResult = await _userService.UpdateUserProfileAsync(
                userId.Value,
                request.Username,
                request.PreferredLanguage,
                cancellationToken);

            if (!updateResult.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Profile Update Failed",
                    Detail = updateResult.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var updatedUser = updateResult.Value;

            var response = new UpdateProfileResponse
            {
                UserId = updatedUser.Id,
                Email = updatedUser.Email,
                Username = updatedUser.Username,
                PreferredLanguage = updatedUser.PreferredLanguage.ToString(),
                Message = "Profile updated successfully",
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("User profile updated successfully for user ID: {UserId}", userId.Value);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile for user ID: {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while updating user profile",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Deactivate user account
    /// </summary>
    /// <param name="request">Account deactivation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deactivation confirmation</returns>
    [HttpPost("deactivate")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAccount([FromBody] DeactivateAccountRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "User ID not found in token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var user = await _userService.GetUserByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = "User account not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Verify current password
            var passwordValid = await _userService.ValidatePasswordAsync(userId.Value, request.CurrentPassword, cancellationToken);
            if (!passwordValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Password",
                    Detail = "Current password is incorrect",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Deactivate account
            var deactivationResult = await _userService.DeactivateUserAsync(userId.Value, request.Reason, cancellationToken);
            if (!deactivationResult.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Deactivation Failed",
                    Detail = deactivationResult.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("User account deactivated for user ID: {UserId}, Reason: {Reason}", userId.Value, request.Reason);

            return Ok(new MessageResponse
            {
                Message = "Your account has been successfully deactivated. We're sorry to see you go!",
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user account for user ID: {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while deactivating your account",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Export user data in specified format
    /// </summary>
    /// <param name="request">Data export request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exported user data file</returns>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportData([FromBody] ExportDataRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "User ID not found in token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var user = await _userService.GetUserByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = "User account not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Export user data
            var exportResult = await _userService.ExportUserDataAsync(
                userId.Value,
                request.Format,
                request.IncludeProfile,
                request.IncludeTasks,
                request.IncludeProjects,
                request.IncludeContacts,
                request.IncludeOAuthAccounts,
                cancellationToken);

            if (!exportResult.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Export Failed",
                    Detail = exportResult.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var exportData = exportResult.Value;

            _logger.LogInformation("User data exported successfully for user ID: {UserId}, Format: {Format}", userId.Value, request.Format);

            var contentType = request.Format.ToLowerInvariant() switch
            {
                "json" => "application/json",
                "csv" => "text/csv",
                _ => "application/octet-stream"
            };

            var fileName = $"user-data-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{request.Format.ToLowerInvariant()}";

            return File(exportData.Data, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting user data for user ID: {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while exporting user data",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get current user ID from JWT token
    /// </summary>
    /// <returns>Current user ID if found, null otherwise</returns>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
