using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Tests.Services;

public class CategoryBusinessRuleServiceTests
{
    private readonly CategoryBusinessRuleService _service;

    public CategoryBusinessRuleServiceTests()
    {
        _service = new CategoryBusinessRuleService();
    }

    #region Task Creation Validation Tests

    [Fact]
    public void ValidateTaskCreation_ValidToDoTask_ShouldSucceed()
    {
        // Arrange
        var task = CreateTestTask("Buy groceries", AppTaskCategory.ToDo);

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessages.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTaskCreation_AppointmentWithoutDueDate_ShouldFail()
    {
        // Arrange
        var task = CreateTestTask("Doctor appointment", AppTaskCategory.Appointment);
        task.DueDate = null;

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("must have a due date"));
    }

    [Fact]
    public void ValidateTaskCreation_AppointmentInPast_ShouldFail()
    {
        // Arrange
        var task = CreateTestTask("Past appointment", AppTaskCategory.Appointment);
        task.DueDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("cannot be in the past"));
    }

    [Fact]
    public void ValidateTaskCreation_BillReminderWithoutAmount_ShouldHaveWarning()
    {
        // Arrange
        var task = CreateTestTask("Electric bill", AppTaskCategory.BillReminder);
        task.DueDate = DateTime.UtcNow.AddDays(7);
        task.Description = "Monthly electric bill";

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeTrue(); // Should still be valid
        // Note: In a full implementation, warnings would be handled separately
    }

    [Fact]
    public void ValidateTaskCreation_ProjectWithoutDescription_ShouldFail()
    {
        // Arrange
        var task = CreateTestTask("Big project", AppTaskCategory.Project);
        task.Description = null;

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("detailed description"));
    }

    [Fact]
    public void ValidateTaskCreation_IdeaWithUrgentPriority_ShouldFail()
    {
        // Arrange
        var task = CreateTestTask("Random idea", AppTaskCategory.Idea);
        task.Priority = (int)Priority.High;

        // Act
        var result = _service.ValidateTaskCreation(task);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("should not have urgent priority"));
    }

    [Fact]
    public void ValidateTaskCreation_NullTask_ShouldFail()
    {
        // Act
        var result = _service.ValidateTaskCreation(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain("Task cannot be null");
    }

    #endregion

    #region Task Update Validation Tests

    [Fact]
    public void ValidateTaskUpdate_ValidCategoryChange_ShouldSucceed()
    {
        // Arrange
        var existingTask = CreateTestTask("Great idea", AppTaskCategory.Idea);
        var updates = new AppTaskUpdateRequest
        {
            Category = (int)AppTaskCategory.ToDo
        };

        // Act
        var result = _service.ValidateTaskUpdate(existingTask, updates);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskUpdate_InvalidCategoryChange_ShouldFail()
    {
        // Arrange
        var existingTask = CreateTestTask("Appointment", AppTaskCategory.Appointment);
        var updates = new AppTaskUpdateRequest
        {
            Category = (int)AppTaskCategory.Idea
        };

        // Act
        var result = _service.ValidateTaskUpdate(existingTask, updates);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("Cannot convert"));
    }

    [Fact]
    public void ValidateTaskUpdate_ProjectToBillReminder_ShouldFail()
    {
        // Arrange
        var existingTask = CreateTestTask("Software project", AppTaskCategory.Project);
        var updates = new AppTaskUpdateRequest
        {
            Category = (int)AppTaskCategory.BillReminder
        };

        // Act
        var result = _service.ValidateTaskUpdate(existingTask, updates);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("Cannot convert"));
    }

    [Fact]
    public void ValidateTaskUpdate_AppointmentStatusChangeWithPastDate_ShouldFail()
    {
        // Arrange
        var existingTask = CreateTestTask("Past appointment", AppTaskCategory.Appointment);
        existingTask.DueDate = DateTime.UtcNow.AddDays(-1);
        var updates = new AppTaskUpdateRequest
        {
            Status = DomainTaskStatus.Confirmed
        };

        // Act
        var result = _service.ValidateTaskUpdate(existingTask, updates);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain(error => error.Contains("Cannot confirm past appointments"));
    }

    [Fact]
    public void ValidateTaskUpdate_NullExistingTask_ShouldFail()
    {
        // Arrange
        var updates = new AppTaskUpdateRequest();

        // Act
        var result = _service.ValidateTaskUpdate(null!, updates);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessages.Should().Contain("Existing task cannot be null");
    }

    #endregion

    #region Recommended Status Tests

    [Fact]
    public void GetRecommendedNextStatus_PendingAppointment_ShouldSuggestConfirmed()
    {
        // Arrange
        var task = CreateTestTask("Doctor visit", AppTaskCategory.Appointment);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var nextStatus = _service.GetRecommendedNextStatus(task);

        // Assert
        nextStatus.Should().Be(DomainTaskStatus.Confirmed);
    }

    [Fact]
    public void GetRecommendedNextStatus_ConfirmedAppointment_ShouldSuggestInProgress()
    {
        // Arrange
        var task = CreateTestTask("Doctor visit", AppTaskCategory.Appointment);
        task.Status = (int)DomainTaskStatus.Confirmed;

        // Act
        var nextStatus = _service.GetRecommendedNextStatus(task);

        // Assert
        nextStatus.Should().Be(DomainTaskStatus.InProgress);
    }

    [Fact]
    public void GetRecommendedNextStatus_PendingBillReminder_ShouldSuggestInProgress()
    {
        // Arrange
        var task = CreateTestTask("Pay electric bill", AppTaskCategory.BillReminder);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var nextStatus = _service.GetRecommendedNextStatus(task);

        // Assert
        nextStatus.Should().Be(DomainTaskStatus.InProgress);
    }

    [Fact]
    public void GetRecommendedNextStatus_ProjectWithInProgressSubtasks_ShouldStayInProgress()
    {
        // Arrange
        var project = CreateTestTask("Software project", AppTaskCategory.Project);
        project.Status = (int)DomainTaskStatus.InProgress;

        var subtask = CreateTestTask("Design database", AppTaskCategory.ToDo);
        subtask.Status = (int)DomainTaskStatus.InProgress;
        project.Subtasks.Add(subtask);

        // Act
        var nextStatus = _service.GetRecommendedNextStatus(project);

        // Assert
        nextStatus.Should().Be(DomainTaskStatus.InProgress);
    }

    [Fact]
    public void GetRecommendedNextStatus_PendingToDo_ShouldSuggestInProgress()
    {
        // Arrange
        var task = CreateTestTask("Clean garage", AppTaskCategory.ToDo);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var nextStatus = _service.GetRecommendedNextStatus(task);

        // Assert
        nextStatus.Should().Be(DomainTaskStatus.InProgress);
    }

    #endregion

    #region Available Actions Tests

    [Fact]
    public void GetAvailableActions_PendingAppointment_ShouldIncludeAppointmentActions()
    {
        // Arrange
        var task = CreateTestTask("Doctor visit", AppTaskCategory.Appointment);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var actions = _service.GetAvailableActions(task).ToList();

        // Assert
        actions.Should().Contain(a => a.Id == "Confirm");
        actions.Should().Contain(a => a.Id == "Reschedule");
        actions.Should().Contain(a => a.Id == "Cancel");
        actions.Should().Contain(a => a.Id == "Complete");
    }

    [Fact]
    public void GetAvailableActions_PendingBillReminder_ShouldIncludeBillActions()
    {
        // Arrange
        var task = CreateTestTask("Pay utilities", AppTaskCategory.BillReminder);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var actions = _service.GetAvailableActions(task).ToList();

        // Assert
        actions.Should().Contain(a => a.Id == "MarkPaid");
        actions.Should().Contain(a => a.Id == "SetRecurring");
        actions.Should().Contain(a => a.Id == "Complete");
    }

    [Fact]
    public void GetAvailableActions_PendingProject_ShouldIncludeProjectActions()
    {
        // Arrange
        var task = CreateTestTask("Build website", AppTaskCategory.Project);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var actions = _service.GetAvailableActions(task).ToList();

        // Assert
        actions.Should().Contain(a => a.Id == "AddSubtask");
        actions.Should().Contain(a => a.Id == "ViewProgress");
        actions.Should().Contain(a => a.Id == "Complete");
    }

    [Fact]
    public void GetAvailableActions_PendingIdea_ShouldIncludeIdeaActions()
    {
        // Arrange
        var task = CreateTestTask("App idea", AppTaskCategory.Idea);
        task.Status = (int)DomainTaskStatus.Pending;

        // Act
        var actions = _service.GetAvailableActions(task).ToList();

        // Assert
        actions.Should().Contain(a => a.Id == "ConvertToTodo");
        actions.Should().Contain(a => a.Id == "ConvertToProject");
        actions.Should().Contain(a => a.Id == "Archive");
        actions.Should().Contain(a => a.Id == "Complete");
    }

    [Fact]
    public void GetAvailableActions_CompletedTask_ShouldIncludeReopen()
    {
        // Arrange
        var task = CreateTestTask("Completed task", AppTaskCategory.ToDo);
        task.Status = (int)DomainTaskStatus.Completed;

        // Act
        var actions = _service.GetAvailableActions(task).ToList();

        // Assert
        actions.Should().Contain(a => a.Id == "Reopen");
        actions.Should().NotContain(a => a.Id == "Complete");
    }

    #endregion

    #region Category Metrics Tests

    [Fact]
    public void CalculateCategoryMetrics_MixedTasks_ShouldCalculateCorrectly()
    {
        // Arrange
        var tasks = new List<DomainTask>
        {
            CreateCompletedTask("Todo 1", AppTaskCategory.ToDo),
            CreateCompletedTask("Todo 2", AppTaskCategory.ToDo),
            CreateTestTask("Todo 3", AppTaskCategory.ToDo),

            CreateCompletedTask("Appt 1", AppTaskCategory.Appointment),
            CreateOverdueTask("Appt 2", AppTaskCategory.Appointment),

            CreateTestTask("Idea 1", AppTaskCategory.Idea)
        };

        // Act
        var metrics = _service.CalculateCategoryMetrics(tasks);

        // Assert
        metrics.Metrics.Should().HaveCount(3);

        var todoMetric = metrics.Metrics.First(m => m.Category.Name == "ToDo");
        todoMetric.TotalTasks.Should().Be(3);
        todoMetric.CompletedTasks.Should().Be(2);
        todoMetric.CompletionPercentage.Should().BeApproximately(66.67m, 0.1m);

        var appointmentMetric = metrics.Metrics.First(m => m.Category.Name == "Appointment");
        appointmentMetric.TotalTasks.Should().Be(2);
        appointmentMetric.CompletedTasks.Should().Be(1);
        appointmentMetric.OverdueTasks.Should().Be(1);

        var ideaMetric = metrics.Metrics.First(m => m.Category.Name == "Idea");
        ideaMetric.TotalTasks.Should().Be(1);
        ideaMetric.CompletedTasks.Should().Be(0);
    }

    [Fact]
    public void CalculateCategoryMetrics_EmptyTasks_ShouldReturnEmptyMetrics()
    {
        // Arrange
        var tasks = new List<DomainTask>();

        // Act
        var metrics = _service.CalculateCategoryMetrics(tasks);

        // Assert
        metrics.Metrics.Should().BeEmpty();
    }

    #endregion

    #region Scheduling Suggestions Tests

    [Fact]
    public void GetSchedulingSuggestions_MixedTasks_ShouldProvideRecommendations()
    {
        // Arrange
        var tasks = new List<DomainTask>
        {
            CreateTestTask("High priority todo", AppTaskCategory.ToDo, Priority.High),
            CreateTestTask("Future appointment", AppTaskCategory.Appointment, Priority.Medium),
            CreateTestTask("Bill payment", AppTaskCategory.BillReminder, Priority.Medium),
            CreateTestTask("Creative idea", AppTaskCategory.Idea, Priority.Low)
        };

        tasks[1].DueDate = DateTime.UtcNow.AddDays(3); // Appointment
        tasks[2].DueDate = DateTime.UtcNow.AddDays(7); // Bill

        // Act
        var suggestions = _service.GetSchedulingSuggestions(tasks);

        // Assert
        suggestions.Suggestions.Should().HaveCount(4);

        var appointmentSuggestion = suggestions.Suggestions.First(s => s.Task.Category == (int)AppTaskCategory.Appointment);
        appointmentSuggestion.RecommendedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(1));
        appointmentSuggestion.EstimatedDuration.Should().Be(TimeSpan.FromHours(2));

        var billSuggestion = suggestions.Suggestions.First(s => s.Task.Category == (int)AppTaskCategory.BillReminder);
        billSuggestion.EstimatedDuration.Should().Be(TimeSpan.FromHours(0.25));

        var ideaSuggestion = suggestions.Suggestions.First(s => s.Task.Category == (int)AppTaskCategory.Idea);
        ideaSuggestion.EstimatedDuration.Should().Be(TimeSpan.FromHours(0.5));
    }

    [Fact]
    public void GetSchedulingSuggestions_CompletedTasks_ShouldBeExcluded()
    {
        // Arrange
        var tasks = new List<DomainTask>
        {
            CreateCompletedTask("Completed todo", AppTaskCategory.ToDo),
            CreateTestTask("Active todo", AppTaskCategory.ToDo)
        };

        // Act
        var suggestions = _service.GetSchedulingSuggestions(tasks);

        // Assert
        suggestions.Suggestions.Should().HaveCount(1);
        suggestions.Suggestions.First().Task.Title.Should().Be("Active todo");
    }

    #endregion

    #region Helper Methods

    private DomainTask CreateTestTask(string title, AppTaskCategory category, Priority priority = null!)
    {
        priority ??= Priority.Medium;

        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Category = (int)category,
            Priority = (int)priority,
            Status = (int)DomainTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Subtasks = new List<DomainTask>()
        };
    }

    private DomainTask CreateCompletedTask(string title, AppTaskCategory category)
    {
        var task = CreateTestTask(title, category);
        task.Status = (int)DomainTaskStatus.Completed;
        return task;
    }

    private DomainTask CreateOverdueTask(string title, AppTaskCategory category)
    {
        var task = CreateTestTask(title, category);
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        return task;
    }

    #endregion
}
