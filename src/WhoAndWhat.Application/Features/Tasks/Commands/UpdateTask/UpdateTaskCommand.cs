using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;

public record UpdateTaskCommand(
    Guid TaskId,
    string? Title,
    string? Description,
    int? Category,
    int? Status,
    int? Priority,
    DateTime? DueDate,
    bool? ClearDueDate,
    TaskMetadataRequest? Metadata,
    List<Guid>? ContactIds,
    Guid UserId
) : IRequest<Result<TaskDto>>;