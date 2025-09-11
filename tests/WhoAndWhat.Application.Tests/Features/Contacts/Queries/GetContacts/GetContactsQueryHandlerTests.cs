using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Features.Contacts.Queries.GetContacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Contacts.Queries.GetContacts;

public class GetContactsQueryHandlerTests
{
    private readonly Mock<IContactRepository> _mockContactRepository;
    private readonly Mock<ILogger<GetContactsQueryHandler>> _mockLogger;
    private readonly GetContactsQueryHandler _handler;

    public GetContactsQueryHandlerTests()
    {
        _mockContactRepository = new Mock<IContactRepository>();
        _mockLogger = new Mock<ILogger<GetContactsQueryHandler>>();
        _handler = new GetContactsQueryHandler(
            _mockContactRepository.Object,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a list of test contacts for pagination and search testing
    /// </summary>
    private static List<Contact> CreateTestContacts(Guid userId)
    {
        return new List<Contact>
        {
            new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Alice Johnson",
                Email = "alice.johnson@example.com",
                Phone = "+1234567890",
                RelationshipType = 1, // Friend
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                IsDeleted = false,
                Tasks = new List<AppTask>()
            },
            new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Bob Smith",
                Email = "bob.smith@example.com",
                Phone = "+1987654321",
                RelationshipType = 2, // Family
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
                IsDeleted = false,
                Tasks = new List<AppTask>()
            },
            new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Charlie Brown",
                Email = "charlie.brown@example.com",
                Phone = "+1555666777",
                RelationshipType = 3, // Colleague
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                IsDeleted = false,
                Tasks = new List<AppTask>()
            },
            new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Diana Prince",
                Email = "diana.prince@example.com",
                Phone = "+1444555666",
                RelationshipType = 1, // Friend
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                IsDeleted = false,
                Tasks = new List<AppTask>()
            },
            new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Eve Adams",
                Email = "eve.adams@example.com",
                Phone = "+1333444555",
                RelationshipType = 4, // Business
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = true, // Soft deleted
                DeletedAt = DateTime.UtcNow.AddHours(-2),
                Tasks = new List<AppTask>()
            }
        };
    }

    /// <summary>
    /// Creates a list of test contacts with the specified count for pagination testing
    /// </summary>
    private static List<Contact> CreateTestContactsWithCount(Guid userId, int count)
    {
        var contacts = new List<Contact>();
        for (int i = 0; i < count; i++)
        {
            contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = $"Test Contact {i + 1}",
                Email = $"contact{i + 1}@example.com",
                Phone = $"+1{i:000}000{i:0000}",
                RelationshipType = (i % 4) + 1, // Cycle through relationship types 1-4
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                UpdatedAt = DateTime.UtcNow.AddDays(-i / 2.0),
                IsDeleted = false,
                Tasks = new List<AppTask>()
            });
        }
        return contacts;
    }

    /// <summary>
    /// Sets up successful repository mocks for pagination queries
    /// </summary>
    private void SetupSuccessfulGetContactsRepositoryMocks(List<Contact> contacts)
    {
        _mockContactRepository.Setup(x => x.FindContactsAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(contacts);
    }

    private static GetContactsQuery CreateValidGetContactsQuery(Guid? userId = null, int pageNumber = 1, int pageSize = 10) => new(
        UserId: userId ?? Guid.NewGuid(),
        Search: null,
        RelationshipTypes: null,
        IncludeDeleted: false,
        SortBy: "Name",
        SortDescending: false,
        PageSize: pageSize,
        PageNumber: pageNumber
    );

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task Handle_Should_Return_Paginated_Contacts_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testContacts = CreateTestContacts(userId).Where(c => !c.IsDeleted).Take(3).ToList();
        var query = CreateValidGetContactsQuery(userId, pageNumber: 1, pageSize: 10);
        SetupSuccessfulGetContactsRepositoryMocks(testContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Contacts.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalPages.Should().Be(1);
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Result_When_No_Contacts_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = CreateValidGetContactsQuery(userId);
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Contacts.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Map_Contact_Properties_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testContacts = CreateTestContacts(userId).Take(1).ToList();
        var query = CreateValidGetContactsQuery(userId);
        SetupSuccessfulGetContactsRepositoryMocks(testContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var contactDto = result.Value.Contacts.First();
        var originalContact = testContacts.First();
        
        contactDto.Id.Should().Be(originalContact.Id);
        contactDto.Name.Should().Be(originalContact.Name);
        contactDto.Email.Should().Be(originalContact.Email);
        contactDto.Phone.Should().Be(originalContact.Phone);
        contactDto.RelationshipType.Should().Be(originalContact.RelationshipType);
        contactDto.RelationshipTypeName.Should().Be(((ContactRelationType)originalContact.RelationshipType).ToString());
        contactDto.CreatedAt.Should().Be(originalContact.CreatedAt);
        contactDto.UpdatedAt.Should().Be(originalContact.UpdatedAt);
        contactDto.IsDeleted.Should().Be(originalContact.IsDeleted);
    }

    #endregion

    #region Pagination Scenarios

    [Fact]
    public async Task Handle_Should_Handle_Different_Page_Sizes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testContacts = CreateTestContacts(userId).Where(c => !c.IsDeleted).Take(2).ToList();
        
        // Test with page size of 2
        var query = CreateValidGetContactsQuery(userId, pageNumber: 1, pageSize: 2);
        SetupSuccessfulGetContactsRepositoryMocks(testContacts); // Total 4 contacts

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(4);
        result.Value.PageSize.Should().Be(2);
        result.Value.TotalPages.Should().Be(2); // 4 total / 2 page size = 2 pages
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Different_Page_Numbers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testContacts = CreateTestContacts(userId).Where(c => !c.IsDeleted).Skip(2).Take(2).ToList();
        
        // Test page 2 with page size of 2
        var query = CreateValidGetContactsQuery(userId, pageNumber: 2, pageSize: 2);
        SetupSuccessfulGetContactsRepositoryMocks(testContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.TotalPages.Should().Be(2);
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Calculate_Total_Pages_Correctly()
    {
        // Test various total count and page size combinations
        var testCases = new[]
        {
            new { TotalCount = 0, PageSize = 10, ExpectedPages = 0 },
            new { TotalCount = 5, PageSize = 10, ExpectedPages = 1 },
            new { TotalCount = 10, PageSize = 10, ExpectedPages = 1 },
            new { TotalCount = 11, PageSize = 10, ExpectedPages = 2 },
            new { TotalCount = 25, PageSize = 10, ExpectedPages = 3 },
            new { TotalCount = 30, PageSize = 10, ExpectedPages = 3 }
        };

        foreach (var testCase in testCases)
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = CreateValidGetContactsQuery(userId, pageSize: testCase.PageSize);
            
            // Create the right number of contacts for each test case
            var contacts = CreateTestContactsWithCount(userId, testCase.TotalCount);
            SetupSuccessfulGetContactsRepositoryMocks(contacts);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TotalPages.Should().Be(testCase.ExpectedPages, 
                $"Expected {testCase.ExpectedPages} pages for {testCase.TotalCount} total items with page size {testCase.PageSize}");
        }
    }

    #endregion

    #region Search Scenarios

    [Fact]
    public async Task Handle_Should_Search_By_Name()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchTerm = "Alice";
        var matchingContact = CreateTestContacts(userId).Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
        var query = new GetContactsQuery(
            UserId: userId,
            Search: searchTerm,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(matchingContact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().HaveCount(1);
        result.Value.Contacts.First().Name.Should().Contain("Alice");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            searchTerm, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Search_By_Email()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchTerm = "smith";
        var matchingContact = CreateTestContacts(userId).Where(c => c.Email != null && c.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
        var query = new GetContactsQuery(
            UserId: userId,
            Search: searchTerm,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(matchingContact);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().HaveCount(1);
        result.Value.Contacts.First().Email.Should().Contain("smith");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            searchTerm, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_For_Non_Matching_Search()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchTerm = "NonExistentContact";
        var query = new GetContactsQuery(
            UserId: userId,
            Search: searchTerm,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    #endregion

    #region Filtering Scenarios

    [Fact]
    public async Task Handle_Should_Include_Deleted_Contacts_When_Specified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var allContacts = CreateTestContacts(userId); // Includes both active and deleted
        var query = new GetContactsQuery(
            UserId: userId,
            Search: null,
            RelationshipTypes: null,
            IncludeDeleted: true, // Include deleted contacts
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(allContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().HaveCount(allContacts.Count);
        result.Value.Contacts.Should().Contain(c => c.IsDeleted); // Should include soft-deleted contacts
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Filter_By_Relationship_Type()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var relationshipType = 1; // Friends
        var friendContacts = CreateTestContacts(userId).Where(c => c.RelationshipType == relationshipType).ToList();
        var query = new GetContactsQuery(
            UserId: userId,
            Search: null,
            RelationshipTypes: new List<int> { relationshipType },
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(friendContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().OnlyContain(c => c.RelationshipType == relationshipType);
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Exclude_Deleted_Contacts_By_Default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeContacts = CreateTestContacts(userId).Where(c => !c.IsDeleted).ToList();
        var query = CreateValidGetContactsQuery(userId);
        SetupSuccessfulGetContactsRepositoryMocks(activeContacts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().NotContain(c => c.IsDeleted);
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            string.Empty, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Is_Empty()
    {
        // Arrange
        var query = new GetContactsQuery(
            UserId: Guid.Empty,
            Search: null,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("User ID is required");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task Handle_Should_Return_Failure_When_PageNumber_Is_Invalid(int invalidPageNumber)
    {
        // Arrange
        var query = new GetContactsQuery(
            UserId: Guid.NewGuid(),
            Search: null,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: invalidPageNumber
        );

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Page number must be greater than 0");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)] // Assuming max page size is 100
    public async Task Handle_Should_Return_Failure_When_PageSize_Is_Invalid(int invalidPageSize)
    {
        // Arrange
        var query = new GetContactsQuery(
            UserId: Guid.NewGuid(),
            Search: null,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: invalidPageSize,
            PageNumber: 1
        );

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Page size must be between 1 and 100");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Repository Failure Scenarios

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var query = CreateValidGetContactsQuery();
        _mockContactRepository.Setup(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while retrieving contacts");
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_Should_Handle_Maximum_Page_Size()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = CreateValidGetContactsQuery(userId, pageSize: 100); // Maximum allowed
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Should_Handle_Empty_Search_Terms(string emptySearchTerm)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetContactsQuery(
            UserId: userId,
            Search: emptySearchTerm,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Should pass null or trimmed search term to repository
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.Is<string>(s => string.IsNullOrWhiteSpace(s) || string.IsNullOrEmpty(s)), userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Handle_Very_Long_Search_Terms()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var longSearchTerm = new string('a', 500);
        var query = new GetContactsQuery(
            UserId: userId,
            Search: longSearchTerm,
            RelationshipTypes: null,
            IncludeDeleted: false,
            SortBy: "Name",
            SortDescending: false,
            PageSize: 10,
            PageNumber: 1
        );
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            longSearchTerm, userId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logging Verification

    [Fact]
    public async Task Handle_Should_Log_Successful_Query()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = CreateValidGetContactsQuery(userId);
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Log_Errors()
    {
        // Arrange
        var query = CreateValidGetContactsQuery();
        _mockContactRepository.Setup(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving contacts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Scenarios

    [Fact]
    public async Task Handle_Should_Pass_Cancellation_Token_To_Repository()
    {
        // Arrange
        var query = CreateValidGetContactsQuery();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetupSuccessfulGetContactsRepositoryMocks(new List<Contact>());

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockContactRepository.Verify(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var query = CreateValidGetContactsQuery();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately
        
        _mockContactRepository.Setup(x => x.FindContactsAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.Handle(query, cancellationTokenSource.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("An error occurred while retrieving contacts");
    }

    #endregion
}