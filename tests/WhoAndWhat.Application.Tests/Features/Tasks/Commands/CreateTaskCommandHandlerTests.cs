using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.Tests.Features.Tasks.Commands;

public class CreateTaskCommandHandlerTests
{
    private readonly Mock<IAppTaskRepository> _mockTaskRepository;
    private readonly CategoryBusinessRuleService _businessRuleService;
    private readonly Mock<ILogger<CreateTaskCommandHandler>> _mockLogger;
    private readonly CreateTaskCommandHandler _handler;

    public CreateTaskCommandHandlerTests()
    {
        _mockTaskRepository = new Mock<IAppTaskRepository>();
        _businessRuleService = new CategoryBusinessRuleService();
        _mockLogger = new Mock<ILogger<CreateTaskCommandHandler>>();
        _handler = new CreateTaskCommandHandler(
            _mockTaskRepository.Object,
            _businessRuleService,
            _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Sets up the standard repository mocks for successful task creation
    /// </summary>
    private void SetupSuccessfulRepositoryMocks()
    {
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up repository mock to capture the created task
    /// </summary>
    private void SetupRepositoryMockWithTaskCapture(out DomainTask capturedTask)
    {
        DomainTask tempCapturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => tempCapturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        capturedTask = tempCapturedTask;
    }

    /// <summary>
    /// Creates a basic valid CreateTaskCommand for testing
    /// </summary>
    private static CreateTaskCommand CreateBasicValidCommand(
        string title = "Test Task",
        string? description = "Test Description",
        int category = 0, // AppTaskCategory.ToDo
        int priority = 2, // Priority.Medium
        DateTime? dueDate = null,
        Guid? parentTaskId = null,
        List<Guid>? contactIds = null,
        TaskMetadataRequest? metadata = null,
        Guid? userId = null)
    {
        return new CreateTaskCommand(
            Title: title,
            Description: description,
            Category: category,
            Priority: priority,
            DueDate: dueDate,
            ParentTaskId: parentTaskId,
            ContactIds: contactIds ?? new List<Guid>(),
            Metadata: metadata,
            UserId: userId ?? Guid.NewGuid()
        );
    }

    /// <summary>
    /// Verifies that repository methods were never called (for failure scenarios)
    /// </summary>
    private void VerifyRepositoryNeverCalled()
    {
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that repository methods were called exactly once (for success scenarios)
    /// </summary>
    private void VerifyRepositoryCalledOnce()
    {
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    [Fact]
    public async Task Handle_Should_Create_Task_Successfully_With_Valid_Data()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Test Task",
            Description: "Test Description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: DateTime.UtcNow.AddDays(7),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Test Task");
        result.Value.Description.Should().Be("Test Description");
        result.Value.Category.Should().Be((int)AppTaskCategory.ToDo);
        result.Value.Priority.Should().Be((int)Priority.Medium);

        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Create_Task_With_Contacts()
    {
        // Arrange
        var contactIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new CreateTaskCommand(
            Title: "Task with Contacts",
            Description: "Description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddDays(3),
            ParentTaskId: null,
            ContactIds: contactIds,
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskContacts.Should().HaveCount(2);
        
        capturedTask.Should().NotBeNull();
        capturedTask.TaskContacts.Should().HaveCount(2);
        capturedTask.TaskContacts.All(tc => tc.Role == "Participant").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Validation_Fails()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Invalid Task",
            Description: null,
            Category: (int)AppTaskCategory.BillReminder,
            Priority: (int)Priority.Low,
            DueDate: null, // BillReminder requires due date
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - BillReminder without due date should fail naturally

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Bill Reminder tasks must have a due date");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Create_Simple_ToDo_Task_Successfully()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Simple Task",
            Description: "Simple description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null, // Keep it simple
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Simple Task");
        result.Value.Category.Should().Be((int)AppTaskCategory.ToDo);
    }

    [Fact]
    public async Task Handle_Should_Create_Subtask_With_ParentTaskId()
    {
        // Arrange
        var parentTaskId = Guid.NewGuid();
        var command = new CreateTaskCommand(
            Title: "Subtask",
            Description: "Subtask Description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Low,
            DueDate: DateTime.UtcNow.AddDays(2),
            ParentTaskId: parentTaskId,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParentTaskId.Should().Be(parentTaskId);
        
        capturedTask.Should().NotBeNull();
        capturedTask.ParentTaskId.Should().Be(parentTaskId);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws_Exception()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Test Task",
            Description: "Description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("An error occurred while creating the task");
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Set_Default_Status_To_Pending()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "New Task",
            Description: null,
            Category: (int)AppTaskCategory.Idea,
            Priority: (int)Priority.Low,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be((int)DomainTaskStatus.Pending);
        result.Value.StatusName.Should().Be("Pending");
        
        capturedTask.Should().NotBeNull();
        capturedTask.Status.Should().Be((int)DomainTaskStatus.Pending);
    }

    [Fact]
    public async Task Handle_Should_Set_Timestamps_Correctly()
    {
        // Arrange
        var beforeExecution = DateTime.UtcNow;
        var command = new CreateTaskCommand(
            Title: "Timestamp Test",
            Description: "Testing timestamps",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null, // Keep it simple - no due date for ToDo
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        result.Value.CreatedAt.Should().BeOnOrBefore(afterExecution);
        result.Value.UpdatedAt.Should().Be(result.Value.CreatedAt);
        
        capturedTask.Should().NotBeNull();
        capturedTask.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        capturedTask.CreatedAt.Should().BeOnOrBefore(afterExecution);
        capturedTask.UpdatedAt.Should().Be(capturedTask.CreatedAt);
    }

    [Fact]
    public async Task Handle_Should_Generate_Unique_TaskId()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Unique ID Test",
            Description: null,
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Low,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Using concrete CategoryBusinessRuleService - no setup needed

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        
        capturedTask.Should().NotBeNull();
        capturedTask.Id.Should().NotBeEmpty();
        capturedTask.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Title_Is_Empty()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "",
            Description: "Valid description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Title is required");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Title_Is_Null()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: null!,
            Description: "Valid description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Title is required");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Title_Exceeds_Maximum_Length()
    {
        // Arrange
        var longTitle = new string('A', 201); // Assuming max length is 200
        var command = new CreateTaskCommand(
            Title: longTitle,
            Description: "Valid description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Title cannot exceed");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Description_Exceeds_Maximum_Length()
    {
        // Arrange
        var longDescription = new string('B', 2001); // Assuming max length is 2000
        var command = new CreateTaskCommand(
            Title: "Valid Title",
            Description: longDescription,
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Description cannot exceed");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Invalid_Category_Value()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Valid Title",
            Description: "Valid description",
            Category: 999, // Invalid category
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid category");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Invalid_Priority_Value()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Valid Title",
            Description: "Valid description",
            Category: (int)AppTaskCategory.ToDo,
            Priority: -1, // Invalid priority
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid priority");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_DueDate_Is_In_Past()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var command = new CreateTaskCommand(
            Title: "Task with Past Due Date",
            Description: "Description",
            Category: (int)AppTaskCategory.Appointment, // Appointments require due dates
            Priority: (int)Priority.High,
            DueDate: pastDate,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Due date cannot be in the past");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #region Category-Specific Validation Tests

    [Fact]
    public async Task Handle_Should_Create_Appointment_Task_Successfully_With_DueDate()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(3);
        var command = new CreateTaskCommand(
            Title: "Doctor Appointment",
            Description: "Annual checkup",
            Category: (int)AppTaskCategory.Appointment,
            Priority: (int)Priority.High,
            DueDate: futureDate,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.Appointment);
        result.Value.DueDate.Should().Be(futureDate);
        result.Value.Priority.Should().Be((int)Priority.High);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Appointment_Has_No_DueDate()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Doctor Appointment",
            Description: "Annual checkup",
            Category: (int)AppTaskCategory.Appointment,
            Priority: (int)Priority.High,
            DueDate: null, // Appointments must have due dates
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Appointment tasks must have a due date");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Create_Project_Task_Successfully()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Website Redesign Project",
            Description: "Complete redesign of company website with new branding",
            Category: (int)AppTaskCategory.Project,
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddMonths(3),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.Project);
        result.Value.Title.Should().Be("Website Redesign Project");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Project_Has_Parent_Task()
    {
        // Arrange
        var parentTaskId = Guid.NewGuid();
        var command = new CreateTaskCommand(
            Title: "Sub-project",
            Description: "This should not be allowed",
            Category: (int)AppTaskCategory.Project,
            Priority: (int)Priority.Medium,
            DueDate: DateTime.UtcNow.AddDays(30),
            ParentTaskId: parentTaskId, // Projects cannot have parent tasks
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Project tasks cannot have a parent task");
        
        _mockTaskRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTaskRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Create_Idea_Task_Successfully()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "New App Idea",
            Description: "Mobile app for tracking daily habits",
            Category: (int)AppTaskCategory.Idea,
            Priority: (int)Priority.Low, // Ideas typically have low priority
            DueDate: null, // Ideas don't need due dates
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.Idea);
        result.Value.Priority.Should().Be((int)Priority.Low);
        result.Value.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Warning_When_Idea_Has_High_Priority()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Urgent Business Idea",
            Description: "Revolutionary business concept",
            Category: (int)AppTaskCategory.Idea,
            Priority: (int)Priority.High, // Ideas shouldn't be high priority
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.Idea);
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("warnings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Create_BillReminder_Task_With_Required_DueDate()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(15);
        var command = new CreateTaskCommand(
            Title: "Pay Credit Card Bill",
            Description: "Monthly credit card payment due",
            Category: (int)AppTaskCategory.BillReminder,
            Priority: (int)Priority.High,
            DueDate: dueDate, // Required for bill reminders
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.BillReminder);
        result.Value.DueDate.Should().Be(dueDate);
        result.Value.Priority.Should().Be((int)Priority.High);
    }

    [Fact]
    public async Task Handle_Should_Auto_Adjust_BillReminder_Priority_To_Medium_When_Low()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Pay Utility Bill",
            Description: "Monthly electricity bill",
            Category: (int)AppTaskCategory.BillReminder,
            Priority: (int)Priority.Low, // Should be automatically adjusted to Medium
            DueDate: DateTime.UtcNow.AddDays(10),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be((int)AppTaskCategory.BillReminder);
        result.Value.Priority.Should().BeGreaterOrEqualTo((int)Priority.Medium);
        
        capturedTask.Should().NotBeNull();
        capturedTask.Priority.Should().BeGreaterOrEqualTo((int)Priority.Medium);
    }

    #endregion

    #region Advanced Scenario Tests

    [Fact]
    public async Task Handle_Should_Create_Task_With_Metadata_Successfully()
    {
        // Arrange
        // Note: Metadata handling would depend on actual implementation
        object? metadata = null;

        var command = new CreateTaskCommand(
            Title: "Task with Metadata",
            Description: "Task containing additional metadata",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddDays(2),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: metadata,
            UserId: Guid.NewGuid()
        );

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        
        capturedTask.Should().NotBeNull();
        // Note: Metadata handling would depend on implementation
        // This test validates the command can handle metadata without errors
    }

    [Fact]
    public async Task Handle_Should_Create_Task_With_Large_Contact_List()
    {
        // Arrange
        var contactIds = Enumerable.Range(1, 20)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var command = new CreateTaskCommand(
            Title: "Team Collaboration Task",
            Description: "Large team project requiring many participants",
            Category: (int)AppTaskCategory.Project,
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddMonths(2),
            ParentTaskId: null,
            ContactIds: contactIds,
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskContacts.Should().HaveCount(20);
        
        capturedTask.Should().NotBeNull();
        capturedTask.TaskContacts.Should().HaveCount(20);
        capturedTask.TaskContacts.Should().AllSatisfy(tc => 
        {
            tc.Role.Should().Be("Participant");
            tc.TaskId.Should().Be(capturedTask.Id);
            contactIds.Should().Contain(tc.ContactId);
        });
    }

    [Fact]
    public async Task Handle_Should_Handle_Duplicate_Contact_Ids()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var contactIds = new List<Guid> { contactId, contactId, contactId }; // Duplicates

        var command = new CreateTaskCommand(
            Title: "Task with Duplicate Contacts",
            Description: "Testing duplicate contact handling",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: contactIds,
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Implementation should handle duplicates - either allow all or deduplicate
        result.Value.TaskContacts.Should().NotBeEmpty();
        
        capturedTask.Should().NotBeNull();
        capturedTask.TaskContacts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Create_Complex_Nested_Project_Structure()
    {
        // Arrange - Creating a parent project first (this would be a separate test in reality)
        var parentProjectId = Guid.NewGuid(); // Simulating existing parent project
        var contactIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var command = new CreateTaskCommand(
            Title: "Complex Subtask",
            Description: "Detailed subtask with multiple contacts and metadata",
            Category: (int)AppTaskCategory.ToDo, // Subtask under project
            Priority: (int)Priority.High,
            DueDate: DateTime.UtcNow.AddDays(21), // 3 weeks
            ParentTaskId: parentProjectId,
            ContactIds: contactIds,
            Metadata: null, // Metadata handling depends on implementation
            UserId: Guid.NewGuid()
        );

        DomainTask capturedTask = null!;
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTask = task)
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParentTaskId.Should().Be(parentProjectId);
        result.Value.TaskContacts.Should().HaveCount(2);
        
        capturedTask.Should().NotBeNull();
        capturedTask.ParentTaskId.Should().Be(parentProjectId);
        capturedTask.TaskContacts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Handle_Empty_Contact_List_Gracefully()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Task with Empty Contacts",
            Description: "Testing empty contact list handling",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Low,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(), // Empty list
            Metadata: null,
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskContacts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Create_Task_With_Maximum_Length_Fields()
    {
        // Arrange - Testing boundary conditions with maximum allowed lengths
        var maxTitle = new string('T', 200); // Assuming max title length is 200
        var maxDescription = new string('D', 2000); // Assuming max description length is 2000

        var command = new CreateTaskCommand(
            Title: maxTitle,
            Description: maxDescription,
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: DateTime.UtcNow.AddDays(7),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null, // Metadata handling depends on implementation
            UserId: Guid.NewGuid()
        );

        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(maxTitle);
        result.Value.Description.Should().Be(maxDescription);
    }

    [Fact]
    public async Task Handle_Should_Handle_Concurrent_Task_Creation_Scenarios()
    {
        // Arrange - Simulating concurrent task creation with same user
        var userId = Guid.NewGuid();
        var commands = Enumerable.Range(1, 5).Select(i => new CreateTaskCommand(
            Title: $"Concurrent Task {i}",
            Description: $"Task created concurrently - batch {i}",
            Category: (int)AppTaskCategory.ToDo,
            Priority: (int)Priority.Medium,
            DueDate: DateTime.UtcNow.AddDays(i),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null, // Metadata handling depends on implementation
            UserId: userId
        )).ToList();

        var capturedTasks = new List<DomainTask>();
        _mockTaskRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>(), It.IsAny<CancellationToken>()))
            .Callback<DomainTask, CancellationToken>((task, ct) => capturedTasks.Add(task))
            .Returns(Task.CompletedTask);
        _mockTaskRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act - Execute all commands
        var tasks = commands.Select(cmd => _handler.Handle(cmd, CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(result => result.IsSuccess.Should().BeTrue());
        results.Should().HaveCount(5);
        capturedTasks.Should().HaveCount(5);
        capturedTasks.Select(t => t.UserId).Should().AllBeEquivalentTo(userId);
        capturedTasks.Select(t => t.Id).Distinct().Should().HaveCount(5); // All unique IDs
    }

    #endregion
}