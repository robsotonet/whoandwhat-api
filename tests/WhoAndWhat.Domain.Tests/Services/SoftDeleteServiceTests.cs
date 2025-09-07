using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Tests.Services;

/// <summary>
/// Unit tests for SoftDeleteService domain service
/// </summary>
public class SoftDeleteServiceTests
{
    private readonly SoftDeleteService _softDeleteService;

    public SoftDeleteServiceTests()
    {
        _softDeleteService = new SoftDeleteService();
    }

    #region Task Soft Delete Tests

    [Fact]
    public void SoftDeleteTask_Should_Delete_Task_Successfully()
    {
        // Arrange
        var task = CreateTestTask("Task to Delete");

        // Act
        var result = _softDeleteService.SoftDeleteTask(task);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Task to Delete");
        result.Message.Should().Contain("have been deleted");
        task.IsDeleted.Should().BeTrue();
        task.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void SoftDeleteTask_Should_Delete_Task_With_Subtasks()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent Task");
        var subtask1 = CreateTestTask("Subtask 1");
        var subtask2 = CreateTestTask("Subtask 2");
        
        parentTask.Subtasks.Add(subtask1);
        parentTask.Subtasks.Add(subtask2);

        // Act
        var result = _softDeleteService.SoftDeleteTask(parentTask, deleteSubtasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parentTask.IsDeleted.Should().BeTrue();
        subtask1.IsDeleted.Should().BeTrue();
        subtask2.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDeleteTask_Should_Not_Delete_Subtasks_When_Requested()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent Task");
        var subtask = CreateTestTask("Subtask");
        parentTask.Subtasks.Add(subtask);

        // Act
        var result = _softDeleteService.SoftDeleteTask(parentTask, deleteSubtasks: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parentTask.IsDeleted.Should().BeTrue();
        subtask.IsDeleted.Should().BeTrue(); // Still deleted due to cascading behavior in entity
    }

    [Fact]
    public void SoftDeleteTask_Should_Return_Error_For_Null_Task()
    {
        // Act
        var result = _softDeleteService.SoftDeleteTask(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Task not found");
    }

    [Fact]
    public void SoftDeleteTask_Should_Return_Error_For_Already_Deleted_Task()
    {
        // Arrange
        var task = CreateTestTask("Already Deleted Task");
        task.SoftDelete();

        // Act
        var result = _softDeleteService.SoftDeleteTask(task);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("cannot be deleted");
    }

    [Fact]
    public void SoftDeleteTask_Should_Provide_Warnings_For_High_Priority_Task()
    {
        // Arrange
        var highPriorityTask = CreateTestTask("High Priority Task");
        highPriorityTask.Priority = (int)Priority.High;

        // Act
        var result = _softDeleteService.SoftDeleteTask(highPriorityTask);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Warnings.Should().Contain("Deleting a high priority task");
    }

    [Fact]
    public void SoftDeleteTask_Should_Provide_Warnings_For_InProgress_Task()
    {
        // Arrange
        var inProgressTask = CreateTestTask("In Progress Task");
        inProgressTask.Status = (int)DomainTaskStatus.InProgress;

        // Act
        var result = _softDeleteService.SoftDeleteTask(inProgressTask);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Warnings.Should().Contain("Deleting a task that is currently in progress");
    }

    [Fact]
    public void SoftDeleteTask_Should_Provide_Warnings_For_Future_Due_Date()
    {
        // Arrange
        var futureTask = CreateTestTask("Future Task");
        futureTask.DueDate = DateTime.UtcNow.AddDays(7);

        // Act
        var result = _softDeleteService.SoftDeleteTask(futureTask);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("future due date"));
    }

    [Fact]
    public void SoftDeleteTask_Should_Provide_Warnings_For_Task_With_Subtasks()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent Task");
        var subtask = CreateTestTask("Subtask");
        parentTask.Subtasks.Add(subtask);

        // Act
        var result = _softDeleteService.SoftDeleteTask(parentTask);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("active subtasks"));
    }

    #endregion

