using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Tests.TestHelpers;

/// <summary>
/// Test factory for creating User entities with specific Ids for testing purposes
/// </summary>
public class UserTestFactory : User
{
    private UserTestFactory(Guid id, string email, string username, Language preferredLanguage, DateTime? createdAt = null)
        : base(id, email, username, preferredLanguage, createdAt)
    {
    }

    /// <summary>
    /// Creates a User instance with a specific Id for testing
    /// </summary>
    public static User CreateWithId(Guid id, string email, string username, Language preferredLanguage, DateTime? createdAt = null)
    {
        return new UserTestFactory(id, email, username, preferredLanguage, createdAt);
    }

    /// <summary>
    /// Creates a User instance with a specific Id and default test values
    /// </summary>
    public static User CreateWithId(Guid id)
    {
        return new UserTestFactory(id, "test@example.com", "testuser", Language.en);
    }

    /// <summary>
    /// Creates a User instance with an expired lock for testing account unlock scenarios
    /// </summary>
    public static User CreateWithExpiredLock(string email, string username, Language language, DateTime expiredLockTime)
    {
        var user = new UserTestFactory(Guid.NewGuid(), email, username, language);
        user.LockAccountUntil(expiredLockTime);
        return user;
    }
}
