using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.SmartScheduling.Queries.GetSchedulingSuggestions;

public class GetSchedulingSuggestionsQueryHandler : IRequestHandler<GetSchedulingSuggestionsQuery, Result<SchedulingSuggestionsResponse>>
{
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<GetSchedulingSuggestionsQueryHandler> _logger;

    public GetSchedulingSuggestionsQueryHandler(
        ISmartSchedulingService smartSchedulingService,
        ILogger<GetSchedulingSuggestionsQueryHandler> logger)
    {
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SchedulingSuggestionsResponse>> Handle(GetSchedulingSuggestionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting scheduling suggestions for user {UserId} on {Date}",
                request.UserId, request.Date);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                return Result<SchedulingSuggestionsResponse>.Failure(validationResult.Error);
            }

            // Create the suggestions request
            var suggestionsRequest = new GetSchedulingSuggestionsRequest(
                request.UserId,
                request.Date,
                request.TaskIds,
                request.MaxSuggestions
            );

            // Get scheduling suggestions
            var suggestions = await _smartSchedulingService.GetSchedulingSuggestionsAsync(suggestionsRequest, cancellationToken);

            _logger.LogInformation("Successfully retrieved {SuggestionCount} scheduling suggestions for user {UserId}",
                suggestions.Suggestions.Count, request.UserId);

            return Result<SchedulingSuggestionsResponse>.Success(suggestions);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for getting scheduling suggestions for user {UserId}", request.UserId);
            return Result<SchedulingSuggestionsResponse>.Failure($"Invalid request parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting scheduling suggestions for user {UserId}", request.UserId);
            return Result<SchedulingSuggestionsResponse>.Failure("An unexpected error occurred while getting scheduling suggestions");
        }
    }

    private static Result ValidateRequest(GetSchedulingSuggestionsQuery request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure("User ID is required");
        }

        if (request.Date.Date < DateTime.UtcNow.Date)
        {
            return Result.Failure("Cannot get suggestions for past dates");
        }

        if (request.Date.Date > DateTime.UtcNow.Date.AddDays(90))
        {
            return Result.Failure("Suggestions are limited to 90 days in the future");
        }

        if (request.MaxSuggestions < 1 || request.MaxSuggestions > 20)
        {
            return Result.Failure("Max suggestions must be between 1 and 20");
        }

        if (request.TaskIds?.Count > 20)
        {
            return Result.Failure("Maximum of 20 tasks can be processed for suggestions at once");
        }

        return Result.Success();
    }
}