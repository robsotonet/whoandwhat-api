using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services.Calendar;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for CalendarPerformanceOptimizer, focusing on the semaphore race condition fix
/// </summary>
public class CalendarPerformanceOptimizerTests : IDisposable
{
    private readonly Mock<ICalendarCacheService> _mockCacheService;
    private readonly Mock<ILogger<CalendarPerformanceOptimizer>> _mockLogger;
    private readonly IOptions<CalendarSyncSettings> _settings;
    private readonly CalendarPerformanceOptimizer _optimizer;
    private bool _disposed;

    public CalendarPerformanceOptimizerTests()
    {
        _mockCacheService = new Mock<ICalendarCacheService>();
        _mockLogger = new Mock<ILogger<CalendarPerformanceOptimizer>>();
        
        _settings = Options.Create(new CalendarSyncSettings
        {
            BatchSize = 10,
            MaxConcurrentOperations = 5,
            TimeoutMs = 30000,
            RetryAttempts = 3,
            BackupEnabled = true,
            PerformanceMonitoring = true,
            RateLimitConfig = new Dictionary<CalendarProvider, RateLimitSettings>
            {
                [CalendarProvider.Google] = new RateLimitSettings 
                { 
                    RequestsPerMinute = 60, 
                    BurstSize = 10, 
                    DelayMs = 1000 
                },
                [CalendarProvider.Outlook] = new RateLimitSettings 
                { 
                    RequestsPerMinute = 30, 
                    BurstSize = 5, 
                    DelayMs = 2000 
                }
            }
        });

        _optimizer = new CalendarPerformanceOptimizer(
            _mockCacheService.Object,
            _settings,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var optimizer = new CalendarPerformanceOptimizer(
            _mockCacheService.Object,
            _settings,
            _mockLogger.Object);

        // Assert
        optimizer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullCacheService_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new CalendarPerformanceOptimizer(
            null!,
            _settings,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheService");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new CalendarPerformanceOptimizer(
            _mockCacheService.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new CalendarPerformanceOptimizer(
            _mockCacheService.Object,
            _settings,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task GetRateLimiterAsync_WithValidProvider_ShouldReturnRateLimiter()
    {
        // Arrange
        var provider = CalendarProvider.Google;

        // Act
        var rateLimiter = await _optimizer.GetRateLimiterAsync(provider);

        // Assert
        rateLimiter.Should().NotBeNull();
        rateLimiter.RequestsPerMinute.Should().Be(60);
        rateLimiter.DelayMs.Should().Be(1000);
    }

    [Theory]
    [InlineData(CalendarProvider.Google, 60)]
    [InlineData(CalendarProvider.Outlook, 30)]
    public async Task GetRateLimiterAsync_WithDifferentProviders_ShouldReturnCorrectLimits(CalendarProvider provider, int expectedRequestsPerMinute)
    {
        // Act
        var rateLimiter = await _optimizer.GetRateLimiterAsync(provider);

        // Assert
        rateLimiter.Should().NotBeNull();
        rateLimiter.RequestsPerMinute.Should().Be(expectedRequestsPerMinute);
    }

    [Fact]
    public async Task RateLimiter_ConcurrentAccess_ShouldHandleRaceConditionsSafely()
    {
        // Arrange
        var rateLimiter = await _optimizer.GetRateLimiterAsync(CalendarProvider.Google);
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();
        var completedTasks = 0;

        // Act - Simulate concurrent access to the rate limiter
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await rateLimiter.WaitAsync(CancellationToken.None);
                    Interlocked.Increment(ref completedTasks);
                    
                    // Simulate some work
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Allow time for any rate limiter resets to occur
        await Task.Delay(2000);

        // Assert
        exceptions.Should().BeEmpty("Rate limiter should handle concurrent access without exceptions");
        completedTasks.Should().BeGreaterThan(0, "At least some tasks should complete successfully");
        
        // Verify rate limiter state is consistent
        rateLimiter.Should().NotBeNull();
        rateLimiter.CurrentRequests.Should().BeGreaterOrEqualTo(0);
        rateLimiter.CurrentRequests.Should().BeLessOrEqualTo(rateLimiter.RequestsPerMinute);
    }

    [Fact]
    public async Task RateLimiter_Reset_ShouldNotCauseExceptions()
    {
        // Arrange
        var rateLimiter = await _optimizer.GetRateLimiterAsync(CalendarProvider.Google);
        
        // Consume some permits
        for (int i = 0; i < 10; i++)
        {
            await rateLimiter.WaitAsync(CancellationToken.None);
        }

        // Act - Wait for reset to occur (this should happen automatically via timer)
        // We can't directly trigger the reset method as it's private, but we can verify behavior
        await Task.Delay(3000); // Allow for potential reset

        // Assert - No exceptions should occur and rate limiter should remain functional
        rateLimiter.Should().NotBeNull();
        rateLimiter.IsThrottled.Should().BeFalse();
        
        // Should be able to make additional requests after reset
        var canMakeRequest = true;
        try
        {
            await rateLimiter.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
        }
        catch (OperationCanceledException)
        {
            canMakeRequest = false;
        }

        canMakeRequest.Should().BeTrue("Rate limiter should be functional after reset");
    }

    [Fact]
    public async Task RateLimiter_Throttling_ShouldPreventExcessiveRequests()
    {
        // Arrange
        var rateLimiter = await _optimizer.GetRateLimiterAsync(CalendarProvider.Outlook); // 30 requests per minute
        var successfulRequests = 0;

        // Act - Try to make more requests than the limit allows
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++) // More than the 30 limit
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await rateLimiter.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
                    Interlocked.Increment(ref successfulRequests);
                }
                catch (OperationCanceledException)
                {
                    // Expected when throttled
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        successfulRequests.Should().BeLessOrEqualTo(30, "Rate limiter should prevent excessive requests");
        rateLimiter.IsThrottled.Should().BeTrue("Rate limiter should be in throttled state after exceeding limit");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _optimizer?.Dispose();
            _disposed = true;
        }
    }
}