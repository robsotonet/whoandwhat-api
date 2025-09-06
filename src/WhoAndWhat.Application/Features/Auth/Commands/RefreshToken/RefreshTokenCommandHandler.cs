using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Handler for refresh token command
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<TokenResult>>
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IJwtTokenService jwtTokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<TokenResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _jwtTokenService.RefreshTokensAsync(request.RefreshToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to refresh token: {Error}", result.Error);
                return Result<TokenResult>.Failure(result.Error);
            }

            _logger.LogInformation("Token refreshed successfully");
            return Result<TokenResult>.Success(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during token refresh");
            return Result<TokenResult>.Failure("An error occurred during token refresh. Please try again.");
        }
    }
}