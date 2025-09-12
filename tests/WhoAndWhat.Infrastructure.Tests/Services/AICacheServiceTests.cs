using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

public class AICacheServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDistributedCache> _distributedCacheMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<AICacheService>> _loggerMock;
    private readonly AICacheService _aiCacheService;
    private readonly RedisCacheSettings _cacheSettings;
    private bool _disposed;

    public AICacheServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _distributedCacheMock = new Mock<IDistributedCache>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<AICacheService>>();

        _cacheSettings = new RedisCacheSettings
        {
            DatabaseIndex = 0,
            KeyPrefix = "whoandwhat",
            DefaultExpirationMinutes = 60,
            EnablePerformanceMonitoring = true
        };

        var optionsMock = new Mock<IOptions<RedisCacheSettings>>();
        optionsMock.Setup(x => x.Value).Returns(_cacheSettings);

        _redisMock.Setup(x => x.GetDatabase(_cacheSettings.DatabaseIndex, It.IsAny<object>()))
                 .Returns(_databaseMock.Object);

        _aiCacheService = new AICacheService(
            _redisMock.Object,
            _distributedCacheMock.Object,
            optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CacheDayPlanAsync_Should_Return_True_When_Successful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planDate = DateTime.Today;
        var dayPlan = new AIGeneratedPlan(
            UserId: userId,
            PlanDate: planDate,
            ScheduledTasks: new List<ScheduledTaskBlock>(),
            TimeBlocks: new List<TimeBlockRecommendation>(),
            ProductivityTips: new List<string> { "Test tip" },
            AnalysisMetadata: new AIAnalysisMetadata(
                ModelUsed: "gpt-4o",
                ModelVersion: "1.0",
                DataSourcesUsed: new List<string>(),
                ProcessingStartTime: DateTime.UtcNow,
                ProcessingDuration: TimeSpan.FromMilliseconds(100),
                ModelParameters: new Dictionary<string, object>()
            ),
            ConfidenceScore: 0.9,
            GeneratedAt: DateTime.UtcNow
        );

        _distributedCacheMock.Setup(x => x.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _aiCacheService.CacheDayPlanAsync(dayPlan, 180);

        // Assert
        result.Should().BeTrue();
        _distributedCacheMock.Verify(x => x.SetStringAsync(
            It.Is<string>(key => key.Contains($"ai:day-plan:{userId}:{planDate:yyyy-MM-dd}")),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheDayPlanAsync_Should_Return_False_When_Exception_Occurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planDate = DateTime.Today;
        var dayPlan = new AIGeneratedPlan(
            UserId: userId,
            PlanDate: planDate,
            ScheduledTasks: new List<ScheduledTaskBlock>(),
            TimeBlocks: new List<TimeBlockRecommendation>(),
            ProductivityTips: new List<string>(),
            AnalysisMetadata: new AIAnalysisMetadata(
                ModelUsed: "test",
                ModelVersion: "1.0",
                DataSourcesUsed: new List<string>(),
                ProcessingStartTime: DateTime.UtcNow,
                ProcessingDuration: TimeSpan.FromMilliseconds(100),
                ModelParameters: new Dictionary<string, object>()
            ),
            ConfidenceScore: 0.9,
            GeneratedAt: DateTime.UtcNow
        );

        _distributedCacheMock.Setup(x => x.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache error"));

        // Act
        var result = await _aiCacheService.CacheDayPlanAsync(dayPlan, 180);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDayPlanAsync_Should_Return_Plan_When_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planDate = DateTime.Today;
        var dayPlan = new AIGeneratedPlan(
            UserId: userId,
            PlanDate: planDate,
            ScheduledTasks: new List<ScheduledTaskBlock>(),
            TimeBlocks: new List<TimeBlockRecommendation>(),
            ProductivityTips: new List<string> { "Test tip" },
            AnalysisMetadata: new AIAnalysisMetadata(
                ModelUsed: "gpt-4o",
                ModelVersion: "1.0",
                DataSourcesUsed: new List<string>(),
                ProcessingStartTime: DateTime.UtcNow,
                ProcessingDuration: TimeSpan.FromMilliseconds(100),
                ModelParameters: new Dictionary<string, object>()
            ),
            ConfidenceScore: 0.9,
            GeneratedAt: DateTime.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(dayPlan);
        _distributedCacheMock.Setup(x => x.GetStringAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await _aiCacheService.GetCachedDayPlanAsync(userId, planDate);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.PlanDate.Should().Be(planDate);
    }

    [Fact]
    public async Task GetCachedDayPlanAsync_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planDate = DateTime.Today;

        _distributedCacheMock.Setup(x => x.GetStringAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _aiCacheService.GetCachedDayPlanAsync(userId, planDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CachePrioritySuggestionAsync_Should_Return_True_When_Successful()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var suggestion = new TaskPrioritySuggestion(
            TaskId: taskId,
            SuggestedPriority: "High",
            ConfidenceScore: 0.85,
            AIReasoning: "Test reasoning",
            InfluencingFactors: new List<string> { "Due date" },
            SuggestionCreatedAt: DateTime.UtcNow
        );

        _distributedCacheMock.Setup(x => x.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _aiCacheService.CachePrioritySuggestionAsync(suggestion, 30);

        // Assert
        result.Should().BeTrue();
        _distributedCacheMock.Verify(x => x.SetStringAsync(
            It.Is<string>(key => key.Contains($"ai:priority:{taskId}")),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCachedPrioritySuggestionAsync_Should_Return_Suggestion_When_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var suggestion = new TaskPrioritySuggestion(
            TaskId: taskId,
            SuggestedPriority: "High",
            ConfidenceScore: 0.85,
            AIReasoning: "Test reasoning",
            InfluencingFactors: new List<string> { "Due date" },
            SuggestionCreatedAt: DateTime.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(suggestion);
        _distributedCacheMock.Setup(x => x.GetStringAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await _aiCacheService.GetCachedPrioritySuggestionAsync(taskId);

        // Assert
        result.Should().NotBeNull();
        result!.TaskId.Should().Be(taskId);
        result.SuggestedPriority.Should().Be("High");
    }

    [Fact]
    public async Task CacheCategorizationSuggestionsAsync_Should_Use_Content_Hash()
    {
        // Arrange
        var taskContent = "Review quarterly reports";
        var suggestions = new List<CategorySuggestion>
        {
            new CategorySuggestion(
                SuggestedCategory: "Work",
                ConfidenceScore: 0.9,
                Reasoning: "Work-related content",
                AlternativeCategories: new List<string> { "Administrative" },
                CategoryProbabilities: new Dictionary<string, double> { ["Work"] = 0.9 }
            )
        };

        _distributedCacheMock.Setup(x => x.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _aiCacheService.CacheCategorizationSuggestionsAsync(taskContent, suggestions, 60);

        // Assert
        result.Should().BeTrue();
        _distributedCacheMock.Verify(x => x.SetStringAsync(
            It.Is<string>(key => key.Contains("ai:categorization:") && key.Length > 20),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateUserAICacheAsync_Should_Return_True()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var serverMock = new Mock<IServer>();
        var endPoints = new EndPoint[] { new DnsEndPoint("localhost", 6379) };

        _redisMock.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(endPoints);
        _redisMock.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(serverMock.Object);

        // Mock the SCAN command to return empty results
        serverMock.Setup(x => x.Execute("SCAN", It.IsAny<object[]>()))
                  .Returns(RedisResult.Create(new RedisValue[] { 0, Array.Empty<RedisValue>() }));

        // Act
        var result = await _aiCacheService.InvalidateUserAICacheAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAICacheMetricsAsync_Should_Return_Valid_Metrics()
    {
        // Act
        var result = await _aiCacheService.GetAICacheMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.EntriesByType.Should().NotBeNull();
        result.HitRatesByType.Should().NotBeNull();
        result.HitRate.Should().BeInRange(0.0, 1.0);
        result.LastResetTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(AICacheType.DayPlans)]
    [InlineData(AICacheType.PrioritySuggestions)]
    [InlineData(AICacheType.ProductivityInsights)]
    [InlineData(AICacheType.ScheduleOptimizations)]
    [InlineData(AICacheType.CategorizationSuggestions)]
    [InlineData(AICacheType.TimeEstimates)]
    [InlineData(AICacheType.All)]
    public async Task InvalidateUserAICacheByTypeAsync_Should_Handle_All_Cache_Types(AICacheType cacheType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cacheTypes = new[] { cacheType };
        var serverMock = new Mock<IServer>();
        var endPoints = new EndPoint[] { new DnsEndPoint("localhost", 6379) };

        _redisMock.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(endPoints);
        _redisMock.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(serverMock.Object);

        serverMock.Setup(x => x.Execute("SCAN", It.IsAny<object[]>()))
                  .Returns(RedisResult.Create(new RedisValue[] { 0, Array.Empty<RedisValue>() }));

        // Act
        var result = await _aiCacheService.InvalidateUserAICacheByTypeAsync(userId, cacheTypes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CacheTaskTimeEstimateAsync_Should_Return_True_When_Successful()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var estimate = new TaskTimeEstimate(
            TaskId: taskId,
            EstimatedDuration: TimeSpan.FromHours(2),
            MinDuration: TimeSpan.FromMinutes(90),
            MaxDuration: TimeSpan.FromHours(3),
            ConfidenceLevel: 0.8,
            EstimationFactors: new List<string> { "Task complexity", "User experience" }
        );

        _distributedCacheMock.Setup(x => x.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _aiCacheService.CacheTaskTimeEstimateAsync(taskId, estimate, 120);

        // Assert
        result.Should().BeTrue();
        _distributedCacheMock.Verify(x => x.SetStringAsync(
            It.Is<string>(key => key.Contains($"ai:time-estimate:{taskId}")),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WarmUserAICacheAsync_Should_Return_Non_Negative_Count()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _aiCacheService.WarmUserAICacheAsync(userId);

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ClearAllAICacheAsync_Should_Handle_Redis_Operations()
    {
        // Arrange
        var serverMock = new Mock<IServer>();
        var endPoints = new EndPoint[] { new DnsEndPoint("localhost", 6379) };

        _redisMock.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(endPoints);
        _redisMock.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(serverMock.Object);

        serverMock.Setup(x => x.Execute("SCAN", It.IsAny<object[]>()))
                  .Returns(RedisResult.Create(new RedisValue[] { 0, Array.Empty<RedisValue>() }));

        // Act
        var result = await _aiCacheService.ClearAllAICacheAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ComputeContentHash_Should_Generate_32_Character_Hash()
    {
        // Arrange - Use reflection to access the private ComputeContentHash method
        var method = typeof(AICacheService).GetMethod("ComputeContentHash", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var testContent = "This is a test content for hash generation";

        // Act
        var hash = (string)method!.Invoke(null, new object[] { testContent });

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(32, "SHA256 hash should use first 32 characters to reduce collision probability");
        hash.Should().MatchRegex("^[A-F0-9]{32}$", "Hash should be 32 hexadecimal characters");
    }

    [Fact]
    public void ComputeContentHash_Should_Generate_Different_Hashes_For_Different_Content()
    {
        // Arrange
        var method = typeof(AICacheService).GetMethod("ComputeContentHash", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var content1 = "Content 1";
        var content2 = "Content 2";

        // Act
        var hash1 = (string)method!.Invoke(null, new object[] { content1 });
        var hash2 = (string)method!.Invoke(null, new object[] { content2 });

        // Assert
        hash1.Should().NotBe(hash2, "Different content should generate different hashes");
        hash1.Length.Should().Be(32);
        hash2.Length.Should().Be(32);
    }

    [Fact]
    public async Task ClearAllAICacheAsync_Should_Handle_Null_Cursor_Safely()
    {
        // Arrange
        var serverMock = new Mock<IServer>();
        var endPoints = new EndPoint[] { new DnsEndPoint("localhost", 6379) };

        _redisMock.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(endPoints);
        _redisMock.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(serverMock.Object);

        // Return null cursor to test null safety improvements
        serverMock.Setup(x => x.Execute("SCAN", It.IsAny<object[]>()))
                  .Returns(RedisResult.Create(new RedisValue[] { RedisValue.Null, Array.Empty<RedisValue>() }));

        // Act & Assert - Should not throw exception due to null cursor
        var action = async () => await _aiCacheService.ClearAllAICacheAsync();
        await action.Should().NotThrowAsync<NullReferenceException>("Null cursor should be handled safely");
        
        var result = await _aiCacheService.ClearAllAICacheAsync();
        result.Should().BeTrue();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _aiCacheService?.Dispose();
            _disposed = true;
        }
    }
}