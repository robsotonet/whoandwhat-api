using FluentAssertions;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

public class AppTaskStatusTests
{
    [Fact]
    public void AppTaskStatus_Should_Have_All_Predefined_Statuses()
    {
        var allStatuses = DomainTaskStatus.GetAll().ToList();

        allStatuses.Should().HaveCount(4);
        allStatuses.Should().Contain(DomainTaskStatus.Pending);
        allStatuses.Should().Contain(DomainTaskStatus.InProgress);
        allStatuses.Should().Contain(DomainTaskStatus.Completed);
        allStatuses.Should().Contain(DomainTaskStatus.Archived);
    }

    [Fact]
    public void AppTaskStatus_Should_Create_From_Valid_Value()
    {
        var status = DomainTaskStatus.FromValue(1);

        status.Should().Be(DomainTaskStatus.InProgress);
        status.Name.Should().Be("InProgress");
        status.Value.Should().Be(1);
        status.Description.Should().Be("Task is currently being worked on");
    }

    [Fact]
    public void AppTaskStatus_Should_Throw_Exception_For_Invalid_Value()
    {
        Action act = () => DomainTaskStatus.FromValue(999);

        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task status value: 999*");
    }

    [Fact]
    public void AppTaskStatus_Should_Create_From_Valid_Name()
    {
        var status = DomainTaskStatus.FromName("Completed");

        status.Should().Be(DomainTaskStatus.Completed);
        status.Name.Should().Be("Completed");
        status.Value.Should().Be(2);
    }

    [Fact]
    public void AppTaskStatus_Should_Create_From_Valid_Name_Case_Insensitive()
    {
        var status = DomainTaskStatus.FromName("completed");

        status.Should().Be(DomainTaskStatus.Completed);
    }

    [Fact]
    public void AppTaskStatus_Should_Throw_Exception_For_Invalid_Name()
    {
        Action act = () => DomainTaskStatus.FromName("InvalidStatus");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task status name: InvalidStatus*");
    }

    [Fact]
    public void AppTaskStatus_Should_Throw_Exception_For_Null_Or_Empty_Name()
    {
        Action actNull = () => DomainTaskStatus.FromName(null!);
        Action actEmpty = () => DomainTaskStatus.FromName("");
        Action actWhiteSpace = () => DomainTaskStatus.FromName("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhiteSpace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AppTaskStatus_TryFromValue_Should_Return_True_For_Valid_Value()
    {
        var result = DomainTaskStatus.TryFromValue(0, out var status);

        result.Should().BeTrue();
        status.Should().Be(DomainTaskStatus.Pending);
    }

    [Fact]
    public void AppTaskStatus_TryFromValue_Should_Return_False_For_Invalid_Value()
    {
        var result = DomainTaskStatus.TryFromValue(999, out var status);

        result.Should().BeFalse();
        status.Should().BeNull();
    }

    [Fact]
    public void AppTaskStatus_TryFromName_Should_Return_True_For_Valid_Name()
    {
        var result = DomainTaskStatus.TryFromName("InProgress", out var status);

        result.Should().BeTrue();
        status.Should().Be(DomainTaskStatus.InProgress);
    }

    [Fact]
    public void AppTaskStatus_TryFromName_Should_Return_False_For_Invalid_Name()
    {
        var result = DomainTaskStatus.TryFromName("InvalidStatus", out var status);

        result.Should().BeFalse();
        status.Should().BeNull();
    }

    [Theory]
    [InlineData("Pending", "InProgress", true)]
    [InlineData("Pending", "Completed", true)]
    [InlineData("InProgress", "Completed", true)]
    [InlineData("InProgress", "Pending", true)]
    [InlineData("Completed", "Archived", true)]
    [InlineData("InProgress", "Archived", false)]
    [InlineData("Completed", "Pending", false)]
    [InlineData("Archived", "Completed", false)]
    [InlineData("Pending", "Pending", false)]
    public void AppTaskStatus_CanTransitionTo_Should_Follow_Business_Rules(string fromStatus, string toStatus, bool expectedResult)
    {
        var from = DomainTaskStatus.FromName(fromStatus);
        var to = DomainTaskStatus.FromName(toStatus);

        var canTransition = from.CanTransitionTo(to);

        canTransition.Should().Be(expectedResult);
    }

    [Fact]
    public void AppTaskStatus_GetValidTransitions_Should_Return_Correct_Transitions()
    {
        var pendingTransitions = DomainTaskStatus.Pending.GetValidTransitions().ToList();
        var inProgressTransitions = DomainTaskStatus.InProgress.GetValidTransitions().ToList();
        var completedTransitions = DomainTaskStatus.Completed.GetValidTransitions().ToList();
        var archivedTransitions = DomainTaskStatus.Archived.GetValidTransitions().ToList();

        pendingTransitions.Should().Contain(DomainTaskStatus.InProgress);
        pendingTransitions.Should().Contain(DomainTaskStatus.Completed);

        inProgressTransitions.Should().Contain(DomainTaskStatus.Completed);
        inProgressTransitions.Should().Contain(DomainTaskStatus.Pending);

        completedTransitions.Should().Contain(DomainTaskStatus.Archived);

        archivedTransitions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Archived", true)]
    public void AppTaskStatus_IsTerminal_Should_Return_Correct_Value(string statusName, bool expectedResult)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.IsTerminal().Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", false)]
    [InlineData("Archived", false)]
    public void AppTaskStatus_IsActive_Should_Return_Correct_Value(string statusName, bool expectedResult)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.IsActive().Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", true)]
    [InlineData("Archived", false)]
    public void AppTaskStatus_IsCompleted_Should_Return_Correct_Value(string statusName, bool expectedResult)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.IsCompleted().Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("Pending", "Pending")]
    [InlineData("InProgress", "In Progress")]
    [InlineData("Completed", "Completed")]
    [InlineData("Archived", "Archived")]
    public void AppTaskStatus_GetDisplayName_Should_Return_Correct_Display_Name(string statusName, string expectedDisplayName)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.GetDisplayName().Should().Be(expectedDisplayName);
    }

    [Theory]
    [InlineData("Pending", "status-pending")]
    [InlineData("InProgress", "status-in-progress")]
    [InlineData("Completed", "status-completed")]
    [InlineData("Archived", "status-archived")]
    public void AppTaskStatus_GetCssClass_Should_Return_Correct_CSS_Class(string statusName, string expectedCssClass)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.GetCssClass().Should().Be(expectedCssClass);
    }

