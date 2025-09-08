using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<DeleteTaskCommandHandler> _logger;

    public DeleteTaskCommandHandler(
        ITaskRepository taskRepository,
        ILogger<DeleteTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.UserId != request.UserId)
            {
                return Result.Failure("Task not found");
            }

            if (request.HardDelete)
            {
                // Hard delete - permanently remove from database
                await _taskRepository.DeleteAsync(task.Id);
                _logger.LogInformation("Hard deleted task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            }
            else
            {
                // Soft delete - mark as archived
                task.IsArchived = true;
                task.ArchivedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                
                await _taskRepository.UpdateAsync(task);
                _logger.LogInformation("Soft deleted (archived) task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            }

            await _taskRepository.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId} for user {UserId}", request.TaskId, request.UserId);
            return Result.Failure("An error occurred while deleting the task");
        }
    }
}