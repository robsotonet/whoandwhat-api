using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Features.SmartScheduling.Queries.GetSchedulingSuggestions;

public record GetSchedulingSuggestionsQuery(
    Guid UserId,
    DateTime Date,
    List<Guid> TaskIds,
    int MaxSuggestions = 5
) : IRequest<Result<SchedulingSuggestionsResponse>>;