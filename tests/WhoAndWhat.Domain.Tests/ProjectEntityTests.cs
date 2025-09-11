using FluentAssertions;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Tests;

public class ProjectEntityTests
{
    [Fact]
    public void Project_Should_Initialize_With_Default_Values()
    {
        var project = new Project();

        project.Id.Should().NotBe(Guid.Empty); // BaseEntity auto-generates ID
        project.Name.Should().BeNull();
        project.Description.Should().BeNull();
        project.StartDate.Should().BeNull();
        project.EndDate.Should().BeNull();
        project.Status.Should().Be(0);
        project.Progress.Should().Be(0);
        project.UserId.Should().Be(Guid.Empty);
        project.Tasks.Should().NotBeNull().And.BeEmpty();
        project.Contacts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Project_Should_Allow_Setting_All_Properties()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var name = "Test Project";
        var description = "Test Description";
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(30);
        var status = 1; // Active status
        var progress = 50;

        var project = new Project
        {
            Id = projectId,
            Name = name,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            Status = status,
            Progress = progress,
            UserId = userId
        };

        project.Id.Should().Be(projectId);
        project.Name.Should().Be(name);
        project.Description.Should().Be(description);
        project.StartDate.Should().Be(startDate);
        project.EndDate.Should().Be(endDate);
        project.Status.Should().Be(status);
        project.Progress.Should().Be(progress);
        project.UserId.Should().Be(userId);
    }

    [Fact]
    public void Project_Should_Allow_Adding_Tasks()
    {
        var project = new Project();
        var task1 = new WhoAndWhat.Domain.Entities.AppTask { Title = "Task 1" };
        var task2 = new WhoAndWhat.Domain.Entities.AppTask { Title = "Task 2" };

        project.Tasks.Add(task1);
        project.Tasks.Add(task2);

        project.Tasks.Should().HaveCount(2);
        project.Tasks.Should().Contain(task1);
        project.Tasks.Should().Contain(task2);
    }

    [Fact]
    public void Project_Should_Allow_Adding_Contacts()
    {
        var project = new Project();
        var contact1 = new Contact { Name = "Contact 1" };
        var contact2 = new Contact { Name = "Contact 2" };

        project.Contacts.Add(contact1);
        project.Contacts.Add(contact2);

        project.Contacts.Should().HaveCount(2);
        project.Contacts.Should().Contain(contact1);
        project.Contacts.Should().Contain(contact2);
    }

    [Fact]
    public void Project_Should_Handle_Null_Description()
    {
        var project = new Project
        {
            Name = "Project without description",
            Description = null
        };

        project.Description.Should().BeNull();
    }

    [Fact]
    public void Project_Should_Handle_Null_StartDate()
    {
        var project = new Project
        {
            Name = "Project without start date",
            StartDate = null
        };

        project.StartDate.Should().BeNull();
    }

    [Fact]
    public void Project_Should_Handle_Null_EndDate()
    {
        var project = new Project
        {
            Name = "Project without end date",
            EndDate = null
        };

        project.EndDate.Should().BeNull();
    }

    [Fact]
    public void Project_Should_Allow_Progress_From_Zero_To_Hundred()
    {
        var project = new Project();

        project.Progress = 0;
        project.Progress.Should().Be(0);

        project.Progress = 50;
        project.Progress.Should().Be(50);

        project.Progress = 100;
        project.Progress.Should().Be(100);
    }
}
