using Microsoft.Extensions.Logging;
using System.Text.Json;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Services;

/// <summary>
/// Application service for user authentication and management
/// Implements timing attack protection and comprehensive authentication logic
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserDomainService _userDomainService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IUserDomainService userDomainService,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _userDomainService = userDomainService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<User>> RegisterUserAsync(string email, string username, string password, Language language, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email))
                return Result<User>.Failure("Email is required");

            if (string.IsNullOrWhiteSpace(username))
                return Result<User>.Failure("Username is required");

            if (string.IsNullOrWhiteSpace(password))
                return Result<User>.Failure("Password is required");

            // Check if email already exists
            var existingEmailUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (existingEmailUser != null)
            {
                _logger.LogWarning("Registration attempt with existing email: {Email}", email);
                return Result<User>.Failure("A user with this email address already exists");
            }

            // Check if username already exists
            var existingUsernameUser = await _userRepository.GetByUsernameAsync(username, cancellationToken);
            if (existingUsernameUser != null)
            {
                _logger.LogWarning("Registration attempt with existing username: {Username}", username);
                return Result<User>.Failure("This username is already taken");
            }

            // Create user using domain service
            var user = _userDomainService.CreateUser(email, username, password, language);

            // Save user to repository
            await _userRepository.AddAsync(user, cancellationToken);

            _logger.LogInformation("User registered successfully: {UserId} - {Email}", user.Id, user.Email);

            return Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with email: {Email}", email);
            return Result<User>.Failure("An error occurred during registration. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<User>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email))
                return Result<User>.Failure("Email is required");

            if (string.IsNullOrWhiteSpace(password))
                return Result<User>.Failure("Password is required");

            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            
            // Timing attack protection - always hash password even if user doesn't exist
            if (user == null)
            {
                // Hash a dummy password to prevent timing attacks
                BCrypt.Net.BCrypt.HashPassword("dummy-password", 12);
                _logger.LogWarning("Authentication attempt with non-existent email: {Email}", email);
                return Result<User>.Failure("Invalid email or password");
            }

            // Check if account is locked
            if (user.IsLocked && user.LockedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("Authentication attempt on locked account: {UserId} - locked until {LockedUntil}", 
                    user.Id, user.LockedUntil);
                return Result<User>.Failure($"Account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm:ss} UTC");
            }

            // Check if account is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Authentication attempt on inactive account: {UserId}", user.Id);
                return Result<User>.Failure("Account is inactive. Please contact support.");
            }

            // Verify password
            var isValidPassword = user.VerifyPassword(password);
            
            // Record login attempt (will handle locking if needed)
            user.RecordLoginAttempt(isValidPassword);
            await _userRepository.UpdateAsync(user, cancellationToken);

            if (!isValidPassword)
            {
                _logger.LogWarning("Failed authentication attempt for user: {UserId} - attempt #{FailedAttempts}", 
                    user.Id, user.FailedLoginAttempts);
                return Result<User>.Failure("Invalid email or password");
            }

            // Check email verification requirement (optional - can be configured)
            if (!user.IsEmailVerified)
            {
                _logger.LogInformation("Authentication successful but email not verified for user: {UserId}", user.Id);
                // This could be a warning rather than a failure depending on business requirements
                // For now, we'll allow login but the JWT will include email_verified=false claim
            }

            _logger.LogInformation("User authenticated successfully: {UserId}", user.Id);
            return Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating user with email: {Email}", email);
            return Result<User>.Failure("An error occurred during authentication. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
                return Result.Failure("Current password is required");

            if (string.IsNullOrWhiteSpace(newPassword))
                return Result.Failure("New password is required");

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Password change attempt for non-existent user: {UserId}", userId);
                return Result.Failure("User not found");
            }

            // Verify current password
            if (!user.VerifyPassword(currentPassword))
            {
                _logger.LogWarning("Password change attempt with invalid current password for user: {UserId}", userId);
                return Result.Failure("Current password is incorrect");
            }

            // Set new password (includes validation)
            user.SetPassword(newPassword);
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Password change validation failed for user {UserId}: {Error}", userId, ex.Message);
            return Result.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
            return Result.Failure("An error occurred while changing password. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> VerifyEmailAsync(Guid userId, string verificationToken, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(verificationToken))
                return Result.Failure("Verification token is required");

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Email verification attempt for non-existent user: {UserId}", userId);
                return Result.Failure("User not found");
            }

            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email verification attempted for already verified user: {UserId}", userId);
                return Result.Success(); // Already verified, treat as success
            }

            if (string.IsNullOrEmpty(user.VerificationToken) || user.VerificationToken != verificationToken)
            {
                _logger.LogWarning("Invalid email verification token for user: {UserId}", userId);
                return Result.Failure("Invalid or expired verification token");
            }

            user.VerifyEmail();
            user.ClearVerificationToken();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("Email verified successfully for user: {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email for user: {UserId}", userId);
            return Result.Failure("An error occurred during email verification. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result.Failure("Email is required");

            if (string.IsNullOrWhiteSpace(resetToken))
                return Result.Failure("Reset token is required");

            if (string.IsNullOrWhiteSpace(newPassword))
                return Result.Failure("New password is required");

            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Password reset attempt for non-existent user: {Email}", email);
                return Result.Failure("Invalid reset token or email");
            }

            if (string.IsNullOrEmpty(user.ResetToken) || user.ResetToken != resetToken)
            {
                _logger.LogWarning("Invalid password reset token for user: {UserId}", user.Id);
                return Result.Failure("Invalid or expired reset token");
            }

            if (user.ResetTokenExpires == null || user.ResetTokenExpires < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired password reset token for user: {UserId}", user.Id);
                return Result.Failure("Reset token has expired");
            }

            // Set new password and clear reset token
            user.SetPassword(newPassword);
            user.ClearPasswordResetToken();
            
            // Clear failed login attempts and unlock account
            user.UnlockAccount();
            
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("Password reset successfully for user: {UserId}", user.Id);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Password reset validation failed for email {Email}: {Error}", email, ex.Message);
            return Result.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for email: {Email}", email);
            return Result.Failure("An error occurred during password reset. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> LockUserAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Account lock attempt for non-existent user: {UserId}", userId);
                return Result.Failure("User not found");
            }

            user.LockAccount();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User account locked: {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking user account: {UserId}", userId);
            return Result.Failure("An error occurred while locking the account. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> UnlockUserAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Account unlock attempt for non-existent user: {UserId}", userId);
                return Result.Failure("User not found");
            }

            user.UnlockAccount();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User account unlocked: {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user account: {UserId}", userId);
            return Result.Failure("An error occurred while unlocking the account. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<User>> UpdateUserProfileAsync(Guid userId, string? username = null, string? preferredLanguage = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Profile update attempt for non-existent user: {UserId}", userId);
                return Result<User>.Failure("User not found");
            }

            // Update username if provided
            if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
            {
                // Check if username is already taken
                var existingUser = await _userRepository.GetByUsernameAsync(username, cancellationToken);
                if (existingUser != null && existingUser.Id != userId)
                {
                    _logger.LogWarning("Profile update attempt with existing username: {Username} for user: {UserId}", username, userId);
                    return Result<User>.Failure("This username is already taken");
                }

                user.UpdateUsername(username);
            }

            // Update preferred language if provided
            if (!string.IsNullOrWhiteSpace(preferredLanguage))
            {
                if (Enum.TryParse<Language>(preferredLanguage, true, out var language) && language != user.PreferredLanguage)
                {
                    user.UpdatePreferredLanguage(language);
                }
                else if (!Enum.TryParse<Language>(preferredLanguage, true, out _))
                {
                    _logger.LogWarning("Invalid language provided for user {UserId}: {Language}", userId, preferredLanguage);
                    return Result<User>.Failure("Invalid language. Supported languages are: en, es");
                }
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User profile updated: {UserId}", userId);
            return Result<User>.Success(user);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Profile update validation failed for user {UserId}: {Error}", userId, ex.Message);
            return Result<User>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile: {UserId}", userId);
            return Result<User>.Failure("An error occurred while updating profile. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userRepository.GetByIdAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _userRepository.GetByEmailAsync(email, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email: {Email}", email);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return await _userRepository.EmailExistsAsync(email, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking email existence: {Email}", email);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return await _userRepository.UsernameExistsAsync(username, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking username existence: {Username}", username);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidatePasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Password validation attempt for non-existent user: {UserId}", userId);
                return false;
            }

            // Use domain service to validate password
            return _userDomainService.ValidatePassword(user, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating password for user: {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeactivateUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Account deactivation attempt for non-existent user: {UserId}", userId);
                return Result.Failure("User not found");
            }

            // Deactivate the user account
            user.DeactivateAccount();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User account deactivated: {UserId}, Reason: {Reason}", userId, reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user account: {UserId}", userId);
            return Result.Failure("An error occurred while deactivating the account. Please try again.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ExportData>> ExportUserDataAsync(Guid userId, string format, bool includeProfile, bool includeTasks, bool includeProjects, bool includeContacts, bool includeOAuthAccounts, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Data export attempt for non-existent user: {UserId}", userId);
                return Result<ExportData>.Failure("User not found");
            }

            var exportData = new Dictionary<string, object>();
            int recordCount = 0;

            // Include profile data
            if (includeProfile)
            {
                exportData["profile"] = new
                {
                    user.Id,
                    user.Email,
                    user.Username,
                    PreferredLanguage = user.PreferredLanguage.ToString(),
                    user.IsEmailVerified,
                    user.IsActive,
                    user.CreatedAt,
                    user.UpdatedAt,
                    user.LastLoginAt
                };
                recordCount++;
            }

            // TODO: Add task, project, contact, and OAuth account data when those repositories are available
            // For now, we'll just include placeholder data structures
            if (includeTasks)
            {
                exportData["tasks"] = new List<object>(); // Will be populated when ITaskRepository is available
            }

            if (includeProjects)
            {
                exportData["projects"] = new List<object>(); // Will be populated when IProjectRepository is available
            }

            if (includeContacts)
            {
                exportData["contacts"] = new List<object>(); // Will be populated when IContactRepository is available
            }

            if (includeOAuthAccounts)
            {
                exportData["oauthAccounts"] = new List<object>(); // Will be populated when IOAuthAccountRepository is available
            }

            // Convert to requested format
            byte[] data;
            string contentType;
            
            switch (format.ToLowerInvariant())
            {
                case "json":
                    var jsonData = System.Text.Json.JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });
                    data = System.Text.Encoding.UTF8.GetBytes(jsonData);
                    contentType = "application/json";
                    break;

                case "csv":
                    // For CSV, we'll create a simple format with profile data only for now
                    var csvLines = new List<string>();
                    
                    if (includeProfile)
                    {
                        csvLines.Add("Type,Id,Email,Username,PreferredLanguage,IsEmailVerified,IsActive,CreatedAt,UpdatedAt,LastLoginAt");
                        csvLines.Add($"Profile,{user.Id},{user.Email},{user.Username},{user.PreferredLanguage},{user.IsEmailVerified},{user.IsActive},{user.CreatedAt:yyyy-MM-dd HH:mm:ss},{user.UpdatedAt:yyyy-MM-dd HH:mm:ss},{user.LastLoginAt:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    var csvContent = string.Join(Environment.NewLine, csvLines);
                    data = System.Text.Encoding.UTF8.GetBytes(csvContent);
                    contentType = "text/csv";
                    break;

                default:
                    _logger.LogWarning("Unsupported export format requested: {Format} for user: {UserId}", format, userId);
                    return Result<ExportData>.Failure("Unsupported export format. Use 'json' or 'csv'.");
            }

            var result = new ExportData
            {
                Data = data,
                ContentType = contentType,
                FileName = $"user-data-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format.ToLowerInvariant()}",
                SizeBytes = data.Length,
                RecordCount = recordCount
            };

            _logger.LogInformation("User data exported successfully: {UserId}, Format: {Format}, Size: {Size} bytes", userId, format, data.Length);
            return Result<ExportData>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting user data: {UserId}", userId);
            return Result<ExportData>.Failure("An error occurred while exporting data. Please try again.");
        }
    }
}