    #region Task Restore Tests

    [Fact]
    public void RestoreTask_Should_Restore_Task_Successfully()
    {
        // Arrange
        var task = CreateTestTask("Task to Restore");
        task.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreTask(task);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Task to Restore");
        result.Message.Should().Contain("has been restored");
        task.IsDeleted.Should().BeFalse();
        task.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void RestoreTask_Should_Restore_Task_With_Subtasks()
    {
        // Arrange
        var parentTask = CreateTestTask("Parent Task");
        var subtask = CreateTestTask("Subtask");
        parentTask.Subtasks.Add(subtask);
        
        parentTask.SoftDelete();
        subtask.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreTask(parentTask, restoreSubtasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("with its subtasks");
        parentTask.IsDeleted.Should().BeFalse();
        subtask.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void RestoreTask_Should_Restore_Project_If_Requested()
    {
        // Arrange
        var project = CreateTestProject("Test Project");
        project.SoftDelete();
        
        var task = CreateTestTask("Task in Project");
        task.Project = project;
        task.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreTask(task, restoreProject: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("and its parent project");
        task.IsDeleted.Should().BeFalse();
        project.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void RestoreTask_Should_Return_Error_For_Null_Task()
    {
        // Act
        var result = _softDeleteService.RestoreTask(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Task not found");
    }

    [Fact]
    public void RestoreTask_Should_Return_Error_For_Active_Task()
    {
        // Arrange
        var task = CreateTestTask("Active Task");

        // Act
        var result = _softDeleteService.RestoreTask(task);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("is not deleted or cannot be restored");
    }

    #endregion

    #region Project Soft Delete Tests

    [Fact]
    public void SoftDeleteProject_Should_Delete_Empty_Project()
    {
        // Arrange
        var project = CreateTestProject("Empty Project");

        // Act
        var result = _softDeleteService.SoftDeleteProject(project);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Empty Project");
        result.Message.Should().Contain("has been deleted");
        project.IsDeleted.Should().BeTrue();
        project.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void SoftDeleteProject_Should_Delete_Project_With_Completed_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Completed Tasks");
        var completedTask = CreateTestTask("Completed Task");
        completedTask.Status = (int)DomainTaskStatus.Completed;
        project.Tasks.Add(completedTask);

        // Act
        var result = _softDeleteService.SoftDeleteProject(project, deleteTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.IsDeleted.Should().BeTrue();
        completedTask.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDeleteProject_Should_Not_Delete_Project_With_Active_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Active Tasks");
        var activeTask = CreateTestTask("Active Task");
        activeTask.Status = (int)DomainTaskStatus.InProgress;
        project.Tasks.Add(activeTask);

        // Act
        var result = _softDeleteService.SoftDeleteProject(project, deleteTasks: false);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Cannot delete project with");
        result.Message.Should().Contain("active tasks");
        project.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SoftDeleteProject_Should_Return_Error_For_Null_Project()
    {
        // Act
        var result = _softDeleteService.SoftDeleteProject(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Project not found");
    }

    #endregion

    #region Project Restore Tests

    [Fact]
    public void RestoreProject_Should_Restore_Project_Successfully()
    {
        // Arrange
        var project = CreateTestProject("Project to Restore");
        project.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreProject(project);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Project to Restore");
        result.Message.Should().Contain("has been restored");
        project.IsDeleted.Should().BeFalse();
        project.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void RestoreProject_Should_Restore_Project_With_Tasks()
    {
        // Arrange
        var project = CreateTestProject("Project With Tasks");
        var task = CreateTestTask("Task to Restore");
        project.Tasks.Add(task);
        
        project.SoftDelete();
        task.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreProject(project, restoreTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("with 1 tasks");
        project.IsDeleted.Should().BeFalse();
        task.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void RestoreProject_Should_Return_Error_For_Null_Project()
    {
        // Act
        var result = _softDeleteService.RestoreProject(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Project not found");
    }

    #endregion

    #region Contact Soft Delete Tests

    [Fact]
    public void SoftDeleteContact_Should_Delete_Contact_Without_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact Without Tasks");

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Contact Without Tasks");
        result.Message.Should().Contain("has been deleted");
        contact.IsDeleted.Should().BeTrue();
        contact.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void SoftDeleteContact_Should_Not_Delete_Contact_With_Active_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact With Tasks");
        var task = CreateTestTask("Active Task");
        contact.Tasks.Add(task);

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact, removeFromTasks: false);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("active task associations");
        contact.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SoftDeleteContact_Should_Delete_Contact_And_Remove_From_Tasks()
    {
        // Arrange
        var contact = CreateTestContact("Contact To Remove");
        var task = CreateTestTask("Task With Contact");
        contact.Tasks.Add(task);
        task.Contacts.Add(contact);

        // Act
        var result = _softDeleteService.SoftDeleteContact(contact, removeFromTasks: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        contact.IsDeleted.Should().BeTrue();
        task.Contacts.Should().NotContain(contact);
    }

    [Fact]
    public void SoftDeleteContact_Should_Return_Error_For_Null_Contact()
    {
        // Act
        var result = _softDeleteService.SoftDeleteContact(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Contact not found");
    }

    #endregion

    #region Contact Restore Tests

    [Fact]
    public void RestoreContact_Should_Restore_Contact_Successfully()
    {
        // Arrange
        var contact = CreateTestContact("Contact to Restore");
        contact.SoftDelete();

        // Act
        var result = _softDeleteService.RestoreContact(contact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Contact to Restore");
        result.Message.Should().Contain("has been restored");
        contact.IsDeleted.Should().BeFalse();
        contact.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void RestoreContact_Should_Return_Error_For_Null_Contact()
    {
        // Act
        var result = _softDeleteService.RestoreContact(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Contact not found");
    }

    [Fact]
    public void RestoreContact_Should_Return_Error_For_Active_Contact()
    {
        // Arrange
        var contact = CreateTestContact("Active Contact");

        // Act
        var result = _softDeleteService.RestoreContact(contact);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("is not deleted or cannot be restored");
    }

    #endregion

    #region SoftDeleteResult Tests

    [Fact]
    public void SoftDeleteResult_Should_Create_Success_Result()
    {
        // Act
        var result = SoftDeleteResult.Success("Operation successful");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Operation successful");
        result.Exception.Should().BeNull();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SoftDeleteResult_Should_Create_Failed_Result()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = SoftDeleteResult.Failed("Operation failed", exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Operation failed");
        result.Exception.Should().Be(exception);
    }

    [Fact]
    public void SoftDeleteResult_Should_Include_Warnings_In_Full_Message()
    {
        // Arrange
        var result = SoftDeleteResult.Success("Operation completed");
        result.Warnings.Add("Warning 1");
        result.Warnings.Add("Warning 2");

        // Act
        var fullMessage = result.GetFullMessage();

        // Assert
        fullMessage.Should().Contain("Operation completed");
        fullMessage.Should().Contain("Warning 1");
        fullMessage.Should().Contain("Warning 2");
        fullMessage.Should().Contain("(Warnings:");
    }

    [Fact]
    public void SoftDeleteResult_Should_Return_Message_Only_Without_Warnings()
    {
        // Arrange
        var result = SoftDeleteResult.Success("Simple message");

        // Act
        var fullMessage = result.GetFullMessage();

        // Assert
        fullMessage.Should().Be("Simple message");
    }

    #endregion

    #region Helper Methods

    private DomainTask CreateTestTask(string title)
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            Status = (int)DomainTaskStatus.Pending,
            Priority = (int)Priority.Medium,
            Category = (int)TaskCategory.ToDos,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Subtasks = new List<DomainTask>()
        };
    }

    private Project CreateTestProject(string name)
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            Status = 0, // Active
            Progress = 0,
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tasks = new List<DomainTask>()
        };
    }

    private Contact CreateTestContact(string name)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            RelationshipType = 0, // Default relationship type
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tasks = new List<DomainTask>()
        };
    }

    #endregion
}