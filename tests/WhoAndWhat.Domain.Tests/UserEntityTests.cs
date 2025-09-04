using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class UserEntityTests
{
    [Fact]
    public void User_Should_Initialize_With_Default_Values()
    {
        var user = new User();
        
        user.Id.Should().Be(Guid.Empty);
        user.Email.Should().BeNull();
        user.Username.Should().BeNull();
        user.PreferredLanguage.Should().Be(Language.en); // Default enum value
        user.CreatedAt.Should().Be(DateTime.MinValue);
        user.LastLoginAt.Should().Be(DateTime.MinValue);
        user.Tasks.Should().NotBeNull().And.BeEmpty();
        user.Contacts.Should().NotBeNull().And.BeEmpty();
        user.Projects.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void User_Should_Allow_Setting_All_Properties()
    {
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var username = "testuser";
        var preferredLanguage = Language.es;
        var createdAt = DateTime.UtcNow;
        var lastLoginAt = DateTime.UtcNow.AddHours(-1);

        var user = new User
        {
            Id = userId,
            Email = email,
            Username = username,
            PreferredLanguage = preferredLanguage,
            CreatedAt = createdAt,
            LastLoginAt = lastLoginAt
        };

        user.Id.Should().Be(userId);
        user.Email.Should().Be(email);
        user.Username.Should().Be(username);
        user.PreferredLanguage.Should().Be(preferredLanguage);
        user.CreatedAt.Should().Be(createdAt);
        user.LastLoginAt.Should().Be(lastLoginAt);
    }

    [Fact]
    public void User_Should_Allow_Adding_Tasks()
    {
        var user = new User();
        var task1 = new WhoAndWhat.Domain.Entities.Task { Title = "Task 1" };
        var task2 = new WhoAndWhat.Domain.Entities.Task { Title = "Task 2" };

        user.Tasks.Add(task1);
        user.Tasks.Add(task2);

        user.Tasks.Should().HaveCount(2);
        user.Tasks.Should().Contain(task1);
        user.Tasks.Should().Contain(task2);
    }

    [Fact]
    public void User_Should_Allow_Adding_Contacts()
    {
        var user = new User();
        var contact1 = new Contact { Name = "Contact 1" };
        var contact2 = new Contact { Name = "Contact 2" };

        user.Contacts.Add(contact1);
        user.Contacts.Add(contact2);

        user.Contacts.Should().HaveCount(2);
        user.Contacts.Should().Contain(contact1);
        user.Contacts.Should().Contain(contact2);
    }

    [Fact]
    public void User_Should_Allow_Adding_Projects()
    {
        var user = new User();
        var project1 = new Project { Name = "Project 1" };
        var project2 = new Project { Name = "Project 2" };

        user.Projects.Add(project1);
        user.Projects.Add(project2);

        user.Projects.Should().HaveCount(2);
        user.Projects.Should().Contain(project1);
        user.Projects.Should().Contain(project2);
    }

    [Fact]
    public void User_Should_Support_English_Language()
    {
        var user = new User
        {
            PreferredLanguage = Language.en
        };

        user.PreferredLanguage.Should().Be(Language.en);
    }

    [Fact]
    public void User_Should_Support_Spanish_Language()
    {
        var user = new User
        {
            PreferredLanguage = Language.es
        };

        user.PreferredLanguage.Should().Be(Language.es);
    }
}