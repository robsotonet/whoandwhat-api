using FluentAssertions;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using Task = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Tests;

public class TaskEntityTests
{
    [Fact]
    public void Task_Should_Initialize_With_Default_Values()
    {
        var task = new Task();
        
        task.Id.Should().Be(Guid.Empty);
        task.Title.Should().BeNull();
        task.Description.Should().BeNull();
        task.DueDate.Should().BeNull();
        task.Priority.Should().Be(0);
        task.Category.Should().Be(0);
        task.Status.Should().Be(0);
        task.CreatedAt.Should().Be(DateTime.MinValue);
        task.UpdatedAt.Should().Be(DateTime.MinValue);
        task.UserId.Should().Be(Guid.Empty);
        task.ProjectId.Should().BeNull();
        task.Contacts.Should().NotBeNull().And.BeEmpty();
        task.Subtasks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Task_Should_Allow_Setting_All_Properties()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var title = "Test Task";
        var description = "Test Description";
        var dueDate = DateTime.UtcNow.AddDays(7);
        var priority = (int)Priority.High;
        var category = (int)AppTaskCategory.ToDo;
        var status = (int)DomainTaskStatus.InProgress;
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(30);

        var task = new Task
        {
            Id = taskId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            Priority = priority,
            Category = category,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            UserId = userId,
            ProjectId = projectId
        };

        task.Id.Should().Be(taskId);
        task.Title.Should().Be(title);
        task.Description.Should().Be(description);
        task.DueDate.Should().Be(dueDate);
        task.Priority.Should().Be(priority);
        task.Category.Should().Be(category);
        task.Status.Should().Be(status);
        task.CreatedAt.Should().Be(createdAt);
        task.UpdatedAt.Should().Be(updatedAt);
        task.UserId.Should().Be(userId);
        task.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public void Task_Should_Allow_Adding_Contacts()
    {
        var task = new Task();
        var contact1 = new Contact { Id = Guid.NewGuid(), Name = "Contact 1" };
        var contact2 = new Contact { Id = Guid.NewGuid(), Name = "Contact 2" };

        task.Contacts.Add(contact1);
        task.Contacts.Add(contact2);

        task.Contacts.Should().HaveCount(2);
        task.Contacts.Should().Contain(contact1);
        task.Contacts.Should().Contain(contact2);
    }

    [Fact]
    public void Task_Should_Allow_Adding_Subtasks()
    {
        var parentTask = new Task { Title = "Parent Task" };
        var subtask1 = new Task { Title = "Subtask 1" };
        var subtask2 = new Task { Title = "Subtask 2" };

        parentTask.Subtasks.Add(subtask1);
        parentTask.Subtasks.Add(subtask2);

        parentTask.Subtasks.Should().HaveCount(2);
        parentTask.Subtasks.Should().Contain(subtask1);
        parentTask.Subtasks.Should().Contain(subtask2);
    }

    [Fact]
    public void Task_Should_Handle_Null_Description()
    {
        var task = new Task
        {
            Title = "Task without description",
            Description = null
        };

        task.Description.Should().BeNull();
    }

    [Fact]
    public void Task_Should_Handle_Null_DueDate()
    {
        var task = new Task
        {
            Title = "Task without due date",
            DueDate = null
        };

        task.DueDate.Should().BeNull();
    }

    [Fact]
    public void Task_Should_Handle_Null_ProjectId()
    {
        var task = new Task
        {
            Title = "Task not in project",
            ProjectId = null
        };

        task.ProjectId.Should().BeNull();
    }
}