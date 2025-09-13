using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.UpdateSchedulingPreferences;

public class UpdateSchedulingPreferencesCommandHandler : IRequestHandler<UpdateSchedulingPreferencesCommand, Result<UpdateSchedulingPreferencesResponse>>
{
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<UpdateSchedulingPreferencesCommandHandler> _logger;

    public UpdateSchedulingPreferencesCommandHandler(
        ISmartSchedulingService smartSchedulingService,
        ILogger<UpdateSchedulingPreferencesCommandHandler> logger)
    {
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<UpdateSchedulingPreferencesResponse>> Handle(UpdateSchedulingPreferencesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating scheduling preferences for user {UserId}", request.UserId);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                return Result<UpdateSchedulingPreferencesResponse>.Failure(validationResult.Error);
            }

            // Create the preferences update request
            var updateRequest = new UpdateSchedulingPreferencesRequest(
                request.UserId,
                request.Preferences
            );

            // Update the preferences
            var updateResult = await _smartSchedulingService.UpdateUserSchedulingPreferencesAsync(updateRequest, cancellationToken);

            _logger.LogInformation("Successfully updated scheduling preferences for user {UserId}", request.UserId);

            return Result<UpdateSchedulingPreferencesResponse>.Success(updateResult);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for updating scheduling preferences for user {UserId}", request.UserId);
            return Result<UpdateSchedulingPreferencesResponse>.Failure($"Invalid request parameters: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during preference update for user {UserId}", request.UserId);
            return Result<UpdateSchedulingPreferencesResponse>.Failure($"Cannot update preferences: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating scheduling preferences for user {UserId}", request.UserId);
            return Result<UpdateSchedulingPreferencesResponse>.Failure("An unexpected error occurred while updating scheduling preferences");
        }
    }

    private static Result ValidateRequest(UpdateSchedulingPreferencesCommand request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure("User ID is required");
        }

        if (request.Preferences == null)
        {
            return Result.Failure("Preferences are required");
        }

        // Validate working hours
        var workingHours = request.Preferences.PreferredWorkingHours;
        if (workingHours.StartTime >= workingHours.EndTime)
        {
            return Result.Failure("Working hours start time must be before end time");
        }

        if (workingHours.EndTime - workingHours.StartTime > TimeSpan.FromHours(16))
        {
            return Result.Failure("Working hours cannot exceed 16 hours per day");
        }

        // Validate task durations
        if (request.Preferences.MinimumTaskDuration >= request.Preferences.MaximumTaskDuration)
        {
            return Result.Failure("Minimum task duration must be less than maximum task duration");
        }

        if (request.Preferences.MinimumTaskDuration < TimeSpan.FromMinutes(5))
        {
            return Result.Failure("Minimum task duration cannot be less than 5 minutes");
        }

        if (request.Preferences.MaximumTaskDuration > TimeSpan.FromHours(8))
        {
            return Result.Failure("Maximum task duration cannot exceed 8 hours");
        }

        // Validate buffer duration
        if (request.Preferences.BufferDuration > TimeSpan.FromHours(1))
        {
            return Result.Failure("Buffer duration cannot exceed 1 hour");
        }

        // Validate max tasks per time block
        if (request.Preferences.MaxTasksPerTimeBlock < 1 || request.Preferences.MaxTasksPerTimeBlock > 10)
        {
            return Result.Failure("Max tasks per time block must be between 1 and 10");
        }

        return Result.Success();
    }
}