using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

public class TaskCategoryTests
{
    [Fact]
    public void TaskCategory_Should_Have_All_Predefined_Categories()
    {
        var allCategories = TaskCategory.GetAll().ToList();
        
        allCategories.Should().HaveCount(5);
        allCategories.Should().Contain(TaskCategory.ToDo);
        allCategories.Should().Contain(TaskCategory.Idea);
        allCategories.Should().Contain(TaskCategory.Appointment);
        allCategories.Should().Contain(TaskCategory.BillReminder);
        allCategories.Should().Contain(TaskCategory.Project);
    }

    [Fact]
    public void TaskCategory_Should_Have_Correct_Static_Values()
    {
        TaskCategory.ToDo.Name.Should().Be("ToDo");
        TaskCategory.ToDo.Value.Should().Be(0);
        TaskCategory.ToDo.RequiresDueDate.Should().BeFalse();
        TaskCategory.ToDo.AllowsSubtasks.Should().BeFalse();
        TaskCategory.ToDo.ColorCode.Should().Be("#007bff");
        TaskCategory.ToDo.IconName.Should().Be("task");
        
        TaskCategory.Appointment.RequiresDueDate.Should().BeTrue();
        TaskCategory.Project.AllowsSubtasks.Should().BeTrue();
    }

    [Fact]
    public void TaskCategory_Should_Create_From_Valid_Value()
    {
        var category = TaskCategory.FromValue(2);
        
        category.Should().Be(TaskCategory.Appointment);
        category.Name.Should().Be("Appointment");
        category.Value.Should().Be(2);
        category.Description.Should().Be("Scheduled appointments");
        category.RequiresDueDate.Should().BeTrue();
        category.AllowsSubtasks.Should().BeFalse();
    }

    [Fact]
    public void TaskCategory_Should_Throw_Exception_For_Invalid_Value()
    {
        Action act = () => TaskCategory.FromValue(999);
        
        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task category value: 999*");
    }

    [Fact]
    public void TaskCategory_Should_Create_From_Valid_Name()
    {
        var category = TaskCategory.FromName("Project");
        
        category.Should().Be(TaskCategory.Project);
        category.Name.Should().Be("Project");
        category.Value.Should().Be(4);
    }

    [Fact]
    public void TaskCategory_Should_Create_From_Valid_Name_Case_Insensitive()
    {
        var category = TaskCategory.FromName("project");
        
        category.Should().Be(TaskCategory.Project);
    }

    [Fact]
    public void TaskCategory_Should_Throw_Exception_For_Invalid_Name()
    {
        Action act = () => TaskCategory.FromName("InvalidCategory");
        
        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task category name: InvalidCategory*");
    }

    [Fact]
    public void TaskCategory_TryFromValue_Should_Work_Correctly()
    {
        var result1 = TaskCategory.TryFromValue(1, out var category1);
        var result2 = TaskCategory.TryFromValue(999, out var category2);
        
        result1.Should().BeTrue();
        category1.Should().Be(TaskCategory.Idea);
        
        result2.Should().BeFalse();
        category2.Should().BeNull();
    }

    [Fact]
    public void TaskCategory_TryFromName_Should_Work_Correctly()
    {
        var result1 = TaskCategory.TryFromName("BillReminder", out var category1);
        var result2 = TaskCategory.TryFromName("Invalid", out var category2);
        
        result1.Should().BeTrue();
        category1.Should().Be(TaskCategory.BillReminder);
        
        result2.Should().BeFalse();
        category2.Should().BeNull();
    }

    [Fact]
    public void TaskCategory_GetProjectConvertibleCategories_Should_Return_Correct_Categories()
    {
        var convertible = TaskCategory.GetProjectConvertibleCategories().ToList();
        
        convertible.Should().Contain(TaskCategory.Idea);
        convertible.Should().Contain(TaskCategory.Project);
        convertible.Should().NotContain(TaskCategory.ToDo);
        convertible.Should().NotContain(TaskCategory.Appointment);
        convertible.Should().NotContain(TaskCategory.BillReminder);
    }

    [Fact]
    public void TaskCategory_GetTimeSensitiveCategories_Should_Return_Correct_Categories()
    {
        var timeSensitive = TaskCategory.GetTimeSensitiveCategories().ToList();
        
        timeSensitive.Should().Contain(TaskCategory.Appointment);
        timeSensitive.Should().Contain(TaskCategory.BillReminder);
        timeSensitive.Should().NotContain(TaskCategory.ToDo);
        timeSensitive.Should().NotContain(TaskCategory.Idea);
        timeSensitive.Should().NotContain(TaskCategory.Project);
    }

    [Theory]
    [InlineData("Idea", "ToDo", true)]
    [InlineData("Idea", "Appointment", true)]
    [InlineData("Idea", "BillReminder", true)]
    [InlineData("Idea", "Project", true)]
    [InlineData("ToDo", "Appointment", false)]
    [InlineData("ToDo", "BillReminder", false)]
    [InlineData("ToDo", "Project", false)]
    [InlineData("ToDo", "Idea", false)]
    [InlineData("Appointment", "ToDo", true)]
    [InlineData("BillReminder", "ToDo", true)]
    [InlineData("Project", "ToDo", false)]
    [InlineData("Project", "Idea", false)]
    [InlineData("Idea", "Idea", false)]
    public void TaskCategory_CanConvertTo_Should_Follow_Business_Rules(string fromCategory, string toCategory, bool expectedResult)
    {
        var from = TaskCategory.FromName(fromCategory);
        var to = TaskCategory.FromName(toCategory);
        
        from.CanConvertTo(to).Should().Be(expectedResult);
    }

