using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System.Net;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the TaskCacheService
/// </summary>
public class TaskCacheServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<TaskCacheService>> _mockLogger;
    private readonly IOptions<RedisCacheSettings> _cacheSettings;
    private readonly TaskCacheService _service;

    public TaskCacheServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<TaskCacheService>>();

        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _cacheSettings = Options.Create(new RedisCacheSettings
        {
            ConnectionString = "localhost:6379",
            DefaultExpirationMinutes = 30,
            TaskCacheExpirationMinutes = 15,
            TaskListCacheExpirationMinutes = 10,
            UserTaskSummaryCacheExpirationMinutes = 5,
            MaxRetryAttempts = 3,
            CommandTimeoutMs = 100,
            EnablePerformanceMonitoring = true,
            KeyPrefix = "test",
            DatabaseIndex = 0
        });

        _service = new TaskCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            _cacheSettings,
            _mockLogger.Object);
    }

    [Fact]
    public void TaskCacheService_Constructor_Should_Throw_When_Redis_Is_Null()
    {
        // Act & Assert
        Action act = () => new TaskCacheService(
            null!,
            _mockDistributedCache.Object,
            _cacheSettings,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*redis*");
    }

    [Fact]
    public void TaskCacheService_Constructor_Should_Throw_When_DistributedCache_Is_Null()
    {
        // Act & Assert
        Action act = () => new TaskCacheService(
            _mockRedis.Object,
            null!,
            _cacheSettings,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*distributedCache*");
    }

    [Fact]
    public void TaskCacheService_Constructor_Should_Throw_When_CacheSettings_Is_Null()
    {
        // Act & Assert
        Action act = () => new TaskCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*cacheSettings*");
    }

    [Fact]
    public void TaskCacheService_Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act & Assert
        Action act = () => new TaskCacheService(
            _mockRedis.Object,
            _mockDistributedCache.Object,
            _cacheSettings,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*logger*");
    }

    [Fact]
    public async Task GetCachedTaskAsync_Should_Return_Null_When_Task_Not_In_Cache()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.GetCachedTaskAsync(taskId);

        // Assert
        result.Should().BeNull();
        _mockDistributedCache.Verify(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheTaskAsync_Should_Handle_Null_Task()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Func<Task> act = async () => await _service.CacheTaskAsync(null!);
        await act.Should().NotThrowAsync();
    }

    [Fact] 
    public async Task InvalidateTaskCacheAsync_Should_Remove_Cache_Entries()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockDistributedCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockServer = new Mock<IServer>();
        var mockEndPoint = new IPEndPoint(IPAddress.Loopback, 6379);
        
        _mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { mockEndPoint });
        _mockRedis.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        // Act
        var result = await _service.InvalidateTaskCacheAsync(taskId, userId);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task InvalidateUserTaskCacheAsync_Should_Remove_User_Cache_Entries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        _mockDistributedCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockServer = new Mock<IServer>();
        var mockEndPoint = new IPEndPoint(IPAddress.Loopback, 6379);
        
        _mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { mockEndPoint });
        _mockRedis.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        // Act
        var result = await _service.InvalidateUserTaskCacheAsync(userId);

        // Assert
        result.Should().BeTrue();
        _mockDistributedCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task ClearAllCacheAsync_Should_Clear_Task_Keys()
    {
        // Arrange
        var mockServer = new Mock<IServer>();
        var mockEndPoint = new IPEndPoint(IPAddress.Loopback, 6379);
        
        _mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { mockEndPoint });
        _mockRedis.Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);
            
        var mockKeys = new RedisKey[] { "test:task:id:123", "test:task:user:456" };
        mockServer.Setup(x => x.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(mockKeys);
            
        _mockDatabase.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);

        // Act
        var result = await _service.ClearAllCacheAsync();

        // Assert
        result.Should().BeTrue();
        _mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetCacheMetricsAsync_Should_Return_Valid_Metrics()
    {
        // Act
        var metrics = await _service.GetCacheMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalRequests.Should().BeGreaterOrEqualTo(0);
        metrics.HitRatio.Should().BeInRange(0, 1);
        metrics.AverageResponseTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }



    [Fact]
    public async Task GetCachedTaskAsync_Should_Handle_Redis_Exception()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        _mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act & Assert
        var result = await _service.GetCachedTaskAsync(taskId);
        result.Should().BeNull(); // Should gracefully handle exception and return null
    }

    [Fact]
    public async Task CacheTaskAsync_Should_Handle_Redis_Exception()
    {
        // Arrange
        var task = CreateTestTask();
        var userId = Guid.NewGuid();
        
        _mockDistributedCache.Setup(x => x.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act & Assert
        Func<Task> act = async () => await _service.CacheTaskAsync(task);
        await act.Should().NotThrowAsync(); // Should gracefully handle exception
    }

    private static DomainTask CreateTestTask()
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Description = "Test Description",
            UserId = Guid.NewGuid(),
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)WhoAndWhat.Domain.ValueObjects.Priority.Medium,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}