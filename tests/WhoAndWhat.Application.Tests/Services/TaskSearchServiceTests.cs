using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Application.Configuration;
using Xunit;

namespace WhoAndWhat.Application.Tests.Services;

/// <summary>
/// Unit tests for TaskSearchService
/// </summary>
public class TaskSearchServiceTests
{
    private readonly Mock<ITaskSearchRepository> _mockSearchRepository;
    private readonly Mock<ITaskCacheService> _mockCacheService;
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<TaskSearchService>> _mockLogger;
    private readonly ICacheSettings _cacheSettings;
    private readonly TaskSearchService _searchService;
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskSearchServiceTests()
    {
        _mockSearchRepository = new Mock<ITaskSearchRepository>();
        _mockCacheService = new Mock<ITaskCacheService>();
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<TaskSearchService>>();

        _cacheSettings = new CacheSettings
        {
            TaskListCacheExpirationMinutes = 5,
            DefaultExpirationMinutes = 30,
            KeyPrefix = "test"
        };

        var options = Options.Create(_cacheSettings);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _searchService = new TaskSearchService(
            _mockSearchRepository.Object,
            _mockCacheService.Object,
            _mockDistributedCache.Object,
            options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SearchTasksAsync_WithValidCriteria_ShouldReturnResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var criteria = new AppTaskSearchCriteria { Query = "test", PageSize = 10 };
        var expectedResult = CreateSampleSearchResult();

        _mockDistributedCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Cache miss

        _mockSearchRepository
            .Setup(x => x.SearchTasksAsync(userId, It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        _mockDistributedCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _searchService.SearchTasksAsync(userId, criteria);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(expectedResult.TotalCount);
        result.Tasks.Should().HaveCount(expectedResult.Tasks.Count());
        result.Metadata.FromCache.Should().BeFalse();

        _mockSearchRepository.Verify(x => x.SearchTasksAsync(userId, It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDistributedCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTasksAsync_WithCachedResult_ShouldReturnFromCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var criteria = new AppTaskSearchCriteria { Query = "test", PageSize = 10 };
        var cachedResult = CreateSampleSearchResult();
        var serializedResult = JsonSerializer.Serialize(cachedResult, _jsonOptions);

        _mockDistributedCache
            .Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedResult);

        _mockSearchRepository
            .Setup(x => x.RecordSearchQueryAsync(It.IsAny<Guid>(), It.IsAny<AppTaskSearchCriteria>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _searchService.SearchTasksAsync(userId, criteria);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(cachedResult.TotalCount);
        result.Metadata.FromCache.Should().BeTrue();

        _mockSearchRepository.Verify(x => x.SearchTasksAsync(It.IsAny<Guid>(), It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockSearchRepository.Verify(x => x.RecordSearchQueryAsync(userId, It.IsAny<AppTaskSearchCriteria>(), cachedResult.TotalCount, It.IsAny<TimeSpan>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTasksAsync_WithInvalidCriteria_ShouldReturnEmptyResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invalidCriteria = new AppTaskSearchCriteria { PageSize = 0 }; // Invalid

        // Act
        var result = await _searchService.SearchTasksAsync(userId, invalidCriteria);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Tasks.Should().BeEmpty();

        _mockSearchRepository.Verify(x => x.SearchTasksAsync(It.IsAny<Guid>(), It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSearchSuggestionsAsync_WithValidQuery_ShouldReturnSuggestions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = "test";
        var expectedSuggestions = new[] { "test task", "test project", "testing" };

        _mockDistributedCache
            .Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // Cache miss

        _mockSearchRepository
            .Setup(x => x.GetSearchSuggestionsAsync(userId, query, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSuggestions);

        // Act
        var result = await _searchService.GetSearchSuggestionsAsync(userId, query);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain("test task");
        result.Should().Contain("test project");
        result.Should().Contain("testing");

        _mockSearchRepository.Verify(x => x.GetSearchSuggestionsAsync(userId, query, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task GetSearchSuggestionsAsync_WithInvalidQuery_ShouldReturnEmpty(string query)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _searchService.GetSearchSuggestionsAsync(userId, query);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockSearchRepository.Verify(x => x.GetSearchSuggestionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ValidateSearchCriteria_WithValidCriteria_ShouldReturnValid()
    {
        // Arrange
        var validCriteria = new AppTaskSearchCriteria { Query = "test", PageSize = 20 };

        // Act
        var result = _searchService.ValidateSearchCriteria(validCriteria);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NormalizedCriteria.Should().NotBeNull();
    }

    [Fact]
    public void ValidateSearchCriteria_WithInvalidCriteria_ShouldReturnInvalid()
    {
        // Arrange
        var invalidCriteria = new AppTaskSearchCriteria { PageSize = 0 };

        // Act
        var result = _searchService.ValidateSearchCriteria(invalidCriteria);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.NormalizedCriteria.Should().BeNull();
    }

    [Fact]
    public async Task WarmSearchCacheAsync_ShouldExecuteCommonSearches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchResult = CreateSampleSearchResult();

        _mockDistributedCache
            .Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // Always cache miss for warming

        _mockSearchRepository
            .Setup(x => x.SearchTasksAsync(userId, It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        // Act
        var warmedCount = await _searchService.WarmSearchCacheAsync(userId);

        // Assert
        warmedCount.Should().BeGreaterThan(0);
        _mockSearchRepository.Verify(x => x.SearchTasksAsync(userId, It.IsAny<AppTaskSearchCriteria>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private TaskSearchResult CreateSampleSearchResult()
    {
        var tasks = new[]
        {
            new TaskSearchItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Task 1",
                Description = "Test description",
                Priority = 1,
                Category = 1,
                Status = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow,
                UserId = Guid.NewGuid(),
                RelevanceScore = 0.95
            },
            new TaskSearchItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Task 2",
                Description = "Another test",
                Priority = 2,
                Category = 0,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddHours(-1),
                UserId = Guid.NewGuid(),
                RelevanceScore = 0.85
            }
        };

        return TaskSearchResult.Create(
            tasks,
            2,
            1,
            20,
            TimeSpan.FromMilliseconds(150),
            "test",
            new SearchResultMetadata
            {
                FromCache = false,
                DatabaseHits = 1,
                DatabaseDuration = TimeSpan.FromMilliseconds(100)
            });
    }
}