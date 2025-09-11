using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class OAuthAccountEntityTests
{
    [Fact]
    public void OAuthAccount_Should_Initialize_With_Valid_Constructor()
    {
        var userId = Guid.NewGuid();
        var provider = "Google";
        var externalId = "123456789";
        var email = "test@gmail.com";
        var name = "Test User";

        var oAuthAccount = new OAuthAccount(userId, provider, externalId, email, name);

        oAuthAccount.Id.Should().NotBe(Guid.Empty);
        oAuthAccount.UserId.Should().Be(userId);
        oAuthAccount.Provider.Should().Be(provider);
        oAuthAccount.ExternalId.Should().Be(externalId);
        oAuthAccount.Email.Should().Be(email);
        oAuthAccount.Name.Should().Be(name);
        oAuthAccount.IsActive.Should().BeTrue();
        oAuthAccount.LastLoginAt.Should().BeNull();
        oAuthAccount.ProfileImageUrl.Should().BeNull();
    }

    [Fact]
    public void OAuthAccount_Should_Initialize_Without_Optional_Parameters()
    {
        var userId = Guid.NewGuid();
        var provider = "Facebook";
        var externalId = "987654321";

        var oAuthAccount = new OAuthAccount(userId, provider, externalId);

        oAuthAccount.UserId.Should().Be(userId);
        oAuthAccount.Provider.Should().Be(provider);
        oAuthAccount.ExternalId.Should().Be(externalId);
        oAuthAccount.Email.Should().BeNull();
        oAuthAccount.Name.Should().BeNull();
        oAuthAccount.IsActive.Should().BeTrue();
    }

    [Fact]
    public void OAuthAccount_Should_Update_Profile_Information()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            "123456789"
        );

        var newEmail = "updated@gmail.com";
        var newName = "Updated Name";
        var newProfileImage = "https://example.com/profile.jpg";

        oAuthAccount.UpdateProfile(newEmail, newName, newProfileImage);

        oAuthAccount.Email.Should().Be(newEmail);
        oAuthAccount.Name.Should().Be(newName);
        oAuthAccount.ProfileImageUrl.Should().Be(newProfileImage);
    }

    [Fact]
    public void OAuthAccount_Should_Update_Modified_Date_When_Profile_Updated()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            "123456789"
        );

        var originalUpdateTime = oAuthAccount.UpdatedAt;
        Thread.Sleep(10); // Small delay to ensure different timestamps

        oAuthAccount.UpdateProfile("new@email.com", "New Name", null);

        oAuthAccount.UpdatedAt.Should().BeAfter(originalUpdateTime);
    }

    [Fact]
    public void OAuthAccount_Should_Record_Login_With_Timestamp()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Apple",
            "apple123"
        );

        oAuthAccount.LastLoginAt.Should().BeNull();

        oAuthAccount.RecordLogin();

        oAuthAccount.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OAuthAccount_Should_Update_Modified_Date_When_Login_Recorded()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            "123456789"
        );

        var originalUpdateTime = oAuthAccount.UpdatedAt;
        Thread.Sleep(10);

        oAuthAccount.RecordLogin();

        oAuthAccount.UpdatedAt.Should().BeAfter(originalUpdateTime);
    }

    [Fact]
    public void OAuthAccount_Should_Deactivate_Account()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Facebook",
            "fb123"
        );

        oAuthAccount.IsActive.Should().BeTrue();

        oAuthAccount.Deactivate();

        oAuthAccount.IsActive.Should().BeFalse();
    }

    [Fact]
    public void OAuthAccount_Should_Reactivate_Account()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            "google123"
        );

        oAuthAccount.Deactivate();
        oAuthAccount.IsActive.Should().BeFalse();

        oAuthAccount.Reactivate();

        oAuthAccount.IsActive.Should().BeTrue();
    }

    [Fact]
    public void OAuthAccount_Should_Update_Modified_Date_When_Deactivated()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Apple",
            "apple123"
        );

        var originalUpdateTime = oAuthAccount.UpdatedAt;
        Thread.Sleep(10);

        oAuthAccount.Deactivate();

        oAuthAccount.UpdatedAt.Should().BeAfter(originalUpdateTime);
    }

    [Fact]
    public void OAuthAccount_Should_Update_Modified_Date_When_Reactivated()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Facebook",
            "fb123"
        );

        oAuthAccount.Deactivate();
        var deactivatedTime = oAuthAccount.UpdatedAt;
        Thread.Sleep(10);

        oAuthAccount.Reactivate();

        oAuthAccount.UpdatedAt.Should().BeAfter(deactivatedTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OAuthAccount_Should_Throw_When_Provider_Is_Invalid(string? invalidProvider)
    {
        var action = () => new OAuthAccount(
            Guid.NewGuid(),
            invalidProvider,
            "123456"
        );

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OAuthAccount_Should_Throw_When_ExternalId_Is_Invalid(string? invalidExternalId)
    {
        var action = () => new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            invalidExternalId
        );

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OAuthAccount_Should_Handle_Null_Values_In_Profile_Update()
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            "Google",
            "123456789",
            "initial@email.com",
            "Initial Name"
        );

        oAuthAccount.UpdateProfile(null, null, null);

        oAuthAccount.Email.Should().BeNull();
        oAuthAccount.Name.Should().BeNull();
        oAuthAccount.ProfileImageUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("Facebook")]
    [InlineData("Apple")]
    [InlineData("Microsoft")]
    public void OAuthAccount_Should_Support_Different_Providers(string provider)
    {
        var oAuthAccount = new OAuthAccount(
            Guid.NewGuid(),
            provider,
            "external123"
        );

        oAuthAccount.Provider.Should().Be(provider);
        oAuthAccount.IsActive.Should().BeTrue();
    }
}
