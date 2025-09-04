using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class UserEntityTests
{
    [Fact]
    public void User_Should_Initialize_With_Valid_Constructor()
    {
        var email = "test@example.com";
        var username = "testuser";
        var language = Language.en;
        
        var user = new User(email, username, language);
        
        user.Id.Should().NotBe(Guid.Empty);
        user.Email.Should().Be(email);
        user.Username.Should().Be(username);
        user.PreferredLanguage.Should().Be(language);
        user.IsActive.Should().BeTrue();
        user.IsEmailVerified.Should().BeFalse();
        user.IsLocked.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.Tasks.Should().NotBeNull().And.BeEmpty();
        user.Contacts.Should().NotBeNull().And.BeEmpty();
        user.Projects.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void User_Should_Set_Password_With_Hashing()
    {
        var user = new User("test@example.com", "testuser", Language.en);
        var password = "TestPassword123!";

        user.SetPassword(password);

        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotBe(password);
        user.VerifyPassword(password).Should().BeTrue();
        user.VerifyPassword("wrongpassword").Should().BeFalse();
    }

    [Fact]
    public void User_Should_Handle_Failed_Login_Attempts()
    {
        var user = new User("test@example.com", "testuser", Language.en);

        // Simulate 4 failed attempts (should not lock)
        for (int i = 0; i < 4; i++)
        {
            user.RecordLoginAttempt(false);
        }

        user.FailedLoginAttempts.Should().Be(4);
        user.IsLocked.Should().BeFalse();

        // 5th attempt should lock the account
        user.RecordLoginAttempt(false);

        user.FailedLoginAttempts.Should().Be(5);
        user.IsLocked.Should().BeTrue();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void User_Should_Reset_Failed_Attempts_On_Successful_Login()
    {
        var user = new User("test@example.com", "testuser", Language.en);

        // Simulate failed attempts
        user.RecordLoginAttempt(false);
        user.RecordLoginAttempt(false);
        user.FailedLoginAttempts.Should().Be(2);

        // Successful login should reset counter
        user.RecordLoginAttempt(true);

        user.FailedLoginAttempts.Should().Be(0);
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Verify_Email()
    {
        var user = new User("test@example.com", "testuser", Language.en);
        
        user.IsEmailVerified.Should().BeFalse();
        
        user.VerifyEmail();
        
        user.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public void User_Should_Update_Preferred_Language()
    {
        var user = new User("test@example.com", "testuser", Language.en);
        
        user.PreferredLanguage.Should().Be(Language.en);
        
        user.UpdatePreferredLanguage(Language.es);
        
        user.PreferredLanguage.Should().Be(Language.es);
    }

    [Fact]
    public void User_Should_Lock_And_Unlock_Account()
    {
        var user = new User("test@example.com", "testuser", Language.en);
        
        user.IsLocked.Should().BeFalse();
        
        user.LockAccount();
        
        user.IsLocked.Should().BeTrue();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
        
        user.UnlockAccount();
        
        user.IsLocked.Should().BeFalse();
        user.LockedUntil.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Theory]
    [InlineData("short")] // Too short
    [InlineData("nouppercase123")] // No uppercase
    [InlineData("NOLOWERCASE123")] // No lowercase  
    [InlineData("NoNumbers")] // No numbers
    public void User_Should_Reject_Invalid_Passwords(string invalidPassword)
    {
        var user = new User("test@example.com", "testuser", Language.en);

        var action = () => user.SetPassword(invalidPassword);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Password does not meet requirements*");
    }
}