using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskWorkflow;

public class GetTaskWorkflowQueryHandler : IRequestHandler<GetTaskWorkflowQuery, Result<TaskWorkflowStateDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly CategoryBusinessRuleService _categoryBusinessRuleService;
    private readonly CategoryWorkflowService _categoryWorkflowService;
    private readonly ILogger<GetTaskWorkflowQueryHandler> _logger;

    public GetTaskWorkflowQueryHandler(
        ITaskRepository taskRepository,
        CategoryBusinessRuleService categoryBusinessRuleService,
        CategoryWorkflowService categoryWorkflowService,
        ILogger<GetTaskWorkflowQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _categoryBusinessRuleService = categoryBusinessRuleService;
        _categoryWorkflowService = categoryWorkflowService;
        _logger = logger;
    }

    public async Task<Result<TaskWorkflowStateDto>> Handle(GetTaskWorkflowQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.UserId != request.UserId)
            {
                return Result<TaskWorkflowStateDto>.Failure("Task not found");
            }

            var currentStatus = TaskStatus.FromValue(task.Status);
            var recommendedStatus = _categoryBusinessRuleService.GetRecommendedNextStatus(task);
            var availableActions = _categoryBusinessRuleService.GetAvailableActions(task);
            var workflowState = _categoryWorkflowService.GetWorkflowState(task);

            var workflowDto = new TaskWorkflowStateDto
            {
                CurrentStatus = task.Status,
                CurrentStatusName = currentStatus.Name,
                RecommendedNextStatus = (int?)recommendedStatus,
                RecommendedNextStatusName = recommendedStatus?.Name,
                AvailableActions = availableActions.Select(action => new TaskActionDto
                {
                    Id = action.Id,
                    DisplayName = action.DisplayName,
                    Description = action.Description,
                    Icon = action.Icon,
                    RequiresConfirmation = action.RequiresConfirmation,
                    Parameters = action.Parameters
                }).ToList(),
                Blockers = workflowState.Blockers.ToList(),
                CanComplete = workflowState.CanProgress,
                CanReopen = workflowState.CanReopen,
                WorkflowStage = workflowState.CurrentStage
            };

            return Result<TaskWorkflowStateDto>.Success(workflowDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow state for task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            return Result<TaskWorkflowStateDto>.Failure("An error occurred while retrieving task workflow state");
        }
    }
}