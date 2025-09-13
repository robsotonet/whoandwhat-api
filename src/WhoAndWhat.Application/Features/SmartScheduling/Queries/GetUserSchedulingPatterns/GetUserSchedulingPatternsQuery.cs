using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Features.SmartScheduling.Queries.GetUserSchedulingPatterns;

public record GetUserSchedulingPatternsQuery(
    Guid UserId,
    DateTime StartDate,
    DateTime EndDate
) : IRequest<Result<UserSchedulingPatternsResponse>>;