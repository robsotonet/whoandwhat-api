using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;

public record CreateTaskCommand(
    string Title,
    string? Description,
    int Category,
    int Priority,
    DateTime? DueDate,
    Guid? ParentTaskId,
    List<Guid> ContactIds,
    TaskMetadataRequest? Metadata,
    Guid UserId
) : IRequest<Result<TaskDto>>;