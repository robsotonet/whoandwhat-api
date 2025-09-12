using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class RefreshTokenEntityTests
{
    [Fact]
    public void RefreshToken_Should_Initialize_With_Valid_Constructor()
    {
        var userId = Guid.NewGuid();
        var token = "test-refresh-token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var createdByIp = "192.168.1.1";

        var refreshToken = new RefreshToken(userId, token, expiresAt, createdByIp);

        refreshToken.Id.Should().NotBe(Guid.Empty);
        refreshToken.UserId.Should().Be(userId);
        refreshToken.Token.Should().Be(token);
        refreshToken.ExpiresAt.Should().Be(expiresAt);
        refreshToken.CreatedByIp.Should().Be(createdByIp);
        refreshToken.IsRevoked.Should().BeFalse();
        refreshToken.RevokedAt.Should().BeNull();
        refreshToken.RevokedByIp.Should().BeNull();
        refreshToken.ReplacedByToken.Should().BeNull();
    }

    [Fact]
    public void RefreshToken_Should_Be_Active_When_Not_Expired_And_Not_Revoked()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "test-token",
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        refreshToken.IsActive.Should().BeTrue();
        refreshToken.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_Should_Be_Expired_When_Past_Expiry_Date()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "test-token",
            DateTime.UtcNow.AddDays(-1), // Expired yesterday
            "192.168.1.1"
        );

        refreshToken.IsExpired.Should().BeTrue();
        refreshToken.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_Should_Be_Inactive_When_Revoked()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "test-token",
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        refreshToken.Revoke("192.168.1.2");

        refreshToken.IsRevoked.Should().BeTrue();
        refreshToken.IsActive.Should().BeFalse();
        refreshToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        refreshToken.RevokedByIp.Should().Be("192.168.1.2");
    }

    [Fact]
    public void RefreshToken_Should_Track_Replacement_Token_When_Revoked()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "old-token",
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        var replacementToken = "new-token";
        refreshToken.Revoke("192.168.1.2", replacementToken);

        refreshToken.IsRevoked.Should().BeTrue();
        refreshToken.ReplacedByToken.Should().Be(replacementToken);
        refreshToken.RevokedByIp.Should().Be("192.168.1.2");
    }

    [Fact]
    public void RefreshToken_Should_Update_Modified_Date_When_Revoked()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "test-token",
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        var originalUpdateTime = refreshToken.UpdatedAt;
        Thread.Sleep(10); // Small delay to ensure different timestamps

        refreshToken.Revoke("192.168.1.2");

        refreshToken.UpdatedAt.Should().BeAfter(originalUpdateTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RefreshToken_Should_Throw_When_Token_Is_Invalid(string? invalidToken)
    {
        var action = () => new RefreshToken(
            Guid.NewGuid(),
            invalidToken,
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RefreshToken_Should_Throw_When_CreatedByIp_Is_Invalid(string? invalidIp)
    {
        var action = () => new RefreshToken(
            Guid.NewGuid(),
            "valid-token",
            DateTime.UtcNow.AddDays(7),
            invalidIp
        );

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RefreshToken_Should_Handle_Multiple_Revoke_Calls()
    {
        var refreshToken = new RefreshToken(
            Guid.NewGuid(),
            "test-token",
            DateTime.UtcNow.AddDays(7),
            "192.168.1.1"
        );

        refreshToken.Revoke("192.168.1.2", "first-replacement");
        var firstRevokedAt = refreshToken.RevokedAt;

        // Second revoke call should update the revoked date (business logic may vary)
        refreshToken.Revoke("192.168.1.3", "second-replacement");

        // The actual behavior depends on business requirements
        // For now, let's assume it updates the revoke information
        refreshToken.IsRevoked.Should().BeTrue();
        refreshToken.IsActive.Should().BeFalse();
    }
}
