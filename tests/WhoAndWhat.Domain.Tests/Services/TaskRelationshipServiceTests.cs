using FluentAssertions;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Common;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Tests.Services;

public class TaskRelationshipServiceTests
{
    private readonly TaskRelationshipService _service;

    public TaskRelationshipServiceTests()
    {
        _service = new TaskRelationshipService();
    }

    private DomainTask CreateValidTask(TaskCategory? category = null, DomainTaskStatus? status = null, Guid? userId = null)
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
            UserId = userId ?? Guid.NewGuid()
        };
    }

    #region EstablishParentChildRelationship Tests

    [Fact]
    public void EstablishParentChildRelationship_Should_Succeed_For_Valid_Project_And_Subtask()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, userId: userId);
        var childTask = CreateValidTask(TaskCategory.ToDo, userId: userId);
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Category_Not_Allowing_Subtasks()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Appointment, userId: userId);
        var childTask = CreateValidTask(TaskCategory.ToDo, userId: userId);
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Appointment tasks cannot have subtasks");
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Child_Already_In_Project()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, userId: userId);
        var childTask = CreateValidTask(TaskCategory.ToDo, userId: userId);
        childTask.ProjectId = Guid.NewGuid();
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Task is already part of another project");
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Project_As_Child()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, userId: userId);
        var childTask = CreateValidTask(TaskCategory.Project, userId: userId);
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Projects cannot be subtasks of other tasks");
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Circular_Dependency()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, userId: userId);
        var childTask = CreateValidTask(TaskCategory.Project, userId: userId);
        
        // Set up circular dependency
        parentTask.ProjectId = childTask.Id;
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot create circular dependency between tasks");
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Different_Users()
    {
        var parentTask = CreateValidTask(TaskCategory.Project, userId: Guid.NewGuid());
        var childTask = CreateValidTask(TaskCategory.ToDo, userId: Guid.NewGuid());
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Parent and child tasks must belong to the same user");
    }

    [Fact]
    public void EstablishParentChildRelationship_Should_Fail_For_Child_Due_After_Parent()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, userId: userId);
        parentTask.DueDate = DateTime.UtcNow.AddDays(5);
        
        var childTask = CreateValidTask(TaskCategory.ToDo, userId: userId);
        childTask.DueDate = DateTime.UtcNow.AddDays(10);
        
        var result = _service.EstablishParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain($"Subtask due date ({childTask.DueDate:yyyy-MM-dd}) cannot be later than parent due date ({parentTask.DueDate:yyyy-MM-dd})");
    }

    #endregion

    #region RemoveParentChildRelationship Tests

    [Fact]
    public void RemoveParentChildRelationship_Should_Succeed_For_Valid_Removal()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, DomainTaskStatus.InProgress, userId);
        var childTask = CreateValidTask(TaskCategory.ToDo, DomainTaskStatus.Pending, userId);
        childTask.ProjectId = parentTask.Id;
        
        var result = _service.RemoveParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RemoveParentChildRelationship_Should_Fail_For_Removing_Active_Child_From_Completed_Parent()
    {
        var userId = Guid.NewGuid();
        var parentTask = CreateValidTask(TaskCategory.Project, DomainTaskStatus.Completed, userId);
        var childTask = CreateValidTask(TaskCategory.ToDo, DomainTaskStatus.InProgress, userId);
        
        var result = _service.RemoveParentChildRelationship(parentTask, childTask);
        
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Cannot remove active subtask from completed parent task");
    }

    #endregion

    #region BuildTaskHierarchy Tests

    [Fact]
    public void BuildTaskHierarchy_Should_Build_Correct_Hierarchy()
    {
        var rootTask = CreateValidTask(TaskCategory.Project);
        var child1 = CreateValidTask(TaskCategory.ToDo);
        child1.ProjectId = rootTask.Id;
        var child2 = CreateValidTask(TaskCategory.ToDo);
        child2.ProjectId = rootTask.Id;
        var grandchild = CreateValidTask(TaskCategory.ToDo);
        grandchild.ProjectId = child1.Id;
        
        var allTasks = new[] { rootTask, child1, child2, grandchild };
        
        var hierarchy = _service.BuildTaskHierarchy(rootTask, allTasks);
        
        hierarchy.Root.Should().Be(rootTask);
        hierarchy.Children.Should().HaveCount(2);
        hierarchy.Children.Should().Contain(h => h.Root == child1);
        hierarchy.Children.Should().Contain(h => h.Root == child2);
        
        var child1Hierarchy = hierarchy.Children.First(h => h.Root == child1);
        child1Hierarchy.Children.Should().HaveCount(1);
        child1Hierarchy.Children[0].Root.Should().Be(grandchild);
    }

    [Fact]
    public void TaskHierarchy_Depth_Should_Calculate_Correctly()
    {
        var rootTask = CreateValidTask(TaskCategory.Project);
        var child = CreateValidTask(TaskCategory.ToDo);
        child.ProjectId = rootTask.Id;
        var grandchild = CreateValidTask(TaskCategory.ToDo);
        grandchild.ProjectId = child.Id;
        
        var allTasks = new[] { rootTask, child, grandchild };
        var hierarchy = _service.BuildTaskHierarchy(rootTask, allTasks);
        
        hierarchy.Depth.Should().Be(3); // Root -> Child -> Grandchild
    }

    [Fact]
    public void TaskHierarchy_AllTasks_Should_Return_All_Tasks_Flattened()
    {
        var rootTask = CreateValidTask(TaskCategory.Project);
        var child = CreateValidTask(TaskCategory.ToDo);
        child.ProjectId = rootTask.Id;
        
        var allTasks = new[] { rootTask, child };
        var hierarchy = _service.BuildTaskHierarchy(rootTask, allTasks);
        
        var flattenedTasks = hierarchy.AllTasks.ToList();
        flattenedTasks.Should().HaveCount(2);
        flattenedTasks.Should().Contain(rootTask);
        flattenedTasks.Should().Contain(child);
    }

    #endregion

    #region CalculateHierarchyMetrics Tests

    [Fact]
    public void CalculateHierarchyMetrics_Should_Calculate_Correct_Metrics()
    {
        var rootTask = CreateValidTask(TaskCategory.Project, DomainTaskStatus.InProgress);
        rootTask.Priority = Priority.High.Value;
        rootTask.DueDate = DateTime.UtcNow.AddDays(10);
        
        var child1 = CreateValidTask(TaskCategory.ToDo, DomainTaskStatus.Completed);
        child1.Priority = Priority.Medium.Value;
        child1.DueDate = DateTime.UtcNow.AddDays(5);
        
        var child2 = CreateValidTask(TaskCategory.ToDo, DomainTaskStatus.Pending);
        child2.Priority = Priority.Urgent.Value;
        child2.DueDate = DateTime.UtcNow.AddDays(-1); // Overdue
        
        var hierarchy = new TaskHierarchy
        {
            Root = rootTask,
            Children = new List<TaskHierarchy>
            {
                new() { Root = child1, Children = new List<TaskHierarchy>() },
                new() { Root = child2, Children = new List<TaskHierarchy>() }
            }
        };
        
        var metrics = _service.CalculateHierarchyMetrics(hierarchy);
        
        metrics.TotalTasks.Should().Be(3);
        metrics.CompletedTasks.Should().Be(1);
        metrics.InProgressTasks.Should().Be(1);
        metrics.PendingTasks.Should().Be(1);
        metrics.ArchivedTasks.Should().Be(0);
        metrics.OverdueTasks.Should().Be(1);
        metrics.CompletionPercentage.Should().Be(33.33m, because: "1 out of 3 tasks completed");
        metrics.IsOverdue.Should().BeTrue();
        metrics.HighestPriority.Should().Be(Priority.Urgent);
        metrics.EarliestDueDate.Should().Be(child2.DueDate);
        metrics.LatestDueDate.Should().Be(rootTask.DueDate);
    }

    [Fact]
    public void CalculateHierarchyMetrics_Should_Handle_Empty_Hierarchy()
    {
        var rootTask = CreateValidTask(TaskCategory.Project, DomainTaskStatus.Pending);
        var hierarchy = new TaskHierarchy
        {
            Root = rootTask,
            Children = new List<TaskHierarchy>()
        };
        
        var metrics = _service.CalculateHierarchyMetrics(hierarchy);
        
        metrics.TotalTasks.Should().Be(1);
        metrics.CompletionPercentage.Should().Be(0);
        metrics.IsOverdue.Should().BeFalse();
    }

    #endregion

    #region ReorderSubtasks Tests

    [Fact]
    public void ReorderSubtasks_Should_Order_By_Status_Then_Priority()
    {
        var parentTask = CreateValidTask(TaskCategory.Project);
        var subtasks = new List<DomainTask>
        {
            new() { Status = DomainTaskStatus.Completed.Value, Priority = Priority.High.Value, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Status = DomainTaskStatus.InProgress.Value, Priority = Priority.Low.Value, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Status = DomainTaskStatus.Pending.Value, Priority = Priority.Urgent.Value, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new() { Status = DomainTaskStatus.InProgress.Value, Priority = Priority.High.Value, CreatedAt = DateTime.UtcNow.AddDays(-4) }
        };
        
        var reordered = _service.ReorderSubtasks(parentTask, subtasks).ToList();
        
        // Should be ordered: InProgress (High first), InProgress (Low), Pending (Urgent), Completed (High)
        reordered[0].Status.Should().Be(DomainTaskStatus.InProgress.Value);
        reordered[0].Priority.Should().Be(Priority.High.Value);
        
        reordered[1].Status.Should().Be(DomainTaskStatus.InProgress.Value);
        reordered[1].Priority.Should().Be(Priority.Low.Value);
        
        reordered[2].Status.Should().Be(DomainTaskStatus.Pending.Value);
        reordered[2].Priority.Should().Be(Priority.Urgent.Value);
        
        reordered[3].Status.Should().Be(DomainTaskStatus.Completed.Value);
    }

    [Fact]
    public void ReorderSubtasks_Should_Order_By_Due_Date_When_Same_Status_And_Priority()
    {
        var parentTask = CreateValidTask(TaskCategory.Project);
        var subtasks = new List<DomainTask>
        {
            new() 
            { 
                Status = DomainTaskStatus.Pending.Value, 
                Priority = Priority.Medium.Value, 
                DueDate = DateTime.UtcNow.AddDays(2),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new() 
            { 
                Status = DomainTaskStatus.Pending.Value, 
                Priority = Priority.Medium.Value, 
                DueDate = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };
        
        var reordered = _service.ReorderSubtasks(parentTask, subtasks).ToList();
        
        reordered[0].DueDate.Should().Be(DateTime.UtcNow.AddDays(1).Date);
        reordered[1].DueDate.Should().Be(DateTime.UtcNow.AddDays(2).Date);
    }

    #endregion

    #region SuggestTaskBreakdown Tests

    [Fact]
    public void SuggestTaskBreakdown_Should_Suggest_Standard_Project_Phases()
    {
        var projectTask = CreateValidTask(TaskCategory.Project);
        
        var suggestions = _service.SuggestTaskBreakdown(projectTask).ToList();
        
        suggestions.Should().HaveCount(5);
        suggestions.Should().Contain(s => s.Title == "Planning Phase");
        suggestions.Should().Contain(s => s.Title == "Research Phase");
        suggestions.Should().Contain(s => s.Title == "Implementation Phase");
        suggestions.Should().Contain(s => s.Title == "Review Phase");
        suggestions.Should().Contain(s => s.Title == "Documentation Phase");
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Set_Due_Dates_For_Project_With_Due_Date()
    {
        var projectTask = CreateValidTask(TaskCategory.Project);
        projectTask.DueDate = DateTime.UtcNow.AddDays(25);
        
        var suggestions = _service.SuggestTaskBreakdown(projectTask).ToList();
        
        suggestions.Should().AllSatisfy(s => s.SuggestedDueDate.Should().NotBeNull());
        suggestions[0].SuggestedDueDate.Should().BeBefore(suggestions[1].SuggestedDueDate!.Value);
        suggestions.Last().SuggestedDueDate.Should().BeCloseTo(projectTask.DueDate!.Value, TimeSpan.FromDays(1));
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Suggest_Idea_Breakdown_For_Complex_Idea()
    {
        var ideaTask = CreateValidTask(TaskCategory.Idea);
        ideaTask.Description = new string('a', 150); // Long description
        
        var suggestions = _service.SuggestTaskBreakdown(ideaTask).ToList();
        
        suggestions.Should().HaveCount(3);
        suggestions.Should().Contain(s => s.Title == "Research the Idea");
        suggestions.Should().Contain(s => s.Title == "Create Action Plan");
        suggestions.Should().Contain(s => s.Title == "Prototype/Test");
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Not_Suggest_For_Simple_Idea()
    {
        var ideaTask = CreateValidTask(TaskCategory.Idea);
        ideaTask.Description = "Simple idea";
        
        var suggestions = _service.SuggestTaskBreakdown(ideaTask).ToList();
        
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Break_Complex_ToDo_By_And_Separator()
    {
        var todoTask = CreateValidTask(TaskCategory.ToDo);
        todoTask.Description = "Buy groceries and cook dinner, clean the house and do laundry";
        
        var suggestions = _service.SuggestTaskBreakdown(todoTask).ToList();
        
        suggestions.Should().HaveCountGreaterThan(0);
        suggestions.Should().AllSatisfy(s => s.Priority.Should().Be(Priority.Medium));
        suggestions.Should().AllSatisfy(s => s.Title.Length.Should().BeGreaterThan(5));
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Not_Suggest_For_Simple_ToDo()
    {
        var todoTask = CreateValidTask(TaskCategory.ToDo);
        todoTask.Description = "Simple task";
        
        var suggestions = _service.SuggestTaskBreakdown(todoTask).ToList();
        
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void SuggestTaskBreakdown_Should_Not_Suggest_For_Non_Breakdown_Categories()
    {
        var appointmentTask = CreateValidTask(TaskCategory.Appointment);
        var billTask = CreateValidTask(TaskCategory.BillReminder);
        
        var appointmentSuggestions = _service.SuggestTaskBreakdown(appointmentTask).ToList();
        var billSuggestions = _service.SuggestTaskBreakdown(billTask).ToList();
        
        appointmentSuggestions.Should().BeEmpty();
        billSuggestions.Should().BeEmpty();
    }

    #endregion
}