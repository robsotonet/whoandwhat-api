using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Services;

public interface ITaskApplicationService
{
    public Task<Result<TaskDto>> CreateTaskAsync(CreateTaskRequest request, Guid userId);
    public Task<Result<TaskDto>> GetTaskAsync(Guid taskId, Guid userId, bool includeSubtasks = true);
    public Task<Result<PagedResult<TaskDto>>> GetTasksAsync(TaskQueryRequest request, Guid userId);
    public Task<Result<TaskDto>> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, Guid userId);
    public Task<Result> DeleteTaskAsync(Guid taskId, Guid userId, bool hardDelete = false);
    public Task<Result<TaskDto>> ConvertTaskAsync(Guid taskId, ConvertTaskRequest request, Guid userId);
    public Task<Result<TaskDto>> ExecuteTaskActionAsync(Guid taskId, TaskActionRequest request, Guid userId);
    public Task<Result<TaskWorkflowStateDto>> GetTaskWorkflowAsync(Guid taskId, Guid userId);
    public Task<Result<TaskSchedulingResponse>> GetTaskSchedulingSuggestionsAsync(Guid userId, DateTime? targetDate = null, int maxSuggestions = 20);
    public Task<Result<TaskStatisticsResponse>> GetTaskStatisticsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    public Task<Result<TaskMetricsDto>> GetTaskMetricsAsync(Guid userId);
}
