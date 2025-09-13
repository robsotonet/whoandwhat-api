using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Services.SmartScheduling;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

public class TimeBlockManagerTests : IDisposable
{
    private readonly Mock<ILogger<TimeBlockManager>> _loggerMock;
    private readonly Mock<ITimeBlockRepository> _timeBlockRepositoryMock;
    private readonly Mock<ISchedulingPatternRepository> _patternRepositoryMock;
    private readonly Mock<IUserSchedulingPreferenceRepository> _preferenceRepositoryMock;
    private readonly Mock<IAIPlanningService> _aiPlanningServiceMock;
    private readonly TimeBlockManager _timeBlockManager;
    private bool _disposed;

    public TimeBlockManagerTests()
    {
        _loggerMock = new Mock<ILogger<TimeBlockManager>>();
        _timeBlockRepositoryMock = new Mock<ITimeBlockRepository>();
        _patternRepositoryMock = new Mock<ISchedulingPatternRepository>();
        _preferenceRepositoryMock = new Mock<IUserSchedulingPreferenceRepository>();
        _aiPlanningServiceMock = new Mock<IAIPlanningService>();

        _timeBlockManager = new TimeBlockManager(
            _loggerMock.Object,
            _timeBlockRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstanceSuccessfully()
    {
        // Arrange & Act
        var manager = new TimeBlockManager(
            _loggerMock.Object,
            _timeBlockRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        // Assert
        manager.Should().NotBeNull();
        manager.Should().BeAssignableTo<ITimeBlockManager>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new TimeBlockManager(
            null!,
            _timeBlockRepositoryMock.Object,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullTimeBlockRepository_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new TimeBlockManager(
            _loggerMock.Object,
            null!,
            _patternRepositoryMock.Object,
            _preferenceRepositoryMock.Object,
            _aiPlanningServiceMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timeBlockRepository");
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnTrueWhenAIServiceIsAvailable()
    {
        // Arrange
        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _timeBlockManager.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnFalseWhenAIServiceIsNotAvailable()
    {
        // Arrange
        _aiPlanningServiceMock.Setup(x => x.IsAIServiceAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _timeBlockManager.IsAvailableAsync();

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
        var result = await _timeBlockManager.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateTimeBlocksAsync_WithValidParameters_ShouldReturnTimeBlocks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scheduledItems = new List<SmartScheduledItem>();
        var preferences = new SmartSchedulingPreferences();

        _patternRepositoryMock.Setup(x => x.GetOptimizationEligiblePatternsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SchedulingPattern>());

        // Act
        var result = await _timeBlockManager.GenerateTimeBlocksAsync(userId, scheduledItems, preferences);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<TimeBlockSuggestion>>();
    }

    [Fact]
    public async Task CreateBufferBlocksAsync_WithValidParameters_ShouldReturnBufferBlocks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scheduledItems = new List<SmartScheduledItem>
        {
            new(Guid.NewGuid(), "Task 1", DateTime.Now, DateTime.Now.AddHours(1), 1, "Description"),
            new(Guid.NewGuid(), "Task 2", DateTime.Now.AddHours(2), DateTime.Now.AddHours(3), 2, "Description")
        };
        var bufferDuration = TimeSpan.FromMinutes(15);

        // Act
        var result = await _timeBlockManager.CreateBufferBlocksAsync(userId, scheduledItems, bufferDuration);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<TimeBlockSuggestion>>();
        result.All(b => b.Purpose == Domain.Entities.TimeBlockPurpose.Buffer).Should().BeTrue();
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