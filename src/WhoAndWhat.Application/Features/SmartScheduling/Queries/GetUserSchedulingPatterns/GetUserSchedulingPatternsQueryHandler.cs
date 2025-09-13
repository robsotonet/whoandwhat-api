using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.SmartScheduling.Queries.GetUserSchedulingPatterns;

public class GetUserSchedulingPatternsQueryHandler : IRequestHandler<GetUserSchedulingPatternsQuery, Result<UserSchedulingPatternsResponse>>
{
    private readonly ISmartSchedulingService _smartSchedulingService;
    private readonly ILogger<GetUserSchedulingPatternsQueryHandler> _logger;

    public GetUserSchedulingPatternsQueryHandler(
        ISmartSchedulingService smartSchedulingService,
        ILogger<GetUserSchedulingPatternsQueryHandler> logger)
    {
        _smartSchedulingService = smartSchedulingService ?? throw new ArgumentNullException(nameof(smartSchedulingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<UserSchedulingPatternsResponse>> Handle(GetUserSchedulingPatternsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting scheduling patterns for user {UserId} from {StartDate} to {EndDate}",
                request.UserId, request.StartDate, request.EndDate);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                return Result<UserSchedulingPatternsResponse>.Failure(validationResult.Error);
            }

            // Create the patterns request
            var patternsRequest = new GetUserSchedulingPatternsRequest(
                request.UserId,
                request.StartDate,
                request.EndDate
            );

            // Get user scheduling patterns
            var patterns = await _smartSchedulingService.AnalyzeUserSchedulingPatternsAsync(patternsRequest, cancellationToken);

            _logger.LogInformation("Successfully retrieved scheduling patterns for user {UserId}",
                request.UserId);

            return Result<UserSchedulingPatternsResponse>.Success(patterns);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for getting scheduling patterns for user {UserId}", request.UserId);
            return Result<UserSchedulingPatternsResponse>.Failure($"Invalid request parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting scheduling patterns for user {UserId}", request.UserId);
            return Result<UserSchedulingPatternsResponse>.Failure("An unexpected error occurred while getting scheduling patterns");
        }
    }

    private static Result ValidateRequest(GetUserSchedulingPatternsQuery request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure("User ID is required");
        }

        if (request.StartDate >= request.EndDate)
        {
            return Result.Failure("Start date must be before end date");
        }

        if (request.EndDate > DateTime.UtcNow)
        {
            return Result.Failure("End date cannot be in the future");
        }

        var timespan = request.EndDate - request.StartDate;
        if (timespan > TimeSpan.FromDays(180))
        {
            return Result.Failure("Pattern analysis is limited to 180 days maximum");
        }

        if (timespan < TimeSpan.FromDays(7))
        {
            return Result.Failure("Pattern analysis requires at least 7 days of data");
        }

        return Result.Success();
    }
}
