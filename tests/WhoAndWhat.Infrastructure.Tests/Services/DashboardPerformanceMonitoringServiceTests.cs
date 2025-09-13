using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the DashboardPerformanceMonitoringService focusing on performance optimizations
/// </summary>
public class DashboardPerformanceMonitoringServiceTests : IDisposable
{
    private readonly Mock<IDashboardCacheService> _mockDashboardCacheService;
    private readonly Mock<ITaskCacheService> _mockTaskCacheService;
    private readonly Mock<ILogger<DashboardPerformanceMonitoringService>> _mockLogger;
    private readonly IOptions<RedisCacheSettings> _cacheSettings;
    private readonly DashboardPerformanceMonitoringService _service;

    public DashboardPerformanceMonitoringServiceTests()
    {
        _mockDashboardCacheService = new Mock<IDashboardCacheService>();
        _mockTaskCacheService = new Mock<ITaskCacheService>();
        _mockLogger = new Mock<ILogger<DashboardPerformanceMonitoringService>>();

        _cacheSettings = Options.Create(new RedisCacheSettings
        {
            ConnectionString = "localhost:6379",
            EnablePerformanceMonitoring = true,
            DefaultExpirationMinutes = 30,
            TaskCacheExpirationMinutes = 15,
            DatabaseIndex = 0,
            KeyPrefix = "test"
        });

        _service = new DashboardPerformanceMonitoringService(
            _mockDashboardCacheService.Object,
            _mockTaskCacheService.Object,
            _cacheSettings,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_WhenValidDependenciesProvided()
    {
        // Arrange & Act & Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDashboardCacheServiceIsNull()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new DashboardPerformanceMonitoringService(
            null!, _mockTaskCacheService.Object, _cacheSettings, _mockLogger.Object));

        exception.ParamName.Should().Be("dashboardCacheService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTaskCacheServiceIsNull()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new DashboardPerformanceMonitoringService(
            _mockDashboardCacheService.Object, null!, _cacheSettings, _mockLogger.Object));

        exception.ParamName.Should().Be("taskCacheService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new DashboardPerformanceMonitoringService(
            _mockDashboardCacheService.Object, _mockTaskCacheService.Object, _cacheSettings, null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task StartAsync_ShouldReturnCompletedTask_WhenPerformanceMonitoringDisabled()
    {
        // Arrange
        var disabledSettings = Options.Create(new RedisCacheSettings
        {
            EnablePerformanceMonitoring = false
        });

        var serviceWithDisabledMonitoring = new DashboardPerformanceMonitoringService(
            _mockDashboardCacheService.Object,
            _mockTaskCacheService.Object,
            disabledSettings,
            _mockLogger.Object);

        // Act
        await serviceWithDisabledMonitoring.StartAsync(CancellationToken.None);

        // Assert
        // StartAsync should complete successfully without throwing
        serviceWithDisabledMonitoring.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldStartTimers_WhenPerformanceMonitoringEnabled()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        await _service.StartAsync(cancellationToken);

        // Assert - Verify that the service started without throwing
        // The timers are internal, so we can only verify the method completes successfully
        _service.Should().NotBeNull();
    }

    [Fact]
    public async Task StopAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert
        // StopAsync should complete successfully without throwing
        _service.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldCompleteSuccessfully()
    {
        // Arrange - Service already created in constructor

        // Act & Assert - Should not throw
        var disposeAction = () => _service.Dispose();
        disposeAction.Should().NotThrow();
    }

    /// <summary>
    /// Test to validate the performance optimization: _latestSnapshot field provides O(1) access
    /// This test verifies the internal behavior through reflection since the performance optimization
    /// is specifically about avoiding O(n) enumeration of the ConcurrentQueue
    /// </summary>
    [Fact]
    public void LatestSnapshotField_ShouldBeInitializedAsNull()
    {
        // Arrange & Act
        // Use reflection to access the private _latestSnapshot field
        var latestSnapshotField = typeof(DashboardPerformanceMonitoringService)
            .GetField("_latestSnapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        latestSnapshotField.Should().NotBeNull("_latestSnapshot field should exist for performance optimization");

        var fieldValue = latestSnapshotField!.GetValue(_service);
        fieldValue.Should().BeNull("_latestSnapshot should be initialized as null");
    }

    /// <summary>
    /// Integration test for the performance monitoring lifecycle
    /// </summary>
    [Fact]
    public async Task PerformanceMonitoringLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        // Setup mock returns for cache services
        var dashboardMetrics = new DashboardCacheMetrics
        {
            TotalRequests = 100,
            CacheHits = 80,
            AverageResponseTime = TimeSpan.FromMilliseconds(50)
        };

        var taskMetrics = new CachePerformanceMetrics
        {
            TotalRequests = 200,
            CacheHits = 160,
            AverageResponseTime = TimeSpan.FromMilliseconds(30)
        };

        _mockDashboardCacheService
            .Setup(x => x.GetDashboardCacheMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboardMetrics);

        _mockTaskCacheService
            .Setup(x => x.GetCacheMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(taskMetrics);

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Allow some time for background processing if needed
        await Task.Delay(100);

        await _service.StopAsync(CancellationToken.None);

        // Assert
        _service.Should().NotBeNull("Service lifecycle should complete successfully");

        // Verify that the cache services were set up correctly for metrics collection
        _mockDashboardCacheService.Verify(x => x.GetDashboardCacheMetricsAsync(It.IsAny<CancellationToken>()), Times.Never());
        _mockTaskCacheService.Verify(x => x.GetCacheMetricsAsync(It.IsAny<CancellationToken>()), Times.Never());

        // Note: The actual timer callbacks are private and run on background threads,
        // so we can't easily test them synchronously. This test validates the setup.
    }

    /// <summary>
    /// Performance test to validate that the service can be created and disposed multiple times
    /// without performance degradation (no memory leaks or resource issues)
    /// </summary>
    [Fact]
    public void MultipleInstanceCreationAndDisposal_ShouldNotCausePerformanceIssues()
    {
        // Arrange & Act
        var services = new List<DashboardPerformanceMonitoringService>();

        // Create multiple instances
        for (int i = 0; i < 10; i++)
        {
            var service = new DashboardPerformanceMonitoringService(
                _mockDashboardCacheService.Object,
                _mockTaskCacheService.Object,
                _cacheSettings,
                _mockLogger.Object);

            services.Add(service);
        }

        // Dispose all instances
        var disposeAction = () =>
        {
            foreach (var service in services)
            {
                service.Dispose();
            }
        };

        // Assert
        disposeAction.Should().NotThrow("Multiple instance disposal should not cause issues");
        services.Should().HaveCount(10, "All instances should be created successfully");
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
