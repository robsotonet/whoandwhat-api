using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.OptimizeSchedule;

public class OptimizeScheduleCommandHandler : IRequestHandler<OptimizeScheduleCommand, Result<ScheduleOptimizationResponse>>
{
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<OptimizeScheduleCommandHandler> _logger;

    public OptimizeScheduleCommandHandler(
        ISmartSchedulingService smartSchedulingService,
        ILogger<OptimizeScheduleCommandHandler> logger)
    {
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ScheduleOptimizationResponse>> Handle(OptimizeScheduleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Optimizing schedule for user {UserId}, schedule {ScheduleId}",
                request.UserId, request.ScheduleId);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                return Result<ScheduleOptimizationResponse>.Failure(validationResult.Error);
            }

            // Create the optimization request
            var optimizationRequest = new OptimizeScheduleRequest(
                request.UserId,
                request.ScheduleId,
                request.CurrentSchedule,
                request.Goals,
                request.Constraints
            );

            // Perform schedule optimization
            var optimizationResult = await _smartSchedulingService.OptimizeScheduleAsync(optimizationRequest, cancellationToken);

            _logger.LogInformation("Successfully optimized schedule for user {UserId} with {ChangeCount} changes",
                request.UserId, optimizationResult.Changes.Count);

            return Result<ScheduleOptimizationResponse>.Success(optimizationResult);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for schedule optimization for user {UserId}", request.UserId);
            return Result<ScheduleOptimizationResponse>.Failure($"Invalid request parameters: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during schedule optimization for user {UserId}", request.UserId);
            return Result<ScheduleOptimizationResponse>.Failure($"Cannot optimize schedule: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error optimizing schedule for user {UserId}", request.UserId);
            return Result<ScheduleOptimizationResponse>.Failure("An unexpected error occurred while optimizing the schedule");
        }
    }

    private static Result ValidateRequest(OptimizeScheduleCommand request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure("User ID is required");
        }

        if (request.ScheduleId == Guid.Empty)
        {
            return Result.Failure("Schedule ID is required");
        }

        if (request.CurrentSchedule == null || !request.CurrentSchedule.Any())
        {
            return Result.Failure("Current schedule is required and cannot be empty");
        }

        if (request.Goals == null)
        {
            return Result.Failure("Optimization goals are required");
        }

        if (request.CurrentSchedule.Count > 50)
        {
            return Result.Failure("Schedule optimization is limited to 50 items maximum");
        }

        // Validate that all scheduled items belong to the user
        if (request.CurrentSchedule.Any(item => item.TaskId != null && item.TaskId == Guid.Empty))
        {
            return Result.Failure("All scheduled items must have valid task IDs");
        }

        return Result.Success();
    }
}
