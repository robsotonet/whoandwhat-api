using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Tests.Entities;

public class EnhancedTaskEntityTests
{
    private DomainTask CreateValidTask()
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Description = "Test Description",
            DueDate = DateTime.UtcNow.AddDays(7),
            Priority = Priority.Medium.Value,
            Category = TaskCategory.ToDo.Value,
            Status = DomainTaskStatus.Pending.Value,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };
    }

    #region Calculated Properties Tests

    [Fact]
    public void IsOverdue_Should_Return_False_For_Task_Without_Due_Date()
    {
        var task = CreateValidTask();
        task.DueDate = null;
        
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_Should_Return_False_For_Completed_Task()
    {
        var task = CreateValidTask();
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        task.Status = DomainTaskStatus.Completed.Value;
        
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_Should_Return_False_For_Archived_Task()
    {
        var task = CreateValidTask();
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        task.Status = DomainTaskStatus.Archived.Value;
        
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_Should_Return_True_For_Past_Due_Active_Task()
    {
        var task = CreateValidTask();
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        task.Status = DomainTaskStatus.InProgress.Value;
        
        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void DaysUntilDue_Should_Return_Null_For_Task_Without_Due_Date()
    {
        var task = CreateValidTask();
        task.DueDate = null;
        
        task.DaysUntilDue.Should().BeNull();
    }

    [Fact]
    public void DaysUntilDue_Should_Return_Correct_Days_For_Future_Due_Date()
    {
        var task = CreateValidTask();
        task.DueDate = DateTime.UtcNow.Date.AddDays(5);
        
        task.DaysUntilDue.Should().Be(5);
    }

    [Fact]
    public void DaysUntilDue_Should_Return_Negative_For_Past_Due_Date()
    {
        var task = CreateValidTask();
        task.DueDate = DateTime.UtcNow.Date.AddDays(-2);
        
        task.DaysUntilDue.Should().Be(-2);
    }

    [Fact]
    public void CompletionPercentage_Should_Return_100_For_Completed_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        
        task.CompletionPercentage.Should().Be(100m);
    }

    [Fact]
    public void CompletionPercentage_Should_Return_50_For_InProgress_Task_Without_Subtasks()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        task.Subtasks = new List<DomainTask>();
        
        task.CompletionPercentage.Should().Be(50m);
    }

    [Fact]
    public void CompletionPercentage_Should_Return_0_For_Pending_Task_Without_Subtasks()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Pending.Value;
        task.Subtasks = new List<DomainTask>();
        
        task.CompletionPercentage.Should().Be(0m);
    }

    [Fact]
    public void CompletionPercentage_Should_Calculate_Based_On_Subtasks()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.Completed.Value },
            new() { Status = DomainTaskStatus.Completed.Value },
            new() { Status = DomainTaskStatus.Pending.Value },
            new() { Status = DomainTaskStatus.Pending.Value }
        };
        
        task.CompletionPercentage.Should().Be(50m); // 2 out of 4 completed
    }

    [Fact]
    public void HasActiveSubtasks_Should_Return_False_When_No_Subtasks()
    {
        var task = CreateValidTask();
        task.Subtasks = new List<DomainTask>();
        
        task.HasActiveSubtasks.Should().BeFalse();
    }

    [Fact]
    public void HasActiveSubtasks_Should_Return_True_When_Has_Active_Subtasks()
    {
        var task = CreateValidTask();
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.InProgress.Value },
            new() { Status = DomainTaskStatus.Completed.Value }
        };
        
        task.HasActiveSubtasks.Should().BeTrue();
    }

    [Fact]
    public void IsStandalone_Should_Return_True_When_No_Project()
    {
        var task = CreateValidTask();
        task.ProjectId = null;
        
        task.IsStandalone.Should().BeTrue();
    }

    [Fact]
    public void IsStandalone_Should_Return_False_When_Has_Project()
    {
        var task = CreateValidTask();
        task.ProjectId = Guid.NewGuid();
        
        task.IsStandalone.Should().BeFalse();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateTitle_Should_Pass_For_Valid_Title()
    {
        var task = CreateValidTask();
        task.Title = "Valid Title";
        
        var result = task.ValidateTitle();
        
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTitle_Should_Fail_For_Null_Title()
    {
        var task = CreateValidTask();
        task.Title = null!;
        
        var result = task.ValidateTitle();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Title is required");
    }

    [Fact]
    public void ValidateTitle_Should_Fail_For_Empty_Title()
    {
        var task = CreateValidTask();
        task.Title = "";
        
        var result = task.ValidateTitle();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Title is required");
    }

    [Fact]
    public void ValidateTitle_Should_Fail_For_Too_Long_Title()
    {
        var task = CreateValidTask();
        task.Title = new string('a', DomainTask.MaxTitleLength + 1);
        
        var result = task.ValidateTitle();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain($"Title cannot exceed {DomainTask.MaxTitleLength} characters");
    }

    [Fact]
    public void ValidateTitle_Should_Fail_For_Title_With_Leading_Or_Trailing_Whitespace()
    {
        var task = CreateValidTask();
        task.Title = " Title with whitespace ";
        
        var result = task.ValidateTitle();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Title cannot start or end with whitespace");
    }

    [Fact]
    public void ValidateDescription_Should_Pass_For_Valid_Description()
    {
        var task = CreateValidTask();
        task.Description = "Valid description";
        
        var result = task.ValidateDescription();
        
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDescription_Should_Pass_For_Null_Description()
    {
        var task = CreateValidTask();
        task.Description = null;
        
        var result = task.ValidateDescription();
        
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDescription_Should_Fail_For_Too_Long_Description()
    {
        var task = CreateValidTask();
        task.Description = new string('a', DomainTask.MaxDescriptionLength + 1);
        
        var result = task.ValidateDescription();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain($"Description cannot exceed {DomainTask.MaxDescriptionLength} characters");
    }

    [Fact]
    public void ValidateDueDate_Should_Pass_For_Valid_Due_Date()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.ToDo.Value;
        task.DueDate = DateTime.UtcNow.AddDays(1);
        
        var result = task.ValidateDueDate();
        
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDueDate_Should_Fail_For_Appointment_Without_Due_Date()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.Appointment.Value;
        task.DueDate = null;
        
        var result = task.ValidateDueDate();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Appointments must have a due date");
    }

    [Fact]
    public void ValidateDueDate_Should_Fail_For_BillReminder_Without_Due_Date()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.BillReminder.Value;
        task.DueDate = null;
        
        var result = task.ValidateDueDate();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Bill reminders must have a due date");
    }

    [Fact]
    public void ValidateDueDate_Should_Fail_For_Past_Due_Date_On_New_Task()
    {
        var task = CreateValidTask();
        task.CreatedAt = DateTime.MinValue; // Indicates new task
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        
        var result = task.ValidateDueDate();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Due date cannot be in the past");
    }

    [Fact]
    public void Validate_Should_Combine_All_Validations()
    {
        var task = CreateValidTask();
        task.Title = null!;
        task.Description = new string('a', DomainTask.MaxDescriptionLength + 1);
        task.Category = TaskCategory.Appointment.Value;
        task.DueDate = null;
        
        var result = task.Validate();
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(2);
        result.Errors.Should().Contain("Title is required");
        result.Errors.Should().Contain($"Description cannot exceed {DomainTask.MaxDescriptionLength} characters");
        result.Errors.Should().Contain("Appointments must have a due date");
    }

    #endregion

    #region Business Methods Tests

    [Fact]
    public void CanBeCompleted_Should_Return_True_For_Valid_Pending_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Pending.Value;
        
        task.CanBeCompleted().Should().BeTrue();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_True_For_InProgress_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        
        task.CanBeCompleted().Should().BeTrue();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_False_For_Already_Completed_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        
        task.CanBeCompleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_False_For_Archived_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Archived.Value;
        
        task.CanBeCompleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_False_For_Deleted_Task()
    {
        var task = CreateValidTask();
        task.IsDeleted = true;
        
        task.CanBeCompleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_False_For_Task_With_Active_Subtasks()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.ToDo.Value; // Not idea or project
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.InProgress.Value }
        };
        
        task.CanBeCompleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeCompleted_Should_Return_True_For_Idea_With_Active_Subtasks()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.Idea.Value;
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.InProgress.Value }
        };
        
        task.CanBeCompleted().Should().BeTrue();
    }

    [Fact]
    public void CanBeArchived_Should_Return_True_For_Completed_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        
        task.CanBeArchived().Should().BeTrue();
    }

    [Fact]
    public void CanBeArchived_Should_Return_True_For_Old_Pending_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Pending.Value;
        task.CreatedAt = DateTime.UtcNow.AddMonths(-7);
        
        task.CanBeArchived().Should().BeTrue();
    }

    [Fact]
    public void CanBeArchived_Should_Return_False_For_Recent_Pending_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Pending.Value;
        task.CreatedAt = DateTime.UtcNow.AddMonths(-3);
        
        task.CanBeArchived().Should().BeFalse();
    }

    [Fact]
    public void CanBeArchived_Should_Return_False_For_Already_Archived_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Archived.Value;
        
        task.CanBeArchived().Should().BeFalse();
    }

    [Fact]
    public void CanConvertToProject_Should_Return_True_For_Idea_With_Subtasks()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.Idea.Value;
        task.Status = DomainTaskStatus.Pending.Value;
        task.Subtasks = new List<DomainTask> { new() };
        
        task.CanConvertToProject().Should().BeTrue();
    }

    [Fact]
    public void CanConvertToProject_Should_Return_True_For_Task_With_Long_Description()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.ToDo.Value;
        task.Status = DomainTaskStatus.Pending.Value;
        task.Description = new string('a', 150);
        
        task.CanConvertToProject().Should().BeTrue();
    }

    [Fact]
    public void CanConvertToProject_Should_Return_False_For_Completed_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        
        task.CanConvertToProject().Should().BeFalse();
    }

    [Fact]
    public void CanConvertToProject_Should_Return_False_For_Already_Project()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.Project.Value;
        
        task.CanConvertToProject().Should().BeFalse();
    }

    [Fact]
    public void CanConvertToProject_Should_Return_False_For_Task_Already_In_Project()
    {
        var task = CreateValidTask();
        task.ProjectId = Guid.NewGuid();
        
        task.CanConvertToProject().Should().BeFalse();
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public void MarkInProgress_Should_Succeed_For_Pending_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Pending.Value;
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.MarkInProgress();
        
        result.Should().BeTrue();
        task.Status.Should().Be(DomainTaskStatus.InProgress.Value);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void MarkInProgress_Should_Succeed_For_InProgress_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        
        var result = task.MarkInProgress();
        
        result.Should().BeTrue();
        task.Status.Should().Be(DomainTaskStatus.InProgress.Value);
    }

    [Fact]
    public void MarkInProgress_Should_Fail_For_Completed_Task()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        
        var result = task.MarkInProgress();
        
        result.Should().BeFalse();
        task.Status.Should().Be(DomainTaskStatus.Completed.Value);
    }

    [Fact]
    public void MarkCompleted_Should_Succeed_When_Can_Be_Completed()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.MarkCompleted();
        
        result.Should().BeTrue();
        task.Status.Should().Be(DomainTaskStatus.Completed.Value);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void MarkCompleted_Should_Fail_When_Cannot_Be_Completed()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Archived.Value;
        
        var result = task.MarkCompleted();
        
        result.Should().BeFalse();
        task.Status.Should().Be(DomainTaskStatus.Archived.Value);
    }

    [Fact]
    public void MarkArchived_Should_Succeed_When_Can_Be_Archived()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.Completed.Value;
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.MarkArchived();
        
        result.Should().BeTrue();
        task.Status.Should().Be(DomainTaskStatus.Archived.Value);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void MarkArchived_Should_Fail_When_Cannot_Be_Archived()
    {
        var task = CreateValidTask();
        task.Status = DomainTaskStatus.InProgress.Value;
        
        var result = task.MarkArchived();
        
        result.Should().BeFalse();
        task.Status.Should().Be(DomainTaskStatus.InProgress.Value);
    }

    [Fact]
    public void SoftDelete_Should_Mark_Task_As_Deleted()
    {
        var task = CreateValidTask();
        var originalUpdatedAt = task.UpdatedAt;
        
        task.SoftDelete();
        
        task.IsDeleted.Should().BeTrue();
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Restore_Should_Unmark_Task_As_Deleted()
    {
        var task = CreateValidTask();
        task.IsDeleted = true;
        var originalUpdatedAt = task.UpdatedAt;
        
        task.Restore();
        
        task.IsDeleted.Should().BeFalse();
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region Update Methods Tests

    [Fact]
    public void UpdateTitle_Should_Succeed_For_Valid_Title()
    {
        var task = CreateValidTask();
        var newTitle = "New Valid Title";
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.UpdateTitle(newTitle);
        
        result.Should().BeTrue();
        task.Title.Should().Be(newTitle);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateTitle_Should_Trim_Whitespace()
    {
        var task = CreateValidTask();
        
        var result = task.UpdateTitle("  Trimmed Title  ");
        
        result.Should().BeTrue();
        task.Title.Should().Be("Trimmed Title");
    }

    [Fact]
    public void UpdateTitle_Should_Fail_For_Invalid_Title_And_Rollback()
    {
        var task = CreateValidTask();
        var originalTitle = task.Title;
        
        var result = task.UpdateTitle(null!);
        
        result.Should().BeFalse();
        task.Title.Should().Be(originalTitle);
    }

    [Fact]
    public void UpdateDescription_Should_Succeed_For_Valid_Description()
    {
        var task = CreateValidTask();
        var newDescription = "New valid description";
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.UpdateDescription(newDescription);
        
        result.Should().BeTrue();
        task.Description.Should().Be(newDescription);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateDescription_Should_Handle_Null_Description()
    {
        var task = CreateValidTask();
        
        var result = task.UpdateDescription(null);
        
        result.Should().BeTrue();
        task.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateDescription_Should_Fail_For_Too_Long_Description_And_Rollback()
    {
        var task = CreateValidTask();
        var originalDescription = task.Description;
        var tooLongDescription = new string('a', DomainTask.MaxDescriptionLength + 1);
        
        var result = task.UpdateDescription(tooLongDescription);
        
        result.Should().BeFalse();
        task.Description.Should().Be(originalDescription);
    }

    [Fact]
    public void UpdateDueDate_Should_Succeed_For_Valid_Due_Date()
    {
        var task = CreateValidTask();
        var newDueDate = DateTime.UtcNow.AddDays(10);
        var originalUpdatedAt = task.UpdatedAt;
        
        var result = task.UpdateDueDate(newDueDate);
        
        result.Should().BeTrue();
        task.DueDate.Should().Be(newDueDate);
        task.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateDueDate_Should_Fail_For_Invalid_Due_Date_And_Rollback()
    {
        var task = CreateValidTask();
        task.Category = TaskCategory.Appointment.Value;
        var originalDueDate = task.DueDate;
        
        var result = task.UpdateDueDate(null);
        
        result.Should().BeFalse();
        task.DueDate.Should().Be(originalDueDate);
    }

    #endregion
}