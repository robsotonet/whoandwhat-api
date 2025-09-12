using MediatR;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.ExportDashboardData;

/// <summary>
/// Handler for exporting dashboard data in various formats (CSV, JSON, Excel)
/// </summary>
public sealed class ExportDashboardDataQueryHandler 
    : IRequestHandler<ExportDashboardDataQuery, Result<ExportDashboardDataResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ExportDashboardDataQueryHandler> _logger;

    public ExportDashboardDataQueryHandler(
        IAppTaskRepository taskRepository,
        IUserRepository userRepository,
        ILogger<ExportDashboardDataQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<ExportDashboardDataResponse>> Handle(
        ExportDashboardDataQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Exporting dashboard data for user {UserId} in format {Format}", 
                request.UserId, request.Format);

            // Verify user exists
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result<ExportDashboardDataResponse>.Failure("User not found");
            }

            // Validate export format
            var supportedFormats = new[] { "csv", "json", "excel" };
            if (!supportedFormats.Contains(request.Format.ToLowerInvariant()))
            {
                return Result<ExportDashboardDataResponse>.Failure(
                    $"Unsupported format '{request.Format}'. Supported formats: {string.Join(", ", supportedFormats)}");
            }

            // Gather dashboard data based on options
            var dashboardData = await GatherDashboardData(request.UserId, request.Options, cancellationToken);

            // Generate export file based on format
            var exportResult = request.Format.ToLowerInvariant() switch
            {
                "csv" => await GenerateCsvExport(dashboardData, request.Options),
                "json" => await GenerateJsonExport(dashboardData, request.Options),
                "excel" => await GenerateExcelExport(dashboardData, request.Options),
                _ => throw new InvalidOperationException($"Unsupported export format: {request.Format}")
            };

            var metadata = new ExportMetadata(
                ExportedAt: DateTime.UtcNow,
                ExportedBy: user.Username ?? user.Email,
                Options: request.Options,
                RecordCounts: dashboardData.RecordCounts,
                FileSizeBytes: exportResult.FileContent.Length,
                ChecksumHash: GenerateChecksum(exportResult.FileContent)
            );

            var response = new ExportDashboardDataResponse(
                FileContent: exportResult.FileContent,
                FileName: exportResult.FileName,
                ContentType: exportResult.ContentType,
                RecordCount: dashboardData.RecordCounts.Values.Sum(),
                Metadata: metadata
            );

            _logger.LogInformation("Successfully exported {RecordCount} records for user {UserId}", 
                response.RecordCount, request.UserId);

            return Result<ExportDashboardDataResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting dashboard data for user {UserId}", request.UserId);
            return Result<ExportDashboardDataResponse>.Failure($"Failed to export dashboard data: {ex.Message}");
        }
    }

    private async Task<DashboardExportData> GatherDashboardData(
        Guid userId, 
        ExportOptionsDto options, 
        CancellationToken cancellationToken)
    {
        var data = new DashboardExportData();
        var dataTypes = options.DataTypes ?? new List<string> { "tasks", "metrics", "streaks", "analytics" };

        // Gather tasks if requested
        if (dataTypes.Contains("tasks"))
        {
            var taskFilter = CreateTaskFilter(userId, options);
            var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, taskFilter, cancellationToken);
            data.Tasks = tasks.ToList();
            data.RecordCounts["tasks"] = data.Tasks.Count;
        }

        // Calculate metrics if requested
        if (dataTypes.Contains("metrics"))
        {
            data.Metrics = await CalculateDashboardMetrics(userId, options, cancellationToken);
            data.RecordCounts["metrics"] = 1; // Single metrics object
        }

        // Calculate streaks if requested
        if (dataTypes.Contains("streaks"))
        {
            data.Streaks = await CalculateProductivityStreaks(userId, options, cancellationToken);
            data.RecordCounts["streaks"] = data.Streaks.Count;
        }

        // Calculate analytics if requested
        if (dataTypes.Contains("analytics"))
        {
            data.Analytics = await CalculateAnalytics(userId, options, cancellationToken);
            data.RecordCounts["analytics"] = data.Analytics.Count;
        }

        return data;
    }

    private TaskFilter CreateTaskFilter(Guid userId, ExportOptionsDto options)
    {
        var filter = new TaskFilter
        {
            CreatedAfter = options.StartDate,
            CreatedBefore = options.EndDate,
            IncludeDeleted = options.IncludeDeleted
        };
        
        if (options.IncludeCategories?.Any() == true)
        {
            // Convert string category names to AppTaskCategory value objects
            var categories = options.IncludeCategories
                .Select(name => AppTaskCategory.TryFromName(name, out var category) ? category : null)
                .Where(category => category != null)
                .Cast<AppTaskCategory>()
                .ToList();
            filter.Categories = categories;
        }
        
        filter.PageSize = 10000; // Get all matching tasks
        return filter;
    }

    private async Task<DashboardMetricsExport> CalculateDashboardMetrics(
        Guid userId, 
        ExportOptionsDto options, 
        CancellationToken cancellationToken)
    {
        var filter = CreateTaskFilter(userId, options);
        var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, filter, cancellationToken);

        var totalTasks = tasks.Count();
        var completedTasks = tasks.Count(t => t.Status == (int)AppTaskStatus.Completed);
        var overdueTasks = tasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.UtcNow && t.Status != (int)AppTaskStatus.Completed);
        
        var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;
        var overdueRate = totalTasks > 0 ? (double)overdueTasks / totalTasks * 100 : 0;

        var categoryBreakdown = tasks
            .GroupBy(t => ((AppTaskCategory)t.Category).ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var priorityBreakdown = tasks
            .GroupBy(t => ((Priority)t.Priority).ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return new DashboardMetricsExport(
            TotalTasks: totalTasks,
            CompletedTasks: completedTasks,
            PendingTasks: totalTasks - completedTasks,
            OverdueTasks: overdueTasks,
            CompletionRate: Math.Round(completionRate, 2),
            OverdueRate: Math.Round(overdueRate, 2),
            CategoryBreakdown: categoryBreakdown,
            PriorityBreakdown: priorityBreakdown,
            CalculatedAt: DateTime.UtcNow
        );
    }

    private async Task<List<ProductivityStreakExport>> CalculateProductivityStreaks(
        Guid userId, 
        ExportOptionsDto options, 
        CancellationToken cancellationToken)
    {
        var filter = CreateTaskFilter(userId, options);
        var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, filter, cancellationToken);

        var completedTasks = tasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed)
            .OrderBy(t => t.UpdatedAt.Date)
            .ToList();

        var streaks = new List<ProductivityStreakExport>();
        var currentStreak = 0;
        var longestStreak = 0;
        var currentStreakStart = DateTime.Today;
        var lastCompletionDate = DateTime.MinValue;

        var dailyCompletions = completedTasks
            .GroupBy(t => t.UpdatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var date in dailyCompletions.Keys.OrderBy(d => d))
        {
            if (lastCompletionDate == DateTime.MinValue || date == lastCompletionDate.AddDays(1))
            {
                if (currentStreak == 0)
                {
                    currentStreakStart = date;
                }
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                if (currentStreak > 0)
                {
                    streaks.Add(new ProductivityStreakExport(
                        StartDate: currentStreakStart,
                        EndDate: lastCompletionDate,
                        Duration: currentStreak,
                        TasksCompleted: completedTasks.Count(t => t.UpdatedAt.Date >= currentStreakStart && 
                                                                t.UpdatedAt.Date <= lastCompletionDate)
                    ));
                }
                currentStreak = 1;
                currentStreakStart = date;
            }
            lastCompletionDate = date;
        }

        // Add final streak if exists
        if (currentStreak > 0)
        {
            streaks.Add(new ProductivityStreakExport(
                StartDate: currentStreakStart,
                EndDate: lastCompletionDate,
                Duration: currentStreak,
                TasksCompleted: completedTasks.Count(t => t.UpdatedAt.Date >= currentStreakStart && 
                                                        t.UpdatedAt.Date <= lastCompletionDate)
            ));
        }

        return streaks;
    }

    private async Task<List<AnalyticsExport>> CalculateAnalytics(
        Guid userId, 
        ExportOptionsDto options, 
        CancellationToken cancellationToken)
    {
        var filter = CreateTaskFilter(userId, options);
        var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, filter, cancellationToken);

        var analytics = new List<AnalyticsExport>();

        // Daily productivity analytics
        var dailyStats = tasks
            .Where(t => t.CreatedAt >= (options.StartDate ?? DateTime.Today.AddMonths(-1)))
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new AnalyticsExport(
                Date: g.Key,
                Metric: "DailyProductivity",
                Value: g.Count(),
                Details: new Dictionary<string, object>
                {
                    ["tasksCreated"] = g.Count(),
                    ["tasksCompleted"] = g.Count(t => t.Status == (int)AppTaskStatus.Completed),
                    ["completionRate"] = g.Any() ? (double)g.Count(t => t.Status == (int)AppTaskStatus.Completed) / g.Count() * 100 : 0
                }
            ))
            .ToList();

        analytics.AddRange(dailyStats);

        // Weekly trends
        var weeklyStats = tasks
            .Where(t => t.CreatedAt >= (options.StartDate ?? DateTime.Today.AddMonths(-3)))
            .GroupBy(t => GetWeekNumber(t.CreatedAt))
            .Select(g => new AnalyticsExport(
                Date: g.First().CreatedAt.Date,
                Metric: "WeeklyTrend",
                Value: g.Count(),
                Details: new Dictionary<string, object>
                {
                    ["weekNumber"] = g.Key,
                    ["tasksCreated"] = g.Count(),
                    ["averagePriority"] = g.Average(t => (int)t.Priority)
                }
            ))
            .ToList();

        analytics.AddRange(weeklyStats);

        return analytics;
    }

    private async Task<ExportFile> GenerateCsvExport(DashboardExportData data, ExportOptionsDto options)
    {
        var csv = new StringBuilder();

        // CSV Headers
        csv.AppendLine("Type,Date,Title,Category,Priority,Status,DueDate,CompletedAt,Description");

        // Export tasks
        foreach (var task in data.Tasks ?? new List<AppTask>())
        {
            csv.AppendLine($"Task,{task.CreatedAt:yyyy-MM-dd},{EscapeCsv(task.Title)},{(AppTaskCategory)task.Category},{(Priority)task.Priority},{(AppTaskStatus)task.Status},{task.DueDate:yyyy-MM-dd},{task.UpdatedAt:yyyy-MM-dd},{EscapeCsv(task.Description ?? "")}");
        }

        // Export metrics as summary rows
        if (data.Metrics != null)
        {
            csv.AppendLine($"Metrics,{data.Metrics.CalculatedAt:yyyy-MM-dd},Dashboard Summary,Summary,Info,Calculated,,{data.Metrics.CalculatedAt:yyyy-MM-dd},Total: {data.Metrics.TotalTasks}, Completed: {data.Metrics.CompletedTasks}, Rate: {data.Metrics.CompletionRate}%");
        }

        var fileContent = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"dashboard_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return new ExportFile(fileContent, fileName, "text/csv");
    }

    private async Task<ExportFile> GenerateJsonExport(DashboardExportData data, ExportOptionsDto options)
    {
        var exportData = new
        {
            ExportInfo = new
            {
                ExportedAt = DateTime.UtcNow,
                Options = options,
                RecordCounts = data.RecordCounts
            },
            Tasks = data.Tasks?.Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                Category = ((AppTaskCategory)t.Category).ToString(),
                Priority = ((Priority)t.Priority).ToString(),
                Status = ((AppTaskStatus)t.Status).ToString(),
                t.CreatedAt,
                t.DueDate,
                CompletedAt = t.Status == (int)AppTaskStatus.Completed ? t.UpdatedAt : (DateTime?)null
            }) ?? Enumerable.Empty<object>(),
            Metrics = data.Metrics,
            Streaks = data.Streaks ?? new List<ProductivityStreakExport>(),
            Analytics = data.Analytics ?? new List<AnalyticsExport>()
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(exportData, jsonOptions);
        var fileContent = Encoding.UTF8.GetBytes(json);
        var fileName = $"dashboard_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

        return new ExportFile(fileContent, fileName, "application/json");
    }

    private async Task<ExportFile> GenerateExcelExport(DashboardExportData data, ExportOptionsDto options)
    {
        // For now, generate a CSV-like format as Excel implementation would require additional libraries
        // In a real implementation, you would use libraries like EPPlus or ClosedXML
        var csvResult = await GenerateCsvExport(data, options);
        
        var fileName = $"dashboard_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return new ExportFile(csvResult.FileContent, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }

    private static string GenerateChecksum(byte[] content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToHexString(hash)[..16]; // First 16 characters
    }

    private static int GetWeekNumber(DateTime date)
    {
        var jan1 = new DateTime(date.Year, 1, 1);
        var daysOffset = (int)jan1.DayOfWeek;
        var firstWeekDay = jan1.AddDays(-daysOffset + 1);
        var weekNumber = (date - firstWeekDay).Days / 7 + 1;
        return weekNumber;
    }
}

// Supporting data models
internal sealed record DashboardExportData
{
    public List<AppTask> Tasks { get; set; } = new();
    public DashboardMetricsExport? Metrics { get; set; }
    public List<ProductivityStreakExport> Streaks { get; set; } = new();
    public List<AnalyticsExport> Analytics { get; set; } = new();
    public Dictionary<string, int> RecordCounts { get; set; } = new();
}

internal sealed record DashboardMetricsExport(
    int TotalTasks,
    int CompletedTasks,
    int PendingTasks,
    int OverdueTasks,
    double CompletionRate,
    double OverdueRate,
    Dictionary<string, int> CategoryBreakdown,
    Dictionary<string, int> PriorityBreakdown,
    DateTime CalculatedAt
);

internal sealed record ProductivityStreakExport(
    DateTime StartDate,
    DateTime EndDate,
    int Duration,
    int TasksCompleted
);

internal sealed record AnalyticsExport(
    DateTime Date,
    string Metric,
    double Value,
    Dictionary<string, object> Details
);

internal sealed record ExportFile(
    byte[] FileContent,
    string FileName,
    string ContentType
);

