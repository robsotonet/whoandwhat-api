using FluentAssertions;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Common;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Tests.Services;

public class TaskConversionServiceTests
{
    private readonly TaskConversionService _service;

    public TaskConversionServiceTests()
    {
        _service = new TaskConversionService();
    }

    private DomainTask CreateValidTask(TaskCategory? category = null, DomainTaskStatus? status = null)
    {
        return new DomainTask
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Description = "Test Description",
            DueDate = DateTime.UtcNow.AddDays(7),
            Priority = Priority.Medium.Value,
            Category = (category ?? TaskCategory.ToDo).Value,
            Status = (status ?? DomainTaskStatus.Pending).Value,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };
    }

    #region ConvertTaskToProject Tests

    [Fact]
    public void ConvertTaskToProject_Should_Succeed_For_Valid_Idea()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.Description = "A complex idea that should become a project";
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task);
        
        result.IsValid.Should().BeTrue();
        convertedTask.Should().NotBeNull();
        convertedTask!.Category.Should().Be(TaskCategory.Project.Value);
        convertedTask.ProjectId.Should().BeNull();
        convertedTask.Description.Should().Contain("Project converted from");
    }

    [Fact]
    public void ConvertTaskToProject_Should_Fail_For_Appointment()
    {
        var task = CreateValidTask(TaskCategory.Appointment);
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Appointments cannot be converted to projects");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskToProject_Should_Fail_For_BillReminder()
    {
        var task = CreateValidTask(TaskCategory.BillReminder);
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Bill reminders cannot be converted to projects");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskToProject_Should_Fail_For_Completed_Task()
    {
        var task = CreateValidTask(TaskCategory.Idea, DomainTaskStatus.Completed);
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Task cannot be converted to project based on current state");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskToProject_Should_Fail_With_Archived_Subtasks()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        var subtasks = new List<DomainTask>
        {
            new() { Title = "Archived Subtask", Status = DomainTaskStatus.Archived.Value }
        };
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task, subtasks);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot include archived subtask 'Archived Subtask' in project conversion");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskToProject_Should_Include_Subtasks()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        var subtasks = new List<DomainTask>
        {
            new() { Title = "Subtask 1", Status = DomainTaskStatus.Pending.Value },
            new() { Title = "Subtask 2", Status = DomainTaskStatus.InProgress.Value }
        };
        
        var (result, convertedTask) = _service.ConvertTaskToProject(task, subtasks);
        
        result.IsValid.Should().BeTrue();
        convertedTask!.Subtasks.Should().HaveCount(2);
    }

    #endregion

    #region ConvertTaskCategory Tests

    [Fact]
    public void ConvertTaskCategory_Should_Succeed_For_Valid_Conversion()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.DueDate = DateTime.UtcNow.AddDays(1);
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.ToDo);
        
        result.IsValid.Should().BeTrue();
        convertedTask.Should().NotBeNull();
        convertedTask!.Category.Should().Be(TaskCategory.ToDo.Value);
    }

    [Fact]
    public void ConvertTaskCategory_Should_Fail_For_Invalid_Conversion()
    {
        var task = CreateValidTask(TaskCategory.ToDo);
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.Idea);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot convert from To-Do to Idea");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskCategory_Should_Fail_Idea_To_Appointment_Without_Due_Date()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.DueDate = null;
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.Appointment);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Ideas being converted to appointments must have a scheduled date");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskCategory_Should_Fail_Idea_To_Appointment_With_Past_Date()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.Appointment);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Appointments cannot be scheduled in the past");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskCategory_Should_Fail_Idea_To_BillReminder_Without_Due_Date()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.DueDate = null;
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.BillReminder);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Ideas being converted to bill reminders must have a due date");
        convertedTask.Should().BeNull();
    }

    [Fact]
    public void ConvertTaskCategory_Should_Adjust_Priority_For_Appointment()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.Priority = Priority.Low.Value;
        task.DueDate = DateTime.UtcNow.AddHours(12);
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.Appointment);
        
        result.IsValid.Should().BeTrue();
        convertedTask!.Priority.Should().Be(Priority.High.Value);
    }

    [Fact]
    public void ConvertTaskCategory_Should_Adjust_Priority_For_BillReminder()
    {
        var task = CreateValidTask(TaskCategory.Idea);
        task.Priority = Priority.Low.Value;
        task.DueDate = DateTime.UtcNow.AddDays(5);
        task.Description = "Electric bill payment";
        
        var (result, convertedTask) = _service.ConvertTaskCategory(task, TaskCategory.BillReminder);
        
        result.IsValid.Should().BeTrue();
        convertedTask!.Priority.Should().Be(Priority.Medium.Value);
    }

    #endregion

    #region BreakdownProject Tests

    [Fact]
    public void BreakdownProject_Should_Create_Subtasks_For_Valid_Project()
    {
        var projectTask = CreateValidTask(TaskCategory.Project);
        var subtaskTemplates = new[]
        {
            ("Planning", "Define scope", (DateTime?)DateTime.UtcNow.AddDays(2), Priority.High),
            ("Implementation", "Build the solution", (DateTime?)DateTime.UtcNow.AddDays(5), Priority.Medium)
        };
        
        var (result, subtasks) = _service.BreakdownProject(projectTask, subtaskTemplates);
        
        result.IsValid.Should().BeTrue();
        subtasks.Should().NotBeNull();
        subtasks!.Should().HaveCount(2);
        
        var subtaskList = subtasks.ToList();
        subtaskList[0].Title.Should().Be("Planning");
        subtaskList[0].Category.Should().Be(TaskCategory.ToDo.Value);
        subtaskList[0].Status.Should().Be(DomainTaskStatus.Pending.Value);
        subtaskList[0].ProjectId.Should().Be(projectTask.Id);
        subtaskList[0].UserId.Should().Be(projectTask.UserId);
    }

    [Fact]
    public void BreakdownProject_Should_Fail_For_Non_Project_Task()
    {
        var task = CreateValidTask(TaskCategory.ToDo);
        var subtaskTemplates = new[]
        {
            ("Subtask", "Description", (DateTime?)null, Priority.Medium)
        };
        
        var (result, subtasks) = _service.BreakdownProject(task, subtaskTemplates);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Only project tasks can be broken down into subtasks");
        subtasks.Should().BeNull();
    }

    [Fact]
    public void BreakdownProject_Should_Fail_For_Empty_Subtask_Title()
    {
        var projectTask = CreateValidTask(TaskCategory.Project);
        var subtaskTemplates = new[]
        {
            ("", "Description", (DateTime?)null, Priority.Medium)
        };
        
        var (result, subtasks) = _service.BreakdownProject(projectTask, subtaskTemplates);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subtask title cannot be empty");
        subtasks.Should().BeNull();
    }

    [Fact]
    public void BreakdownProject_Should_Fail_For_Subtask_Due_After_Project()
    {
        var projectTask = CreateValidTask(TaskCategory.Project);
        projectTask.DueDate = DateTime.UtcNow.AddDays(5);
        
        var subtaskTemplates = new[]
        {
            ("Subtask", "Description", (DateTime?)DateTime.UtcNow.AddDays(10), Priority.Medium)
        };
        
        var (result, subtasks) = _service.BreakdownProject(projectTask, subtaskTemplates);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Subtask 'Subtask' cannot have due date later than project due date");
        subtasks.Should().BeNull();
    }

    #endregion

    #region SuggestTaskCategory Tests

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Appointment_For_Meeting_Keywords()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Team meeting", "Weekly standup call", DateTime.UtcNow.AddDays(1));
        
        category.Should().Be(TaskCategory.Appointment);
        confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_BillReminder_For_Payment_Keywords()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Pay electric bill", "Monthly utilities payment", DateTime.UtcNow.AddDays(3));
        
        category.Should().Be(TaskCategory.BillReminder);
        confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Project_For_Complex_Keywords()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Build new website", "Develop and implement a new company website with modern design", DateTime.UtcNow.AddDays(30));
        
        category.Should().Be(TaskCategory.Project);
        confidence.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Idea_For_Brainstorm_Keywords()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Brainstorm marketing ideas", "Think about new marketing concepts", null);
        
        category.Should().Be(TaskCategory.Idea);
        confidence.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Appointment_For_Urgent_Due_Date()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Important meeting", "", DateTime.UtcNow.AddHours(6));
        
        category.Should().Be(TaskCategory.Appointment);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Project_For_Long_Description()
    {
        var longDescription = new string('a', 300);
        var (category, confidence) = _service.SuggestTaskCategory("Complex task", longDescription, DateTime.UtcNow.AddDays(14));
        
        category.Should().Be(TaskCategory.Project);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_ToDo_For_Short_Simple_Task()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Buy milk", "", DateTime.UtcNow.AddDays(1));
        
        category.Should().Be(TaskCategory.ToDo);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Suggest_Idea_For_No_Due_Date()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Something to consider", "Maybe think about this later", null);
        
        category.Should().Be(TaskCategory.Idea);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Have_Valid_Confidence_Range()
    {
        var (category, confidence) = _service.SuggestTaskCategory("Test task", "Test description", DateTime.UtcNow.AddDays(1));
        
        confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void SuggestTaskCategory_Should_Handle_Empty_Content()
    {
        var (category, confidence) = _service.SuggestTaskCategory("", "", null);
        
        category.Should().BeOneOf(TaskCategory.GetAll());
        confidence.Should().BeGreaterOrEqualTo(0.0);
    }

    [Theory]
    [InlineData("meeting", "appointment", "call")]
    [InlineData("bill", "payment", "invoice")]
    [InlineData("project", "develop", "build")]
    [InlineData("idea", "brainstorm", "think")]
    public void SuggestTaskCategory_Should_Count_Keywords_Correctly(params string[] keywords)
    {
        var title = string.Join(" ", keywords);
        var (category, confidence) = _service.SuggestTaskCategory(title, "", null);
        
        confidence.Should().BeGreaterThan(0.1); // Base score plus keyword bonus
    }

    #endregion
}