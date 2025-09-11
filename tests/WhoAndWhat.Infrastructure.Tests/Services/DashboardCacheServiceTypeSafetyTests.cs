using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for Redis SCAN type safety fixes in DashboardCacheService
/// Focuses on validating safe pattern matching and error handling
/// </summary>
public class DashboardCacheServiceTypeSafetyTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IServer> _mockServer;
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<DashboardCacheService>> _mockLogger;
    private readonly IOptions<RedisCacheSettings> _cacheSettings;
    private readonly DashboardCacheService _service;

    public DashboardCacheServiceTypeSafetyTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockServer = new Mock<IServer>();
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<DashboardCacheService>>();

        _cacheSettings = Options.Create(new RedisCacheSettings
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "test",
            DefaultExpirationMinutes = 30,
            DatabaseIndex = 0,
            EnablePerformanceMonitoring = true
        });

        // Setup Redis mocks
        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
        
        _mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new System.Net.EndPoint[] { new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379) });
        
        _mockRedis.Setup(x => x.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(_mockServer.Object);

        _service = new DashboardCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            _cacheSettings,
            _mockLogger.Object);
    }

    /// <summary>
    /// Test SCAN behavior with valid string Redis results
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithValidStringResults_ShouldProcessCorrectly()
    {
        // Arrange
        var testKeys = new[] { "test:dashboard:key1", "test:dashboard:key2" };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)testKeys.Select(k => (RedisResult)k).ToArray()
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(testKeys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify keys were processed without type errors
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == testKeys.Length), 
            It.IsAny<CommandFlags>()), Times.Once);
        
        // Verify no type safety warnings were logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected Redis key result type")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Test SCAN behavior with null Redis results (type safety fix)
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithNullResults_ShouldHandleGracefully()
    {
        // Arrange
        var keysWithNulls = new RedisResult[]
        {
            (RedisResult)"test:dashboard:key1",
            (RedisResult)RedisValue.Null,
            (RedisResult)"test:dashboard:key2",
            (RedisResult)RedisValue.Null
        };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)keysWithNulls
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify only non-null keys were processed (2 out of 4)
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 2), 
            It.IsAny<CommandFlags>()), Times.Once);
        
        // Verify no type errors or exceptions occurred
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Test SCAN behavior with mixed data types (type safety fix)
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithMixedDataTypes_ShouldFilterAndLogWarnings()
    {
        // Arrange - Mix of strings, integers, and other types
        var mixedResults = new RedisResult[]
        {
            (RedisResult)"test:dashboard:key1",      // Valid string
            (RedisResult)12345,                       // Invalid integer
            (RedisResult)"test:dashboard:key2",      // Valid string
            (RedisResult)new byte[] { 1, 2, 3 },     // Invalid byte array
            (RedisResult)"",                          // Empty string (should be filtered)
            (RedisResult)"test:dashboard:key3"       // Valid string
        };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)mixedResults
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify only valid string keys were processed (3 out of 6)
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 3), 
            It.IsAny<CommandFlags>()), Times.Once);
        
        // Verify warnings were logged for invalid types
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected Redis key result type")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2)); // Should log for integer and byte array
    }

    /// <summary>
    /// Test SCAN behavior with empty string keys (edge case)
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithEmptyStringKeys_ShouldFilterOut()
    {
        // Arrange
        var keysWithEmpties = new RedisResult[]
        {
            (RedisResult)"test:dashboard:key1",
            (RedisResult)"",
            (RedisResult)"test:dashboard:key2",
            (RedisResult)"   ",  // Whitespace-only
            (RedisResult)"test:dashboard:key3"
        };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)keysWithEmpties
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify only non-empty keys were processed (4 out of 5, whitespace is considered valid)
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 4), 
            It.IsAny<CommandFlags>()), Times.Once);
    }

    /// <summary>
    /// Test SCAN pattern helper method with type safety
    /// </summary>
    [Fact]
    public async Task ScanKeysForPatternAsync_WithValidKeys_ShouldReturnCorrectKeys()
    {
        // Arrange
        var expectedKeys = new[] { "test:dashboard:user:123:snapshot:1", "test:dashboard:user:123:snapshot:2" };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0",
            (RedisResult)expectedKeys.Select(k => (RedisResult)k).ToArray()
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        // Use reflection to access the private helper method
        var method = typeof(DashboardCacheService).GetMethod("ScanKeysForPatternAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull();

        // Act
        var result = await (Task<RedisKey[]>)method!.Invoke(_service, 
            new object[] { "test:dashboard:user:123:snapshot:*", CancellationToken.None })!;

        // Assert
        result.Should().HaveCount(2);
        result.Select(k => k.ToString()).Should().BeEquivalentTo(expectedKeys);
    }

    /// <summary>
    /// Test that user-specific invalidation methods use SCAN consistently
    /// </summary>
    [Fact]
    public async Task InvalidateUserCaches_ShouldUseScanPattern()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockKeys = new[] { $"test:dashboard:user:{userId}:snapshot:1" };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0",
            (RedisResult)mockKeys.Select(k => (RedisResult)k).ToArray()
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        // Act
        await _service.InvalidateUserDashboardCacheAsync(userId);

        // Assert
        // Verify SCAN was called (not Keys)
        _mockServer.Verify(s => s.Execute("SCAN", It.IsAny<object[]>()), Times.AtLeast(2));
        
        // Verify Keys method was not called directly
        _mockServer.Verify(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), 
            It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    /// <summary>
    /// Performance test to ensure type safety doesn't degrade SCAN performance
    /// </summary>
    [Fact]
    public async Task TypeSafeScan_Performance_ShouldNotDegradeSignificantly()
    {
        // Arrange
        var largeKeySet = Enumerable.Range(1, 1000)
            .Select(i => $"test:dashboard:key{i}")
            .ToArray();
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0",
            (RedisResult)largeKeySet.Select(k => (RedisResult)k).ToArray()
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act & Assert
        var startTime = DateTime.UtcNow;
        
        var result = await _service.ClearAllDashboardCacheAsync();
        
        var elapsed = DateTime.UtcNow - startTime;
        
        result.Should().BeTrue();
        elapsed.TotalMilliseconds.Should().BeLessThan(1000, 
            "Type-safe SCAN should complete large operations within reasonable time");
        
        // Verify all keys were processed
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == largeKeySet.Length), 
            It.IsAny<CommandFlags>()), Times.AtLeast(1));
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}