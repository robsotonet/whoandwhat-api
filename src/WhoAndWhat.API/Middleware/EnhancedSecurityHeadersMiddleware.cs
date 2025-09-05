using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.API.Middleware;

/// <summary>
/// Enhanced middleware to add comprehensive security headers to all responses
/// </summary>
public class EnhancedSecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EnhancedSecurityHeadersMiddleware> _logger;

    /// <summary>
    /// Initializes the enhanced security headers middleware
    /// </summary>
    /// <param name="next">Next middleware in pipeline</param>
    /// <param name="settings">Security headers settings</param>
    /// <param name="environment">Web host environment</param>
    /// <param name="logger">Logger for security headers middleware</param>
    public EnhancedSecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersSettings> settings,
        IWebHostEnvironment environment,
        ILogger<EnhancedSecurityHeadersMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the enhanced security headers middleware
    /// </summary>
    /// <param name="context">HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var headers = context.Response.Headers;
        var request = context.Request;

        // Generate nonce for CSP if enabled
        string? nonce = null;
        if (_settings.ContentSecurityPolicy.Enabled && _settings.ContentSecurityPolicy.UseNonce)
        {
            nonce = GenerateNonce();
            context.Items["csp-nonce"] = nonce; // Store for use in views/templates
        }

        // Remove server information
        if (_settings.RemoveServerHeader)
        {
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");
            headers.Remove("X-AspNetMvc-Version");
        }

        // Add basic security headers
        AddBasicSecurityHeaders(headers);

        // Add HSTS header for HTTPS requests
        AddHSTSHeader(headers, context);

        // Add Content Security Policy
        AddContentSecurityPolicy(headers, nonce);

        // Add Permissions Policy
        AddPermissionsPolicy(headers);

        // Add modern security headers
        AddModernSecurityHeaders(headers);

        // Add Clear-Site-Data header for logout endpoints
        AddClearSiteDataHeader(headers, request);

        await _next(context);
    }

    /// <summary>
    /// Adds basic security headers
    /// </summary>
    private void AddBasicSecurityHeaders(IHeaderDictionary headers)
    {
        // X-Content-Type-Options
        if (!string.IsNullOrEmpty(_settings.XContentTypeOptions))
        {
            headers.TryAdd("X-Content-Type-Options", _settings.XContentTypeOptions);
        }

        // X-Frame-Options
        if (!string.IsNullOrEmpty(_settings.XFrameOptions))
        {
            headers.TryAdd("X-Frame-Options", _settings.XFrameOptions);
        }

        // X-XSS-Protection (deprecated but still useful for older browsers)
        if (!string.IsNullOrEmpty(_settings.XXSSProtection))
        {
            headers.TryAdd("X-XSS-Protection", _settings.XXSSProtection);
        }

        // Referrer-Policy
        if (!string.IsNullOrEmpty(_settings.ReferrerPolicy))
        {
            headers.TryAdd("Referrer-Policy", _settings.ReferrerPolicy);
        }

        // X-Permitted-Cross-Domain-Policies
        if (!string.IsNullOrEmpty(_settings.XPermittedCrossDomainPolicies))
        {
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", _settings.XPermittedCrossDomainPolicies);
        }
    }

    /// <summary>
    /// Adds modern security headers (COEP, COOP, CORP)
    /// </summary>
    private void AddModernSecurityHeaders(IHeaderDictionary headers)
    {
        // Cross-Origin-Embedder-Policy
        if (!string.IsNullOrEmpty(_settings.CrossOriginEmbedderPolicy))
        {
            headers.TryAdd("Cross-Origin-Embedder-Policy", _settings.CrossOriginEmbedderPolicy);
        }

        // Cross-Origin-Opener-Policy
        if (!string.IsNullOrEmpty(_settings.CrossOriginOpenerPolicy))
        {
            headers.TryAdd("Cross-Origin-Opener-Policy", _settings.CrossOriginOpenerPolicy);
        }

        // Cross-Origin-Resource-Policy
        if (!string.IsNullOrEmpty(_settings.CrossOriginResourcePolicy))
        {
            headers.TryAdd("Cross-Origin-Resource-Policy", _settings.CrossOriginResourcePolicy);
        }
    }

    /// <summary>
    /// Adds HSTS header for HTTPS requests
    /// </summary>
    private void AddHSTSHeader(IHeaderDictionary headers, HttpContext context)
    {
        if (!_settings.HSTS.Enabled || !context.Request.IsHttps)
        {
            return;
        }

        var hstsValue = new StringBuilder($"max-age={_settings.HSTS.MaxAge}");

        if (_settings.HSTS.IncludeSubDomains)
        {
            hstsValue.Append("; includeSubDomains");
        }

        if (_settings.HSTS.Preload)
        {
            hstsValue.Append("; preload");
        }

        headers.TryAdd("Strict-Transport-Security", hstsValue.ToString());
    }

    /// <summary>
    /// Adds Content Security Policy header
    /// </summary>
    private void AddContentSecurityPolicy(IHeaderDictionary headers, string? nonce)
    {
        if (!_settings.ContentSecurityPolicy.Enabled)
        {
            return;
        }

        var csp = BuildContentSecurityPolicy(nonce);
        var headerName = _settings.ContentSecurityPolicy.ReportOnly 
            ? "Content-Security-Policy-Report-Only" 
            : "Content-Security-Policy";

        headers.TryAdd(headerName, csp);

        _logger.LogDebug("Added CSP header: {CSP}", csp);
    }

    /// <summary>
    /// Builds Content Security Policy string
    /// </summary>
    private string BuildContentSecurityPolicy(string? nonce)
    {
        var csp = new List<string>();
        var settings = _settings.ContentSecurityPolicy;

        // Default source
        if (!string.IsNullOrEmpty(settings.DefaultSrc))
        {
            csp.Add($"default-src {settings.DefaultSrc}");
        }

        // Script source with nonce support
        if (!string.IsNullOrEmpty(settings.ScriptSrc))
        {
            var scriptSrc = settings.ScriptSrc;
            if (!string.IsNullOrEmpty(nonce))
            {
                scriptSrc += $" 'nonce-{nonce}'";
            }
            csp.Add($"script-src {scriptSrc}");
        }

        // Style source with nonce support
        if (!string.IsNullOrEmpty(settings.StyleSrc))
        {
            var styleSrc = settings.StyleSrc;
            if (!string.IsNullOrEmpty(nonce))
            {
                styleSrc += $" 'nonce-{nonce}'";
            }
            csp.Add($"style-src {styleSrc}");
        }

        // Other CSP directives
        if (!string.IsNullOrEmpty(settings.ImgSrc))
        {
            csp.Add($"img-src {settings.ImgSrc}");
        }

        if (!string.IsNullOrEmpty(settings.FontSrc))
        {
            csp.Add($"font-src {settings.FontSrc}");
        }

        if (!string.IsNullOrEmpty(settings.ConnectSrc))
        {
            csp.Add($"connect-src {settings.ConnectSrc}");
        }

        if (!string.IsNullOrEmpty(settings.MediaSrc))
        {
            csp.Add($"media-src {settings.MediaSrc}");
        }

        if (!string.IsNullOrEmpty(settings.ObjectSrc))
        {
            csp.Add($"object-src {settings.ObjectSrc}");
        }

        if (!string.IsNullOrEmpty(settings.ChildSrc))
        {
            csp.Add($"child-src {settings.ChildSrc}");
        }

        if (!string.IsNullOrEmpty(settings.FrameAncestors))
        {
            csp.Add($"frame-ancestors {settings.FrameAncestors}");
        }

        if (!string.IsNullOrEmpty(settings.FormAction))
        {
            csp.Add($"form-action {settings.FormAction}");
        }

        if (!string.IsNullOrEmpty(settings.BaseUri))
        {
            csp.Add($"base-uri {settings.BaseUri}");
        }

        // Security directives
        if (settings.UpgradeInsecureRequests)
        {
            csp.Add("upgrade-insecure-requests");
        }

        if (settings.BlockAllMixedContent)
        {
            csp.Add("block-all-mixed-content");
        }

        // Report URI
        if (!string.IsNullOrEmpty(settings.ReportUri))
        {
            csp.Add($"report-uri {settings.ReportUri}");
        }

        return string.Join("; ", csp);
    }

    /// <summary>
    /// Adds Permissions Policy header
    /// </summary>
    private void AddPermissionsPolicy(IHeaderDictionary headers)
    {
        if (!_settings.PermissionsPolicy.Enabled)
        {
            return;
        }

        var permissions = new List<string>();
        var policy = _settings.PermissionsPolicy;

        // Build permissions policy
        AddPermissionDirective(permissions, "camera", policy.Camera);
        AddPermissionDirective(permissions, "microphone", policy.Microphone);
        AddPermissionDirective(permissions, "geolocation", policy.Geolocation);
        AddPermissionDirective(permissions, "gyroscope", policy.Gyroscope);
        AddPermissionDirective(permissions, "accelerometer", policy.Accelerometer);
        AddPermissionDirective(permissions, "magnetometer", policy.Magnetometer);
        AddPermissionDirective(permissions, "payment", policy.Payment);
        AddPermissionDirective(permissions, "usb", policy.Usb);
        AddPermissionDirective(permissions, "picture-in-picture", policy.PictureInPicture);
        AddPermissionDirective(permissions, "fullscreen", policy.Fullscreen);
        AddPermissionDirective(permissions, "autoplay", policy.Autoplay);
        AddPermissionDirective(permissions, "encrypted-media", policy.EncryptedMedia);

        if (permissions.Count > 0)
        {
            headers.TryAdd("Permissions-Policy", string.Join(", ", permissions));
        }
    }

    /// <summary>
    /// Adds a permission directive to the list
    /// </summary>
    private static void AddPermissionDirective(List<string> permissions, string feature, string policy)
    {
        if (!string.IsNullOrEmpty(policy))
        {
            permissions.Add($"{feature}={policy}");
        }
    }

    /// <summary>
    /// Adds Clear-Site-Data header for logout endpoints
    /// </summary>
    private void AddClearSiteDataHeader(IHeaderDictionary headers, HttpRequest request)
    {
        if (!_settings.ClearSiteData.Enabled)
        {
            return;
        }

        var path = request.Path.Value?.ToLowerInvariant() ?? "";
        var shouldClear = _settings.ClearSiteData.TriggerEndpoints
            .Any(endpoint => path.Contains(endpoint.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));

        if (!shouldClear)
        {
            return;
        }

        var clearDirectives = new List<string>();

        if (_settings.ClearSiteData.ClearCache)
        {
            clearDirectives.Add("\"cache\"");
        }

        if (_settings.ClearSiteData.ClearCookies)
        {
            clearDirectives.Add("\"cookies\"");
        }

        if (_settings.ClearSiteData.ClearStorage)
        {
            clearDirectives.Add("\"storage\"");
        }

        if (_settings.ClearSiteData.ClearExecutionContexts)
        {
            clearDirectives.Add("\"executionContexts\"");
        }

        if (clearDirectives.Count > 0)
        {
            headers.TryAdd("Clear-Site-Data", string.Join(", ", clearDirectives));
            _logger.LogInformation("Clear-Site-Data header added for logout endpoint: {Path}", request.Path);
        }
    }

    /// <summary>
    /// Generates a cryptographically secure nonce for CSP
    /// </summary>
    private static string GenerateNonce()
    {
        var nonceBytes = new byte[32]; // 256 bits
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonceBytes);
        }
        return Convert.ToBase64String(nonceBytes);
    }
}