using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the ArchiveCriteria value object
/// </summary>
public class ArchiveCriteriaTests
{
    [Fact]
    public void ArchiveCriteria_Should_Have_Default_Values()
    {
        // Arrange & Act
        var criteria = new ArchiveCriteria();

        // Assert
        criteria.MinimumCompletedAge.Should().Be(TimeSpan.FromDays(90));
        criteria.MinimumCanceledAge.Should().Be(TimeSpan.FromDays(30));
        criteria.IncludeActiveProjectTasks.Should().BeFalse();
        criteria.IncludeParentTasks.Should().BeTrue();
        criteria.MaxArchiveBatchSize.Should().Be(1000);
        criteria.ArchivableStatuses.Should().NotBeNull();
        criteria.UserId.Should().BeNull();
        criteria.MaxPriorityToArchive.Should().BeNull();
    }

    [Fact]
    public void ArchiveCriteria_Should_Allow_Custom_Values()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customAge = TimeSpan.FromDays(60);
        var customCanceledAge = TimeSpan.FromDays(15);

        // Act
        var criteria = new ArchiveCriteria
        {
            MinimumCompletedAge = customAge,
            MinimumCanceledAge = customCanceledAge,
            IncludeActiveProjectTasks = true,
            IncludeParentTasks = false,
            MaxArchiveBatchSize = 50,
            UserId = userId,
            MaxPriorityToArchive = Priority.Low
        };

        // Assert
        criteria.MinimumCompletedAge.Should().Be(customAge);
        criteria.MinimumCanceledAge.Should().Be(customCanceledAge);
        criteria.IncludeActiveProjectTasks.Should().BeTrue();
        criteria.IncludeParentTasks.Should().BeFalse();
        criteria.MaxArchiveBatchSize.Should().Be(50);
        criteria.UserId.Should().Be(userId);
        criteria.MaxPriorityToArchive.Should().Be(Priority.Low);
    }

    [Fact]
    public void ArchiveCriteria_Should_Have_Archivable_Statuses()
    {
        // Arrange & Act
        var criteria = new ArchiveCriteria();

        // Assert
        criteria.ArchivableStatuses.Should().NotBeNull();
        criteria.ArchivableStatuses.Should().NotBeEmpty();
        criteria.ArchivableStatuses.Should().Contain(DomainTaskStatus.Completed);
        criteria.ArchivableStatuses.Should().Contain(DomainTaskStatus.Archived);
    }

    [Fact]
    public void ArchiveCriteria_Should_Support_Record_Equality()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var criteria1 = new ArchiveCriteria
        {
            UserId = userId,
            MinimumCompletedAge = TimeSpan.FromDays(45),
            IncludeActiveProjectTasks = true
        };

        var criteria2 = new ArchiveCriteria
        {
            UserId = userId,
            MinimumCompletedAge = TimeSpan.FromDays(45),
            IncludeActiveProjectTasks = true
        };

        var criteria3 = new ArchiveCriteria
        {
            UserId = userId,
            MinimumCompletedAge = TimeSpan.FromDays(30),
            IncludeActiveProjectTasks = true
        };

        // Act & Assert
        criteria1.Should().Be(criteria2);
        criteria1.Should().NotBe(criteria3);
        (criteria1 == criteria2).Should().BeTrue();
        (criteria1 == criteria3).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    public void ArchiveCriteria_MaxArchiveBatchSize_Should_Handle_Various_Values(int batchSize, bool isValid)
    {
        // Arrange & Act
        var criteria = new ArchiveCriteria { MaxArchiveBatchSize = batchSize };

        // Assert
        if (isValid)
        {
            criteria.MaxArchiveBatchSize.Should().Be(batchSize);
        }
        else
        {
            // For invalid values, the criteria should still be created but may cause issues during validation
            criteria.MaxArchiveBatchSize.Should().Be(batchSize);
        }
    }

    [Fact]
    public void ArchiveCriteria_Should_Support_With_Expression()
    {
        // Arrange
        var originalCriteria = new ArchiveCriteria
        {
            MinimumCompletedAge = TimeSpan.FromDays(90),
            IncludeActiveProjectTasks = false
        };

        // Act
        var modifiedCriteria = originalCriteria with 
        { 
            MinimumCompletedAge = TimeSpan.FromDays(60),
            IncludeActiveProjectTasks = true 
        };

        // Assert
        originalCriteria.MinimumCompletedAge.Should().Be(TimeSpan.FromDays(90));
        originalCriteria.IncludeActiveProjectTasks.Should().BeFalse();
        
        modifiedCriteria.MinimumCompletedAge.Should().Be(TimeSpan.FromDays(60));
        modifiedCriteria.IncludeActiveProjectTasks.Should().BeTrue();
        modifiedCriteria.MinimumCanceledAge.Should().Be(originalCriteria.MinimumCanceledAge);
    }
}