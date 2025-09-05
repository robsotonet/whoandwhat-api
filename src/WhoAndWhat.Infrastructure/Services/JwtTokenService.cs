using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WhoAndWhat.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _context;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(IOptions<JwtSettings> jwtSettings, ApplicationDbContext context)
    {
        _jwtSettings = jwtSettings.Value;
        _context = context;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<TokenResult> GenerateTokensAsync(User user)
    {
        var accessToken = await GenerateAccessTokenAsync(user);
        var refreshToken = await GenerateRefreshTokenAsync();
        
        // Store refresh token
        var refreshTokenEntity = new RefreshToken(
            user.Id, 
            refreshToken, 
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            GetClientIpAddress());

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new TokenResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtSettings.AccessTokenExpiryMinutes * 60,
            TokenType = "Bearer",
            IssuedAt = DateTime.UtcNow
        };
    }

    public System.Threading.Tasks.Task<string> GenerateAccessTokenAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new("preferred_language", user.PreferredLanguage.ToString()),
            new("email_verified", user.IsEmailVerified.ToString().ToLower()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return System.Threading.Tasks.Task.FromResult(_tokenHandler.WriteToken(token));
    }

    public async Task<string> GenerateRefreshTokenAsync()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        var refreshToken = Convert.ToBase64String(randomBytes);
        
        // Ensure uniqueness
        var exists = await _context.RefreshTokens.AnyAsync(rt => rt.Token == refreshToken);
        if (exists)
        {
            return await GenerateRefreshTokenAsync(); // Recursively generate a new one
        }
        
        return refreshToken;
    }

    public async Task<Result<TokenResult>> RefreshTokensAsync(string refreshToken)
    {
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null)
        {
            return Result<TokenResult>.Failure("Invalid refresh token");
        }

        if (!storedToken.IsActive)
        {
            return Result<TokenResult>.Failure("Refresh token is not active");
        }

        // Revoke the old refresh token
        storedToken.Revoke(GetClientIpAddress());

        // Generate new tokens
        var newTokenResult = await GenerateTokensAsync(storedToken.User);

        await _context.SaveChangesAsync();

        return Result<TokenResult>.Success(newTokenResult);
    }

    public async Task<Result<User>> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = _jwtSettings.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                ValidateIssuer = _jwtSettings.ValidateIssuer,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = _jwtSettings.ValidateAudience,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = _jwtSettings.ValidateLifetime,
                RequireExpirationTime = _jwtSettings.RequireExpirationTime,
                ClockSkew = TimeSpan.FromMinutes(_jwtSettings.ClockSkewMinutes)
            };

            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return Result<User>.Failure("Invalid token");
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Result<User>.Failure("Invalid user ID in token");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<User>.Failure("User not found");
            }

            if (!user.IsActive)
            {
                return Result<User>.Failure("User account is inactive");
            }

            return Result<User>.Success(user);
        }
        catch (SecurityTokenExpiredException)
        {
            return Result<User>.Failure("Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            return Result<User>.Failure($"Token validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"Token validation error: {ex.Message}");
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);

        return refreshToken?.IsActive == true;
    }

    public async System.Threading.Tasks.Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token != null)
        {
            token.Revoke(GetClientIpAddress());
            await _context.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task RevokeAllUserTokensAsync(Guid userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        var clientIp = GetClientIpAddress();
        foreach (var token in tokens)
        {
            token.Revoke(clientIp);
        }

        if (tokens.Count > 0)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static string GetClientIpAddress()
    {
        // TODO: In a real application, this should get the actual client IP
        // This would typically be injected via IHttpContextAccessor
        return "127.0.0.1";
    }
}