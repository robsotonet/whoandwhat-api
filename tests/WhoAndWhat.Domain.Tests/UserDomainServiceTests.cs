
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Domain.Tests;

public class UserDomainServiceTests
{
    private readonly IUserDomainService _userDomainService;

    public UserDomainServiceTests()
    {
        _userDomainService = new UserDomainService();
    }

    [Fact]
    public void CreateUser_ShouldCreateUserWithPasswordSet()
    {
        // Arrange
        var email = "test@example.com";
        var username = "testuser";
        var password = "TestPassword123!";
        var language = Language.en;

        // Act
        var user = _userDomainService.CreateUser(email, username, password, language);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(username, user.Username);
        Assert.Equal(language, user.PreferredLanguage);
        Assert.True(user.VerifyPassword(password));
        Assert.False(user.VerifyPassword("wrongpassword"));
        Assert.True(user.IsActive);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public void CreateUser_ShouldGenerateDomainEvents()
    {
        // Arrange
        var email = "test@example.com";
        var username = "testuser";
        var password = "TestPassword123!";
        var language = Language.en;

        // Act
        var user = _userDomainService.CreateUser(email, username, password, language);

        // Assert
        Assert.Equal(2, user.DomainEvents.Count); // UserCreatedEvent and UserPasswordChangedEvent
        Assert.Contains(user.DomainEvents, e => e.GetType().Name == "UserCreatedEvent");
        Assert.Contains(user.DomainEvents, e => e.GetType().Name == "UserPasswordChangedEvent");
    }
}
