
using WhoAndWhat.Domain.Services;
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
    public void CreatePasswordHash_ShouldReturnHashAndSalt()
    {
        // Arrange
        var password = "password123";

        // Act
        var (passwordHash, salt) = _userDomainService.CreatePasswordHash(password);

        // Assert
        Assert.NotNull(passwordHash);
        Assert.NotNull(salt);
        Assert.NotEmpty(passwordHash);
        Assert.NotEmpty(salt);
    }

    [Fact]
    public void VerifyPasswordHash_ShouldReturnTrue_ForCorrectPassword()
    {
        // Arrange
        var password = "password123";
        var (passwordHash, salt) = _userDomainService.CreatePasswordHash(password);

        // Act
        var result = _userDomainService.VerifyPasswordHash(password, passwordHash, salt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPasswordHash_ShouldReturnFalse_ForIncorrectPassword()
    {
        // Arrange
        var password = "password123";
        var incorrectPassword = "wrongpassword";
        var (passwordHash, salt) = _userDomainService.CreatePasswordHash(password);

        // Act
        var result = _userDomainService.VerifyPasswordHash(incorrectPassword, passwordHash, salt);

        // Assert
        Assert.False(result);
    }
}
