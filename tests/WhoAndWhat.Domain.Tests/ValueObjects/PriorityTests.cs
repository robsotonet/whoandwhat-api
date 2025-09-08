using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

public class PriorityTests
{
    [Fact]
    public void Priority_Should_Have_All_Predefined_Priorities()
    {
        var allPriorities = Priority.GetAll().ToList();
        
        allPriorities.Should().HaveCount(4);
        allPriorities.Should().Contain(Priority.Low);
        allPriorities.Should().Contain(Priority.Medium);
        allPriorities.Should().Contain(Priority.High);
        allPriorities.Should().Contain(Priority.Urgent);
    }

    [Fact]
    public void Priority_GetAll_Should_Return_Ordered_By_Importance()
    {
        var priorities = Priority.GetAll().ToList();
        
        priorities[0].Should().Be(Priority.Urgent);
        priorities[1].Should().Be(Priority.High);
        priorities[2].Should().Be(Priority.Medium);
        priorities[3].Should().Be(Priority.Low);
    }

    [Fact]
    public void Priority_GetAllByValue_Should_Return_Ordered_By_Value()
    {
        var priorities = Priority.GetAllByValue().ToList();
        
        priorities[0].Should().Be(Priority.Low);
        priorities[1].Should().Be(Priority.Medium);
        priorities[2].Should().Be(Priority.High);
        priorities[3].Should().Be(Priority.Urgent);
    }

    [Fact]
    public void Priority_Should_Have_Correct_Static_Values()
    {
        Priority.Low.Name.Should().Be("Low");
        Priority.Low.Value.Should().Be(0);
        Priority.Low.SortOrder.Should().Be(7);
        
        Priority.Medium.Name.Should().Be("Medium");
        Priority.Medium.Value.Should().Be(1);
        Priority.Medium.SortOrder.Should().Be(3);
        
        Priority.High.Name.Should().Be("High");
        Priority.High.Value.Should().Be(2);
        Priority.High.SortOrder.Should().Be(1);
        
        Priority.Urgent.Name.Should().Be("Urgent");
        Priority.Urgent.Value.Should().Be(3);
        Priority.Urgent.SortOrder.Should().Be(0);
    }

    [Fact]
    public void Priority_Should_Create_From_Valid_Value()
    {
        var priority = Priority.FromValue(2);
        
        priority.Should().Be(Priority.High);
        priority.Name.Should().Be("High");
        priority.Value.Should().Be(2);
        priority.Description.Should().Be("High priority - should be done today");
        priority.ColorCode.Should().Be("#fd7e14");
    }

    [Fact]
    public void Priority_Should_Throw_Exception_For_Invalid_Value()
    {
        Action act = () => Priority.FromValue(999);
        
        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid priority value: 999*");
    }

    [Fact]
    public void Priority_Should_Create_From_Valid_Name()
    {
        var priority = Priority.FromName("Urgent");
        
        priority.Should().Be(Priority.Urgent);
        priority.Name.Should().Be("Urgent");
        priority.Value.Should().Be(3);
    }

    [Fact]
    public void Priority_Should_Create_From_Valid_Name_Case_Insensitive()
    {
        var priority = Priority.FromName("urgent");
        
        priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public void Priority_Should_Throw_Exception_For_Invalid_Name()
    {
        Action act = () => Priority.FromName("InvalidPriority");
        
        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid priority name: InvalidPriority*");
    }

    [Fact]
    public void Priority_TryFromValue_Should_Work_Correctly()
    {
        var result1 = Priority.TryFromValue(1, out var priority1);
        var result2 = Priority.TryFromValue(999, out var priority2);
        
        result1.Should().BeTrue();
        priority1.Should().Be(Priority.Medium);
        
        result2.Should().BeFalse();
        priority2.Should().BeNull();
    }

    [Fact]
    public void Priority_TryFromName_Should_Work_Correctly()
    {
        var result1 = Priority.TryFromName("High", out var priority1);
        var result2 = Priority.TryFromName("Invalid", out var priority2);
        var result3 = Priority.TryFromName(null, out var priority3);
        
        result1.Should().BeTrue();
        priority1.Should().Be(Priority.High);
        
        result2.Should().BeFalse();
        priority2.Should().BeNull();
        
        result3.Should().BeFalse();
        priority3.Should().BeNull();
    }

    [Fact]
    public void Priority_GetHighImportance_Should_Return_High_And_Urgent()
    {
        var highImportance = Priority.GetHighImportance().ToList();
        
        highImportance.Should().HaveCount(2);
        highImportance.Should().Contain(Priority.High);
        highImportance.Should().Contain(Priority.Urgent);
    }

    [Fact]
    public void Priority_GetLowImportance_Should_Return_Low_And_Medium()
    {
        var lowImportance = Priority.GetLowImportance().ToList();
        
        lowImportance.Should().HaveCount(2);
        lowImportance.Should().Contain(Priority.Low);
        lowImportance.Should().Contain(Priority.Medium);
    }

    [Theory]
    [InlineData("Low", true)]
    [InlineData("Medium", true)]
    [InlineData("High", true)]
    [InlineData("Urgent", false)]
    public void Priority_CanEscalate_Should_Return_Correct_Value(string priorityName, bool expectedCanEscalate)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.CanEscalate().Should().Be(expectedCanEscalate);
    }

