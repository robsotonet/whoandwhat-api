using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskStatistics;

public record GetTaskStatisticsQuery(
    Guid UserId,
    DateTime? From = null,
    DateTime? To = null
) : IRequest&lt;Result&lt;TaskStatisticsResponse&gt;&gt;;