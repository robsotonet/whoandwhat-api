
using Moq;
using WhoAndWhat.Application.Features.Auth;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Application.Tests.Features.Auth;

public class AccountVerificationServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly AccountVerificationService _sut;

    public AccountVerificationServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sut = new AccountVerificationService(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task GenerateVerificationTokenAsync_ShouldReturnToken_WhenUserExistsAndIsNotVerified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("test@example.com", "testuser", Language.en);
        // Set user ID using reflection since it's needed for the test
        typeof(BaseEntity).GetProperty("Id")!.SetValue(user, userId);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId, default)).ReturnsAsync(user);

        // Act
        var result = await _sut.GenerateVerificationTokenAsync(userId);

        // Assert
        Assert.NotNull(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task GenerateVerificationTokenAsync_ShouldReturnNull_WhenUserIsAlreadyVerified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("test@example.com", "testuser", Language.en);
        // Set user ID using reflection and verify email
        typeof(BaseEntity).GetProperty("Id")!.SetValue(user, userId);
        user.VerifyEmail();
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId, default)).ReturnsAsync(user);

        // Act
        var result = await _sut.GenerateVerificationTokenAsync(userId);

        // Assert
        Assert.Null(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task VerifyAccountAsync_ShouldReturnTrue_WhenTokenIsValid()
    {
        // Arrange
        var token = "valid-token";
        var user = new User("test@example.com", "testuser", Language.en);
        user.SetVerificationToken(token);
        _userRepositoryMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>(), default)).ReturnsAsync(new[] { user });

        // Act
        var result = await _sut.VerifyAccountAsync(token);

        // Assert
        Assert.True(result);
        Assert.True(user.IsEmailVerified);
        Assert.Null(user.VerificationToken);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task VerifyAccountAsync_ShouldReturnFalse_WhenTokenIsInvalid()
    {
        // Arrange
        var token = "invalid-token";
        _userRepositoryMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>(), default)).ReturnsAsync(new User[] { });

        // Act
        var result = await _sut.VerifyAccountAsync(token);

        // Assert
        Assert.False(result);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }
}
