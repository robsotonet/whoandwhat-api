using WhoAndWhat.Domain.Events;
using Xunit;
using FluentAssertions;
using System.Linq;

namespace WhoAndWhat.Domain.Tests;

// A sample entity for testing domain events
public class TestEntity : HasDomainEvents
{
    public void AddTestEvent()
    {
        AddDomainEvent(new TestDomainEvent());
    }
}

// A sample domain event for testing
public class TestDomainEvent : DomainEvent
{
}

public class DomainEventTests
{
    [Fact]
    public void Should_Add_Domain_Event()
    {
        // Arrange
        var entity = new TestEntity();

        // Act
        entity.AddTestEvent();

        // Assert
        entity.DomainEvents.Should().HaveCount(1);
        entity.DomainEvents.First().Should().BeOfType<TestDomainEvent>();
    }

    [Fact]
    public void Should_Clear_Domain_Events()
    {
        // Arrange
        var entity = new TestEntity();
        entity.AddTestEvent();

        // Act
        entity.ClearDomainEvents();

        // Assert
        entity.DomainEvents.Should().BeEmpty();
    }
}
