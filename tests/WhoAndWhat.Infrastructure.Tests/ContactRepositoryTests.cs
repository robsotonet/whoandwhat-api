using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;
using WhoAndWhat.Infrastructure.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace WhoAndWhat.Infrastructure.Tests;

public class ContactRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<Contact> _repository;
    private readonly User _testUser;

    public ContactRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<Contact>(_context);
        
        // Create test user for foreign key relationships
        _testUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = "testuser", 
            Email = "test@test.com" 
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Should_Add_Contact()
    {
        // Arrange
        var contact = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Contact", 
            Email = "contact@test.com",
            Phone = "123-456-7890",
            UserId = _testUser.Id
        };

        // Act
        await _repository.AddAsync(contact);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Contact");
        result.Email.Should().Be("contact@test.com");
        result.Phone.Should().Be("123-456-7890");
    }

    [Fact]
    public async Task Should_Get_All_Contacts()
    {
        // Arrange
        var contact1 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Contact 1", 
            UserId = _testUser.Id 
        };
        var contact2 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Contact 2", 
            UserId = _testUser.Id 
        };
        await _context.Contacts.AddRangeAsync(contact1, contact2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Get_Contact_By_Id()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var contact = new Contact 
        { 
            Id = contactId, 
            Name = "Test Contact", 
            UserId = _testUser.Id 
        };
        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(contactId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(contactId);
        result.Name.Should().Be("Test Contact");
    }

    [Fact]
    public async Task Should_Find_Contacts_By_Name()
    {
        // Arrange
        var contact1 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "John Doe", 
            UserId = _testUser.Id 
        };
        var contact2 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Jane Smith", 
            UserId = _testUser.Id 
        };
        await _context.Contacts.AddRangeAsync(contact1, contact2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(c => c.Name.Contains("John"));

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task Should_Find_Contacts_By_Email()
    {
        // Arrange
        var contact1 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Contact 1", 
            Email = "unique@test.com",
            UserId = _testUser.Id 
        };
        var contact2 = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Contact 2", 
            Email = "another@test.com",
            UserId = _testUser.Id 
        };
        await _context.Contacts.AddRangeAsync(contact1, contact2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(c => c.Email == "unique@test.com");

        // Assert
        result.Should().HaveCount(1);
        result.First().Email.Should().Be("unique@test.com");
    }

    [Fact]
    public async Task Should_Update_Contact()
    {
        // Arrange
        var contact = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Original Name", 
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(contact);
        await _repository.SaveChangesAsync();

        // Act
        contact.Name = "Updated Name";
        contact.Email = "updated@test.com";
        _repository.Update(contact);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Email.Should().Be("updated@test.com");
    }

    [Fact]
    public async Task Should_Remove_Contact()
    {
        // Arrange
        var contact = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Contact to Remove", 
            UserId = _testUser.Id 
        };
        await _repository.AddAsync(contact);
        await _repository.SaveChangesAsync();

        // Act
        _repository.Remove(contact);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Add_Contact_With_QR_Code()
    {
        // Arrange
        var contact = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "QR Contact", 
            QRCode = "QR_CODE_DATA_12345",
            UserId = _testUser.Id 
        };

        // Act
        await _repository.AddAsync(contact);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.QRCode.Should().Be("QR_CODE_DATA_12345");
    }

    [Fact]
    public async Task Should_Add_Contact_With_Invite_Code()
    {
        // Arrange
        var contact = new Contact 
        { 
            Id = Guid.NewGuid(), 
            Name = "Invite Contact", 
            InviteCode = "INVITE123",
            UserId = _testUser.Id 
        };

        // Act
        await _repository.AddAsync(contact);
        await _repository.SaveChangesAsync();

        // Assert
        var result = await _context.Contacts.FindAsync(contact.Id);
        result.Should().NotBeNull();
        result.InviteCode.Should().Be("INVITE123");
    }
}