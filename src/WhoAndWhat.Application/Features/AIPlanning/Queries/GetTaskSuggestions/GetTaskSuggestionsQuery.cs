using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskSuggestions;

public record GetTaskSuggestionsQuery(
    Guid UserId,
    string? ContextType,
    int MaxSuggestions,
    List<string> IncludeCategories
) : IRequest<Result<TaskSuggestionsResponse>>;

public record TaskSuggestionsResponse(
    List<TaskPrioritySuggestion> Suggestions,
    int TotalSuggestions,
    string SuggestionContext,
    DateTime GeneratedAt
);