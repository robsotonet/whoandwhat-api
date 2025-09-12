using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Options;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.API.Middleware;

/// <summary>
/// Advanced DDoS protection middleware that monitors request patterns and blocks suspicious activities
/// </summary>
public class DDoSProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DDoSProtectionSettings _settings;
    private readonly ILogger<DDoSProtectionMiddleware> _logger;

    // Thread-safe collections for tracking request patterns
    private static readonly ConcurrentDictionary<string, RequestTracker> _ipTrackers = new();
    private static readonly ConcurrentDictionary<string, DateTime> _blockedIPs = new();
    private static readonly ConcurrentDictionary<string, SuspiciousPatternTracker> _patternTrackers = new();

    // Timer for cleanup operations
    private static readonly Timer _cleanupTimer = new(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

    /// <summary>
    /// Initializes the DDoS protection middleware
    /// </summary>
    /// <param name="next">Next middleware in pipeline</param>
    /// <param name="settings">DDoS protection settings</param>
    /// <param name="logger">Logger for DDoS protection middleware</param>
    public DDoSProtectionMiddleware(
        RequestDelegate next,
        IOptions<DDoSProtectionSettings> settings,
        ILogger<DDoSProtectionMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the DDoS protection middleware
    /// </summary>
    /// <param name="context">HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var clientIP = GetClientIP(context);

        // Skip protection for whitelisted IPs
        if (IsWhitelistedIP(clientIP))
        {
            await _next(context);
            return;
        }

        // Check if IP is currently blocked
        if (IsIPBlocked(clientIP))
        {
            await HandleBlockedRequest(context, clientIP);
            return;
        }

        // Analyze request patterns for suspicious activity
        var suspiciousScore = AnalyzeRequestPattern(context, clientIP);

        // Update request tracking
        UpdateRequestTracking(clientIP, context, suspiciousScore);

        // Evaluate if IP should be blocked
        if (ShouldBlockIP(clientIP, suspiciousScore))
        {
            BlockIP(clientIP);
            await HandleBlockedRequest(context, clientIP);
            return;
        }

        // Log suspicious activity if score is high but not blocking threshold
        if (suspiciousScore > _settings.LogSuspiciousThreshold)
        {
            _logger.LogWarning("Suspicious activity detected from IP: {ClientIP}, Score: {Score}, Path: {Path}, UserAgent: {UserAgent}",
                clientIP, suspiciousScore, context.Request.Path, context.Request.Headers.UserAgent.FirstOrDefault());
        }

        await _next(context);
    }

    /// <summary>
    /// Extracts client IP address from request context
    /// </summary>
    private string GetClientIP(HttpContext context)
    {
        // Check for forwarded IP headers (common in load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIP))
        {
            return realIP;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Checks if IP address is in whitelist
    /// </summary>
    private bool IsWhitelistedIP(string clientIP)
    {
        if (IPAddress.TryParse(clientIP, out var ipAddress))
        {
            return _settings.WhitelistedIPs.Any(whitelistedIP =>
                IPAddress.TryParse(whitelistedIP, out var whiteIP) &&
                whiteIP.Equals(ipAddress));
        }
        return false;
    }

    /// <summary>
    /// Checks if IP is currently blocked
    /// </summary>
    private bool IsIPBlocked(string clientIP)
    {
        if (_blockedIPs.TryGetValue(clientIP, out var blockTime))
        {
            if (DateTime.UtcNow - blockTime < TimeSpan.FromMinutes(_settings.BlockDurationMinutes))
            {
                return true;
            }

            // Remove expired block
            _blockedIPs.TryRemove(clientIP, out _);
        }

        return false;
    }

    /// <summary>
    /// Analyzes request patterns to detect suspicious activities
    /// </summary>
    private int AnalyzeRequestPattern(HttpContext context, string clientIP)
    {
        int suspiciousScore = 0;
        var request = context.Request;
        var userAgent = request.Headers.UserAgent.FirstOrDefault() ?? "";
        var path = request.Path.Value ?? "";

        // Check for common bot/crawler patterns
        if (IsCommonBotUserAgent(userAgent))
        {
            suspiciousScore += _settings.BotUserAgentPenalty;
        }

        // Check for suspicious paths
        if (IsSuspiciousPath(path))
        {
            suspiciousScore += _settings.SuspiciousPathPenalty;
        }

        // Check for excessive different endpoints access
        var patternTracker = _patternTrackers.GetOrAdd(clientIP, _ => new SuspiciousPatternTracker());
        patternTracker.RecordRequest(path, userAgent);

        if (patternTracker.GetUniqueEndpointsInWindow(_settings.PatternAnalysisWindowMinutes) > _settings.MaxUniqueEndpointsThreshold)
        {
            suspiciousScore += _settings.ExcessiveEndpointsPenalty;
        }

        // Check for rapid succession of different user agents
        if (patternTracker.GetUniqueUserAgentsInWindow(_settings.PatternAnalysisWindowMinutes) > _settings.MaxUserAgentChangesThreshold)
        {
            suspiciousScore += _settings.UserAgentVariationPenalty;
        }

        // Check request frequency
        var tracker = _ipTrackers.GetOrAdd(clientIP, _ => new RequestTracker());
        var requestsInWindow = tracker.GetRequestCountInWindow(TimeSpan.FromMinutes(_settings.RequestFrequencyWindowMinutes));

        if (requestsInWindow > _settings.MaxRequestsPerWindow)
        {
            suspiciousScore += _settings.HighFrequencyPenalty;
        }

        return suspiciousScore;
    }

    /// <summary>
    /// Updates request tracking for the client IP
    /// </summary>
    private void UpdateRequestTracking(string clientIP, HttpContext context, int suspiciousScore)
    {
        var tracker = _ipTrackers.GetOrAdd(clientIP, _ => new RequestTracker());
        tracker.RecordRequest(suspiciousScore);
    }

    /// <summary>
    /// Evaluates whether an IP should be blocked based on accumulated suspicious score
    /// </summary>
    private bool ShouldBlockIP(string clientIP, int currentScore)
    {
        if (_ipTrackers.TryGetValue(clientIP, out var tracker))
        {
            var totalScore = tracker.GetAccumulatedSuspiciousScore(TimeSpan.FromMinutes(_settings.ScoreAccumulationWindowMinutes));
            return totalScore > _settings.BlockingThreshold;
        }

        return currentScore > _settings.ImmediateBlockThreshold;
    }

    /// <summary>
    /// Blocks an IP address
    /// </summary>
    private void BlockIP(string clientIP)
    {
        _blockedIPs.TryAdd(clientIP, DateTime.UtcNow);
        _logger.LogWarning("IP blocked due to suspicious activity: {ClientIP}, Block duration: {Duration} minutes",
            clientIP, _settings.BlockDurationMinutes);
    }

    /// <summary>
    /// Handles blocked request by returning 429 Too Many Requests
    /// </summary>
    private async Task HandleBlockedRequest(HttpContext context, string clientIP)
    {
        _logger.LogInformation("Blocked request from IP: {ClientIP}, Path: {Path}", clientIP, context.Request.Path);

        context.Response.StatusCode = 429;
        context.Response.Headers.TryAdd("Retry-After", (_settings.BlockDurationMinutes * 60).ToString());

        await context.Response.WriteAsync($"{{\"error\":\"Too many requests. IP temporarily blocked.\",\"retryAfter\":\"{_settings.BlockDurationMinutes} minutes\"}}");
    }

    /// <summary>
    /// Checks if user agent matches common bot patterns
    /// </summary>
    private bool IsCommonBotUserAgent(string userAgent)
    {
        var botPatterns = new[]
        {
            "bot", "crawler", "spider", "scraper", "curl", "wget", "python", "java", "go-http-client",
            "scanner", "vulnerability", "nikto", "sqlmap", "nmap", "masscan"
        };

        return botPatterns.Any(pattern => userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if path is commonly targeted by attackers
    /// </summary>
    private bool IsSuspiciousPath(string path)
    {
        var suspiciousPaths = new[]
        {
            ".php", ".asp", ".jsp", "wp-admin", "wp-login", "admin", "administrator",
            "phpmyadmin", "xmlrpc", ".env", "config", "backup", "sql", "database",
            "shell", "cmd", "exec", "/etc/passwd", "web.config", ".htaccess"
        };

        return suspiciousPaths.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Cleanup expired tracking entries
    /// </summary>
    private static void CleanupExpiredEntries(object? state)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-1);

        // Clean up expired IP trackers
        foreach (var kvp in _ipTrackers.ToArray())
        {
            if (kvp.Value.LastActivity < cutoffTime)
            {
                _ipTrackers.TryRemove(kvp.Key, out _);
            }
        }

        // Clean up expired pattern trackers
        foreach (var kvp in _patternTrackers.ToArray())
        {
            if (kvp.Value.LastActivity < cutoffTime)
            {
                _patternTrackers.TryRemove(kvp.Key, out _);
            }
        }

        // Clean up expired blocked IPs
        foreach (var kvp in _blockedIPs.ToArray())
        {
            if (DateTime.UtcNow - kvp.Value > TimeSpan.FromHours(24)) // Keep blocks for max 24 hours
            {
                _blockedIPs.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Tracks request patterns for suspicious activity detection
/// </summary>
public class RequestTracker
{
    private readonly List<RequestRecord> _requests = new();
    private readonly object _lock = new();

    /// <summary>
    /// Last activity timestamp for this IP address
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Records a new request with its suspicious score
    /// </summary>
    /// <param name="suspiciousScore">Suspicious activity score for this request</param>
    public void RecordRequest(int suspiciousScore)
    {
        lock (_lock)
        {
            _requests.Add(new RequestRecord(DateTime.UtcNow, suspiciousScore));
            LastActivity = DateTime.UtcNow;

            // Keep only recent requests (last 24 hours)
            var cutoff = DateTime.UtcNow.AddHours(-24);
            _requests.RemoveAll(r => r.Timestamp < cutoff);
        }
    }

    /// <summary>
    /// Gets the number of requests within the specified time window
    /// </summary>
    /// <param name="window">Time window to check</param>
    /// <returns>Number of requests in the window</returns>
    public int GetRequestCountInWindow(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        lock (_lock)
        {
            return _requests.Count(r => r.Timestamp > cutoff);
        }
    }

    /// <summary>
    /// Gets the accumulated suspicious score within the specified time window
    /// </summary>
    /// <param name="window">Time window to check</param>
    /// <returns>Total suspicious score in the window</returns>
    public int GetAccumulatedSuspiciousScore(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        lock (_lock)
        {
            return _requests.Where(r => r.Timestamp > cutoff).Sum(r => r.SuspiciousScore);
        }
    }

    private record RequestRecord(DateTime Timestamp, int SuspiciousScore);
}

/// <summary>
/// Tracks patterns in requests for advanced suspicious activity detection
/// </summary>
public class SuspiciousPatternTracker
{
    private readonly List<PatternRecord> _patterns = new();
    private readonly object _lock = new();

    /// <summary>
    /// Last activity timestamp for this IP address pattern tracking
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Records a new request pattern with path and user agent
    /// </summary>
    /// <param name="path">Request path</param>
    /// <param name="userAgent">User agent string</param>
    public void RecordRequest(string path, string userAgent)
    {
        lock (_lock)
        {
            _patterns.Add(new PatternRecord(DateTime.UtcNow, path, userAgent));
            LastActivity = DateTime.UtcNow;

            // Keep only recent patterns (last 2 hours)
            var cutoff = DateTime.UtcNow.AddHours(-2);
            _patterns.RemoveAll(p => p.Timestamp < cutoff);
        }
    }

    /// <summary>
    /// Gets the number of unique endpoints accessed within the specified time window
    /// </summary>
    /// <param name="windowMinutes">Time window in minutes</param>
    /// <returns>Number of unique endpoints</returns>
    public int GetUniqueEndpointsInWindow(int windowMinutes)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
        lock (_lock)
        {
            return _patterns.Where(p => p.Timestamp > cutoff).Select(p => p.Path).Distinct().Count();
        }
    }

    /// <summary>
    /// Gets the number of unique user agents used within the specified time window
    /// </summary>
    /// <param name="windowMinutes">Time window in minutes</param>
    /// <returns>Number of unique user agents</returns>
    public int GetUniqueUserAgentsInWindow(int windowMinutes)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
        lock (_lock)
        {
            return _patterns.Where(p => p.Timestamp > cutoff).Select(p => p.UserAgent).Distinct().Count();
        }
    }

    private record PatternRecord(DateTime Timestamp, string Path, string UserAgent);
}
