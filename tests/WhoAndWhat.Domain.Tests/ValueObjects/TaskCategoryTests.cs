using FluentAssertions;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

public class AppTaskCategoryTests
{
    [Fact]
    public void AppTaskCategory_Should_Have_All_Predefined_Categories()
    {
        var allCategories = AppTaskCategory.GetAll().ToList();

        allCategories.Should().HaveCount(5);
        allCategories.Should().Contain(AppTaskCategory.ToDo);
        allCategories.Should().Contain(AppTaskCategory.Idea);
        allCategories.Should().Contain(AppTaskCategory.Appointment);
        allCategories.Should().Contain(AppTaskCategory.BillReminder);
        allCategories.Should().Contain(AppTaskCategory.Project);
    }

    [Fact]
    public void AppTaskCategory_Should_Have_Correct_Static_Values()
    {
        AppTaskCategory.ToDo.Name.Should().Be("ToDo");
        AppTaskCategory.ToDo.Value.Should().Be(0);
        AppTaskCategory.ToDo.RequiresDueDate.Should().BeFalse();
        AppTaskCategory.ToDo.AllowsSubtasks.Should().BeFalse();
        AppTaskCategory.ToDo.ColorCode.Should().Be("#007bff");
        AppTaskCategory.ToDo.IconName.Should().Be("task");

        AppTaskCategory.Appointment.RequiresDueDate.Should().BeTrue();
        AppTaskCategory.Project.AllowsSubtasks.Should().BeTrue();
    }

    [Fact]
    public void AppTaskCategory_Should_Create_From_Valid_Value()
    {
        var category = AppTaskCategory.FromValue(2);

        category.Should().Be(AppTaskCategory.Appointment);
        category.Name.Should().Be("Appointment");
        category.Value.Should().Be(2);
        category.Description.Should().Be("Scheduled appointments");
        category.RequiresDueDate.Should().BeTrue();
        category.AllowsSubtasks.Should().BeFalse();
    }

    [Fact]
    public void AppTaskCategory_Should_Throw_Exception_For_Invalid_Value()
    {
        Action act = () => AppTaskCategory.FromValue(999);

        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task category value: 999*");
    }

    [Fact]
    public void AppTaskCategory_Should_Create_From_Valid_Name()
    {
        var category = AppTaskCategory.FromName("Project");

        category.Should().Be(AppTaskCategory.Project);
        category.Name.Should().Be("Project");
        category.Value.Should().Be(4);
    }

    [Fact]
    public void AppTaskCategory_Should_Create_From_Valid_Name_Case_Insensitive()
    {
        var category = AppTaskCategory.FromName("project");

        category.Should().Be(AppTaskCategory.Project);
    }

    [Fact]
    public void AppTaskCategory_Should_Throw_Exception_For_Invalid_Name()
    {
        Action act = () => AppTaskCategory.FromName("InvalidCategory");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid task category name: InvalidCategory*");
    }

    [Fact]
    public void AppTaskCategory_TryFromValue_Should_Work_Correctly()
    {
        var result1 = AppTaskCategory.TryFromValue(1, out var category1);
        var result2 = AppTaskCategory.TryFromValue(999, out var category2);

        result1.Should().BeTrue();
        category1.Should().Be(AppTaskCategory.Idea);

        result2.Should().BeFalse();
        category2.Should().BeNull();
    }

    [Fact]
    public void AppTaskCategory_TryFromName_Should_Work_Correctly()
    {
        var result1 = AppTaskCategory.TryFromName("BillReminder", out var category1);
        var result2 = AppTaskCategory.TryFromName("Invalid", out var category2);

        result1.Should().BeTrue();
        category1.Should().Be(AppTaskCategory.BillReminder);

        result2.Should().BeFalse();
        category2.Should().BeNull();
    }

    [Fact]
    public void AppTaskCategory_GetProjectConvertibleCategories_Should_Return_Correct_Categories()
    {
        var convertible = AppTaskCategory.GetProjectConvertibleCategories().ToList();

        convertible.Should().Contain(AppTaskCategory.Idea);
        convertible.Should().Contain(AppTaskCategory.Project);
        convertible.Should().NotContain(AppTaskCategory.ToDo);
        convertible.Should().NotContain(AppTaskCategory.Appointment);
        convertible.Should().NotContain(AppTaskCategory.BillReminder);
    }

