using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskStatistics;

public class GetTaskStatisticsQueryHandler : IRequestHandler<GetTaskStatisticsQuery, Result<TaskStatisticsResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetTaskStatisticsQueryHandler> _logger;

    public GetTaskStatisticsQueryHandler(
        IAppTaskRepository taskRepository,
        ILogger<GetTaskStatisticsQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<TaskStatisticsResponse>> Handle(GetTaskStatisticsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var searchCriteria = new AppTaskSearchCriteria
            {
                UserId = request.UserId,
                CreatedFrom = request.From,
                CreatedTo = request.To,
                IncludeArchived = true
            };

            var statistics = await _taskRepository.GetStatisticsAsync(searchCriteria);

            // Get category-specific statistics
            var categoryStats = new List<CategoryStatistics>();
            foreach (var category in AppTaskCategory.GetAll())
            {
                var categorySearchCriteria = new AppTaskSearchCriteria
                {
                    UserId = request.UserId,
                    Categories = new List<int> { category.Value },
                    CreatedFrom = request.From,
                    CreatedTo = request.To,
                    IncludeArchived = true
                };

                var categoryStatistics = await _taskRepository.GetStatisticsAsync(categorySearchCriteria);
                categoryStats.Add(new CategoryStatistics
                {
                    Category = category.Value,
                    CategoryName = category.GetDisplayName(),
                    TotalTasks = categoryStatistics.TotalTasks,
                    CompletedTasks = categoryStatistics.CompletedTasks,
                    OverdueTasks = categoryStatistics.OverdueTasks,
                    CompletionPercentage = categoryStatistics.TotalTasks > 0
                        ? Math.Round((decimal)categoryStatistics.CompletedTasks / categoryStatistics.TotalTasks * 100, 2)
                        : 0,
                    AverageCompletionTime = categoryStatistics.AverageCompletionTime
                });
            }

            // Get priority-specific statistics
            var priorityStats = new List<PriorityStatistics>();
            foreach (var priority in Priority.GetAll())
            {
                var prioritySearchCriteria = new AppTaskSearchCriteria
                {
                    UserId = request.UserId,
                    Priorities = new List<int> { priority.Value },
                    CreatedFrom = request.From,
                    CreatedTo = request.To,
                    IncludeArchived = true
                };

                var priorityStatistics = await _taskRepository.GetStatisticsAsync(prioritySearchCriteria);
                priorityStats.Add(new PriorityStatistics
                {
                    Priority = priority.Value,
                    PriorityName = priority.Name,
                    TotalTasks = priorityStatistics.TotalTasks,
                    CompletedTasks = priorityStatistics.CompletedTasks,
                    OverdueTasks = priorityStatistics.OverdueTasks
                });
            }

            var response = new TaskStatisticsResponse
            {
                TotalTasks = statistics.TotalTasks,
                CompletedTasks = statistics.CompletedTasks,
                OverdueTasks = statistics.OverdueTasks,
                TasksDueToday = statistics.TasksDueToday,
                TasksDueThisWeek = statistics.TasksDueThisWeek,
                CategoryStats = categoryStats.ToArray(),
                PriorityStats = priorityStats.ToArray(),
                CompletionRate = statistics.TotalTasks > 0
                    ? Math.Round((decimal)statistics.CompletedTasks / statistics.TotalTasks * 100, 2)
                    : 0,
                AverageCompletionTime = statistics.AverageCompletionTime
            };

            return Result<TaskStatisticsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task statistics for user {UserId}", request.UserId);
            return Result<TaskStatisticsResponse>.Failure("An error occurred while retrieving task statistics");
        }
    }
}
