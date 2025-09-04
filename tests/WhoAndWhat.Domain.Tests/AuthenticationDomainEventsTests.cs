using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Events;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class AuthenticationDomainEventsTests
{
    [Fact]
    public void User_Should_Raise_UserCreatedEvent_When_Created()
    {
        // Act
        var user = new User("test@example.com", "testuser", Language.en);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserCreatedEvent>();
        
        var userCreatedEvent = domainEvent as UserCreatedEvent;
        userCreatedEvent!.UserId.Should().Be(user.Id);
        userCreatedEvent.Email.Should().Be("test@example.com");
        userCreatedEvent.Username.Should().Be("testuser");
        userCreatedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserPasswordChangedEvent_When_Password_Set()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents(); // Clear creation event

        // Act
        user.SetPassword("TestPassword123!");

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserPasswordChangedEvent>();
        
        var passwordChangedEvent = domainEvent as UserPasswordChangedEvent;
        passwordChangedEvent!.UserId.Should().Be(user.Id);
        passwordChangedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserLoggedInEvent_When_Successful_Login_Recorded()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.RecordLoginAttempt(true);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserLoggedInEvent>();
        
        var loggedInEvent = domainEvent as UserLoggedInEvent;
        loggedInEvent!.UserId.Should().Be(user.Id);
        loggedInEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Not_Raise_LoginEvent_When_Failed_Login_Recorded()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.RecordLoginAttempt(false);

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void User_Should_Raise_UserLockedEvent_When_Account_Locked()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.LockAccount();

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserLockedEvent>();
        
        var lockedEvent = domainEvent as UserLockedEvent;
        lockedEvent!.UserId.Should().Be(user.Id);
        lockedEvent.LockedUntil.Should().BeAfter(DateTime.UtcNow);
        lockedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserLockedEvent_When_Failed_Login_Limit_Reached()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act - Record 5 failed attempts to trigger lockout
        for (int i = 0; i < 5; i++)
        {
            user.RecordLoginAttempt(false);
        }

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserLockedEvent>();
        
        var lockedEvent = domainEvent as UserLockedEvent;
        lockedEvent!.UserId.Should().Be(user.Id);
        lockedEvent.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void User_Should_Raise_UserUnlockedEvent_When_Account_Unlocked()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.LockAccount();
        user.ClearDomainEvents();

        // Act
        user.UnlockAccount();

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserUnlockedEvent>();
        
        var unlockedEvent = domainEvent as UserUnlockedEvent;
        unlockedEvent!.UserId.Should().Be(user.Id);
        unlockedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserEmailVerifiedEvent_When_Email_Verified()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.VerifyEmail();

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserEmailVerifiedEvent>();
        
        var emailVerifiedEvent = domainEvent as UserEmailVerifiedEvent;
        emailVerifiedEvent!.UserId.Should().Be(user.Id);
        emailVerifiedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserDeactivatedEvent_When_Account_Deactivated()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.DeactivateAccount();

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserDeactivatedEvent>();
        
        var deactivatedEvent = domainEvent as UserDeactivatedEvent;
        deactivatedEvent!.UserId.Should().Be(user.Id);
        deactivatedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Raise_UserPreferencesUpdatedEvent_When_Language_Updated()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();

        // Act
        user.UpdatePreferredLanguage(Language.es);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<UserPreferencesUpdatedEvent>();
        
        var preferencesUpdatedEvent = domainEvent as UserPreferencesUpdatedEvent;
        preferencesUpdatedEvent!.UserId.Should().Be(user.Id);
        preferencesUpdatedEvent.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_Should_Accumulate_Multiple_Domain_Events()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        
        // Act - Perform multiple operations that generate events
        user.SetPassword("TestPassword123!");
        user.VerifyEmail();
        user.UpdatePreferredLanguage(Language.es);

        // Assert
        user.DomainEvents.Should().HaveCount(4); // Creation + Password + Email + Preferences
        user.DomainEvents.Should().ContainSingle(e => e is UserCreatedEvent);
        user.DomainEvents.Should().ContainSingle(e => e is UserPasswordChangedEvent);
        user.DomainEvents.Should().ContainSingle(e => e is UserEmailVerifiedEvent);
        user.DomainEvents.Should().ContainSingle(e => e is UserPreferencesUpdatedEvent);
    }

    [Fact]
    public void User_Should_Clear_Domain_Events_When_Requested()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("TestPassword123!");
        user.VerifyEmail();
        
        user.DomainEvents.Should().HaveCount(3); // Creation + Password + Email

        // Act
        user.ClearDomainEvents();

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void User_Should_Continue_Generating_Events_After_Clear()
    {
        // Arrange
        var user = new User("test@example.com", "testuser", Language.en);
        user.ClearDomainEvents();
        
        // Act
        user.UpdatePreferredLanguage(Language.es);
        user.LockAccount();

        // Assert
        user.DomainEvents.Should().HaveCount(2);
        user.DomainEvents.Should().ContainSingle(e => e is UserPreferencesUpdatedEvent);
        user.DomainEvents.Should().ContainSingle(e => e is UserLockedEvent);
    }

    [Fact]
    public void DomainEvent_Should_Have_Consistent_DateOccurred_Property()
    {
        // Arrange & Act
        var user = new User("test@example.com", "testuser", Language.en);
        var creationTime = DateTime.UtcNow;

        // Assert
        var domainEvent = user.DomainEvents.First();
        domainEvent.DateOccurred.Should().BeCloseTo(creationTime, TimeSpan.FromSeconds(5));
        
        // All domain events should implement IDomainEvent correctly
        domainEvent.Should().BeAssignableTo<IDomainEvent>();
    }
}