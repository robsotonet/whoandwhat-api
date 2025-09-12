using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GenerateDashboardReport;

/// <summary>
/// Query to generate a comprehensive dashboard report with analytics and insights
/// </summary>
public sealed record GenerateDashboardReportQuery(
    Guid UserId,
    string ReportType, // summary, detailed, analytical
    ReportOptionsDto Options) : IRequest<Result<GenerateDashboardReportResponse>>;

/// <summary>
/// Report generation options
/// </summary>
public sealed record ReportOptionsDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string Format = "pdf", // pdf, html, markdown
    List<string>? Sections = null, // overview, tasks, productivity, trends, recommendations
    bool IncludeCharts = true,
    bool IncludeInsights = true,
    bool IncludeRecommendations = true,
    string TimeZone = "UTC",
    Dictionary<string, object>? CustomSettings = null
);

/// <summary>
/// Response containing the generated dashboard report
/// </summary>
public sealed record GenerateDashboardReportResponse(
    byte[] ReportContent,
    string ReportFileName,
    string ContentType,
    ReportMetadata Metadata
);

/// <summary>
/// Metadata about the generated report
/// </summary>
public sealed record ReportMetadata(
    DateTime GeneratedAt,
    string GeneratedBy,
    string ReportType,
    ReportOptionsDto Options,
    ReportSummary Summary,
    long FileSizeBytes,
    string ChecksumHash
);

/// <summary>
/// Summary statistics included in the report
/// </summary>
public sealed record ReportSummary(
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate,
    int ProductivityStreak,
    int InsightsGenerated,
    int RecommendationsProvided,
    DateTime PeriodStart,
    DateTime PeriodEnd
);