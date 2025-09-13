using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Commands.PrioritizeTasks;

public record PrioritizeTasksCommand(
    Guid UserId,
    List<TaskAnalysisContext> TaskAnalysisContexts,
    PriorityAnalysisContext PriorityContext,
    int MaxPrioritySuggestions
) : IRequest<Result<TaskPrioritizationResponse>>;

public record TaskPrioritizationResponse(
    List<TaskPrioritySuggestion> PrioritySuggestions,
    PriorityAnalysisContext AnalysisContext,
    double OverallConfidence,
    List<string> AnalysisNotes,
    DateTime GeneratedAt
);