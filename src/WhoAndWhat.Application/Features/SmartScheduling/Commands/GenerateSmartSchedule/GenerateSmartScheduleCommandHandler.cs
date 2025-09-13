using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.GenerateSmartSchedule;

public class GenerateSmartScheduleCommandHandler : IRequestHandler<GenerateSmartScheduleCommand, Result<SmartScheduleResponse>>
{
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<GenerateSmartScheduleCommandHandler> _logger;

    public GenerateSmartScheduleCommandHandler(
        ISmartSchedulingService smartSchedulingService,
        ILogger<GenerateSmartScheduleCommandHandler> logger)
    {
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SmartScheduleResponse>> Handle(GenerateSmartScheduleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating smart schedule for user {UserId} from {StartDate} to {EndDate}",
                request.UserId, request.StartDate, request.EndDate);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                return Result<SmartScheduleResponse>.Failure(validationResult.Error);
            }

            // Create the scheduling request
            var schedulingRequest = new GenerateSmartScheduleRequest(
                request.UserId,
                request.StartDate,
                request.EndDate,
                request.TaskIds,
                request.Preferences,
                request.IncludeCalendarEvents,
                request.OptimizeForProductivity
            );

            // Generate the smart schedule
            var smartSchedule = await _smartSchedulingService.GenerateSmartScheduleAsync(schedulingRequest, cancellationToken);

            _logger.LogInformation("Successfully generated smart schedule for user {UserId} with {ItemCount} items",
                request.UserId, smartSchedule.ScheduledItems.Count);

            return Result<SmartScheduleResponse>.Success(smartSchedule);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for smart schedule generation for user {UserId}", request.UserId);
            return Result<SmartScheduleResponse>.Failure($"Invalid request parameters: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during smart schedule generation for user {UserId}", request.UserId);
            return Result<SmartScheduleResponse>.Failure($"Cannot generate schedule: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating smart schedule for user {UserId}", request.UserId);
            return Result<SmartScheduleResponse>.Failure("An unexpected error occurred while generating the smart schedule");
        }
    }

    private static Result ValidateRequest(GenerateSmartScheduleCommand request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure("User ID is required");
        }

        if (request.StartDate >= request.EndDate)
        {
            return Result.Failure("Start date must be before end date");
        }

        if (request.EndDate < DateTime.UtcNow.Date)
        {
            return Result.Failure("End date cannot be in the past");
        }

        var timespan = request.EndDate - request.StartDate;
        if (timespan > TimeSpan.FromDays(30))
        {
            return Result.Failure("Schedule generation is limited to 30 days maximum");
        }

        if (request.TaskIds?.Count > 100)
        {
            return Result.Failure("Maximum of 100 tasks can be scheduled at once");
        }

        return Result.Success();
    }
}
