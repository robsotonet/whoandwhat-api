using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Infrastructure.Tests;

/// <summary>
/// Integration tests for Contact repository soft delete functionality
/// </summary>
public class ContactRepositoryIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<Contact> _baseRepository;
    private readonly SoftDeleteService _softDeleteService;
    private readonly User _testUser;
    private readonly User _otherUser;

    public ContactRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _baseRepository = new Repository<Contact>(_context);
        _softDeleteService = new SoftDeleteService();
        
        // Setup test data
        SetupTestData().GetAwaiter().GetResult();
    }

    private async Task SetupTestData()
    {
        // Create test users
        _testUser = new User("test@test.com", "testuser", Language.en);
        _testUser.SetPassword("TestPassword123!");
        
        _otherUser = new User("other@test.com", "otheruser", Language.en);
        _otherUser.SetPassword("TestPassword123!");

        await _context.Users.AddRangeAsync(_testUser, _otherUser);
        await _context.SaveChangesAsync();
    }

    #region Basic Repository Operations

    [Fact]
    public async Task Should_Add_Contact_Successfully()
    {
        // Arrange
        var contact = CreateTestContact("John Doe", _testUser.Id);

        // Act
        await _baseRepository.AddAsync(contact);
        await _baseRepository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("John Doe");
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Get_Contact_By_Id()
    {
        // Arrange
        var contact = CreateTestContact("Jane Smith", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.GetByIdAsync(contact.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(contact.Id);
        result.Name.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Should_Get_All_Contacts_Excluding_Deleted()
    {
        // Arrange
        var activeContact = CreateTestContact("Active Contact", _testUser.Id);
        var deletedContact = CreateTestContact("Deleted Contact", _testUser.Id);
        deletedContact.SoftDelete();

        await _context.Contacts.AddRangeAsync(activeContact, deletedContact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.GetAllAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Name.Should().Be("Active Contact");
    }

    [Fact]
    public async Task Should_Update_Contact_Successfully()
    {
        // Arrange
        var contact = CreateTestContact("Original Name", _testUser.Id);
        await _baseRepository.AddAsync(contact);
        await _baseRepository.SaveChangesAsync();

        // Act
        contact.Name = "Updated Name";
        contact.Email = "updated@test.com";
        contact.Phone = "+1234567890";
        _baseRepository.Update(contact);
        await _baseRepository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Email.Should().Be("updated@test.com");
        result.Phone.Should().Be("+1234567890");
    }

    #endregion

    #region Contact Search and Filtering

    [Fact]
    public async Task Should_Find_Contacts_By_Name()
    {
        // Arrange
        var contact1 = CreateTestContact("John Doe", _testUser.Id);
        var contact2 = CreateTestContact("Jane Smith", _testUser.Id);
        var contact3 = CreateTestContact("Bob Johnson", _testUser.Id);

        await _context.Contacts.AddRangeAsync(contact1, contact2, contact3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.FindAsync(c => c.Name.Contains("John"));

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(c => c.Name == "John Doe");
        resultList.Should().Contain(c => c.Name == "Bob Johnson");
    }

    [Fact]
    public async Task Should_Find_Contacts_By_Email()
    {
        // Arrange
        var contact1 = CreateTestContact("John Doe", _testUser.Id);
        contact1.Email = "john@company.com";
        
        var contact2 = CreateTestContact("Jane Smith", _testUser.Id);
        contact2.Email = "jane@company.com";
        
        var contact3 = CreateTestContact("Bob Wilson", _testUser.Id);
        contact3.Email = "bob@personal.com";

        await _context.Contacts.AddRangeAsync(contact1, contact2, contact3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.FindAsync(c => c.Email.Contains("@company.com"));

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(c => c.Name == "John Doe");
        resultList.Should().Contain(c => c.Name == "Jane Smith");
    }

    [Fact]
    public async Task Should_Filter_Contacts_By_User()
    {
        // Arrange
        var userContact = CreateTestContact("User Contact", _testUser.Id);
        var otherContact = CreateTestContact("Other Contact", _otherUser.Id);

        await _context.Contacts.AddRangeAsync(userContact, otherContact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _baseRepository.FindAsync(c => c.UserId == _testUser.Id);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.First().Name.Should().Be("User Contact");
    }

    #endregion

    #region Soft Delete Operations

    [Fact]
    public async Task Should_Soft_Delete_Contact_Without_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact Without Tasks", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("has been deleted");
        
        contact.IsDeleted.Should().BeTrue();
        contact.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Should not appear in normal queries
        var normalQuery = await _baseRepository.GetByIdAsync(contact.Id);
        normalQuery.Should().BeNull();

        // Should appear in queries that ignore filters
        var deletedContact = await _context.Contacts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        deletedContact.Should().NotBeNull();
        deletedContact.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Not_Delete_Contact_With_Active_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact With Tasks", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        var task = CreateTestTask("Task With Contact", _testUser.Id);
        task.Contacts.Add(contact);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Reload contact with tasks
        contact = await _context.Contacts
            .Include(c => c.Tasks)
            .FirstAsync(c => c.Id == contact.Id);

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("associated with");
        result.Message.Should().Contain("active tasks");
        
        contact.IsDeleted.Should().BeFalse();
        contact.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_Delete_Contact_And_Remove_From_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact To Remove", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        var task = CreateTestTask("Task With Contact", _testUser.Id);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Add contact to task
        task.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        // Reload contact with tasks
        contact = await _context.Contacts
            .Include(c => c.Tasks)
            .FirstAsync(c => c.Id == contact.Id);

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact, removeFromTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        contact.IsDeleted.Should().BeTrue();
        
        // Contact should be removed from task
        var taskWithContacts = await _context.Tasks
            .Include(t => t.Contacts)
            .FirstAsync(t => t.Id == task.Id);
        taskWithContacts.Contacts.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Restore_Contact_Successfully()
    {
        // Arrange
        var contact = CreateTestContact("Contact to Restore", _testUser.Id);
        contact.SoftDelete();
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = _softDeleteService.RestoreContact(contact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("has been restored");
        
        contact.IsDeleted.Should().BeFalse();
        contact.DeletedAt.Should().BeNull();

        // Should appear in normal queries again
        var restoredContact = await _baseRepository.GetByIdAsync(contact.Id);
        restoredContact.Should().NotBeNull();
    }

    #endregion

    #region Contact Business Rules

    [Fact]
    public async Task Should_Validate_Contact_Can_Be_Deleted()
    {
        // Arrange
        var contact = CreateTestContact("Validation Test Contact", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var canDelete = contact.CanSoftDelete();

        // Assert
        canDelete.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Validate_Contact_Cannot_Be_Deleted_With_Active_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact With Tasks", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        var task = CreateTestTask("Active Task", _testUser.Id);
        task.Contacts.Add(contact);
        await _context.Tasks.AddAsync(task);
        await _context.SaveChangesAsync();

        // Reload contact with tasks
        contact = await _context.Contacts
            .Include(c => c.Tasks)
            .FirstAsync(c => c.Id == contact.Id);

        // Act
        var canDelete = contact.CanSoftDelete();

        // Assert
        canDelete.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Validate_Deleted_Contact_Can_Be_Restored()
    {
        // Arrange
        var contact = CreateTestContact("Deleted Contact", _testUser.Id);
        contact.SoftDelete();
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var canRestore = contact.CanRestore();

        // Assert
        canRestore.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Check_Active_Task_Associations()
    {
        // Arrange
        var contact = CreateTestContact("Contact With Associations", _testUser.Id);
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        var activeTask = CreateTestTask("Active Task", _testUser.Id);
        var deletedTask = CreateTestTask("Deleted Task", _testUser.Id);
        deletedTask.SoftDelete();

        activeTask.Contacts.Add(contact);
        deletedTask.Contacts.Add(contact);

        await _context.Tasks.AddRangeAsync(activeTask, deletedTask);
        await _context.SaveChangesAsync();

        // Reload contact with tasks
        contact = await _context.Contacts
            .Include(c => c.Tasks)
            .FirstAsync(c => c.Id == contact.Id);

        // Act
        var hasActiveAssociations = contact.HasActiveTaskAssociations();

        // Assert
        hasActiveAssociations.Should().BeTrue(); // Should only count active tasks
    }

    #endregion

    #region Contact Relationships and Properties

    [Fact]
    public async Task Should_Handle_Contact_Relationship_Types()
    {
        // Arrange
        var familyContact = CreateTestContact("Family Member", _testUser.Id);
        familyContact.RelationshipType = 1; // Family
        
        var businessContact = CreateTestContact("Business Contact", _testUser.Id);
        businessContact.RelationshipType = 2; // Business
        
        var friendContact = CreateTestContact("Friend", _testUser.Id);
        friendContact.RelationshipType = 3; // Friend

        await _context.Contacts.AddRangeAsync(familyContact, businessContact, friendContact);
        await _context.SaveChangesAsync();

        // Act
        var familyContacts = await _context.Contacts
            .Where(c => c.RelationshipType == 1)
            .ToListAsync();

        // Assert
        familyContacts.Should().HaveCount(1);
        familyContacts.First().Name.Should().Be("Family Member");
    }

    [Fact]
    public async Task Should_Handle_Contact_QR_Code_And_Invite_Code()
    {
        // Arrange
        var contact = CreateTestContact("QR Contact", _testUser.Id);
        contact.QRCode = "QR123456";
        contact.InviteCode = "INVITE789";

        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var contactWithCodes = await _context.Contacts
            .FirstOrDefaultAsync(c => c.QRCode == "QR123456");

        // Assert
        contactWithCodes.Should().NotBeNull();
        contactWithCodes.Name.Should().Be("QR Contact");
        contactWithCodes.InviteCode.Should().Be("INVITE789");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Should_Handle_Large_Number_Of_Contacts()
    {
        // Arrange
        var contacts = new List<Contact>();
        for (int i = 0; i < 100; i++)
        {
            contacts.Add(CreateTestContact($"Contact {i}", _testUser.Id));
        }
        
        await _context.Contacts.AddRangeAsync(contacts);
        await _context.SaveChangesAsync();

        // Act
        var start = DateTime.UtcNow;
        var result = await _context.Contacts
            .Where(c => c.UserId == _testUser.Id)
            .ToListAsync();
        var duration = DateTime.UtcNow - start;

        // Assert
        result.Should().HaveCount(100);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should be fast
    }

    [Fact]
    public async Task Should_Handle_Contact_Search_Performance()
    {
        // Arrange
        var contacts = new List<Contact>();
        for (int i = 0; i < 50; i++)
        {
            var contact = CreateTestContact($"Test Contact {i}", _testUser.Id);
            contact.Email = $"testcontact{i}@example.com";
            contacts.Add(contact);
        }
        
        await _context.Contacts.AddRangeAsync(contacts);
        await _context.SaveChangesAsync();

        // Act
        var start = DateTime.UtcNow;
        var result = await _context.Contacts
            .Where(c => c.UserId == _testUser.Id && c.Name.Contains("Contact 1"))
            .ToListAsync();
        var duration = DateTime.UtcNow - start;

        // Assert
        result.Should().HaveCountGreaterThan(0);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task Should_Not_Allow_Access_To_Other_Users_Contacts()
    {
        // Arrange
        var otherUserContact = CreateTestContact("Other User Contact", _otherUser.Id);
        await _context.Contacts.AddAsync(otherUserContact);
        await _context.SaveChangesAsync();

        // Act
        var userContacts = await _context.Contacts
            .Where(c => c.UserId == _testUser.Id)
            .ToListAsync();

        // Assert
        userContacts.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Maintain_User_Isolation_In_Soft_Delete()
    {
        // Arrange
        var userContact = CreateTestContact("User Contact", _testUser.Id);
        var otherContact = CreateTestContact("Other Contact", _otherUser.Id);
        
        await _context.Contacts.AddRangeAsync(userContact, otherContact);
        await _context.SaveChangesAsync();

        // Act - Try to delete other user's contact (should work but verify isolation)
        var result = _softDeleteService.SoftDeleteContact(otherContact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify the contact belongs to the other user
        otherContact.UserId.Should().Be(_otherUser.Id);
        otherContact.UserId.Should().NotBe(_testUser.Id);
        
        // User should still see their own contacts
        var userContacts = await _context.Contacts
            .Where(c => c.UserId == _testUser.Id)
            .ToListAsync();
        userContacts.Should().HaveCount(1);
        userContacts.First().Name.Should().Be("User Contact");
    }

    #endregion

    #region Helper Methods

    private Contact CreateTestContact(string name, Guid userId)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            Phone = "+1234567890",
            UserId = userId,
            RelationshipType = 0, // Default relationship type
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DomainTask CreateTestTask(string title, Guid userId)
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            UserId = userId,
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Medium,
            Category = (int)TaskCategory.ToDos,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}