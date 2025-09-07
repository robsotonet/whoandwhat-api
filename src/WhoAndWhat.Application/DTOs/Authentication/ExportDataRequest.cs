using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for user data export
/// </summary>
public class ExportDataRequest
{
    /// <summary>
    /// Export format (json, csv)
    /// </summary>
    [Required(ErrorMessage = "Export format is required")]
    [RegularExpression(@"^(json|csv)$", ErrorMessage = "Export format must be 'json' or 'csv'")]
    public string Format { get; set; } = "json";

    /// <summary>
    /// Include user profile data
    /// </summary>
    public bool IncludeProfile { get; set; } = true;

    /// <summary>
    /// Include tasks data
    /// </summary>
    public bool IncludeTasks { get; set; } = true;

    /// <summary>
    /// Include projects data
    /// </summary>
    public bool IncludeProjects { get; set; } = true;

    /// <summary>
    /// Include contacts data
    /// </summary>
    public bool IncludeContacts { get; set; } = true;

    /// <summary>
    /// Include OAuth account connections
    /// </summary>
    public bool IncludeOAuthAccounts { get; set; } = false;
}