    [Theory]
    [InlineData("Pending", "#6c757d")]
    [InlineData("InProgress", "#007bff")]
    [InlineData("Completed", "#28a745")]
    [InlineData("Archived", "#17a2b8")]
    public void AppTaskStatus_GetColorCode_Should_Return_Correct_Color(string statusName, string expectedColor)
    {
        var status = DomainTaskStatus.FromName(statusName);

        status.GetColorCode().Should().Be(expectedColor);
    }

    [Fact]
    public void AppTaskStatus_ValidateTransition_Should_Validate_Basic_Rules()
    {
        var validation = DomainTaskStatus.Pending.ValidateTransition(DomainTaskStatus.InProgress, false, null);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AppTaskStatus_ValidateTransition_Should_Prevent_Invalid_Transition()
    {
        var validation = DomainTaskStatus.Completed.ValidateTransition(DomainTaskStatus.Pending, false, null);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Cannot transition from Completed to Pending");
    }

    [Fact]
    public void AppTaskStatus_ValidateTransition_Should_Prevent_Completing_With_Active_Subtasks()
    {
        var validation = DomainTaskStatus.InProgress.ValidateTransition(DomainTaskStatus.Completed, true, AppTaskCategory.ToDo);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Cannot complete task while it has active subtasks");
    }

    [Fact]
    public void AppTaskStatus_ValidateTransition_Should_Allow_Completing_Project_With_Active_Subtasks()
    {
        var validation = DomainTaskStatus.InProgress.ValidateTransition(DomainTaskStatus.Completed, true, AppTaskCategory.Project);

        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AppTaskStatus_ValidateTransition_Should_Prevent_Archiving_Non_Completed_Task()
    {
        var validation = DomainTaskStatus.InProgress.ValidateTransition(DomainTaskStatus.Archived, false, null);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Only completed tasks can be archived");
    }

    [Fact]
    public void AppTaskStatus_Should_Support_Explicit_Conversion_To_Int()
    {
        int statusValue = (int)DomainTaskStatus.InProgress;

        statusValue.Should().Be(1);
    }

    [Fact]
    public void AppTaskStatus_Should_Support_Explicit_Conversion_From_Int()
    {
        var status = (DomainTaskStatus)2;

        status.Should().Be(DomainTaskStatus.Completed);
    }

    [Fact]
    public void AppTaskStatus_ToString_Should_Return_Name()
    {
        DomainTaskStatus.InProgress.ToString().Should().Be("InProgress");
    }
}
