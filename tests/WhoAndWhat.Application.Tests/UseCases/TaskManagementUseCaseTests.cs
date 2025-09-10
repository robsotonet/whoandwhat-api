using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;
using WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;
using WhoAndWhat.Application.Features.Tasks.Commands.ConvertTask;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTask;
using WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using AppTaskUpdateRequest = WhoAndWhat.Domain.Services.AppTaskUpdateRequest;

namespace WhoAndWhat.Application.Tests.UseCases;

/// <summary>
/// Unit tests for Task Management use cases
/// </summary>
public class TaskManagementUseCaseTests
{
    private readonly Mock<IAppTaskRepository> _mockRepository;
    private readonly Mock<CategoryBusinessRuleService> _mockCategoryService;
    private readonly Mock<ILogger<CreateTaskCommandHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<UpdateTaskCommandHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteTaskCommandHandler>> _mockDeleteLogger;
    private readonly Mock<ILogger<ConvertTaskCommandHandler>> _mockConvertLogger;
    private readonly Mock<ILogger<GetTaskQueryHandler>> _mockGetTaskLogger;
    private readonly Mock<ILogger<GetTasksQueryHandler>> _mockGetTasksLogger;
    private readonly Guid _testUserId = Guid.NewGuid();

    public TaskManagementUseCaseTests()
    {
        _mockRepository = new Mock<IAppTaskRepository>();
        _mockCategoryService = new Mock<CategoryBusinessRuleService>();
        _mockCreateLogger = new Mock<ILogger<CreateTaskCommandHandler>>();
        _mockUpdateLogger = new Mock<ILogger<UpdateTaskCommandHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteTaskCommandHandler>>();
        _mockConvertLogger = new Mock<ILogger<ConvertTaskCommandHandler>>();
        _mockGetTaskLogger = new Mock<ILogger<GetTaskQueryHandler>>();
        _mockGetTasksLogger = new Mock<ILogger<GetTasksQueryHandler>>();
    }

    #region CreateTaskCommandHandler Tests

