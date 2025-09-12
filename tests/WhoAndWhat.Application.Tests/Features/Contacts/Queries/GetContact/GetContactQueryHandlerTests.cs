using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Queries.GetContact;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Queries;

public class GetContactQueryHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<GetContactQueryHandler>> _mockLogger;
    private readonly GetContactQueryHandler _handler;

    public GetContactQueryHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<GetContactQueryHandler>>();
        _handler = new GetContactQueryHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test contact with basic information
    /// </summary>
    private static Contact CreateTestContact(Guid? contactId = null, Guid? userId = null)
    {
        return new Contact
        {
            Id = contactId ?? Guid.NewGuid(),
            Name = "Test Contact",
            Email = "test@example.com",
            Phone = "+1234567890",
            QRCode = "QR12345",
            InviteCode = "INV12345",
            RelationshipType = 1, // Friend
            UserId = userId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false,
            DeletedAt = null,
            Tasks = new List<DomainTask>(),
            TaskContacts = new List<TaskContact>()
        };
    }

    /// <summary>
    /// Creates a test contact with associated tasks
    /// </summary>
    private static Contact CreateContactWithTasks(Guid? contactId = null, Guid? userId = null)
    {
        var contact = CreateTestContact(contactId, userId);

        var task1 = new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Task 1",
            Description = "Test task 1",
            Category = (int)AppTaskCategory.ToDo,
            Priority = (int)Priority.Medium,
            Status = (int)AppTaskStatus.Pending,
            UserId = contact.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var task2 = new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Task 2",
            Description = "Test task 2",
            Category = (int)AppTaskCategory.Idea,
            Priority = (int)Priority.High,
            Status = (int)AppTaskStatus.Completed,
            UserId = contact.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        contact.Tasks = new List<DomainTask> { task1, task2 };
        contact.TaskContacts = new List<TaskContact>
        {
            new TaskContact
            {
                Id = Guid.NewGuid(),
                TaskId = task1.Id,
                ContactId = contact.Id,
                Role = "Owner",
                LinkedAt = DateTime.UtcNow.AddDays(-5),
                Notes = "Primary owner"
            },
            new TaskContact
            {
                Id = Guid.NewGuid(),
                TaskId = task2.Id,
                ContactId = contact.Id,
                Role = "Collaborator",
                LinkedAt = DateTime.UtcNow.AddDays(-3),
                Notes = "Helper"
            }
        };

        return contact;
    }

    /// <summary>
    /// Creates a soft-deleted test contact
    /// </summary>
    private static Contact CreateSoftDeletedContact(Guid? contactId = null, Guid? userId = null)
    {
        var contact = CreateTestContact(contactId, userId);
        contact.IsDeleted = true;
        contact.DeletedAt = DateTime.UtcNow.AddDays(-1);
        return contact;
    }

    private static GetContactQuery CreateValidQuery(Guid? contactId = null, Guid? userId = null, bool includeDeleted = false, bool includeTasks = false) =>
        new(contactId ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), includeDeleted, includeTasks);

    #endregion

    #region Success Scenarios - Basic Contact Retrieval

    [Fact]
    public async Task Handle_Should_Return_Contact_Successfully()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(contactId);
        result.Value.Name.Should().Be("Test Contact");
        result.Value.Email.Should().Be("test@example.com");
        result.Value.Phone.Should().Be("+1234567890");
        result.Value.QRCode.Should().Be("QR12345");
        result.Value.InviteCode.Should().Be("INV12345");
        result.Value.RelationshipType.Should().Be(1);
        result.Value.CreatedAt.Should().Be(contact.CreatedAt);
        result.Value.UpdatedAt.Should().Be(contact.UpdatedAt);
        result.Value.IsDeleted.Should().BeFalse();
        result.Value.DeletedAt.Should().BeNull();
        result.Value.AssociatedTasks.Should().BeNull(); // Not included by default

        _mockContactRepository.Verify(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Filter_Contact_By_User()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);
        var query = CreateValidQuery(contactId, differentUserId); // Different user

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
    }

    [Fact]
    public async Task Handle_Should_Return_Contact_With_All_Properties_Mapped_Correctly()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            Name = "Detailed Contact",
            Email = "detailed@example.com",
            Phone = "+0987654321",
            QRCode = "DETAILED_QR",
            InviteCode = "DETAILED_INV",
            RelationshipType = 3, // Family
            UserId = userId,
            CreatedAt = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2023, 2, 20, 15, 45, 0, DateTimeKind.Utc),
            IsDeleted = false,
            DeletedAt = null
        };
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Id.Should().Be(contactId);
        dto.Name.Should().Be("Detailed Contact");
        dto.Email.Should().Be("detailed@example.com");
        dto.Phone.Should().Be("+0987654321");
        dto.QRCode.Should().Be("DETAILED_QR");
        dto.InviteCode.Should().Be("DETAILED_INV");
        dto.RelationshipType.Should().Be(3);
        dto.CreatedAt.Should().Be(new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        dto.UpdatedAt.Should().Be(new DateTime(2023, 2, 20, 15, 45, 0, DateTimeKind.Utc));
        dto.IsDeleted.Should().BeFalse();
        dto.DeletedAt.Should().BeNull();
    }

    #endregion

    #region Success Scenarios - With Tasks

    [Fact]
    public async Task Handle_Should_Return_Contact_With_Tasks_When_Requested()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contactWithTasks = CreateContactWithTasks(contactId, userId);
        var query = CreateValidQuery(contactId, userId, includeTasks: true);

        _mockContactRepository.Setup(x => x.GetContactWithTasksAsync(contactId, userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactWithTasks);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AssociatedTasks.Should().NotBeNull();
        result.Value.AssociatedTasks.Should().HaveCount(2);

        var task1 = result.Value.AssociatedTasks.FirstOrDefault(t => t.TaskTitle == "Task 1");
        task1.Should().NotBeNull();
        task1!.TaskStatus.Should().Be((int)AppTaskStatus.Pending);
        task1.Role.Should().Be("Owner");

        var task2 = result.Value.AssociatedTasks.FirstOrDefault(t => t.TaskTitle == "Task 2");
        task2.Should().NotBeNull();
        task2!.TaskStatus.Should().Be((int)AppTaskStatus.Completed);
        task2.Role.Should().Be("Collaborator");

        _mockContactRepository.Verify(x => x.GetContactWithTasksAsync(contactId, userId, false, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Use_Correct_Repository_Method_Based_On_Parameters()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);

        // Test without tasks
        var queryWithoutTasks = CreateValidQuery(contactId, userId, includeTasks: false);
        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        await _handler.Handle(queryWithoutTasks, CancellationToken.None);

        // Assert
        _mockContactRepository.Verify(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.GetContactWithTasksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Success Scenarios - Soft Deleted Contacts

    [Fact]
    public async Task Handle_Should_Return_Soft_Deleted_Contact_When_Requested()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId, includeTasks: false, includeDeleted: true);

        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(contactId);
        result.Value.IsDeleted.Should().BeTrue();
        result.Value.DeletedAt.Should().Be(deletedContact.DeletedAt);

        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Use_Correct_Repository_Method_For_Deleted_Contacts()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deletedContact = CreateSoftDeletedContact(contactId, userId);

        // Test including deleted
        var queryIncludingDeleted = CreateValidQuery(contactId, userId, includeTasks: false, includeDeleted: true);
        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedContact);

        // Act
        await _handler.Handle(queryIncludingDeleted, CancellationToken.None);

        // Assert
        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockContactRepository.Verify(x => x.GetContactWithTasksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Contact Not Found Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_Not_Found()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");

        _mockContactRepository.Verify(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Contact_With_Tasks_Not_Found()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId, includeTasks: true);

        _mockContactRepository.Setup(x => x.GetContactWithTasksAsync(contactId, userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");

        _mockContactRepository.Verify(x => x.GetContactWithTasksAsync(contactId, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Deleted_Contact_Not_Found()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId, includeDeleted: true);

        _mockContactRepository.Setup(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");

        _mockContactRepository.Verify(x => x.GetContactIncludingDeletedAsync(contactId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region User Authorization Scenarios

    [Fact]
    public async Task Handle_Should_Not_Return_Contact_Belonging_To_Different_User()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var contactOwnerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid(); // Different user
        var contact = CreateTestContact(contactId, contactOwnerId);
        var query = CreateValidQuery(contactId, requestingUserId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
    }

    #endregion

    #region Repository Exception Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var query = CreateValidQuery();

        _mockContactRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving the contact");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_With_Tasks_Throws_Exception()
    {
        // Arrange
        var query = CreateValidQuery(includeTasks: true);

        _mockContactRepository.Setup(x => x.GetContactWithTasksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving the contact");
    }

    #endregion

    #region Validation Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_ContactId_Is_Empty()
    {
        // Arrange
        var query = new GetContactQuery(Guid.Empty, Guid.NewGuid(), false, false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");

        _mockContactRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var query = new GetContactQuery(Guid.NewGuid(), Guid.Empty, false, false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Contact not found");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_Null_Optional_Fields()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            Name = "Minimal Contact",
            Email = null, // Null email
            Phone = null, // Null phone
            QRCode = null, // Null QR code
            InviteCode = null, // Null invite code
            RelationshipType = 0,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Minimal Contact");
        result.Value.Email.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.QRCode.Should().BeNull();
        result.Value.InviteCode.Should().BeNull();
        result.Value.RelationshipType.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Handle_Contact_With_Empty_Task_List()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);
        contact.Tasks = new List<DomainTask>(); // Empty list
        contact.TaskContacts = new List<TaskContact>(); // Empty list
        var query = CreateValidQuery(contactId, userId, includeTasks: true);

        _mockContactRepository.Setup(x => x.GetContactWithTasksAsync(contactId, userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedTasks.Should().NotBeNull();
        result.Value.AssociatedTasks.Should().BeEmpty();
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, cancellationToken))
            .ReturnsAsync(contact);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.GetByIdAsync(contactId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var query = CreateValidQuery();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately

        _mockContactRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(query, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while retrieving the contact");
    }

    #endregion

    #region Logging Scenarios

    [Fact]
    public async Task Handle_Should_Log_Information_On_Successful_Retrieval()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contact = CreateTestContact(contactId, userId);
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting contact {contactId} for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Warning_When_Contact_Not_Found()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact)null!);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Contact {contactId} not found for user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Error_On_Exception()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = CreateValidQuery(contactId, userId);
        var expectedException = new Exception("Database error");

        _mockContactRepository.Setup(x => x.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error retrieving contact {contactId} for user {userId}")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