    [Fact]
    public void AppTaskCategory_GetTimeSensitiveCategories_Should_Return_Correct_Categories()
    {
        var timeSensitive = AppTaskCategory.GetTimeSensitiveCategories().ToList();

        timeSensitive.Should().Contain(AppTaskCategory.Appointment);
        timeSensitive.Should().Contain(AppTaskCategory.BillReminder);
        timeSensitive.Should().NotContain(AppTaskCategory.ToDo);
        timeSensitive.Should().NotContain(AppTaskCategory.Idea);
        timeSensitive.Should().NotContain(AppTaskCategory.Project);
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
    public void AppTaskCategory_CanConvertTo_Should_Follow_Business_Rules(string fromCategory, string toCategory, bool expectedResult)
    {
        var from = AppTaskCategory.FromName(fromCategory);
        var to = AppTaskCategory.FromName(toCategory);

        from.CanConvertTo(to).Should().Be(expectedResult);
    }

    [Fact]
    public void AppTaskCategory_GetValidConversions_Should_Return_Correct_Conversions()
    {
        var ideaConversions = AppTaskCategory.Idea.GetValidConversions().ToList();
        var todoConversions = AppTaskCategory.ToDo.GetValidConversions().ToList();
        var projectConversions = AppTaskCategory.Project.GetValidConversions().ToList();

        ideaConversions.Should().HaveCount(4); // Can convert to all others
        ideaConversions.Should().NotContain(AppTaskCategory.Idea);

        todoConversions.Should().BeEmpty(); // Cannot convert to any

        projectConversions.Should().BeEmpty(); // Cannot convert to any
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Pass_For_Valid_Data()
    {
        var validation = AppTaskCategory.ToDo.ValidateTaskData("Test Task", "Description", null, false);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Fail_For_Missing_Title()
    {
        var validation = AppTaskCategory.ToDo.ValidateTaskData(null, "Description", null, false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Title is required for all task categories");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Fail_For_Appointment_Without_Due_Date()
    {
        var validation = AppTaskCategory.Appointment.ValidateTaskData("Meeting", "Description", null, false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment tasks must have a due date");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Fail_For_BillReminder_Without_Due_Date()
    {
        var validation = AppTaskCategory.BillReminder.ValidateTaskData("Bill", "Description", null, false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Bill Reminder tasks must have a due date");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Fail_For_Subtasks_When_Not_Allowed()
    {
        var validation = AppTaskCategory.Appointment.ValidateTaskData("Meeting", "Description", DateTime.UtcNow.AddDays(1), true);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment tasks cannot have subtasks");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Fail_For_Past_Appointment()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var validation = AppTaskCategory.Appointment.ValidateTaskData("Meeting", "Description", pastDate, false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Appointment date cannot be in the past");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Warn_For_BillReminder_Without_Description()
    {
        var validation = AppTaskCategory.BillReminder.ValidateTaskData("Bill", null, DateTime.UtcNow.AddDays(1), false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Bill reminders should include payment details in the description");
    }

    [Fact]
    public void AppTaskCategory_ValidateTaskData_Should_Warn_For_Project_Without_Description()
    {
        var validation = AppTaskCategory.Project.ValidateTaskData("Project", null, null, false);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("Projects should include a detailed description");
    }

    [Theory]
    [InlineData("ToDo", "To-Do")]
    [InlineData("Idea", "Idea")]
    [InlineData("Appointment", "Appointment")]
    [InlineData("BillReminder", "Bill Reminder")]
    [InlineData("Project", "Project")]
    public void AppTaskCategory_GetDisplayName_Should_Return_Correct_Display_Name(string categoryName, string expectedDisplayName)
    {
        var category = AppTaskCategory.FromName(categoryName);

        category.GetDisplayName().Should().Be(expectedDisplayName);
    }

    [Theory]
    [InlineData("ToDo", "category-todo")]
    [InlineData("Idea", "category-idea")]
    [InlineData("Appointment", "category-appointment")]
    [InlineData("BillReminder", "category-bill-reminder")]
    [InlineData("Project", "category-project")]
    public void AppTaskCategory_GetCssClass_Should_Return_Correct_CSS_Class(string categoryName, string expectedCssClass)
    {
        var category = AppTaskCategory.FromName(categoryName);

        category.GetCssClass().Should().Be(expectedCssClass);
    }

    [Fact]
    public void AppTaskCategory_GetSuggestedPriorities_Should_Return_Correct_Priorities_For_Appointment()
    {
        var suggested = AppTaskCategory.Appointment.GetSuggestedPriorities().ToList();

        suggested.Should().Contain(Priority.High);
        suggested.Should().Contain(Priority.Urgent);
        suggested.Should().HaveCount(2);
    }

    [Fact]
    public void AppTaskCategory_GetSuggestedPriorities_Should_Return_All_Priorities_For_ToDo()
    {
        var suggested = AppTaskCategory.ToDo.GetSuggestedPriorities().ToList();

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
    public void AppTaskCategory_GetEstimatedHours_Should_Return_Correct_Hours(string categoryName, double expectedHours)
    {
        var category = AppTaskCategory.FromName(categoryName);

        category.GetEstimatedHours().Should().Be(expectedHours);
    }

    [Theory]
    [InlineData("ToDo", false)]
    [InlineData("Idea", false)]
    [InlineData("Appointment", true)]
    [InlineData("BillReminder", true)]
    [InlineData("Project", false)]
    public void AppTaskCategory_ShouldAutoArchive_Should_Return_Correct_Value(string categoryName, bool expectedAutoArchive)
    {
        var category = AppTaskCategory.FromName(categoryName);

        category.ShouldAutoArchive().Should().Be(expectedAutoArchive);
    }

    [Fact]
    public void AppTaskCategory_Should_Support_Implicit_Conversion_To_Int()
    {
        int categoryValue = AppTaskCategory.Project;

        categoryValue.Should().Be(4);
    }

    [Fact]
    public void AppTaskCategory_Should_Support_Explicit_Conversion_From_Int()
    {
        var category = (AppTaskCategory)1;

        category.Should().Be(AppTaskCategory.Idea);
    }

    [Fact]
    public void AppTaskCategory_ToString_Should_Return_Name()
    {
        AppTaskCategory.Appointment.ToString().Should().Be("Appointment");
    }
}
