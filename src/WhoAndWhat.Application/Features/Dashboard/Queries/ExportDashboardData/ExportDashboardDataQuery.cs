using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.ExportDashboardData;

/// <summary>
/// Query to export dashboard data in various formats
/// </summary>
public sealed record ExportDashboardDataQuery(
    Guid UserId,
    string Format, // csv, json, excel
    ExportOptionsDto Options) : IRequest<Result<ExportDashboardDataResponse>>;

/// <summary>
/// Export options for dashboard data
/// </summary>
public sealed record ExportOptionsDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    List<string>? IncludeCategories = null,
    List<string>? IncludePriorities = null,
    List<string>? IncludeStatuses = null,
    List<string>? DataTypes = null, // tasks, metrics, streaks, analytics
    bool IncludeDeleted = false,
    bool IncludeArchived = false,
    string TimeZone = "UTC",
    Dictionary<string, object>? CustomFilters = null
);

/// <summary>
/// Response containing exported dashboard data
/// </summary>
public sealed record ExportDashboardDataResponse(
    byte[] FileContent,
    string FileName,
    string ContentType,
    int RecordCount,
    ExportMetadata Metadata
);

/// <summary>
/// Metadata about the exported data
/// </summary>
public sealed record ExportMetadata(
    DateTime ExportedAt,
    string ExportedBy,
    ExportOptionsDto Options,
    Dictionary<string, int> RecordCounts,
    long FileSizeBytes,
    string ChecksumHash
);