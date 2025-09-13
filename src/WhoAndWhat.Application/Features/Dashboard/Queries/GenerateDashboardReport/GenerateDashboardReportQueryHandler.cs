using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GenerateDashboardReport;

/// <summary>
/// Handler for generating comprehensive dashboard reports with analytics and insights
/// </summary>
public sealed class GenerateDashboardReportQueryHandler
    : IRequestHandler<GenerateDashboardReportQuery, Result<GenerateDashboardReportResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GenerateDashboardReportQueryHandler> _logger;

    public GenerateDashboardReportQueryHandler(
        IAppTaskRepository taskRepository,
        IUserRepository userRepository,
        ILogger<GenerateDashboardReportQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<GenerateDashboardReportResponse>> Handle(
        GenerateDashboardReportQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating dashboard report for user {UserId}, type: {ReportType}",
                request.UserId, request.ReportType);

            // Verify user exists
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result<GenerateDashboardReportResponse>.Failure("User not found");
            }

            // Validate report type
            var supportedTypes = new[] { "summary", "detailed", "analytical" };
            if (!supportedTypes.Contains(request.ReportType.ToLowerInvariant()))
            {
                return Result<GenerateDashboardReportResponse>.Failure(
                    $"Unsupported report type '{request.ReportType}'. Supported types: {string.Join(", ", supportedTypes)}");
            }

            // Validate format
            var supportedFormats = new[] { "pdf", "html", "markdown" };
            if (!supportedFormats.Contains(request.Options.Format.ToLowerInvariant()))
            {
                return Result<GenerateDashboardReportResponse>.Failure(
                    $"Unsupported format '{request.Options.Format}'. Supported formats: {string.Join(", ", supportedFormats)}");
            }

            // Generate report data
            var reportData = await GenerateReportData(request.UserId, request.ReportType, request.Options, cancellationToken);

            // Generate report file
            var reportResult = request.Options.Format.ToLowerInvariant() switch
            {
                "html" => GenerateHtmlReport(reportData, request.ReportType, request.Options),
                "markdown" => GenerateMarkdownReport(reportData, request.ReportType, request.Options),
                "pdf" => GeneratePdfReport(reportData, request.ReportType, request.Options),
                _ => throw new InvalidOperationException($"Unsupported report format: {request.Options.Format}")
            };

            var metadata = new ReportMetadata(
                GeneratedAt: DateTime.UtcNow,
                GeneratedBy: user.Username ?? user.Email,
                ReportType: request.ReportType,
                Options: request.Options,
                Summary: reportData.Summary,
                FileSizeBytes: reportResult.ReportContent.Length,
                ChecksumHash: GenerateChecksum(reportResult.ReportContent)
            );

            var response = new GenerateDashboardReportResponse(
                ReportContent: reportResult.ReportContent,
                ReportFileName: reportResult.ReportFileName,
                ContentType: reportResult.ContentType,
                Metadata: metadata
            );

            _logger.LogInformation("Successfully generated {ReportType} report for user {UserId}, size: {Size} bytes",
                request.ReportType, request.UserId, response.ReportContent.Length);

            return Result<GenerateDashboardReportResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard report for user {UserId}", request.UserId);
            return Result<GenerateDashboardReportResponse>.Failure($"Failed to generate dashboard report: {ex.Message}");
        }
    }

    private async Task<ReportData> GenerateReportData(
        Guid userId,
        string reportType,
        ReportOptionsDto options,
        CancellationToken cancellationToken)
    {
        var startDate = options.StartDate ?? DateTime.Today.AddMonths(-1);
        var endDate = options.EndDate ?? DateTime.Today;

        // Get tasks for the period
        var filter = new TaskFilter
        {
            CreatedAfter = startDate,
            CreatedBefore = endDate,
            PageSize = 10000
        };
        var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, filter, cancellationToken);
        var filteredTasks = tasks.ToList();

        // Calculate basic metrics
        var totalTasks = filteredTasks.Count;
        var completedTasks = filteredTasks.Count(t => t.Status == (int)AppTaskStatus.Completed);
        var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

        // Calculate productivity streak
        var productivityStreak = await CalculateCurrentStreak(userId, cancellationToken);

        // Generate insights based on report type
        var insights = reportType.ToLowerInvariant() switch
        {
            "analytical" => GenerateAnalyticalInsights(filteredTasks, options),
            "detailed" => GenerateDetailedInsights(filteredTasks, options),
            _ => GenerateSummaryInsights(filteredTasks, options)
        };

        // Generate recommendations
        var recommendations = options.IncludeRecommendations
            ? GenerateRecommendations(filteredTasks, insights, options)
            : new List<ReportRecommendation>();

        var summary = new ReportSummary(
            TotalTasks: totalTasks,
            CompletedTasks: completedTasks,
            CompletionRate: Math.Round(completionRate, 2),
            ProductivityStreak: productivityStreak,
            InsightsGenerated: insights.Count,
            RecommendationsProvided: recommendations.Count,
            PeriodStart: startDate,
            PeriodEnd: endDate
        );

        return new ReportData(
            Tasks: filteredTasks,
            Summary: summary,
            Insights: insights,
            Recommendations: recommendations,
            Charts: options.IncludeCharts ? GenerateChartData(filteredTasks, options) : new List<ChartData>()
        );
    }

    private List<ReportInsight> GenerateSummaryInsights(List<AppTask> tasks, ReportOptionsDto options)
    {
        var insights = new List<ReportInsight>();

        // Completion trend insight
        var weeklyCompletions = tasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed)
            .GroupBy(t => GetWeekNumber(t.UpdatedAt))
            .Select(g => new { Week = g.Key, Count = g.Count() })
            .OrderBy(x => x.Week)
            .ToList();

        if (weeklyCompletions.Count >= 2)
        {
            var trend = weeklyCompletions.Last().Count - weeklyCompletions.First().Count;
            var trendText = trend > 0 ? "improving" : trend < 0 ? "declining" : "stable";

            insights.Add(new ReportInsight(
                Title: "Completion Trend",
                Description: $"Your task completion rate is {trendText} with {Math.Abs(trend)} tasks difference compared to the start of the period.",
                Type: "trend",
                Impact: trend > 0 ? "positive" : trend < 0 ? "negative" : "neutral",
                Data: new Dictionary<string, object> { ["trend"] = trend, ["weeklyData"] = weeklyCompletions }
            ));
        }

        // Category distribution insight
        var categoryStats = tasks
            .GroupBy(t => t.Category.ToString())
            .Select(g => new { Category = g.Key, Count = g.Count(), Completed = g.Count(t => t.Status == (int)AppTaskStatus.Completed) })
            .OrderByDescending(x => x.Count)
            .ToList();

        if (categoryStats.Any())
        {
            var topCategory = categoryStats.First();
            insights.Add(new ReportInsight(
                Title: "Most Active Category",
                Description: $"You're most active in {topCategory.Category} with {topCategory.Count} tasks ({topCategory.Completed} completed).",
                Type: "category",
                Impact: "informational",
                Data: new Dictionary<string, object> { ["categoryStats"] = categoryStats }
            ));
        }

        return insights;
    }

    private List<ReportInsight> GenerateDetailedInsights(List<AppTask> tasks, ReportOptionsDto options)
    {
        var insights = GenerateSummaryInsights(tasks, options);

        // Add priority analysis
        var priorityStats = tasks
            .GroupBy(t => t.Priority.ToString())
            .Select(g => new
            {
                Priority = g.Key,
                Count = g.Count(),
                Completed = g.Count(t => t.Status == (int)AppTaskStatus.Completed),
                AvgCompletionTime = g.Where(t => t.CreatedAt != default)
                    .Select(t => (t.UpdatedAt - t.CreatedAt).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average()
            })
            .ToList();

        insights.Add(new ReportInsight(
            Title: "Priority Management",
            Description: $"Analysis of how you handle different priority levels across {priorityStats.Sum(p => p.Count)} tasks.",
            Type: "priority",
            Impact: "informational",
            Data: new Dictionary<string, object> { ["priorityStats"] = priorityStats }
        ));

        // Add overdue analysis
        var overdueTasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate < DateTime.Now && t.Status != (int)AppTaskStatus.Completed).ToList();
        if (overdueTasks.Any())
        {
            insights.Add(new ReportInsight(
                Title: "Overdue Tasks",
                Description: $"You have {overdueTasks.Count} overdue tasks that need attention.",
                Type: "warning",
                Impact: "negative",
                Data: new Dictionary<string, object>
                {
                    ["overdueCount"] = overdueTasks.Count,
                    ["categories"] = overdueTasks.GroupBy(t => t.Category.ToString()).Select(g => new { Category = g.Key, Count = g.Count() })
                }
            ));
        }

        return insights;
    }

    private List<ReportInsight> GenerateAnalyticalInsights(List<AppTask> tasks, ReportOptionsDto options)
    {
        var insights = GenerateDetailedInsights(tasks, options);

        // Add time-based productivity analysis
        var hourlyStats = tasks
            .Where(t => t.CreatedAt != default)
            .GroupBy(t => t.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(x => x.Hour)
            .ToList();

        var peakHour = hourlyStats.OrderByDescending(h => h.Count).FirstOrDefault();
        if (peakHour != null)
        {
            insights.Add(new ReportInsight(
                Title: "Peak Productivity Hour",
                Description: $"Your most productive hour is {peakHour.Hour}:00 with {peakHour.Count} tasks created.",
                Type: "productivity",
                Impact: "positive",
                Data: new Dictionary<string, object> { ["hourlyStats"] = hourlyStats, ["peakHour"] = peakHour.Hour }
            ));
        }

        // Add completion velocity analysis
        var completionVelocity = tasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed && t.CreatedAt != default)
            .Select(t => (t.UpdatedAt - t.CreatedAt).TotalHours)
            .ToList();

        if (completionVelocity.Any())
        {
            var avgVelocity = completionVelocity.Average();
            insights.Add(new ReportInsight(
                Title: "Task Completion Velocity",
                Description: $"On average, you complete tasks in {avgVelocity:F1} hours after creation.",
                Type: "velocity",
                Impact: "informational",
                Data: new Dictionary<string, object>
                {
                    ["averageHours"] = avgVelocity,
                    ["velocityDistribution"] = completionVelocity.GroupBy(v => Math.Floor(v / 24)).Select(g => new { Days = g.Key, Count = g.Count() })
                }
            ));
        }

        return insights;
    }

    private List<ReportRecommendation> GenerateRecommendations(
        List<AppTask> tasks,
        List<ReportInsight> insights,
        ReportOptionsDto options)
    {
        var recommendations = new List<ReportRecommendation>();

        // Completion rate recommendation
        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t => t.Status == (int)AppTaskStatus.Completed);
        var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

        if (completionRate < 70)
        {
            recommendations.Add(new ReportRecommendation(
                Title: "Improve Task Completion Rate",
                Description: $"Your current completion rate is {completionRate:F1}%. Consider breaking large tasks into smaller, manageable pieces.",
                Priority: "high",
                Category: "productivity",
                ActionItems: new List<string>
                {
                    "Review pending tasks and identify blockers",
                    "Break down complex tasks into subtasks",
                    "Set realistic deadlines for tasks",
                    "Use time-blocking techniques for focused work"
                }
            ));
        }

        // Overdue tasks recommendation
        var overdueTasks = tasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.Now && t.Status != (int)AppTaskStatus.Completed);
        if (overdueTasks > 0)
        {
            recommendations.Add(new ReportRecommendation(
                Title: "Address Overdue Tasks",
                Description: $"You have {overdueTasks} overdue tasks. Addressing these should be your immediate priority.",
                Priority: "high",
                Category: "time-management",
                ActionItems: new List<string>
                {
                    "Review all overdue tasks immediately",
                    "Reschedule or reassess unrealistic deadlines",
                    "Complete quick tasks (< 15 minutes) right away",
                    "Consider delegating or removing non-essential tasks"
                }
            ));
        }

        // Category balance recommendation
        var categoryStats = tasks.GroupBy(t => t.Category.ToString()).Select(g => new { Category = g.Key, Count = g.Count() }).ToList();
        var maxCategory = categoryStats.OrderByDescending(c => c.Count).FirstOrDefault();
        var totalCategoryTasks = categoryStats.Sum(c => c.Count);

        if (maxCategory != null && maxCategory.Count > totalCategoryTasks * 0.7)
        {
            recommendations.Add(new ReportRecommendation(
                Title: "Balance Task Categories",
                Description: $"Most of your tasks ({maxCategory.Count}/{totalCategoryTasks}) are in {maxCategory.Category}. Consider diversifying your task types.",
                Priority: "medium",
                Category: "balance",
                ActionItems: new List<string>
                {
                    "Review if some tasks can be categorized differently",
                    "Consider dedicating time to other important areas",
                    "Set goals for maintaining category balance",
                    "Use time allocation strategies across different task types"
                }
            ));
        }

        return recommendations;
    }

    private List<ChartData> GenerateChartData(List<AppTask> tasks, ReportOptionsDto options)
    {
        var charts = new List<ChartData>();

        // Task completion trend chart
        var weeklyCompletions = tasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed)
            .GroupBy(t => GetWeekNumber(t.UpdatedAt))
            .Select(g => new { Week = g.Key, Count = g.Count() })
            .OrderBy(x => x.Week)
            .ToList();

        charts.Add(new ChartData(
            Title: "Weekly Task Completions",
            Type: "line",
            Data: weeklyCompletions.Select(w => new Dictionary<string, object> { ["week"] = w.Week, ["completions"] = w.Count }).ToList()
        ));

        // Category distribution chart
        var categoryData = tasks
            .GroupBy(t => t.Category.ToString())
            .Select(g => new Dictionary<string, object> { ["category"] = g.Key, ["count"] = g.Count() })
            .ToList();

        charts.Add(new ChartData(
            Title: "Task Distribution by Category",
            Type: "pie",
            Data: categoryData
        ));

        return charts;
    }

    private async Task<int> CalculateCurrentStreak(Guid userId, CancellationToken cancellationToken)
    {
        var (tasks, _) = await _taskRepository.GetTasksByUserIdAsync(userId, new TaskFilter { PageSize = 10000 }, cancellationToken);
        var completedTasks = tasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed)
            .OrderByDescending(t => t.UpdatedAt.Date)
            .ToList();

        var currentStreak = 0;
        var currentDate = DateTime.Today;

        foreach (var task in completedTasks)
        {
            var taskDate = task.UpdatedAt.Date;

            if (taskDate == currentDate || (currentStreak == 0 && taskDate < currentDate))
            {
                currentStreak++;
                currentDate = taskDate.AddDays(-1);
            }
            else if (taskDate == currentDate)
            {
                // Continue streak for the same day
                continue;
            }
            else
            {
                break;
            }
        }

        return currentStreak;
    }

    private ReportFile GenerateHtmlReport(ReportData data, string reportType, ReportOptionsDto options)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Dashboard Report</title>");
        html.AppendLine("<style>body{font-family:Arial,sans-serif;margin:40px;} .metric{background:#f5f5f5;padding:20px;margin:10px 0;border-radius:5px;} .insight{border-left:4px solid #007bff;padding-left:15px;margin:15px 0;} .recommendation{background:#e8f5e9;padding:15px;border-radius:5px;margin:10px 0;}</style>");
        html.AppendLine("</head><body>");

        html.AppendLine($"<h1>Dashboard Report - {reportType.ToUpperInvariant()}</h1>");
        html.AppendLine($"<p>Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");
        html.AppendLine($"<p>Period: {data.Summary.PeriodStart:yyyy-MM-dd} to {data.Summary.PeriodEnd:yyyy-MM-dd}</p>");

        // Summary section
        html.AppendLine("<h2>Summary</h2>");
        html.AppendLine("<div class='metric'>");
        html.AppendLine($"<h3>Key Metrics</h3>");
        html.AppendLine($"<p><strong>Total Tasks:</strong> {data.Summary.TotalTasks}</p>");
        html.AppendLine($"<p><strong>Completed Tasks:</strong> {data.Summary.CompletedTasks}</p>");
        html.AppendLine($"<p><strong>Completion Rate:</strong> {data.Summary.CompletionRate}%</p>");
        html.AppendLine($"<p><strong>Current Streak:</strong> {data.Summary.ProductivityStreak} days</p>");
        html.AppendLine("</div>");

        // Insights section
        if (data.Insights.Any())
        {
            html.AppendLine("<h2>Insights</h2>");
            foreach (var insight in data.Insights)
            {
                html.AppendLine("<div class='insight'>");
                html.AppendLine($"<h4>{insight.Title}</h4>");
                html.AppendLine($"<p>{insight.Description}</p>");
                html.AppendLine("</div>");
            }
        }

        // Recommendations section
        if (data.Recommendations.Any())
        {
            html.AppendLine("<h2>Recommendations</h2>");
            foreach (var rec in data.Recommendations)
            {
                html.AppendLine("<div class='recommendation'>");
                html.AppendLine($"<h4>{rec.Title} <span style='color:{GetPriorityColor(rec.Priority)}'>[{rec.Priority.ToUpper()}]</span></h4>");
                html.AppendLine($"<p>{rec.Description}</p>");
                if (rec.ActionItems.Any())
                {
                    html.AppendLine("<ul>");
                    foreach (var action in rec.ActionItems)
                    {
                        html.AppendLine($"<li>{action}</li>");
                    }
                    html.AppendLine("</ul>");
                }
                html.AppendLine("</div>");
            }
        }

        html.AppendLine("</body></html>");

        var content = Encoding.UTF8.GetBytes(html.ToString());
        var fileName = $"dashboard_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";

        return new ReportFile(content, fileName, "text/html");
    }

    private ReportFile GenerateMarkdownReport(ReportData data, string reportType, ReportOptionsDto options)
    {
        var md = new StringBuilder();
        md.AppendLine($"# Dashboard Report - {reportType.ToUpperInvariant()}");
        md.AppendLine();
        md.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        md.AppendLine($"**Period:** {data.Summary.PeriodStart:yyyy-MM-dd} to {data.Summary.PeriodEnd:yyyy-MM-dd}");
        md.AppendLine();

        // Summary section
        md.AppendLine("## Summary");
        md.AppendLine();
        md.AppendLine("| Metric | Value |");
        md.AppendLine("|--------|-------|");
        md.AppendLine($"| Total Tasks | {data.Summary.TotalTasks} |");
        md.AppendLine($"| Completed Tasks | {data.Summary.CompletedTasks} |");
        md.AppendLine($"| Completion Rate | {data.Summary.CompletionRate}% |");
        md.AppendLine($"| Current Streak | {data.Summary.ProductivityStreak} days |");
        md.AppendLine();

        // Insights section
        if (data.Insights.Any())
        {
            md.AppendLine("## Insights");
            md.AppendLine();
            foreach (var insight in data.Insights)
            {
                md.AppendLine($"### {insight.Title}");
                md.AppendLine();
                md.AppendLine(insight.Description);
                md.AppendLine();
            }
        }

        // Recommendations section
        if (data.Recommendations.Any())
        {
            md.AppendLine("## Recommendations");
            md.AppendLine();
            foreach (var rec in data.Recommendations)
            {
                md.AppendLine($"### {rec.Title} [{rec.Priority.ToUpper()}]");
                md.AppendLine();
                md.AppendLine(rec.Description);
                md.AppendLine();
                if (rec.ActionItems.Any())
                {
                    md.AppendLine("**Action Items:**");
                    foreach (var action in rec.ActionItems)
                    {
                        md.AppendLine($"- {action}");
                    }
                    md.AppendLine();
                }
            }
        }

        var content = Encoding.UTF8.GetBytes(md.ToString());
        var fileName = $"dashboard_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md";

        return new ReportFile(content, fileName, "text/markdown");
    }

    private ReportFile GeneratePdfReport(ReportData data, string reportType, ReportOptionsDto options)
    {
        // For now, generate HTML and indicate it would be converted to PDF
        // In a real implementation, you would use libraries like iTextSharp, PuppeteerSharp, or similar
        var htmlResult = GenerateHtmlReport(data, reportType, options);

        var fileName = $"dashboard_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return new ReportFile(htmlResult.ReportContent, fileName, "application/pdf");
    }

    private static string GetPriorityColor(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "high" => "#d32f2f",
            "medium" => "#f57c00",
            "low" => "#388e3c",
            _ => "#757575"
        };
    }

    private static string GenerateChecksum(byte[] content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToHexString(hash)[..16];
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
internal sealed record ReportData(
    List<AppTask> Tasks,
    ReportSummary Summary,
    List<ReportInsight> Insights,
    List<ReportRecommendation> Recommendations,
    List<ChartData> Charts
);

internal sealed record ReportInsight(
    string Title,
    string Description,
    string Type,
    string Impact,
    Dictionary<string, object> Data
);

internal sealed record ReportRecommendation(
    string Title,
    string Description,
    string Priority,
    string Category,
    List<string> ActionItems
);

internal sealed record ChartData(
    string Title,
    string Type,
    List<Dictionary<string, object>> Data
);

internal sealed record ReportFile(
    byte[] ReportContent,
    string ReportFileName,
    string ContentType
);