    [Fact]
    public async Task CreateTaskCommand_Should_Create_Task_Successfully()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Test Task",
            Description: "Test Description",
            Category: 0, // ToDo
            Priority: 1, // Low
            DueDate: DateTime.UtcNow.AddDays(7),
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: _testUserId
        );

        var validationResult = ValidationResult.Success();
        _mockCategoryService.Setup(x => x.ValidateTaskCreation(It.IsAny<DomainTask>()))
            .Returns(validationResult);

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new CreateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockCreateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Test Task");
        result.Value.Description.Should().Be("Test Description");
        result.Value.Category.Should().Be(0);
        result.Value.Priority.Should().Be(1);

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>()), Times.Once);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTaskCommand_Should_Fail_When_Validation_Fails()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "",  // Invalid empty title
            Description: "Test Description",
            Category: 0,
            Priority: 1,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: new List<Guid>(),
            Metadata: null,
            UserId: _testUserId
        );

        var validationResult = ValidationResult.Failure("Title is required");
        _mockCategoryService.Setup(x => x.ValidateTaskCreation(It.IsAny<DomainTask>()))
            .Returns(validationResult);

        var handler = new CreateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockCreateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Title is required");

        _mockRepository.Verify(x => x.AddAsync(It.IsAny<DomainTask>()), Times.Never);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateTaskCommand_Should_Add_Task_Contacts_When_Provided()
    {
        // Arrange
        var contactIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new CreateTaskCommand(
            Title: "Task with Contacts",
            Description: "Test Description",
            Category: 0,
            Priority: 1,
            DueDate: null,
            ParentTaskId: null,
            ContactIds: contactIds,
            Metadata: null,
            UserId: _testUserId
        );

        var validationResult = ValidationResult.Success();
        _mockCategoryService.Setup(x => x.ValidateTaskCreation(It.IsAny<DomainTask>()))
            .Returns(validationResult);

        DomainTask savedTask = null!;
        _mockRepository.Setup(x => x.AddAsync(It.IsAny<DomainTask>()))
            .Callback<DomainTask>(task => savedTask = task)
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new CreateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockCreateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        savedTask.Should().NotBeNull();
        savedTask.TaskContacts.Should().HaveCount(2);
        savedTask.TaskContacts.Select(tc => tc.ContactId).Should().BeEquivalentTo(contactIds);
    }

    #endregion

    #region UpdateTaskCommandHandler Tests

    [Fact]
    public async Task UpdateTaskCommand_Should_Update_Task_Successfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Original Title",
            Description = "Original Description",
            Category = 0,
            Priority = 1,
            Status = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: "Updated Description",
            Category: null,
            Status: 1, // InProgress
            Priority: 2, // Medium
            DueDate: DateTime.UtcNow.AddDays(3),
            ClearDueDate: null,
            Metadata: null,
            ContactIds: null,
            UserId: _testUserId
        );

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        var validationResult = ValidationResult.Success();
        _mockCategoryService.Setup(x => x.ValidateTaskUpdate(It.IsAny<DomainTask>(), It.IsAny<AppTaskUpdateRequest>()))
            .Returns(validationResult);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new UpdateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockUpdateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Updated Title");
        result.Value.Description.Should().Be("Updated Description");
        result.Value.Status.Should().Be(1);
        result.Value.Priority.Should().Be(2);

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Once);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateTaskCommand_Should_Fail_When_Task_Not_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: null,
            Metadata: null,
            ContactIds: null,
            UserId: _testUserId
        );

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(null));

        var handler = new UpdateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockUpdateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskCommand_Should_Fail_When_User_Not_Authorized()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = differentUserId, // Different user
            Title = "Original Title",
            Category = 0,
            Priority = 1,
            Status = 0
        };

        var command = new UpdateTaskCommand(
            TaskId: taskId,
            Title: "Updated Title",
            Description: null,
            Category: null,
            Status: null,
            Priority: null,
            DueDate: null,
            ClearDueDate: null,
            Metadata: null,
            ContactIds: null,
            UserId: _testUserId // Different from task owner
        );

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        var handler = new UpdateTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockUpdateLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    #endregion

    #region DeleteTaskCommandHandler Tests

    [Fact]
    public async Task DeleteTaskCommand_Should_Soft_Delete_Task_Successfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Task to Delete",
            Category = 0,
            Priority = 1,
            Status = 0
        };

        var command = new DeleteTaskCommand(taskId, _testUserId, HardDelete: false);

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new DeleteTaskCommandHandler(
            _mockRepository.Object,
            _mockDeleteLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Once);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskCommand_Should_Fail_When_Task_Not_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new DeleteTaskCommand(taskId, _testUserId, HardDelete: false);

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(null));

        var handler = new DeleteTaskCommandHandler(
            _mockRepository.Object,
            _mockDeleteLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteTaskCommand_Should_Hard_Delete_Task_When_Requested()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Task to Hard Delete",
            Category = 0,
            Priority = 1,
            Status = 0
        };

        var command = new DeleteTaskCommand(taskId, _testUserId, HardDelete: true);

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        _mockRepository.Setup(x => x.DeleteAsync(It.IsAny<DomainTask>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new DeleteTaskCommandHandler(
            _mockRepository.Object,
            _mockDeleteLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify hard delete was called
        _mockRepository.Verify(x => x.DeleteAsync(existingTask), Times.Once);
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
    }

    #endregion

    #region GetTasksQueryHandler Tests

    [Fact]
    public async Task GetTasksQuery_Should_Return_User_Tasks()
    {
        // Arrange
        var tasks = new List<DomainTask>
        {
            new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Title = "Task 1",
                Category = 0,
                Priority = 1,
                Status = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Title = "Task 2",
                Category = 1,
                Priority = 2,
                Status = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var query = new GetTasksQuery(
            UserId: _testUserId,
            PageSize: 10,
            PageNumber: 1
        );

        _mockRepository.Setup(x => x.GetPagedAsync(
            It.IsAny<TaskSearchCriteria>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<bool>()))
            .Returns(Task.FromResult((tasks, 2)));

        var handler = new GetTasksQueryHandler(
            _mockRepository.Object,
            _mockGetTasksLogger.Object
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetTasksQuery_Should_Filter_By_Category()
    {
        // Arrange
        var tasks = new List<DomainTask>
        {
            new DomainTask
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Title = "ToDo Task",
                Category = 0, // ToDo
                Priority = 1,
                Status = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var query = new GetTasksQuery(
            UserId: _testUserId,
            Categories: new List<int> { 0 }, // Filter by ToDo
            PageSize: 10,
            PageNumber: 1
        );

        _mockRepository.Setup(x => x.GetPagedAsync(
            It.Is<TaskSearchCriteria>(c => c.Categories != null && c.Categories.Contains(AppTaskCategory.ToDo)),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<bool>()))
            .Returns(Task.FromResult((tasks, 1)));

        var handler = new GetTasksQueryHandler(
            _mockRepository.Object,
            _mockGetTasksLogger.Object
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Category.Should().Be(0);
    }

    [Fact]
    public async Task GetTasksQuery_Should_Support_Pagination()
    {
        // Arrange
        var allTasks = Enumerable.Range(1, 15).Select(i => new DomainTask
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = $"Task {i}",
            Category = 0,
            Priority = 1,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        var query = new GetTasksQuery(
            UserId: _testUserId,
            PageSize: 5,
            PageNumber: 2
        );

        _mockRepository.Setup(x => x.GetPagedAsync(
            It.IsAny<TaskSearchCriteria>(),
            5,
            2,
            It.IsAny<string>(),
            It.IsAny<bool>()))
            .Returns(Task.FromResult((allTasks.Skip(5).Take(5).ToList(), 15)));

        var handler = new GetTasksQueryHandler(
            _mockRepository.Object,
            _mockGetTasksLogger.Object
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(5);
        result.Value.TotalCount.Should().Be(15);
        result.Value.Page.Should().Be(2);
        result.Value.TotalPages.Should().Be(3);
    }

    #endregion

    #region GetTaskQueryHandler Tests

    [Fact]
    public async Task GetTaskQuery_Should_Return_Task_When_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Test Task",
            Description = "Test Description",
            Category = 0,
            Priority = 1,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var query = new GetTaskQuery(taskId, _testUserId, IncludeSubtasks: true);

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(task));

        var handler = new GetTaskQueryHandler(
            _mockRepository.Object,
            _mockGetTaskLogger.Object
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(taskId);
        result.Value.Title.Should().Be("Test Task");
    }

    [Fact]
    public async Task GetTaskQuery_Should_Fail_When_Task_Not_Found()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var query = new GetTaskQuery(taskId, _testUserId, IncludeSubtasks: false);

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(null));

        var handler = new GetTaskQueryHandler(
            _mockRepository.Object,
            _mockGetTaskLogger.Object
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region ConvertTaskCommandHandler Tests

    [Fact]
    public async Task ConvertTaskCommand_Should_Convert_Task_To_Project()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Task to Convert",
            Description = "Description",
            Category = 0, // ToDo
            Priority = 2,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var command = new ConvertTaskCommand(
            TaskId: taskId,
            ToCategory: 4, // Project
            Reason: "Converting to project",
            CreateSubtasks: false,
            UserId: _testUserId
        );

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        var validationResult = ValidationResult.Success();
        _mockCategoryService.Setup(x => x.ValidateTaskUpdate(
            It.IsAny<DomainTask>(),
            It.IsAny<AppTaskUpdateRequest>()))
            .Returns(validationResult);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<DomainTask>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(x => x.SaveChangesAsync())
            .Returns(Task.FromResult(1));

        var handler = new ConvertTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockConvertLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Category.Should().Be(4); // Project

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Once);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ConvertTaskCommand_Should_Fail_When_Conversion_Not_Allowed()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new DomainTask
        {
            Id = taskId,
            UserId = _testUserId,
            Title = "Appointment Task",
            Category = 2, // Appointment
            Priority = 2,
            Status = 0
        };

        var command = new ConvertTaskCommand(
            TaskId: taskId,
            ToCategory: 0, // ToDo (invalid conversion from Appointment)
            Reason: "Invalid conversion",
            CreateSubtasks: false,
            UserId: _testUserId
        );

        _mockRepository.Setup(x => x.GetByIdAsync(taskId))
            .Returns(Task.FromResult<DomainTask?>(existingTask));

        var validationResult = ValidationResult.Failure("Cannot convert from Appointment to ToDo");
        _mockCategoryService.Setup(x => x.ValidateTaskUpdate(
            It.IsAny<DomainTask>(),
            It.IsAny<AppTaskUpdateRequest>()))
            .Returns(validationResult);

        var handler = new ConvertTaskCommandHandler(
            _mockRepository.Object,
            _mockCategoryService.Object,
            _mockConvertLogger.Object
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot convert");

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
        _mockRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    #endregion
}