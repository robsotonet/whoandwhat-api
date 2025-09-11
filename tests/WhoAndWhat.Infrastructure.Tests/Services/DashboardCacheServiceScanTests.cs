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
/// Tests for Redis SCAN implementation in DashboardCacheService
/// Focuses on validating the critical SCAN behavior fix
/// </summary>
public class DashboardCacheServiceScanTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IServer> _mockServer;
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<DashboardCacheService>> _mockLogger;
    private readonly IOptions<RedisCacheSettings> _cacheSettings;
    private readonly DashboardCacheService _service;

    public DashboardCacheServiceScanTests()
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
    /// Test SCAN behavior with single iteration (cursor returns to 0)
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithSingleScanIteration_ShouldProcessAllKeys()
    {
        // Arrange
        var testKeys = new[] { "test:dashboard:key1", "test:dashboard:key2", "test:dashboard:key3" };
        
        // Mock SCAN command returning all keys in first iteration
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
        
        // Verify SCAN was called with correct parameters
        _mockServer.Verify(s => s.Execute("SCAN", 0L, "MATCH", "test:dashboard:*", "COUNT", 100), Times.Once);
        
        // Verify all keys were deleted
        _mockDatabase.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == testKeys.Length && 
                                    keys.All(k => testKeys.Contains(k))), 
            It.IsAny<CommandFlags>()), Times.Once);
    }

    /// <summary>
    /// Test SCAN behavior with multiple iterations (cursor progression)
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithMultipleScanIterations_ShouldProcessAllKeys()
    {
        // Arrange
        var firstBatchKeys = new[] { "test:dashboard:key1", "test:dashboard:key2" };
        var secondBatchKeys = new[] { "test:dashboard:key3", "test:dashboard:key4" };
        
        // Mock SCAN command sequence
        var scanCalls = new Queue<RedisResult>();
        
        // First SCAN call (cursor 0 -> 123)
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"123", // next cursor
            (RedisResult)firstBatchKeys.Select(k => (RedisResult)k).ToArray()
        });
        
        // Second SCAN call (cursor 123 -> 0, scan complete)
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)secondBatchKeys.Select(k => (RedisResult)k).ToArray()
        });
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns(() => scanCalls.Dequeue());

        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify SCAN was called twice with correct cursors
        _mockServer.Verify(s => s.Execute("SCAN", 0L, "MATCH", "test:dashboard:*", "COUNT", 100), Times.Once);
        _mockServer.Verify(s => s.Execute("SCAN", 123L, "MATCH", "test:dashboard:*", "COUNT", 100), Times.Once);
        
        // Verify keys from both batches were deleted
        _mockDatabase.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    /// <summary>
    /// Test SCAN behavior with empty result sets
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithEmptyScanResults_ShouldCompleteSuccessfully()
    {
        // Arrange - SCAN returns no keys
        var scanResult = new RedisResult[]
        {
            (RedisResult)"0", // cursor = 0 (scan complete)
            (RedisResult)new RedisResult[0] // empty keys array
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify SCAN was called
        _mockServer.Verify(s => s.Execute("SCAN", 0L, "MATCH", "test:dashboard:*", "COUNT", 100), Times.Once);
        
        // Verify no delete operations were performed
        _mockDatabase.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    /// <summary>
    /// Test SCAN behavior with cancellation token
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithCancellation_ShouldStopGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var testKeys = new[] { "test:dashboard:key1", "test:dashboard:key2" };
        
        var scanResult = new RedisResult[]
        {
            (RedisResult)"123", // cursor != 0 (more data available)
            (RedisResult)testKeys.Select(k => (RedisResult)k).ToArray()
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult)
            .Callback(() => cts.Cancel()); // Cancel after first SCAN
        
        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(testKeys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync(cts.Token);

        // Assert
        result.Should().BeFalse(); // Should return false due to cancellation
        
        // Verify SCAN was called at least once
        _mockServer.Verify(s => s.Execute("SCAN", It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test SCAN error handling with invalid Redis response
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithInvalidScanResponse_ShouldHandleGracefully()
    {
        // Arrange - Invalid SCAN response (missing array elements)
        var invalidScanResult = new RedisResult[] { (RedisResult)"0" }; // Missing keys array
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)invalidScanResult);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue(); // Should complete successfully despite invalid response
        
        // Verify warning was logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid SCAN response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test SCAN error handling with invalid cursor value
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithInvalidCursor_ShouldHandleGracefully()
    {
        // Arrange - SCAN response with invalid cursor
        var scanResult = new RedisResult[]
        {
            (RedisResult)"invalid_cursor", // Invalid cursor value
            (RedisResult)new RedisResult[] { (RedisResult)"test:dashboard:key1" }
        };
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns((RedisResult)scanResult);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue(); // Should complete successfully despite invalid cursor
        
        // Verify error was logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to parse SCAN cursor")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test SCAN behavior with large key sets requiring batching
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_WithLargeKeySet_ShouldProcessInBatches()
    {
        // Arrange - Generate 250 keys (more than batch size of 100)
        var allKeys = Enumerable.Range(1, 250)
            .Select(i => $"test:dashboard:key{i}")
            .ToArray();
        
        // Split into batches that SCAN might return
        var firstBatch = allKeys.Take(100).ToArray();
        var secondBatch = allKeys.Skip(100).Take(100).ToArray();
        var thirdBatch = allKeys.Skip(200).ToArray();
        
        var scanCalls = new Queue<RedisResult>();
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"100",
            (RedisResult)firstBatch.Select(k => (RedisResult)k).ToArray()
        });
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"200", 
            (RedisResult)secondBatch.Select(k => (RedisResult)k).ToArray()
        });
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"0", // scan complete
            (RedisResult)thirdBatch.Select(k => (RedisResult)k).ToArray()
        });
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns(() => scanCalls.Dequeue());
        
        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => keys.Length);

        // Act
        var result = await _service.ClearAllDashboardCacheAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify SCAN was called for each batch
        _mockServer.Verify(s => s.Execute("SCAN", It.IsAny<object[]>()), Times.Exactly(3));
        
        // Verify keys were deleted in appropriate batches
        // Due to batching logic, should have multiple delete operations
        _mockDatabase.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), 
            Times.AtLeast(2));
    }

    /// <summary>
    /// Performance validation test - ensure SCAN doesn't timeout on large datasets
    /// </summary>
    [Fact]
    public async Task ClearAllDashboardCacheAsync_PerformanceValidation_ShouldCompleteWithinTimeout()
    {
        // Arrange
        const int iterations = 10; // Simulate multiple SCAN iterations
        var scanCalls = new Queue<RedisResult>();
        
        for (int i = 0; i < iterations - 1; i++)
        {
            scanCalls.Enqueue((RedisResult)new RedisResult[]
            {
                (RedisResult)(i + 1).ToString(),
                (RedisResult)new RedisResult[] { (RedisResult)$"test:dashboard:key{i}" }
            });
        }
        
        // Final iteration
        scanCalls.Enqueue((RedisResult)new RedisResult[]
        {
            (RedisResult)"0",
            (RedisResult)new RedisResult[] { (RedisResult)"test:dashboard:final" }
        });
        
        _mockServer.Setup(s => s.Execute("SCAN", It.IsAny<object[]>()))
            .Returns(() => scanCalls.Dequeue());
        
        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        // Act & Assert - Should complete within reasonable time
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await _service.ClearAllDashboardCacheAsync(cts.Token);
        
        result.Should().BeTrue();
        _mockServer.Verify(s => s.Execute("SCAN", It.IsAny<object[]>()), Times.Exactly(iterations));
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}