using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Controller for password management operations (forgot, reset, change)
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Tags("Password Management")]
public class PasswordController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PasswordController> _logger;

    public PasswordController(
        IUserService userService,
        IEmailService emailService,
        ILogger<PasswordController> logger)
    {
        _userService = userService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Request password reset email
    /// </summary>
    /// <param name="request">Forgot password request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response (always returns success to prevent email enumeration)</returns>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "The request contains invalid data",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get user by email - don't reveal if user exists
            var user = await _userService.GetUserByEmailAsync(request.Email, cancellationToken);
            
            if (user != null)
            {
                // Generate password reset token
                var resetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .Replace("/", "_")
                    .Replace("+", "-")
                    .TrimEnd('=');

                // Set reset token on user
                user.SetPasswordResetToken(resetToken, DateTime.UtcNow.AddHours(1));
                
                // This would normally be handled by the repository's SaveChanges
                // For now, we'll use a direct repository call
                var userRepository = HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                await userRepository.UpdateAsync(user, cancellationToken);

                // Send password reset email
                var emailSent = await _emailService.SendPasswordResetEmailAsync(
                    user.Email,
                    user.Username,
                    resetToken,
                    cancellationToken);

                if (emailSent)
                {
                    _logger.LogInformation("Password reset email sent successfully to {Email}", request.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send password reset email to {Email}", request.Email);
                }
            }
            else
            {
                // User doesn't exist, but we don't want to reveal this information
                _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            }

            // Always return success to prevent email enumeration attacks
            return Ok(new MessageResponse
            {
                Message = "If an account with that email address exists, we have sent you an email with instructions to reset your password."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for email: {Email}", request.Email);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Forgot Password Error",
                Detail = "An unexpected error occurred. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Reset password using reset token
    /// </summary>
    /// <param name="request">Reset password request with token and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error response</returns>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "The request contains invalid data",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _userService.ResetPasswordAsync(
                request.Email,
                request.Token,
                request.NewPassword,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Password Reset Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get user to send confirmation email
            var user = await _userService.GetUserByEmailAsync(request.Email, cancellationToken);
            if (user != null)
            {
                // Send password changed confirmation email
                await _emailService.SendPasswordChangedEmailAsync(
                    user.Email,
                    user.Username,
                    cancellationToken);
            }

            _logger.LogInformation("Password reset successfully for email: {Email}", request.Email);

            return Ok(new MessageResponse
            {
                Message = "Your password has been successfully reset. You can now log in with your new password."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing password reset request for email: {Email}", request.Email);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Password Reset Error",
                Detail = "An unexpected error occurred. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    /// <param name="request">Change password request with current and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error response</returns>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "The request contains invalid data",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Authentication Error",
                    Detail = "Invalid or missing user identifier",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var result = await _userService.ChangePasswordAsync(
                userId,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Password Change Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get user to send confirmation email
            var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                // Send password changed confirmation email
                await _emailService.SendPasswordChangedEmailAsync(
                    user.Email,
                    user.Username,
                    cancellationToken);
            }

            _logger.LogInformation("Password changed successfully for user: {UserId}", userId);

            return Ok(new MessageResponse
            {
                Message = "Your password has been successfully changed."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing password change request for user: {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Password Change Error",
                Detail = "An unexpected error occurred. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Verify email address using verification token
    /// </summary>
    /// <param name="request">Email verification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error response</returns>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "The request contains invalid data",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _userService.VerifyEmailAsync(
                request.UserId,
                request.Token,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Email Verification Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get user to send welcome email
            var user = await _userService.GetUserByIdAsync(request.UserId, cancellationToken);
            if (user != null)
            {
                // Send welcome email after successful verification
                await _emailService.SendWelcomeEmailAsync(
                    user.Email,
                    user.Username,
                    cancellationToken);
            }

            _logger.LogInformation("Email verified successfully for user: {UserId}", request.UserId);

            return Ok(new MessageResponse
            {
                Message = "Your email address has been successfully verified. Welcome to WhoAndWhat!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email verification request for user: {UserId}", request.UserId);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Email Verification Error",
                Detail = "An unexpected error occurred. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}