    [Theory]
    [InlineData("Low", "Medium")]
    [InlineData("Medium", "High")]
    [InlineData("High", "Urgent")]
    [InlineData("Urgent", "Urgent")]
    public void Priority_Escalate_Should_Return_Correct_Priority(string fromPriority, string expectedToPriority)
    {
        var priority = Priority.FromName(fromPriority);
        var escalated = priority.Escalate();
        
        escalated.Name.Should().Be(expectedToPriority);
    }

    [Theory]
    [InlineData("Low", false)]
    [InlineData("Medium", true)]
    [InlineData("High", true)]
    [InlineData("Urgent", true)]
    public void Priority_CanDeEscalate_Should_Return_Correct_Value(string priorityName, bool expectedCanDeEscalate)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.CanDeEscalate().Should().Be(expectedCanDeEscalate);
    }

    [Theory]
    [InlineData("Low", "Low")]
    [InlineData("Medium", "Low")]
    [InlineData("High", "Medium")]
    [InlineData("Urgent", "High")]
    public void Priority_DeEscalate_Should_Return_Correct_Priority(string fromPriority, string expectedToPriority)
    {
        var priority = Priority.FromName(fromPriority);
        var deEscalated = priority.DeEscalate();
        
        deEscalated.Name.Should().Be(expectedToPriority);
    }

    [Fact]
    public void Priority_IsHigherThan_Should_Work_Correctly()
    {
        Priority.Urgent.IsHigherThan(Priority.High).Should().BeTrue();
        Priority.High.IsHigherThan(Priority.Medium).Should().BeTrue();
        Priority.Medium.IsHigherThan(Priority.Low).Should().BeTrue();
        Priority.Low.IsHigherThan(Priority.Medium).Should().BeFalse();
    }

    [Fact]
    public void Priority_IsLowerThan_Should_Work_Correctly()
    {
        Priority.Low.IsLowerThan(Priority.Medium).Should().BeTrue();
        Priority.Medium.IsLowerThan(Priority.High).Should().BeTrue();
        Priority.High.IsLowerThan(Priority.Urgent).Should().BeTrue();
        Priority.Urgent.IsLowerThan(Priority.High).Should().BeFalse();
    }

    [Theory]
    [InlineData("Urgent", 0)]
    [InlineData("High", 1)]
    [InlineData("Medium", 7)]
    [InlineData("Low", 30)]
    public void Priority_GetRecommendedDueDateOffset_Should_Return_Correct_Days(string priorityName, int expectedDays)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.GetRecommendedDueDateOffset().Should().Be(expectedDays);
    }

    [Theory]
    [InlineData("Urgent", 1)]
    [InlineData("High", 4)]
    [InlineData("Medium", 24)]
    [InlineData("Low", 168)]
    public void Priority_GetNotificationLeadHours_Should_Return_Correct_Hours(string priorityName, int expectedHours)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.GetNotificationLeadHours().Should().Be(expectedHours);
    }

    [Fact]
    public void Priority_SuggestFromDueDate_Should_Suggest_Correct_Priority_For_No_Due_Date()
    {
        var suggested = Priority.SuggestFromDueDate(null);
        
        suggested.Should().Be(Priority.Medium);
    }

    [Fact]
    public void Priority_SuggestFromDueDate_Should_Suggest_Urgent_For_Overdue()
    {
        var overdue = DateTime.UtcNow.AddDays(-1);
        var suggested = Priority.SuggestFromDueDate(overdue);
        
        suggested.Should().Be(Priority.Urgent);
    }

    [Fact]
    public void Priority_SuggestFromDueDate_Should_Suggest_High_For_Due_Tomorrow()
    {
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var suggested = Priority.SuggestFromDueDate(tomorrow);
        
        suggested.Should().Be(Priority.High);
    }

    [Fact]
    public void Priority_SuggestFromDueDate_Should_Suggest_Medium_For_Due_This_Week()
    {
        var thisWeek = DateTime.UtcNow.AddDays(5);
        var suggested = Priority.SuggestFromDueDate(thisWeek);
        
        suggested.Should().Be(Priority.Medium);
    }

    [Fact]
    public void Priority_SuggestFromDueDate_Should_Suggest_Low_For_Due_Later()
    {
        var later = DateTime.UtcNow.AddDays(30);
        var suggested = Priority.SuggestFromDueDate(later);
        
        suggested.Should().Be(Priority.Low);
    }

    [Fact]
    public void Priority_ValidatePriorityAssignment_Should_Pass_For_Valid_Assignment()
    {
        var validation = Priority.High.ValidatePriorityAssignment(AppTaskCategory.Appointment, DateTime.UtcNow.AddDays(1));
        
        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Priority_ValidatePriorityAssignment_Should_Warn_For_Low_Priority_Appointment()
    {
        var validation = Priority.Low.ValidatePriorityAssignment(AppTaskCategory.Appointment, DateTime.UtcNow.AddDays(1));
        
        validation.IsValid.Should().BeTrue(); // Warnings don't make it invalid
        validation.Errors.Should().Contain("Appointments typically should be High or Urgent priority");
    }

    [Theory]
    [InlineData("Low", 1.0)]
    [InlineData("Medium", 2.0)]
    [InlineData("High", 4.0)]
    [InlineData("Urgent", 8.0)]
    public void Priority_GetWeight_Should_Return_Correct_Weight(string priorityName, double expectedWeight)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.GetWeight().Should().Be(expectedWeight);
    }

    [Theory]
    [InlineData("Low", "priority-low")]
    [InlineData("Medium", "priority-medium")]
    [InlineData("High", "priority-high")]
    [InlineData("Urgent", "priority-urgent")]
    public void Priority_GetCssClass_Should_Return_Correct_CSS_Class(string priorityName, string expectedCssClass)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.GetCssClass().Should().Be(expectedCssClass);
    }

    [Theory]
    [InlineData("Low", "arrow-down")]
    [InlineData("Medium", "minus")]
    [InlineData("High", "arrow-up")]
    [InlineData("Urgent", "exclamation-triangle")]
    public void Priority_GetIconName_Should_Return_Correct_Icon(string priorityName, string expectedIcon)
    {
        var priority = Priority.FromName(priorityName);
        
        priority.GetIconName().Should().Be(expectedIcon);
    }

    [Fact]
    public void Priority_Should_Support_Comparison_Operators()
    {
        (Priority.Urgent > Priority.High).Should().BeTrue();
        (Priority.High > Priority.Medium).Should().BeTrue();
        (Priority.Medium > Priority.Low).Should().BeTrue();
        
        (Priority.Low < Priority.Medium).Should().BeTrue();
        (Priority.Medium < Priority.High).Should().BeTrue();
        (Priority.High < Priority.Urgent).Should().BeTrue();
        
        (Priority.High >= Priority.High).Should().BeTrue();
        (Priority.High >= Priority.Medium).Should().BeTrue();
        (Priority.Medium >= Priority.High).Should().BeFalse();
        
        (Priority.Medium <= Priority.Medium).Should().BeTrue();
        (Priority.Medium <= Priority.High).Should().BeTrue();
        (Priority.High <= Priority.Medium).Should().BeFalse();
    }

    [Fact]
    public void Priority_CompareTo_Should_Work_Correctly()
    {
        Priority.Urgent.CompareTo(Priority.High).Should().BeLessThan(0);
        Priority.High.CompareTo(Priority.High).Should().Be(0);
        Priority.Low.CompareTo(Priority.High).Should().BeGreaterThan(0);
        Priority.Medium.CompareTo(null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Priority_Should_Support_Explicit_Conversion_To_Int()
    {
        int priorityValue = (int)Priority.High;
        
        priorityValue.Should().Be(2);
    }

    [Fact]
    public void Priority_Should_Support_Explicit_Conversion_From_Int()
    {
        var priority = (Priority)3;
        
        priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public void Priority_ToString_Should_Return_Name()
    {
        Priority.High.ToString().Should().Be("High");
    }

    [Fact]
    public void Priority_GetDisplayName_Should_Return_Name()
    {
        Priority.Medium.GetDisplayName().Should().Be("Medium");
    }
}