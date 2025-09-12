using FluentAssertions;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Tests.Services;

public class AppTaskWorkflowServiceTests
{
    private readonly AppTaskWorkflowService _service;

    public AppTaskWorkflowServiceTests()
    {
        _service = new AppTaskWorkflowService();
    }

    private DomainTask CreateValidTask(DomainTaskStatus? status = null, AppTaskCategory? category = null)
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Description = "Test Description",
            DueDate = DateTime.UtcNow.AddDays(7),
            Priority = Priority.Medium.Value,
            Category = (category ?? AppTaskCategory.ToDo).Value,
            Status = (status ?? DomainTaskStatus.Pending).Value,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };
    }

    #region TransitionTaskStatus Tests

    [Fact]
    public void TransitionTaskStatus_Should_Succeed_For_Valid_Transition()
    {
        var task = CreateValidTask(DomainTaskStatus.Pending);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.InProgress);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TransitionTaskStatus_Should_Fail_For_Invalid_Transition()
    {
        var task = CreateValidTask(DomainTaskStatus.Completed);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Pending);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot transition from Completed to Pending");
    }

    [Fact]
    public void TransitionTaskStatus_Should_Prevent_Completing_Appointment_Before_Time()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Appointment);
        task.DueDate = DateTime.UtcNow.AddHours(2);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Completed);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot mark appointment as completed before its scheduled time");
    }

    [Fact]
    public void TransitionTaskStatus_Should_Allow_Completing_Past_Appointment()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Appointment);
        task.DueDate = DateTime.UtcNow.AddHours(-1);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Completed);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TransitionTaskStatus_Should_Prevent_Early_Bill_Reminder_Completion()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.BillReminder);
        task.DueDate = DateTime.UtcNow.AddDays(5);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Completed);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Bill reminders should not be completed more than a day before due date");
    }

    [Fact]
    public void TransitionTaskStatus_Should_Prevent_Completing_Project_With_Active_Subtasks()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Project);
        var subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.InProgress.Value },
            new() { Status = DomainTaskStatus.Completed.Value }
        };

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Completed, subtasks);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot complete project while 1 subtasks are still active");
    }

    [Fact]
    public void TransitionTaskStatus_Should_Prevent_Archiving_Non_Completed_Task()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Archived);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Only completed tasks can be archived (except for very old pending tasks)");
    }

    [Fact]
    public void TransitionTaskStatus_Should_Allow_Archiving_Old_Pending_Task()
    {
        var task = CreateValidTask(DomainTaskStatus.Pending);
        task.CreatedAt = DateTime.UtcNow.AddMonths(-7);

        var result = _service.TransitionTaskStatus(task, DomainTaskStatus.Archived);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region AutoManageTaskStatuses Tests

    [Fact]
    public void AutoManageTaskStatuses_Should_Auto_Complete_Past_Appointments()
    {
        var tasks = new List<DomainTask>
        {
            CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Appointment)
        };
        tasks[0].DueDate = DateTime.UtcNow.AddHours(-3);

        var updates = _service.AutoManageTaskStatuses(tasks).ToList();

        updates.Should().HaveCount(1);
        updates[0].Task.Should().Be(tasks[0]);
        updates[0].NewStatus.Should().Be(DomainTaskStatus.Completed);
    }

    [Fact]
    public void AutoManageTaskStatuses_Should_Not_Auto_Complete_Recent_Appointments()
    {
        var tasks = new List<DomainTask>
        {
            CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Appointment)
        };
        tasks[0].DueDate = DateTime.UtcNow.AddHours(-1);

        var updates = _service.AutoManageTaskStatuses(tasks).ToList();

        updates.Should().BeEmpty();
    }

    [Fact]
    public void AutoManageTaskStatuses_Should_Auto_Archive_Old_Bill_Reminders()
    {
        var task = CreateValidTask(DomainTaskStatus.Completed, AppTaskCategory.BillReminder);
        task.UpdatedAt = DateTime.UtcNow.AddDays(-35);

        var updates = _service.AutoManageTaskStatuses(new[] { task }).ToList();

        updates.Should().HaveCount(1);
        updates[0].NewStatus.Should().Be(DomainTaskStatus.Archived);
    }

    [Fact]
    public void AutoManageTaskStatuses_Should_Not_Process_Already_Completed_Tasks()
    {
        var task = CreateValidTask(DomainTaskStatus.Completed);

        var updates = _service.AutoManageTaskStatuses(new[] { task }).ToList();

        updates.Should().BeEmpty();
    }

    #endregion

    #region SuggestPriorityEscalations Tests

    [Fact]
    public void SuggestPriorityEscalations_Should_Suggest_Higher_Priority_For_Due_Soon()
    {
        var tasks = new List<DomainTask>
        {
            CreateValidTask()
        };
        tasks[0].Priority = Priority.Low.Value;
        tasks[0].DueDate = DateTime.UtcNow.AddDays(1);

        var suggestions = _service.SuggestPriorityEscalations(tasks).ToList();

        suggestions.Should().HaveCount(1);
        suggestions[0].SuggestedPriority.Should().Be(Priority.High);
    }

    [Fact]
    public void SuggestPriorityEscalations_Should_Ensure_High_Priority_For_Urgent_Appointments()
    {
        var task = CreateValidTask(DomainTaskStatus.Pending, AppTaskCategory.Appointment);
        task.Priority = Priority.Low.Value;
        task.DueDate = DateTime.UtcNow.AddHours(12);

        var suggestions = _service.SuggestPriorityEscalations(new[] { task }).ToList();

        suggestions.Should().HaveCount(1);
        suggestions[0].SuggestedPriority.Should().Be(Priority.High);
    }

    [Fact]
    public void SuggestPriorityEscalations_Should_Not_Suggest_Lower_Priority()
    {
        var task = CreateValidTask();
        task.Priority = Priority.Urgent.Value;
        task.DueDate = DateTime.UtcNow.AddDays(7);

        var suggestions = _service.SuggestPriorityEscalations(new[] { task }).ToList();

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void SuggestPriorityEscalations_Should_Ignore_Tasks_Without_Due_Date()
    {
        var task = CreateValidTask();
        task.DueDate = null;

        var suggestions = _service.SuggestPriorityEscalations(new[] { task }).ToList();

        suggestions.Should().BeEmpty();
    }

    #endregion

    #region ValidateTaskDependencies Tests

    [Fact]
    public void ValidateTaskDependencies_Should_Pass_For_Valid_Project_With_Subtasks()
    {
        var parentTask = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Project);
        var subtasks = new List<DomainTask>
        {
            new()
            {
                UserId = parentTask.UserId,
                DueDate = parentTask.DueDate.Value.AddDays(-1),
                Priority = Priority.Medium.Value
            }
        };

        var result = _service.ValidateTaskDependencies(parentTask, subtasks);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskDependencies_Should_Fail_For_Category_Not_Allowing_Subtasks()
    {
        var parentTask = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Appointment);
        var subtasks = new List<DomainTask> { new() };

        var result = _service.ValidateTaskDependencies(parentTask, subtasks);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Appointment tasks cannot have subtasks");
    }

    [Fact]
    public void ValidateTaskDependencies_Should_Fail_For_Subtask_Due_After_Parent()
    {
        var parentTask = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Project);
        parentTask.DueDate = DateTime.UtcNow.AddDays(5);

        var subtasks = new List<DomainTask>
        {
            new()
            {
                DueDate = DateTime.UtcNow.AddDays(10),
                UserId = parentTask.UserId,
                Priority = Priority.Medium.Value
            }
        };

        var result = _service.ValidateTaskDependencies(parentTask, subtasks);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain($"Subtasks cannot have due dates later than parent task ({parentTask.DueDate:yyyy-MM-dd})");
    }

    [Fact]
    public void ValidateTaskDependencies_Should_Warn_For_Higher_Priority_Subtask()
    {
        var parentTask = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Project);
        parentTask.Priority = Priority.Medium.Value;

        var subtasks = new List<DomainTask>
        {
            new()
            {
                UserId = parentTask.UserId,
                Priority = Priority.Urgent.Value
            }
        };

        var result = _service.ValidateTaskDependencies(parentTask, subtasks);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subtasks should not have higher priority than their parent task");
    }

    [Fact]
    public void ValidateTaskDependencies_Should_Fail_For_Different_Users()
    {
        var parentTask = CreateValidTask(DomainTaskStatus.InProgress, AppTaskCategory.Project);
        var subtasks = new List<DomainTask>
        {
            new()
            {
                UserId = Guid.NewGuid(), // Different user
                Priority = Priority.Medium.Value
            }
        };

        var result = _service.ValidateTaskDependencies(parentTask, subtasks);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Parent and child tasks must belong to the same user");
    }

    #endregion

    #region CalculateTaskCompletionScore Tests

    [Fact]
    public void CalculateTaskCompletionScore_Should_Return_100_For_Completed_Task()
    {
        var task = CreateValidTask(DomainTaskStatus.Completed);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(100.0);
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Return_100_For_Archived_Task()
    {
        var task = CreateValidTask(DomainTaskStatus.Archived);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(100.0);
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Return_50_For_InProgress_Task_Without_Subtasks()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(50.0);
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Return_0_For_Pending_Task()
    {
        var task = CreateValidTask(DomainTaskStatus.Pending);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Use_Subtask_Completion_Percentage()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.Completed.Value },
            new() { Status = DomainTaskStatus.Completed.Value },
            new() { Status = DomainTaskStatus.Completed.Value },
            new() { Status = DomainTaskStatus.Pending.Value }
        };

        var subtasks = task.Subtasks;
        var score = _service.CalculateTaskCompletionScore(task, subtasks);

        score.Should().Be(75.0); // 3 out of 4 completed
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Apply_Overdue_Penalty()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);
        task.DueDate = DateTime.UtcNow.AddDays(-1);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(40.0); // 50 * 0.8 penalty
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Add_Age_Bonus_For_InProgress_Tasks()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);
        task.UpdatedAt = DateTime.UtcNow.AddDays(-5);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(60.0); // 50 + (5 * 2) age bonus
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Cap_Age_Bonus_At_20_Points()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);
        task.UpdatedAt = DateTime.UtcNow.AddDays(-20);

        var score = _service.CalculateTaskCompletionScore(task);

        score.Should().Be(70.0); // 50 + 20 (capped) age bonus
    }

    [Fact]
    public void CalculateTaskCompletionScore_Should_Cap_Total_Score_At_100()
    {
        var task = CreateValidTask(DomainTaskStatus.InProgress);
        task.UpdatedAt = DateTime.UtcNow.AddDays(-50);
        task.Subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.Completed.Value }
        };

        var score = _service.CalculateTaskCompletionScore(task, task.Subtasks);

        score.Should().Be(100.0); // Capped at 100
    }

    #endregion
}
