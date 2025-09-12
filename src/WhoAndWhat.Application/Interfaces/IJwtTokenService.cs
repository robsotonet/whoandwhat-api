using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IJwtTokenService
{
    public System.Threading.Tasks.Task<TokenResult> GenerateTokensAsync(User user);
    public System.Threading.Tasks.Task<Result<TokenResult>> RefreshTokensAsync(string refreshToken);
    public System.Threading.Tasks.Task<Result<User>> ValidateTokenAsync(string token);
    public System.Threading.Tasks.Task RevokeRefreshTokenAsync(string refreshToken);
    public System.Threading.Tasks.Task RevokeAllUserTokensAsync(Guid userId);
    public System.Threading.Tasks.Task<string> GenerateAccessTokenAsync(User user);
    public System.Threading.Tasks.Task<string> GenerateRefreshTokenAsync();
    public System.Threading.Tasks.Task<bool> ValidateRefreshTokenAsync(string token);
}
