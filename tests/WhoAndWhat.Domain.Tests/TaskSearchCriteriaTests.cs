using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Domain.Tests;

/// <summary>
/// Unit tests for TaskSearchCriteria value object
/// </summary>
public class TaskSearchCriteriaTests
{
    [Fact]
    public void TaskSearchCriteria_WithValidDefaults_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var criteria = new TaskSearchCriteria();

        // Assert
        criteria.Query.Should().BeEmpty();
        criteria.SortBy.Should().Be(TaskSearchSortBy.Relevance);
        criteria.SortDescending.Should().BeTrue();
        criteria.PageNumber.Should().Be(1);
        criteria.PageSize.Should().Be(20);
        criteria.IncludeCompleted.Should().BeTrue();
        criteria.IncludeArchived.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")] // Too short
    public void TaskSearchCriteria_WithInvalidQuery_ShouldFailValidation(string query)
    {
        // Arrange
        var criteria = new TaskSearchCriteria { Query = query };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void TaskSearchCriteria_WithTooLongQuery_ShouldFailValidation()
    {
        // Arrange
        var longQuery = new string('a', 201); // Exceeds 200 character limit
        var criteria = new TaskSearchCriteria { Query = longQuery };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("cannot exceed 200 characters"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TaskSearchCriteria_WithInvalidPageNumber_ShouldFailValidation(int pageNumber)
    {
        // Arrange
        var criteria = new TaskSearchCriteria { PageNumber = pageNumber };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Page number must be greater than 0"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)] // Exceeds max page size
    public void TaskSearchCriteria_WithInvalidPageSize_ShouldFailValidation(int pageSize)
    {
        // Arrange
        var criteria = new TaskSearchCriteria { PageSize = pageSize };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains($"Page size must be between 1 and {TaskSearchCriteria.MaxPageSize}"));
    }

    [Fact]
    public void TaskSearchCriteria_WithInvalidDateRange_ShouldFailValidation()
    {
        // Arrange
        var criteria = new TaskSearchCriteria
        {
            DueDateFrom = DateTime.Today.AddDays(1),
            DueDateTo = DateTime.Today
        };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Due date 'from' cannot be later than due date 'to'"));
    }

    [Fact]
    public void TaskSearchCriteria_WithInvalidCreatedDateRange_ShouldFailValidation()
    {
        // Arrange
        var criteria = new TaskSearchCriteria
        {
            CreatedAfter = DateTime.Today.AddDays(1),
            CreatedBefore = DateTime.Today
        };

        // Act
        var errors = criteria.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Created 'after' date cannot be later than created 'before' date"));
    }

    [Fact]
    public void TaskSearchCriteria_Normalize_ShouldClampValues()
    {
        // Arrange
        var criteria = new TaskSearchCriteria
        {
            Query = "  test query  ",
            PageNumber = 0,
            PageSize = 1000
        };

        // Act
        var normalized = criteria.Normalize();

        // Assert
        normalized.Query.Should().Be("test query");
        normalized.PageNumber.Should().Be(1);
        normalized.PageSize.Should().Be(TaskSearchCriteria.MaxPageSize);
    }

    [Theory]
    [InlineData("search term", true)]
    [InlineData("ab", true)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    public void TaskSearchCriteria_IsFullTextSearch_ShouldReturnCorrectValue(string query, bool expected)
    {
        // Arrange
        var criteria = new TaskSearchCriteria { Query = query };

        // Act & Assert
        criteria.IsFullTextSearch.Should().Be(expected);
    }

    [Fact]
    public void TaskSearchCriteria_HasFilters_WithNoFilters_ShouldReturnFalse()
    {
        // Arrange
        var criteria = new TaskSearchCriteria();

        // Act & Assert
        criteria.HasFilters.Should().BeFalse();
    }

    [Fact]
    public void TaskSearchCriteria_HasFilters_WithFilters_ShouldReturnTrue()
    {
        // Arrange
        var criteria = new TaskSearchCriteria { Category = TaskCategory.ToDo };

        // Act & Assert
        criteria.HasFilters.Should().BeTrue();
    }

    [Fact]
    public void TaskSearchCriteria_Offset_ShouldCalculateCorrectly()
    {
        // Arrange
        var criteria = new TaskSearchCriteria { PageNumber = 3, PageSize = 10 };

        // Act & Assert
        criteria.Offset.Should().Be(20); // (3-1) * 10
    }

    [Fact]
    public void TaskSearchCriteria_GetCacheKey_ShouldGenerateConsistentKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var criteria = new TaskSearchCriteria
        {
            Query = "test",
            Category = TaskCategory.ToDo,
            PageNumber = 1,
            PageSize = 20
        };

        // Act
        var key1 = criteria.GetCacheKey(userId);
        var key2 = criteria.GetCacheKey(userId);

        // Assert
        key1.Should().Be(key2);
        key1.Should().Contain(userId.ToString());
    }

    [Fact]
    public void TaskSearchCriteria_GetCacheKey_ShouldGenerateDifferentKeysForDifferentCriteria()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var criteria1 = new TaskSearchCriteria { Query = "test1" };
        var criteria2 = new TaskSearchCriteria { Query = "test2" };

        // Act
        var key1 = criteria1.GetCacheKey(userId);
        var key2 = criteria2.GetCacheKey(userId);

        // Assert
        key1.Should().NotBe(key2);
    }
}