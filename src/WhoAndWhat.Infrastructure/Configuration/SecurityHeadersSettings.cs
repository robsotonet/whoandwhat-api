namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for security headers middleware
/// </summary>
public class SecurityHeadersSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "SecurityHeaders";

    /// <summary>
    /// Enable or disable security headers middleware
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Remove server header to hide server information
    /// </summary>
    public bool RemoveServerHeader { get; set; } = true;

    /// <summary>
    /// X-Content-Type-Options header configuration
    /// </summary>
    public string XContentTypeOptions { get; set; } = "nosniff";

    /// <summary>
    /// X-Frame-Options header configuration
    /// </summary>
    public string XFrameOptions { get; set; } = "DENY";

    /// <summary>
    /// X-XSS-Protection header configuration
    /// </summary>
    public string XXSSProtection { get; set; } = "1; mode=block";

    /// <summary>
    /// Referrer-Policy header configuration
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Cross-Origin-Embedder-Policy header configuration
    /// </summary>
    public string CrossOriginEmbedderPolicy { get; set; } = "require-corp";

    /// <summary>
    /// Cross-Origin-Opener-Policy header configuration
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>
    /// Cross-Origin-Resource-Policy header configuration
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";

    /// <summary>
    /// X-Permitted-Cross-Domain-Policies header configuration
    /// </summary>
    public string XPermittedCrossDomainPolicies { get; set; } = "none";

    /// <summary>
    /// HSTS (HTTP Strict Transport Security) configuration
    /// </summary>
    public HSTSSettings HSTS { get; set; } = new();

    /// <summary>
    /// Content Security Policy configuration
    /// </summary>
    public CSPSettings ContentSecurityPolicy { get; set; } = new();

    /// <summary>
    /// Permissions Policy configuration
    /// </summary>
    public PermissionsPolicySettings PermissionsPolicy { get; set; } = new();

    /// <summary>
    /// Clear-Site-Data header configuration
    /// </summary>
    public ClearSiteDataSettings ClearSiteData { get; set; } = new();
}

/// <summary>
/// HTTP Strict Transport Security (HSTS) settings
/// </summary>
public class HSTSSettings
{
    /// <summary>
    /// Enable HSTS header
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HSTS max age in seconds (default: 1 year)
    /// </summary>
    public int MaxAge { get; set; } = 31536000;

    /// <summary>
    /// Include subdomains in HSTS
    /// </summary>
    public bool IncludeSubDomains { get; set; } = true;

    /// <summary>
    /// Preload HSTS (requires submission to browsers' HSTS preload list)
    /// </summary>
    public bool Preload { get; set; } = false;
}

/// <summary>
/// Content Security Policy (CSP) settings
/// </summary>
public class CSPSettings
{
    /// <summary>
    /// Enable CSP header
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use CSP report-only mode for testing
    /// </summary>
    public bool ReportOnly { get; set; } = false;

    /// <summary>
    /// CSP reporting endpoint URI
    /// </summary>
    public string? ReportUri { get; set; }

    /// <summary>
    /// Enable nonce generation for inline scripts and styles
    /// </summary>
    public bool UseNonce { get; set; } = true;

    /// <summary>
    /// Default source policy
    /// </summary>
    public string DefaultSrc { get; set; } = "'self'";

    /// <summary>
    /// Script source policy
    /// </summary>
    public string ScriptSrc { get; set; } = "'self'";

    /// <summary>
    /// Style source policy
    /// </summary>
    public string StyleSrc { get; set; } = "'self'";

    /// <summary>
    /// Image source policy
    /// </summary>
    public string ImgSrc { get; set; } = "'self' data: https:";

    /// <summary>
    /// Font source policy
    /// </summary>
    public string FontSrc { get; set; } = "'self'";

    /// <summary>
    /// Connect source policy (for AJAX, WebSocket, etc.)
    /// </summary>
    public string ConnectSrc { get; set; } = "'self'";

    /// <summary>
    /// Media source policy
    /// </summary>
    public string MediaSrc { get; set; } = "'self'";

    /// <summary>
    /// Object source policy
    /// </summary>
    public string ObjectSrc { get; set; } = "'none'";

    /// <summary>
    /// Child source policy (for frames, workers)
    /// </summary>
    public string ChildSrc { get; set; } = "'self'";

    /// <summary>
    /// Frame ancestors policy
    /// </summary>
    public string FrameAncestors { get; set; } = "'none'";

    /// <summary>
    /// Form action policy
    /// </summary>
    public string FormAction { get; set; } = "'self'";

    /// <summary>
    /// Base URI policy
    /// </summary>
    public string BaseUri { get; set; } = "'self'";

    /// <summary>
    /// Upgrade insecure requests
    /// </summary>
    public bool UpgradeInsecureRequests { get; set; } = true;

    /// <summary>
    /// Block all mixed content
    /// </summary>
    public bool BlockAllMixedContent { get; set; } = true;
}

/// <summary>
/// Permissions Policy settings
/// </summary>
public class PermissionsPolicySettings
{
    /// <summary>
    /// Enable Permissions Policy header
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Camera permission policy
    /// </summary>
    public string Camera { get; set; } = "()";

    /// <summary>
    /// Microphone permission policy
    /// </summary>
    public string Microphone { get; set; } = "()";

    /// <summary>
    /// Geolocation permission policy
    /// </summary>
    public string Geolocation { get; set; } = "()";

    /// <summary>
    /// Gyroscope permission policy
    /// </summary>
    public string Gyroscope { get; set; } = "()";

    /// <summary>
    /// Accelerometer permission policy
    /// </summary>
    public string Accelerometer { get; set; } = "()";

    /// <summary>
    /// Magnetometer permission policy
    /// </summary>
    public string Magnetometer { get; set; } = "()";

    /// <summary>
    /// Payment permission policy
    /// </summary>
    public string Payment { get; set; } = "()";

    /// <summary>
    /// USB permission policy
    /// </summary>
    public string Usb { get; set; } = "()";

    /// <summary>
    /// Picture-in-picture permission policy
    /// </summary>
    public string PictureInPicture { get; set; } = "()";

    /// <summary>
    /// Fullscreen permission policy
    /// </summary>
    public string Fullscreen { get; set; } = "'self'";

    /// <summary>
    /// Autoplay permission policy
    /// </summary>
    public string Autoplay { get; set; } = "()";

    /// <summary>
    /// Encrypted media permission policy
    /// </summary>
    public string EncryptedMedia { get; set; } = "()";
}

/// <summary>
/// Clear-Site-Data header settings
/// </summary>
public class ClearSiteDataSettings
{
    /// <summary>
    /// Enable Clear-Site-Data header
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Clear cache on logout endpoints
    /// </summary>
    public bool ClearCache { get; set; } = true;

    /// <summary>
    /// Clear cookies on logout endpoints
    /// </summary>
    public bool ClearCookies { get; set; } = true;

    /// <summary>
    /// Clear storage on logout endpoints
    /// </summary>
    public bool ClearStorage { get; set; } = true;

    /// <summary>
    /// Clear execution contexts on logout endpoints
    /// </summary>
    public bool ClearExecutionContexts { get; set; } = true;

    /// <summary>
    /// Endpoints that trigger Clear-Site-Data header
    /// </summary>
    public List<string> TriggerEndpoints { get; set; } = new() { "/api/auth/logout", "/logout" };
}