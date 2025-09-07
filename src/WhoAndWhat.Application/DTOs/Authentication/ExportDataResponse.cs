namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for user data export
/// </summary>
public class ExportDataResponse
{
    /// <summary>
    /// Export file name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Export file content type
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Export file size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Export generation timestamp
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Number of records exported
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Export format used
    /// </summary>
    public string Format { get; set; } = string.Empty;
}