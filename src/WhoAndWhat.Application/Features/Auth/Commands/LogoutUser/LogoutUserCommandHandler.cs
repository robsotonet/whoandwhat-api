using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Auth.Commands.LogoutUser;

/// <summary>
/// Handler for user logout command
/// </summary>
public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, Result<LogoutResponse>>
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LogoutUserCommandHandler> _logger;

    public LogoutUserCommandHandler(
        IJwtTokenService jwtTokenService,
        ILogger<LogoutUserCommandHandler> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<LogoutResponse>> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            int tokensRevoked = 0;

            if (request.RevokeAllTokens)
            {
                // Revoke all tokens for the user
                await _jwtTokenService.RevokeAllUserTokensAsync(request.UserId);
                tokensRevoked = 1; // We don't track exact count, but indicate tokens were revoked
                _logger.LogInformation("All tokens revoked for user: {UserId}", request.UserId);
            }
            else if (!string.IsNullOrEmpty(request.RefreshToken))
            {
                // Revoke specific refresh token
                await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
                tokensRevoked = 1;
                _logger.LogInformation("Specific refresh token revoked for user: {UserId}", request.UserId);
            }
            else
            {
                // Default behavior: revoke all tokens
                await _jwtTokenService.RevokeAllUserTokensAsync(request.UserId);
                tokensRevoked = 1;
                _logger.LogInformation("All tokens revoked for user: {UserId} (default logout behavior)", request.UserId);
            }

            var response = new LogoutResponse
            {
                Message = "Logged out successfully",
                TokensRevoked = tokensRevoked,
                LogoutAt = DateTime.UtcNow
            };

            return Result<LogoutResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user logout for user: {UserId}", request.UserId);
            return Result<LogoutResponse>.Failure("An error occurred during logout. Please try again.");
        }
    }
}