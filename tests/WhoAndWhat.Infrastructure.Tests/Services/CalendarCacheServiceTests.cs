using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services.Calendar;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the CalendarCacheService
/// </summary>
public class CalendarCacheServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<CalendarCacheService>> _mockLogger;
    private readonly IOptions<RedisCacheSettings> _cacheSettings;
    private readonly CalendarCacheService _service;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly CalendarProvider _testProvider = CalendarProvider.Google;
    private readonly string _testCalendarId = "test-calendar-123";

    public CalendarCacheServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<CalendarCacheService>>();

        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _cacheSettings = Options.Create(new RedisCacheSettings
        {
            ConnectionString = "localhost:6379",
            DefaultExpirationMinutes = 30,
            KeyPrefix = "test",
            DatabaseIndex = 0
        });

        _service = new CalendarCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            _cacheSettings,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void CalendarCacheService_Constructor_Should_Throw_When_Redis_Is_Null()
    {
        // Act & Assert
        Action act = () => new CalendarCacheService(
            null!,
            _mockDistributedCache.Object,
            _cacheSettings,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*redis*");
    }

    [Fact]
    public void CalendarCacheService_Constructor_Should_Throw_When_DistributedCache_Is_Null()
    {
        // Act & Assert
        Action act = () => new CalendarCacheService(
            _mockRedis.Object,
            null!,
            _cacheSettings,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*distributedCache*");
    }

    [Fact]
    public void CalendarCacheService_Constructor_Should_Throw_When_Settings_Is_Null()
    {
        // Act & Assert
        Action act = () => new CalendarCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*settings*");
    }

    [Fact]
    public void CalendarCacheService_Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act & Assert
        Action act = () => new CalendarCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            _cacheSettings,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*logger*");
    }

    #endregion

    #region Calendar Events Caching Tests

    [Fact]
    public async Task CacheCalendarEventsAsync_Should_Cache_Events_Successfully()
    {
        // Arrange
        var testEvents = CreateTestEvents(3);
        var expectedKey = $"test:calendar:events:{_testUserId}:{_testProvider}:{_testCalendarId}";

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                expectedKey,
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheCalendarEventsAsync(
            _testUserId, _testProvider, _testCalendarId, testEvents, 30);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.SetStringAsync(
            expectedKey,
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheCalendarEventsAsync_Should_Return_False_When_Caching_Fails()
    {
        // Arrange
        var testEvents = CreateTestEvents(2);
        
        _mockDistributedCache.Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache error"));

        // Act
        var result = await _service.CacheCalendarEventsAsync(
            _testUserId, _testProvider, _testCalendarId, testEvents, 30);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedCalendarEventsAsync_Should_Return_Cached_Events()
    {
        // Arrange
        var testEvents = CreateTestEvents(2);
        var jsonData = System.Text.Json.JsonSerializer.Serialize(testEvents);
        var expectedKey = $"test:calendar:events:{_testUserId}:{_testProvider}:{_testCalendarId}";

        _mockDistributedCache.Setup(x => x.GetStringAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _service.GetCachedCalendarEventsAsync(_testUserId, _testProvider, _testCalendarId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!.First().Title.Should().Be("Test Event 0");
    }

    [Fact]
    public async Task GetCachedCalendarEventsAsync_Should_Return_Null_When_No_Cache()
    {
        // Arrange
        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.GetCachedCalendarEventsAsync(_testUserId, _testProvider, _testCalendarId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedCalendarEventsAsync_Should_Filter_By_DateRange()
    {
        // Arrange
        var testEvents = new List<ExternalCalendarEvent>
        {
            CreateTestEvent("Event 1", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2).AddHours(1)),
            CreateTestEvent("Event 2", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            CreateTestEvent("Event 3", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(1))
        };
        var jsonData = System.Text.Json.JsonSerializer.Serialize(testEvents);
        var dateRange = new TimeRange(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _service.GetCachedCalendarEventsAsync(_testUserId, _testProvider, _testCalendarId, dateRange);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result!.First().Title.Should().Be("Event 2");
    }

    #endregion

    #region Sync Token Caching Tests

    [Fact]
    public async Task CacheSyncTokenAsync_Should_Cache_Token_Successfully()
    {
        // Arrange
        var testToken = "test-sync-token-123";
        var expectedKey = $"test:calendar:synctoken:{_testUserId}:{_testProvider}:{_testCalendarId}";

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                expectedKey,
                testToken,
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheSyncTokenAsync(_testUserId, _testProvider, _testCalendarId, testToken, 60);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.SetStringAsync(
            expectedKey,
            testToken,
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCachedSyncTokenAsync_Should_Return_Cached_Token()
    {
        // Arrange
        var testToken = "cached-sync-token-456";
        var expectedKey = $"test:calendar:synctoken:{_testUserId}:{_testProvider}:{_testCalendarId}";

        _mockDistributedCache.Setup(x => x.GetStringAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testToken);

        // Act
        var result = await _service.GetCachedSyncTokenAsync(_testUserId, _testProvider, _testCalendarId);

        // Assert
        result.Should().Be(testToken);
    }

    #endregion

    #region Access Token Caching Tests

    [Fact]
    public async Task CacheAccessTokenAsync_Should_Cache_Token_Data_Successfully()
    {
        // Arrange
        var tokenData = new CalendarAccessToken(
            "access-token-123",
            "refresh-token-456",
            DateTime.UtcNow.AddHours(1),
            new[] { "calendar.read", "calendar.write" }
        );

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheAccessTokenAsync(_testUserId, _testProvider, tokenData, 60);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetCachedAccessTokenAsync_Should_Return_Null_For_Expired_Token()
    {
        // Arrange
        var expiredTokenData = new CalendarAccessToken(
            "access-token-123",
            "refresh-token-456",
            DateTime.UtcNow.AddHours(-1), // Expired
            new[] { "calendar.read" }
        );
        var jsonData = System.Text.Json.JsonSerializer.Serialize(expiredTokenData);

        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        _mockDistributedCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetCachedAccessTokenAsync(_testUserId, _testProvider);

        // Assert
        result.Should().BeNull();
        _mockDistributedCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCachedAccessTokenAsync_Should_Return_Valid_Token()
    {
        // Arrange
        var validTokenData = new CalendarAccessToken(
            "access-token-123",
            "refresh-token-456",
            DateTime.UtcNow.AddHours(1), // Valid
            new[] { "calendar.read", "calendar.write" }
        );
        var jsonData = System.Text.Json.JsonSerializer.Serialize(validTokenData);

        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _service.GetCachedAccessTokenAsync(_testUserId, _testProvider);

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("access-token-123");
        result.RefreshToken.Should().Be("refresh-token-456");
        result.Scopes.Should().Contain("calendar.read");
        result.Scopes.Should().Contain("calendar.write");
    }

    #endregion

    #region Conflict Resolution Caching Tests

    [Fact]
    public async Task CacheConflictResolutionAsync_Should_Cache_Resolution_Successfully()
    {
        // Arrange
        var conflictId = Guid.NewGuid();
        var resolution = new ConflictResolution(
            ConflictResolutionType.KeepInternal,
            "Keep internal event",
            0.8,
            null
        );

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheConflictResolutionAsync(_testUserId, conflictId, resolution, 30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetCachedConflictResolutionAsync_Should_Return_Cached_Resolution()
    {
        // Arrange
        var conflictId = Guid.NewGuid();
        var resolution = new ConflictResolution(
            ConflictResolutionType.MergeEvents,
            "Merge conflicting events",
            0.9,
            null
        );
        var jsonData = System.Text.Json.JsonSerializer.Serialize(resolution);
        var expectedKey = $"test:calendar:conflict:{_testUserId}:{conflictId}";

        _mockDistributedCache.Setup(x => x.GetStringAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _service.GetCachedConflictResolutionAsync(_testUserId, conflictId);

        // Assert
        result.Should().NotBeNull();
        result!.ResolutionType.Should().Be(ConflictResolutionType.MergeEvents);
        result.Description.Should().Be("Merge conflicting events");
        result.Confidence.Should().Be(0.9);
    }

    #endregion

    #region Free/Busy Data Caching Tests

    [Fact]
    public async Task CacheFreeBusyDataAsync_Should_Cache_Data_With_Time_Key()
    {
        // Arrange
        var timeRange = new TimeRange(
            new DateTime(2023, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 10, 1, 17, 0, 0, DateTimeKind.Utc)
        );
        var freeBusyData = new FreeBusyResult(
            new List<CalendarFreeBusy>(),
            true,
            null
        );

        var expectedKey = $"test:calendar:freebusy:{_testUserId}:{_testProvider}:202310010900-202310011700";

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                expectedKey,
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheFreeBusyDataAsync(_testUserId, _testProvider, timeRange, freeBusyData, 15);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.SetStringAsync(
            expectedKey,
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task InvalidateUserCalendarCacheAsync_Should_Delete_User_Keys()
    {
        // Arrange
        var testKeys = new RedisKey[]
        {
            $"test:calendar:events:{_testUserId}:Google:cal1",
            $"test:calendar:events:{_testUserId}:Outlook:cal2",
            $"test:calendar:synctoken:{_testUserId}:Google:cal1"
        };

        SetupKeySearch($"test:calendar:*:{_testUserId}:*", testKeys);

        _mockDatabase.Setup(x => x.KeyDeleteAsync(testKeys, It.IsAny<CommandFlags>()))
            .ReturnsAsync(testKeys.Length);

        // Act
        var result = await _service.InvalidateUserCalendarCacheAsync(_testUserId);

        // Assert
        result.Should().BeTrue();
        _mockDatabase.Verify(x => x.KeyDeleteAsync(testKeys, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCalendarCacheByTypeAsync_Should_Delete_Specific_Type_Keys()
    {
        // Arrange
        var cacheTypes = new[] { CalendarCacheType.Events, CalendarCacheType.SyncTokens };
        var testKeys = new RedisKey[]
        {
            $"test:calendar:events:{_testUserId}:Google:cal1",
            $"test:calendar:synctoken:{_testUserId}:Google:cal1"
        };

        SetupKeySearch("test:calendar:events:*", testKeys.Take(1).ToArray());
        SetupKeySearch("test:calendar:synctoken:*", testKeys.Skip(1).ToArray());

        _mockDatabase.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags flags) => keys.Length);

        // Act
        var result = await _service.InvalidateCalendarCacheByTypeAsync(_testUserId, null, cacheTypes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidateEventCacheAsync_Should_Remove_Event_And_Status_Cache()
    {
        // Arrange
        var modifiedEventIds = new[] { "event1", "event2", "event3" };
        var eventKey = $"test:calendar:events:{_testUserId}:{_testProvider}:{_testCalendarId}";
        var statusKey = $"test:calendar:syncstatus:{_testUserId}:{_testProvider}";

        _mockDistributedCache.Setup(x => x.RemoveAsync(eventKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockDistributedCache.Setup(x => x.RemoveAsync(statusKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.InvalidateEventCacheAsync(_testUserId, _testProvider, _testCalendarId, modifiedEventIds);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.RemoveAsync(eventKey, It.IsAny<CancellationToken>()), Times.Once);
        _mockDistributedCache.Verify(x => x.RemoveAsync(statusKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cache Metrics Tests

    [Fact]
    public async Task GetCalendarCacheMetricsAsync_Should_Return_Aggregated_Metrics()
    {
        // Act
        var result = await _service.GetCalendarCacheMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalEntries.Should().BeGreaterOrEqualTo(0);
        result.HitRate.Should().BeInRange(0.0, 1.0);
        result.EntriesByType.Should().NotBeNull();
        result.HitRatesByType.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearAllCalendarCacheAsync_Should_Clear_All_Keys()
    {
        // Arrange
        var testKeys = new RedisKey[] { "test:calendar:events:key1", "test:calendar:token:key2" };
        SetupKeySearch("test:calendar:*", testKeys);

        _mockDatabase.Setup(x => x.KeyDeleteAsync(testKeys, It.IsAny<CommandFlags>()))
            .ReturnsAsync(testKeys.Length);

        // Act
        var result = await _service.ClearAllCalendarCacheAsync();

        // Assert
        result.Should().BeTrue();
        _mockDatabase.Verify(x => x.KeyDeleteAsync(testKeys, It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region Cache Warming Tests

    [Fact]
    public async Task WarmUserCalendarCacheAsync_Should_Return_Warmed_Count()
    {
        // Arrange
        var providers = new[] { CalendarProvider.Google, CalendarProvider.Outlook };

        // Act
        var result = await _service.WarmUserCalendarCacheAsync(_testUserId, providers);

        // Assert
        result.Should().Be(providers.Length);
    }

    [Fact]
    public async Task PreloadUpcomingEventsAsync_Should_Return_Estimated_Count()
    {
        // Arrange
        var lookAheadDays = 7;

        // Act
        var result = await _service.PreloadUpcomingEventsAsync(_testUserId, _testProvider, lookAheadDays);

        // Assert
        result.Should().BeGreaterThan(0);
        result.Should().Be(lookAheadDays * 5); // 5 events per day estimate
    }

    #endregion

    #region Event ID Mappings Tests

    [Fact]
    public async Task CacheEventMappingsAsync_Should_Cache_Mappings_Successfully()
    {
        // Arrange
        var mappings = new List<EventIdMapping>
        {
            new(Guid.NewGuid(), "external-1", CalendarProvider.Google, DateTime.UtcNow),
            new(Guid.NewGuid(), "external-2", CalendarProvider.Google, DateTime.UtcNow)
        };

        _mockDistributedCache.Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CacheEventMappingsAsync(_testUserId, mappings, 30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetCachedEventMappingsAsync_Should_Return_Filtered_Mappings()
    {
        // Arrange
        var mappings = new List<EventIdMapping>
        {
            new(Guid.NewGuid(), "google-1", CalendarProvider.Google, DateTime.UtcNow),
            new(Guid.NewGuid(), "outlook-1", CalendarProvider.Outlook, DateTime.UtcNow),
            new(Guid.NewGuid(), "google-2", CalendarProvider.Google, DateTime.UtcNow)
        };
        var jsonData = System.Text.Json.JsonSerializer.Serialize(mappings);

        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _service.GetCachedEventMappingsAsync(_testUserId, CalendarProvider.Google);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!.All(m => m.Provider == CalendarProvider.Google).Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CacheOperations_Should_Handle_Serialization_Errors_Gracefully()
    {
        // Arrange
        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid-json-data");

        // Act
        var result = await _service.GetCachedCalendarEventsAsync(_testUserId, _testProvider, _testCalendarId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CacheOperations_Should_Handle_Redis_Exceptions_Gracefully()
    {
        // Arrange
        _mockDistributedCache.Setup(x => x.SetStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisException("Redis connection failed"));

        var testEvents = CreateTestEvents(1);

        // Act
        var result = await _service.CacheCalendarEventsAsync(_testUserId, _testProvider, _testCalendarId, testEvents, 30);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    // Helper methods

    private void SetupKeySearch(string pattern, RedisKey[] keys)
    {
        var mockServer = new Mock<IServer>();
        var mockEndPoint = new Mock<System.Net.EndPoint>();
        
        mockServer.Setup(x => x.IsConnected).Returns(true);
        mockServer.Setup(x => x.KeysAsync(It.IsAny<int>(), pattern, It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(keys.ToAsyncEnumerable());

        _mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new[] { mockEndPoint.Object });
        
        _mockRedis.Setup(x => x.GetServer(mockEndPoint.Object, It.IsAny<object>()))
            .Returns(mockServer.Object);
    }

    private List<ExternalCalendarEvent> CreateTestEvents(int count)
    {
        var events = new List<ExternalCalendarEvent>();
        for (int i = 0; i < count; i++)
        {
            events.Add(CreateTestEvent($"Test Event {i}", DateTime.UtcNow.AddHours(i), DateTime.UtcNow.AddHours(i + 1)));
        }
        return events;
    }

    private ExternalCalendarEvent CreateTestEvent(string title, DateTime start, DateTime end)
    {
        return new ExternalCalendarEvent(
            Guid.NewGuid().ToString(),
            title,
            $"Description for {title}",
            start,
            end,
            false,
            "Test Location",
            new List<ExternalAttendee>(),
            new List<string>(),
            new List<ExternalEventReminder>(),
            new List<ExternalEventAttachment>(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            ExternalEventStatus.Confirmed,
            "public",
            new Dictionary<string, object>()
        );
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}

// Extension method to convert IEnumerable to IAsyncEnumerable for testing
internal static class AsyncEnumerableExtensions
{
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        return ToAsyncEnumerableInternal(source);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerableInternal<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
    }
}