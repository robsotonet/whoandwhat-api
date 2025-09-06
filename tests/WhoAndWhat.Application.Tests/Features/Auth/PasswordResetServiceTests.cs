
using Moq;
using WhoAndWhat.Application.Features.Auth;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth;

public class PasswordResetServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUserDomainService> _userDomainServiceMock;
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _userDomainServiceMock = new Mock<IUserDomainService>();
        _sut = new PasswordResetService(_userRepositoryMock.Object, _userDomainServiceMock.Object);
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_ShouldReturnToken_WhenUserExists()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User(email, "testuser", Language.en);
        _userRepositoryMock.Setup(x => x.GetUserByEmailAsync(email, default)).ReturnsAsync(user);

        // Act
        var result = await _sut.GeneratePasswordResetTokenAsync(email);

        // Assert
        Assert.NotNull(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var email = "nonexistent@example.com";
        _userRepositoryMock.Setup(x => x.GetUserByEmailAsync(email, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GeneratePasswordResetTokenAsync(email);

        // Assert
        Assert.Null(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldReturnTrue_WhenTokenIsValid()
    {
        // Arrange
        var token = "valid-token";
        var newPassword = "newPassword123";
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetPassword("oldPassword123"); // Set an initial password
        user.SetPasswordResetToken(token, DateTime.UtcNow.AddHours(1));
        _userRepositoryMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>(), default)).ReturnsAsync(new[] { user });

        // Act
        var result = await _sut.ResetPasswordAsync(token, newPassword);

        // Assert
        Assert.True(result);
        // Verify the password was changed to the new password
        Assert.True(user.VerifyPassword(newPassword));
        Assert.False(user.VerifyPassword("oldPassword123")); // Old password should no longer work
        Assert.Null(user.ResetToken);
        Assert.Null(user.ResetTokenExpires);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldReturnFalse_WhenTokenIsInvalid()
    {
        // Arrange
        var token = "invalid-token";
        var newPassword = "newPassword123";
        _userRepositoryMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>(), default)).ReturnsAsync(new User[] { });

        // Act
        var result = await _sut.ResetPasswordAsync(token, newPassword);

        // Assert
        Assert.False(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }
}
