using WhoAndWhat.Application.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Application service for user authentication and management
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Register a new user with email and password
    /// </summary>
    /// <param name="email">User email address</param>
    /// <param name="username">Unique username</param>
    /// <param name="password">User password (will be hashed)</param>
    /// <param name="language">Preferred language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the created user or error message</returns>
    public Task<Result<User>> RegisterUserAsync(string email, string username, string password, Language language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate user with email and password
    /// </summary>
    /// <param name="email">User email address</param>
    /// <param name="password">User password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the authenticated user or error message</returns>
    public Task<Result<User>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Change user password (requires current password verification)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="currentPassword">Current password for verification</param>
    /// <param name="newPassword">New password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify user email with verification token
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="verificationToken">Email verification token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> VerifyEmailAsync(Guid userId, string verificationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset user password with reset token
    /// </summary>
    /// <param name="email">User email address</param>
    /// <param name="resetToken">Password reset token</param>
    /// <param name="newPassword">New password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lock user account (admin function)
    /// </summary>
    /// <param name="userId">User ID to lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> LockUserAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlock user account (admin function)
    /// </summary>
    /// <param name="userId">User ID to unlock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> UnlockUserAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user profile information
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="username">New username (optional)</param>
    /// <param name="preferredLanguage">New preferred language (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing updated user or error message</returns>
    public Task<Result<User>> UpdateUserProfileAsync(Guid userId, string? username = null, string? preferredLanguage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User entity or null if not found</returns>
    public Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user by email address
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User entity or null if not found</returns>
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email address already exists
    /// </summary>
    /// <param name="email">Email address to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email exists, false otherwise</returns>
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if username already exists
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if username exists, false otherwise</returns>
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate user password
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="password">Password to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if password is valid, false otherwise</returns>
    public Task<bool> ValidatePasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivate user account
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="reason">Reason for deactivation (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> DeactivateUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user entity (used for internal operations like recording login attempts)
    /// </summary>
    /// <param name="user">User entity to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public Task<Result> UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export user data in specified format
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="format">Export format (json, csv)</param>
    /// <param name="includeProfile">Include profile data</param>
    /// <param name="includeTasks">Include tasks data</param>
    /// <param name="includeProjects">Include projects data</param>
    /// <param name="includeContacts">Include contacts data</param>
    /// <param name="includeOAuthAccounts">Include OAuth accounts data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing exported data or error message</returns>
    public Task<Result<ExportData>> ExportUserDataAsync(Guid userId, string format, bool includeProfile, bool includeTasks, bool includeProjects, bool includeContacts, bool includeOAuthAccounts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer object for exported user data
/// </summary>
public class ExportData
{
    /// <summary>
    /// Exported data as byte array
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Content type of exported data
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File name for the exported data
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of exported data in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Number of records exported
    /// </summary>
    public int RecordCount { get; set; }
}
