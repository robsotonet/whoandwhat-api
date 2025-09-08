using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

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
            DefaultExpiry = TimeSpan.FromMinutes(30),
            TaskExpiry = TimeSpan.FromMinutes(15),
            UserTasksExpiry = TimeSpan.FromMinutes(10),
            SearchResultsExpiry = TimeSpan.FromMinutes(5),
            MaxRetryAttempts = 3,
            RetryDelay = TimeSpan.FromMilliseconds(100),
            EnablePerformanceMetrics = true
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
    public async Task GetTaskAsync_Should_Return_Null_When_Task_Not_In_Cache()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        _mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetTaskAsync(taskId, userId);

        // Assert
        result.Should().BeNull();
        _mockDatabase.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetTaskAsync_Should_Handle_Null_Task()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Func<Task> act = async () => await _service.SetTaskAsync(null!, userId);
        await act.Should().NotThrowAsync();
    }

    [Fact] 
    public async Task RemoveTaskAsync_Should_Call_Database_Delete()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockDatabase.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RemoveTaskAsync(taskId, userId);

        // Assert
        _mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserTasksAsync_Should_Call_Appropriate_Methods()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        _mockDatabase.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RemoveUserTasksAsync(userId);

        // Assert
        _mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ClearAllAsync_Should_Call_FlushDatabase()
    {
        // Arrange
        var mockServer = new Mock<IServer>();
        _mockRedis.Setup(x => x.GetServer(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        // Act
        await _service.ClearAllAsync();

        // Assert
        mockServer.Verify(x => x.FlushDatabaseAsync(It.IsAny<int>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void GetCacheMetrics_Should_Return_Valid_Metrics()
    {
        // Act
        var metrics = _service.GetCacheMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalRequests.Should().BeGreaterOrEqualTo(0);
        metrics.CacheHitRatio.Should().BeInRange(0, 1);
        metrics.AverageResponseTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ResetMetrics_Should_Reset_Performance_Counters()
    {
        // Act
        _service.ResetMetrics();

        // Assert
        var metrics = _service.GetCacheMetrics();
        metrics.TotalRequests.Should().Be(0);
        metrics.CacheHits.Should().Be(0);
        metrics.CacheMisses.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsHealthyAsync_Should_Return_Connection_Status(bool isConnected)
    {
        // Arrange
        _mockRedis.Setup(x => x.IsConnected).Returns(isConnected);

        // Act
        var result = await _service.IsHealthyAsync();

        // Assert
        result.Should().Be(isConnected);
    }

    [Fact]
    public async Task GetTaskAsync_Should_Handle_Redis_Exception()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        _mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act & Assert
        var result = await _service.GetTaskAsync(taskId, userId);
        result.Should().BeNull(); // Should gracefully handle exception and return null
    }

    [Fact]
    public async Task SetTaskAsync_Should_Handle_Redis_Exception()
    {
        // Arrange
        var task = CreateTestTask();
        var userId = Guid.NewGuid();
        
        _mockDatabase.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act & Assert
        Func<Task> act = async () => await _service.SetTaskAsync(task, userId);
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