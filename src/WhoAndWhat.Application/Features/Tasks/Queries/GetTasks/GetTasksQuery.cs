using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTasks;

public record GetTasksQuery(
    Guid UserId,
    string? Search = null,
    List<int>? Categories = null,
    List<int>? Statuses = null,
    List<int>? Priorities = null,
    DateTime? DueDateFrom = null,
    DateTime? DueDateTo = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    List<Guid>? ContactIds = null,
    bool? HasDueDate = null,
    bool? IsOverdue = null,
    bool? HasSubtasks = null,
    Guid? ParentTaskId = null,
    string? SortBy = "UpdatedAt",
    bool SortDescending = true,
    int PageSize = 20,
    int PageNumber = 1,
    bool IncludeArchived = false
) : IRequest<Result<PagedResult<TaskDto>>>;