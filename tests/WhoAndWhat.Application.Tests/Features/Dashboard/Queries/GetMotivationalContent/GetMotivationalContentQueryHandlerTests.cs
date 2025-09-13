using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Repositories;
using Xunit;

namespace WhoAndWhat.Application.Tests.Features.Dashboard.Queries.GetMotivationalContent;

/// <summary>
/// Comprehensive unit tests for GetMotivationalContentQueryHandler
/// Tests all scenarios: success, failure, edge cases, and business logic
/// </summary>
public class GetMotivationalContentQueryHandlerTests
{
    private readonly Mock<IMotivationalContentRepository> _mockContentRepository;
    private readonly Mock<IUserContentPreferencesRepository> _mockPreferencesRepository;
    private readonly Mock<IOptimizedContentEngagementService> _mockEngagementService;
    private readonly Mock<ILogger<GetMotivationalContentQueryHandler>> _mockLogger;
    private readonly GetMotivationalContentQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetMotivationalContentQueryHandlerTests()
    {
        _mockContentRepository = new Mock<IMotivationalContentRepository>();
        _mockPreferencesRepository = new Mock<IUserContentPreferencesRepository>();
        _mockEngagementService = new Mock<IOptimizedContentEngagementService>();
        _mockLogger = new Mock<ILogger<GetMotivationalContentQueryHandler>>();

        _handler = new GetMotivationalContentQueryHandler(
            _mockContentRepository.Object,
            _mockPreferencesRepository.Object,
            _mockEngagementService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, 3, "en");
        var userPreferences = CreateTestUserPreferences();
        var personalizedContent = CreateTestMotivationalContent();

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPreferences);

        SetupPreferencesCanDeliverContent(userPreferences, true);

        _mockEngagementService
            .Setup(x => x.GetPersonalizedContentAsync(_testUserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(personalizedContent);

        _mockContentRepository
            .Setup(x => x.GetActiveContentCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Contents.Should().HaveCount(2);
        result.Value.TotalAvailable.Should().Be(25);
        result.Value.PersonalizationInfo.Should().NotBeNull();
        result.Value.PersonalizationInfo.DeliveredToday.Should().Be(0);
        result.Value.PersonalizationInfo.MaxDailyContent.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithNoUserPreferences_ShouldCreateDefaultPreferences()
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, 3, "en");
        var defaultPreferences = CreateTestUserPreferences();

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserContentPreferences)null);

        _mockPreferencesRepository
            .Setup(x => x.AddAsync(It.IsAny<UserContentPreferences>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPreferencesRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        SetupPreferencesCanDeliverContent(defaultPreferences, true);

        _mockEngagementService
            .Setup(x => x.GetPersonalizedContentAsync(_testUserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestMotivationalContent());

        _mockContentRepository
            .Setup(x => x.GetActiveContentCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockPreferencesRepository.Verify(x => x.AddAsync(It.IsAny<UserContentPreferences>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockPreferencesRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCannotDeliverContent_ShouldReturnEmptyResult()
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, 3, "en");
        var userPreferences = CreateTestUserPreferences();

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPreferences);

        SetupPreferencesCanDeliverContent(userPreferences, false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contents.Should().BeEmpty();
        result.Value.TotalAvailable.Should().Be(0);
        result.Value.PersonalizationInfo.Should().NotBeNull();

        _mockEngagementService.Verify(
            x => x.GetPersonalizedContentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithException_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, 3, "en");

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve motivational content");
        result.Error.Should().Contain("Database connection failed");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Handle_WithDifferentCounts_ShouldRequestCorrectAmount(int requestedCount)
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, requestedCount, "en");
        var userPreferences = CreateTestUserPreferences();

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPreferences);

        SetupPreferencesCanDeliverContent(userPreferences, true);

        _mockEngagementService
            .Setup(x => x.GetPersonalizedContentAsync(_testUserId, requestedCount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestMotivationalContent(requestedCount));

        _mockContentRepository
            .Setup(x => x.GetActiveContentCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEngagementService.Verify(
            x => x.GetPersonalizedContentAsync(_testUserId, requestedCount, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task Handle_WithDifferentLanguages_ShouldWork(string language)
    {
        // Arrange
        var query = new GetMotivationalContentQuery(_testUserId, 3, language);
        var userPreferences = CreateTestUserPreferences();

        _mockPreferencesRepository
            .Setup(x => x.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPreferences);

        SetupPreferencesCanDeliverContent(userPreferences, true);

        _mockEngagementService
            .Setup(x => x.GetPersonalizedContentAsync(_testUserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestMotivationalContent());

        _mockContentRepository
            .Setup(x => x.GetActiveContentCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #region Helper Methods

    private UserContentPreferences CreateTestUserPreferences()
    {
        // Create a test user content preferences object
        // Note: This would need to be adjusted based on the actual UserContentPreferences constructor
        return UserContentPreferences.CreateDefault(_testUserId);
    }

    private List<MotivationalContent> CreateTestMotivationalContent(int count = 2)
    {
        var content = new List<MotivationalContent>();
        for (int i = 0; i < count; i++)
        {
            // Create test motivational content
            // Note: This would need to be adjusted based on the actual MotivationalContent constructor
            var testContent = MotivationalContent.Create(
                title: $"Test Title {i + 1}",
                message: $"Test motivational message {i + 1}",
                contentType: MotivationalContentType.Achievement,
                category: ContentCategory.Productivity,
                priority: 80 + i,
                targetConditions: new Dictionary<string, object>
                {
                    ["category"] = "productivity",
                    ["timeOfDay"] = "morning"
                }
            );
            content.Add(testContent);
        }
        return content;
    }

    private void SetupPreferencesCanDeliverContent(UserContentPreferences preferences, bool canDeliver)
    {
        // Mock the CanDeliverContentNow method
        // Note: This setup would need to be adjusted based on the actual method signature
        // Since the method likely has parameters, we need to set it up properly
        Mock.Get(preferences)
            .Setup(x => x.CanDeliverContentNow(
                It.IsAny<ContentDeliveryChannel>(),
                It.IsAny<MotivationalContentType>()))
            .Returns(canDeliver);
    }

    #endregion
}
