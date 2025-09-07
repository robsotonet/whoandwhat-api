using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Services;

public interface ITaskApplicationService
{
    Task<Result<TaskDto>> CreateTaskAsync(CreateTaskRequest request, Guid userId);
    Task<Result<TaskDto>> GetTaskAsync(Guid taskId, Guid userId, bool includeSubtasks = true);
    Task<Result<PagedResult<TaskDto>>> GetTasksAsync(TaskQueryRequest request, Guid userId);
    Task<Result<TaskDto>> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, Guid userId);
    Task<Result> DeleteTaskAsync(Guid taskId, Guid userId, bool hardDelete = false);
    Task<Result<TaskDto>> ConvertTaskAsync(Guid taskId, ConvertTaskRequest request, Guid userId);
    Task<Result<TaskDto>> ExecuteTaskActionAsync(Guid taskId, TaskActionRequest request, Guid userId);
    Task<Result<TaskWorkflowStateDto>> GetTaskWorkflowAsync(Guid taskId, Guid userId);
    Task<Result<TaskSchedulingResponse>> GetTaskSchedulingSuggestionsAsync(Guid userId, DateTime? targetDate = null, int maxSuggestions = 20);
    Task<Result<TaskStatisticsResponse>> GetTaskStatisticsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<Result<TaskMetricsDto>> GetTaskMetricsAsync(Guid userId);
}