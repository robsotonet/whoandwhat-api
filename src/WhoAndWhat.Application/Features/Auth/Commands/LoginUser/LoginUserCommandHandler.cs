using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Features.Auth.Commands.LoginUser;

/// <summary>
/// Handler for user login command
/// </summary>
public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        ILogger<LoginUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                return Result<LoginResponse>.Failure("Invalid email or password");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt with inactive user: {UserId}", user.Id);
                return Result<LoginResponse>.Failure("Account is deactivated. Please contact support.");
            }

            // Check if user is locked
            if (user.IsLocked)
            {
                _logger.LogWarning("Login attempt with locked user: {UserId}", user.Id);
                
                if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
                {
                    return Result<LoginResponse>.Failure($"Account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC");
                }
                
                // If lock period expired, unlock the user
                user.UnlockAccount();
                await _userRepository.UpdateAsync(user, cancellationToken);
            }

            // Verify password
            if (!user.VerifyPassword(request.Password))
            {
                _logger.LogWarning("Invalid password attempt for user: {UserId}", user.Id);
                
                // Increment failed login attempts
                user.RecordLoginAttempt(false);
                await _userRepository.UpdateAsync(user, cancellationToken);
                
                // Check if user should be locked after failed attempts
                if (user.IsLocked)
                {
                    _logger.LogWarning("User locked due to failed login attempts: {UserId}", user.Id);
                    return Result<LoginResponse>.Failure("Account has been locked due to too many failed login attempts");
                }
                
                return Result<LoginResponse>.Failure("Invalid email or password");
            }

            // Check email verification (optional - can be configured)
            // For now, we'll allow login without email verification but track it
            if (!user.IsEmailVerified)
            {
                _logger.LogInformation("Login attempt with unverified email: {UserId}", user.Id);
                // Could return failure here if email verification is required
                // return Result<LoginResponse>.Failure("Please verify your email before logging in");
            }

            // Reset failed login attempts on successful login
            user.RecordLoginAttempt(true);
            await _userRepository.UpdateAsync(user, cancellationToken);

            // Generate JWT tokens
            var tokenResult = await _jwtTokenService.GenerateTokensAsync(user);

            _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

            // Prepare response
            var response = new LoginResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
                PreferredLanguage = user.PreferredLanguage.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                TokenType = tokenResult.TokenType,
                ExpiresIn = tokenResult.ExpiresIn,
                IssuedAt = tokenResult.IssuedAt,
                LastLoginAt = user.LastLoginAt
            };

            return Result<LoginResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user login for email: {Email}", request.Email);
            return Result<LoginResponse>.Failure("An error occurred during login. Please try again.");
        }
    }
}