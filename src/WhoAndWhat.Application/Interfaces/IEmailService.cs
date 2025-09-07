namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for sending various types of emails (password reset, verification, notifications, etc.)
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send password reset email with reset token
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">Username for personalization</param>
    /// <param name="resetToken">Password reset token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendPasswordResetEmailAsync(string email, string username, string resetToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email verification email with verification token
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">Username for personalization</param>
    /// <param name="verificationToken">Email verification token</param>
    /// <param name="userId">User ID for verification link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailVerificationAsync(string email, string username, string verificationToken, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send welcome email to new users
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">Username for personalization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendWelcomeEmailAsync(string email, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send account locked notification email
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">Username for personalization</param>
    /// <param name="lockedUntil">When the account will be unlocked</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendAccountLockedEmailAsync(string email, string username, DateTime lockedUntil, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send password changed notification email
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">Username for personalization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendPasswordChangedEmailAsync(string email, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send generic email with custom content
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlContent">HTML email content</param>
    /// <param name="plainTextContent">Plain text email content (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailAsync(string to, string subject, string htmlContent, string? plainTextContent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email service is properly configured and available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email service is ready to send emails</returns>
    Task<bool> IsEmailServiceAvailableAsync(CancellationToken cancellationToken = default);
}