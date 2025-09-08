namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Email service configuration settings
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";
    
    /// <summary>
    /// Display name for the sender (e.g., "WhoAndWhat Support")
    /// </summary>
    public string FromName { get; set; } = "WhoAndWhat";
    
    /// <summary>
    /// Email address to send from
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;
    
    /// <summary>
    /// SMTP server port (typically 587 for TLS, 465 for SSL, 25 for non-secure)
    /// </summary>
    public int SmtpPort { get; set; } = 587;
    
    /// <summary>
    /// Username for SMTP authentication
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Password or app password for SMTP authentication
    /// </summary>
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// Enable SSL/TLS encryption (recommended for production)
    /// </summary>
    public bool UseSsl { get; set; } = true;
    
    /// <summary>
    /// Enable SMTP authentication
    /// </summary>
    public bool UseAuthentication { get; set; } = true;
    
    /// <summary>
    /// Timeout in milliseconds for SMTP operations
    /// </summary>
    public int TimeoutMs { get; set; } = 30000; // 30 seconds
    
    /// <summary>
    /// Base URL for the application (used in email links)
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:7071";
    
    /// <summary>
    /// Enable email sending (can be disabled for testing/development)
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Email template configuration
    /// </summary>
    public EmailTemplateSettings Templates { get; set; } = new();
}

/// <summary>
/// Email template configuration settings
/// </summary>
public class EmailTemplateSettings
{
    /// <summary>
    /// Password reset token expiration in hours
    /// </summary>
    public int PasswordResetExpirationHours { get; set; } = 1;
    
    /// <summary>
    /// Email verification token expiration in hours
    /// </summary>
    public int EmailVerificationExpirationHours { get; set; } = 24;
    
    /// <summary>
    /// Company/application logo URL for email templates
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Support email address displayed in templates
    /// </summary>
    public string SupportEmail { get; set; } = "support@whoandwhat.com";
    
    /// <summary>
    /// Company name displayed in templates
    /// </summary>
    public string CompanyName { get; set; } = "WhoAndWhat";
}