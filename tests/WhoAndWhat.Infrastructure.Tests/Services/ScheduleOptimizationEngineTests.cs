using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Services.SmartScheduling;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

public class ScheduleOptimizationEngineTests : IDisposable
{
    private readonly Mock<ILogger<ScheduleOptimizationEngine>> _loggerMock;
    private readonly Mock<IAppTaskRepository> _taskRepositoryMock;
    private readonly Mock<ISchedulingPatternRepository> _patternRepositoryMock;
    private readonly Mock<IUserSchedulingPreferenceRepository> _preferenceRepositoryMock;
    private readonly Mock<IAIPlanningService> _aiPlanningServiceMock;
    private readonly ScheduleOptimizationEngine _scheduleOptimizationEngine;
    private bool _disposed;

    public ScheduleOptimizationEngineTests()
    {
        _loggerMock = new Mock<ILogger<ScheduleOptimizationEngine>>();
        _taskRepositoryMock = new Mock<IAppTaskRepository>();
        _patternRepositoryMock = new Mock<ISchedulingPatternRepository>();
        _preferenceRepositoryMock = new Mock<IUserSchedulingPreferenceRepository>();
        _aiPlanningServiceMock = new Mock<IAIPlanningService>();

        _scheduleOptimizationEngine = new ScheduleOptimizationEngine(
            _loggerMock.Object,
            _taskRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstanceSuccessfully()
    {
        // Arrange & Act
        var engine = new ScheduleOptimizationEngine(
            _loggerMock.Object,
            _taskRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeAssignableTo<IScheduleOptimizationEngine>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ScheduleOptimizationEngine(
            null!,
            _taskRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullTaskRepository_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ScheduleOptimizationEngine(
            _loggerMock.Object,
            null!,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("taskRepository");
    }

    [Fact]
    public void Constructor_WithNullPatternRepository_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ScheduleOptimizationEngine(
            _loggerMock.Object,
            _taskRepositoryMock.Object,
            null!,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("patternRepository");
    }

    [Fact]
    public void Constructor_WithNullPreferenceRepository_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ScheduleOptimizationEngine(
            _loggerMock.Object,
            _taskRepositoryMock.Object,
            _patternRepositoryMock.Object,
            null!,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("preferenceRepository");
    }

    [Fact]
    public void Constructor_WithNullAIPlanningService_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ScheduleOptimizationEngine(
            _loggerMock.Object,
            _taskRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiPlanningService");
    }

    [Fact]
    public async Task OptimizeScheduleAsync_WithValidContext_ShouldReturnOptimizationResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var context = new ScheduleOptimizationContext(
            TaskIds: new List<Guid> { Guid.NewGuid() },
            OptimizationWindow: new TimeWindow(DateTime.Today, DateTime.Today.AddDays(1)),
            OptimizationGoals: new List<OptimizationGoal> { OptimizationGoal.MaximizeProductivity },
            Constraints: new List<SchedulingConstraint>(),
            Priority: OptimizationPriority.Balanced);

        // Setup mocks to return empty collections to avoid null reference exceptions
        _taskRepositoryMock.Setup(x => x.GetTasksByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppTask>());

        _patternRepositoryMock.Setup(x => x.GetOptimizationEligiblePatternsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SchedulingPattern>());

        _preferenceRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSchedulingPreference { UserId = userId });

        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _scheduleOptimizationEngine.OptimizeScheduleAsync(userId, context);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ScheduleOptimizationResult>();
        result.UserId.Should().Be(userId);
        result.OptimizedSchedule.Should().NotBeNull();
    }

    [Fact]
    public async Task OptimizeScheduleAsync_WithEmptyTaskIds_ShouldReturnResultWithEmptySchedule()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var context = new ScheduleOptimizationContext(
            TaskIds: new List<Guid>(),
            OptimizationWindow: new TimeWindow(DateTime.Today, DateTime.Today.AddDays(1)),
            OptimizationGoals: new List<OptimizationGoal> { OptimizationGoal.MaximizeProductivity },
            Constraints: new List<SchedulingConstraint>(),
            Priority: OptimizationPriority.Balanced);

        // Setup mocks
        _taskRepositoryMock.Setup(x => x.GetTasksByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppTask>());

        _patternRepositoryMock.Setup(x => x.GetOptimizationEligiblePatternsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SchedulingPattern>());

        _preferenceRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSchedulingPreference { UserId = userId });

        // Act
        var result = await _scheduleOptimizationEngine.OptimizeScheduleAsync(userId, context);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedSchedule.ScheduledItems.Should().BeEmpty();
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnTrueWhenEngineIsOperational()
    {
        // Arrange
        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _scheduleOptimizationEngine.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnFalseWhenAIServiceIsUnavailable()
    {
        // Arrange
        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _scheduleOptimizationEngine.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnFalseWhenExceptionOccurs()
    {
        // Arrange
        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _scheduleOptimizationEngine.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // No explicit cleanup needed for this test
            _disposed = true;
        }
    }
}