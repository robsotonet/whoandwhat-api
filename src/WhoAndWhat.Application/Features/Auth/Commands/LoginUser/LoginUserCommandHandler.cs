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
    private readonly IUserService _userService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(
        IUserService userService,
        IJwtTokenService jwtTokenService,
        ILogger<LoginUserCommandHandler> logger)
    {
        _userService = userService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Use UserService for authentication logic
            var authenticationResult = await _userService.AuthenticateAsync(
                request.Email,
                request.Password,
                cancellationToken);

            if (!authenticationResult.IsSuccess)
            {
                return Result<LoginResponse>.Failure(authenticationResult.Error);
            }

            var user = authenticationResult.Value;

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
            await _userService.UpdateUserAsync(user, cancellationToken);

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