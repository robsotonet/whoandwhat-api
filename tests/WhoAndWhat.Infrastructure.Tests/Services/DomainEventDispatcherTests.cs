using FluentAssertions;
using MediatR;
using Moq;
using WhoAndWhat.Domain.Events;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the DomainEventDispatcher
/// </summary>
public class DomainEventDispatcherTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly DomainEventDispatcher _dispatcher;

    public DomainEventDispatcherTests()
    {
        _mockMediator = new Mock<IMediator>();
        _dispatcher = new DomainEventDispatcher(_mockMediator.Object);
    }

    [Fact]
    public void DomainEventDispatcher_Constructor_Should_Throw_When_Mediator_Is_Null()
    {
        // Act & Assert
        Action act = () => new DomainEventDispatcher(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*mediator*");
    }

    [Fact]
    public async Task Dispatch_Should_Call_Mediator_Publish()
    {
        // Arrange
        var domainEvent = new TestDomainEvent(Guid.NewGuid(), "Test Event");
        _mockMediator.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _dispatcher.Dispatch(domainEvent);

        // Assert
        _mockMediator.Verify(x => x.Publish(domainEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_Should_Handle_Multiple_Events()
    {
        // Arrange
        var event1 = new TestDomainEvent(Guid.NewGuid(), "Event 1");
        var event2 = new TestDomainEvent(Guid.NewGuid(), "Event 2");
        var event3 = new TestDomainEvent(Guid.NewGuid(), "Event 3");

        _mockMediator.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _dispatcher.Dispatch(event1);
        await _dispatcher.Dispatch(event2);
        await _dispatcher.Dispatch(event3);

        // Assert
        _mockMediator.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockMediator.Verify(x => x.Publish(event1, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(x => x.Publish(event2, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(x => x.Publish(event3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_Should_Propagate_Mediator_Exceptions()
    {
        // Arrange
        var domainEvent = new TestDomainEvent(Guid.NewGuid(), "Test Event");
        var expectedException = new InvalidOperationException("Mediator failed");

        _mockMediator.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dispatcher.Dispatch(domainEvent));

        exception.Should().BeSameAs(expectedException);
        _mockMediator.Verify(x => x.Publish(domainEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_Should_Work_With_Various_DomainEvent_Types()
    {
        // Arrange
        var userCreatedEvent = new UserCreatedEvent(Guid.NewGuid(), "test@example.com", "Test User");
        var userDeactivatedEvent = new UserDeactivatedEvent(Guid.NewGuid());

        _mockMediator.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _dispatcher.Dispatch(userCreatedEvent);
        await _dispatcher.Dispatch(userDeactivatedEvent);

        // Assert
        _mockMediator.Verify(x => x.Publish(userCreatedEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(x => x.Publish(userDeactivatedEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_Should_Handle_Concurrent_Events()
    {
        // Arrange
        var events = Enumerable.Range(1, 10)
            .Select(i => new TestDomainEvent(Guid.NewGuid(), $"Event {i}"))
            .ToArray();

        _mockMediator.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var tasks = events.Select(e => _dispatcher.Dispatch(e));
        await Task.WhenAll(tasks);

        // Assert
        _mockMediator.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
        foreach (var domainEvent in events)
        {
            _mockMediator.Verify(x => x.Publish(domainEvent, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>
    /// Test domain event for testing purposes
    /// </summary>
    private class TestDomainEvent : DomainEvent
    {
        public Guid Id { get; }
        public string Message { get; }

        public TestDomainEvent(Guid id, string message)
        {
            Id = id;
            Message = message;
        }
    }
}
