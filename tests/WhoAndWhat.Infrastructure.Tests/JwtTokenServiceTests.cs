using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Tests;

public class JwtTokenServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly Mock<ILogger<JwtTokenService>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;

    public JwtTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<JwtTokenService>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _jwtSettings = new JwtSettings
        {
            SecretKey = "super-secret-key-that-is-long-enough-for-hmac-256-encryption-and-security",
            Issuer = "WhoAndWhat.Test",
            Audience = "WhoAndWhat.Test.Client",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7,
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkewMinutes = 1
        };

        var jwtOptions = Options.Create(_jwtSettings);
        _jwtTokenService = new JwtTokenService(jwtOptions, _context, _mockLogger.Object, _mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task GenerateTokensAsync_Should_Create_Access_And_Refresh_Tokens()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jwtTokenService.GenerateTokensAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().Be(_jwtSettings.AccessTokenExpiryMinutes * 60);
        result.TokenType.Should().Be("Bearer");
        result.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify refresh token was stored in database
        var storedRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == user.Id);
        storedRefreshToken.Should().NotBeNull();
        storedRefreshToken!.Token.Should().Be(result.RefreshToken);
        storedRefreshToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_Should_Create_Valid_JWT_With_Claims()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.VerifyEmail();

        // Act
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);

        // Assert
        accessToken.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(accessToken);

        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
        jsonToken.Claims.Should().Contain(c => c.Type == "preferred_language" && c.Value == "en");
        jsonToken.Claims.Should().Contain(c => c.Type == "email_verified" && c.Value == "true");
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);

        jsonToken.Issuer.Should().Be(_jwtSettings.Issuer);
        jsonToken.Audiences.Should().Contain(_jwtSettings.Audience);
        jsonToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_Should_Create_Unique_Tokens()
    {
        // Act
        var token1 = await _jwtTokenService.GenerateRefreshTokenAsync();
        var token2 = await _jwtTokenService.GenerateRefreshTokenAsync();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
        token1.Length.Should().BeGreaterThan(50); // Base64 encoded 64 bytes should be > 50 chars
        token2.Length.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task RefreshTokensAsync_Should_Generate_New_Tokens_And_Revoke_Old_One()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var originalTokens = await _jwtTokenService.GenerateTokensAsync(user);
        var originalRefreshToken = originalTokens.RefreshToken;

        // Act
        var result = await _jwtTokenService.RefreshTokensAsync(originalRefreshToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBe(originalRefreshToken);

        // Verify old token is revoked
        var oldToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == originalRefreshToken);
        oldToken!.IsRevoked.Should().BeTrue();
        oldToken.IsActive.Should().BeFalse();

        // Verify new token exists
        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == result.Value.RefreshToken);
        newToken.Should().NotBeNull();
        newToken!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokensAsync_Should_Fail_With_Invalid_Token()
    {
        // Act
        var result = await _jwtTokenService.RefreshTokensAsync("invalid-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokensAsync_Should_Fail_With_Revoked_Token()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var tokens = await _jwtTokenService.GenerateTokensAsync(user);
        await _jwtTokenService.RevokeRefreshTokenAsync(tokens.RefreshToken);

        // Act
        var result = await _jwtTokenService.RefreshTokensAsync(tokens.RefreshToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Refresh token is not active");
    }

    [Fact]
    public async Task RefreshTokensAsync_Should_Fail_With_Expired_Token()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);

        var expiredToken = new RefreshToken(
            user.Id,
            "expired-token",
            DateTime.UtcNow.AddDays(-1), // Expired yesterday
            "127.0.0.1"
        );
        await _context.RefreshTokens.AddAsync(expiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jwtTokenService.RefreshTokensAsync("expired-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Refresh token is not active");
    }

    [Fact]
    public async Task ValidateTokenAsync_Should_Return_User_For_Valid_Token()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);

        // Act
        var result = await _jwtTokenService.ValidateTokenAsync(accessToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);
    }

    [Fact]
    public async Task ValidateTokenAsync_Should_Fail_For_Invalid_Token()
    {
        // Act
        var result = await _jwtTokenService.ValidateTokenAsync("invalid.jwt.token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Token validation");
    }

    [Fact]
    public async Task ValidateTokenAsync_Should_Fail_For_Inactive_User()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.DeactivateAccount();
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);

        // Act
        var result = await _jwtTokenService.ValidateTokenAsync(accessToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User account is inactive");
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_Should_Return_True_For_Active_Token()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var tokens = await _jwtTokenService.GenerateTokensAsync(user);

        // Act
        var isValid = await _jwtTokenService.ValidateRefreshTokenAsync(tokens.RefreshToken);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_Should_Return_False_For_Invalid_Token()
    {
        // Act
        var isValid = await _jwtTokenService.ValidateRefreshTokenAsync("non-existent-token");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_Should_Mark_Token_As_Revoked()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var tokens = await _jwtTokenService.GenerateTokensAsync(user);

        // Act
        await _jwtTokenService.RevokeRefreshTokenAsync(tokens.RefreshToken);

        // Assert
        var revokedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == tokens.RefreshToken);
        revokedToken!.IsRevoked.Should().BeTrue();
        revokedToken.IsActive.Should().BeFalse();
        revokedToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_Should_Handle_Non_Existent_Token_Gracefully()
    {
        // Act & Assert (should not throw)
        await _jwtTokenService.RevokeRefreshTokenAsync("non-existent-token");
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_Should_Revoke_All_Active_Tokens_For_User()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Generate multiple tokens
        var tokens1 = await _jwtTokenService.GenerateTokensAsync(user);
        var tokens2 = await _jwtTokenService.GenerateTokensAsync(user);
        var tokens3 = await _jwtTokenService.GenerateTokensAsync(user);

        // Act
        await _jwtTokenService.RevokeAllUserTokensAsync(user.Id);

        // Assert
        var userTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();

        userTokens.Should().HaveCount(3);
        userTokens.Should().OnlyContain(rt => rt.IsRevoked);
        userTokens.Should().OnlyContain(rt => !rt.IsActive);
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_Should_Handle_User_With_No_Tokens()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act & Assert (should not throw)
        await _jwtTokenService.RevokeAllUserTokensAsync(user.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not-long-enough-key")]
    public async Task JwtTokenService_Should_Throw_For_Invalid_Secret_Key(string invalidKey)
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            SecretKey = invalidKey,
            Issuer = "Test",
            Audience = "Test"
        };

        // Act & Assert
        var mockLogger = new Mock<ILogger<JwtTokenService>>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var action = () => new JwtTokenService(Options.Create(invalidSettings), _context, mockLogger.Object, mockHttpContextAccessor.Object);
        
        // Note: This would likely fail during token generation, not construction
        // So we test during token generation instead
        var service = new JwtTokenService(Options.Create(invalidSettings), _context, mockLogger.Object, mockHttpContextAccessor.Object);
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        
        var tokenGeneration = async () => await service.GenerateAccessTokenAsync(user);
        await tokenGeneration.Should().ThrowAsync<Exception>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}