    [Fact]
    public void TaskCategory_GetValidConversions_Should_Return_Correct_Conversions()
    {
        var ideaConversions = TaskCategory.Idea.GetValidConversions().ToList();
        var todoConversions = TaskCategory.ToDo.GetValidConversions().ToList();
        var projectConversions = TaskCategory.Project.GetValidConversions().ToList();
        
        ideaConversions.Should().HaveCount(4); // Can convert to all others
        ideaConversions.Should().NotContain(TaskCategory.Idea);
        
        todoConversions.Should().BeEmpty(); // Cannot convert to any
        
        projectConversions.Should().BeEmpty(); // Cannot convert to any
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Pass_For_Valid_Data()
    {
        var validation = TaskCategory.ToDo.ValidateTaskData("Test Task", "Description", null, false);
        
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Fail_For_Missing_Title()
    {
        var validation = TaskCategory.ToDo.ValidateTaskData(null, "Description", null, false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Title is required for all task categories");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Fail_For_Appointment_Without_Due_Date()
    {
        var validation = TaskCategory.Appointment.ValidateTaskData("Meeting", "Description", null, false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment tasks must have a due date");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Fail_For_BillReminder_Without_Due_Date()
    {
        var validation = TaskCategory.BillReminder.ValidateTaskData("Bill", "Description", null, false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Bill Reminder tasks must have a due date");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Fail_For_Subtasks_When_Not_Allowed()
    {
        var validation = TaskCategory.Appointment.ValidateTaskData("Meeting", "Description", DateTime.UtcNow.AddDays(1), true);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment tasks cannot have subtasks");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Fail_For_Past_Appointment()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var validation = TaskCategory.Appointment.ValidateTaskData("Meeting", "Description", pastDate, false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment date cannot be in the past");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Warn_For_BillReminder_Without_Description()
    {
        var validation = TaskCategory.BillReminder.ValidateTaskData("Bill", null, DateTime.UtcNow.AddDays(1), false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Bill reminders should include payment details in the description");
    }

    [Fact]
    public void TaskCategory_ValidateTaskData_Should_Warn_For_Project_Without_Description()
    {
        var validation = TaskCategory.Project.ValidateTaskData("Project", null, null, false);
        
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Projects should include a detailed description");
    }

    [Theory]
    [InlineData("ToDo", "To-Do")]
    [InlineData("Idea", "Idea")]
    [InlineData("Appointment", "Appointment")]
    [InlineData("BillReminder", "Bill Reminder")]
    [InlineData("Project", "Project")]
    public void TaskCategory_GetDisplayName_Should_Return_Correct_Display_Name(string categoryName, string expectedDisplayName)
    {
        var category = TaskCategory.FromName(categoryName);
        
        category.GetDisplayName().Should().Be(expectedDisplayName);
    }

    [Theory]
    [InlineData("ToDo", "category-todo")]
    [InlineData("Idea", "category-idea")]
    [InlineData("Appointment", "category-appointment")]
    [InlineData("BillReminder", "category-bill-reminder")]
    [InlineData("Project", "category-project")]
    public void TaskCategory_GetCssClass_Should_Return_Correct_CSS_Class(string categoryName, string expectedCssClass)
    {
        var category = TaskCategory.FromName(categoryName);
        
        category.GetCssClass().Should().Be(expectedCssClass);
    }

    [Fact]
    public void TaskCategory_GetSuggestedPriorities_Should_Return_Correct_Priorities_For_Appointment()
    {
        var suggested = TaskCategory.Appointment.GetSuggestedPriorities().ToList();
        
        suggested.Should().Contain(Priority.High);
        suggested.Should().Contain(Priority.Urgent);
        suggested.Should().HaveCount(2);
    }

    [Fact]
    public void TaskCategory_GetSuggestedPriorities_Should_Return_All_Priorities_For_ToDo()
    {
        var suggested = TaskCategory.ToDo.GetSuggestedPriorities().ToList();
        
        suggested.Should().HaveCount(4);
        suggested.Should().Contain(Priority.Low);
        suggested.Should().Contain(Priority.Medium);
        suggested.Should().Contain(Priority.High);
        suggested.Should().Contain(Priority.Urgent);
    }

    [Theory]
    [InlineData("ToDo", 1.0)]
    [InlineData("Idea", 0.5)]
    [InlineData("Appointment", 2.0)]
    [InlineData("BillReminder", 0.25)]
    [InlineData("Project", 8.0)]
    public void TaskCategory_GetEstimatedHours_Should_Return_Correct_Hours(string categoryName, double expectedHours)
    {
        var category = TaskCategory.FromName(categoryName);
        
        category.GetEstimatedHours().Should().Be(expectedHours);
    }

    [Theory]
    [InlineData("ToDo", false)]
    [InlineData("Idea", false)]
    [InlineData("Appointment", true)]
    [InlineData("BillReminder", true)]
    [InlineData("Project", false)]
    public void TaskCategory_ShouldAutoArchive_Should_Return_Correct_Value(string categoryName, bool expectedAutoArchive)
    {
        var category = TaskCategory.FromName(categoryName);
        
        category.ShouldAutoArchive().Should().Be(expectedAutoArchive);
    }

    [Fact]
    public void TaskCategory_Should_Support_Implicit_Conversion_To_Int()
    {
        int categoryValue = TaskCategory.Project;
        
        categoryValue.Should().Be(4);
    }

    [Fact]
    public void TaskCategory_Should_Support_Explicit_Conversion_From_Int()
    {
        var category = (TaskCategory)1;
        
        category.Should().Be(TaskCategory.Idea);
    }

    [Fact]
    public void TaskCategory_ToString_Should_Return_Name()
    {
        TaskCategory.Appointment.ToString().Should().Be("Appointment");
    }
}