namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for DDoS protection middleware
/// </summary>
public class DDoSProtectionSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "DDoSProtection";

    /// <summary>
    /// List of whitelisted IP addresses that bypass DDoS protection
    /// </summary>
    public List<string> WhitelistedIPs { get; set; } = new();

    /// <summary>
    /// Duration in minutes to block suspicious IPs
    /// </summary>
    public int BlockDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of requests allowed in the frequency window
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 100;

    /// <summary>
    /// Time window in minutes for request frequency analysis
    /// </summary>
    public int RequestFrequencyWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Time window in minutes for suspicious score accumulation
    /// </summary>
    public int ScoreAccumulationWindowMinutes { get; set; } = 10;

    /// <summary>
    /// Time window in minutes for pattern analysis (endpoints and user agents)
    /// </summary>
    public int PatternAnalysisWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Threshold for blocking based on accumulated suspicious score
    /// </summary>
    public int BlockingThreshold { get; set; } = 100;

    /// <summary>
    /// Threshold for immediate blocking (single request with very high score)
    /// </summary>
    public int ImmediateBlockThreshold { get; set; } = 50;

    /// <summary>
    /// Threshold for logging suspicious activity (but not blocking)
    /// </summary>
    public int LogSuspiciousThreshold { get; set; } = 25;

    /// <summary>
    /// Penalty points for bot-like user agents
    /// </summary>
    public int BotUserAgentPenalty { get; set; } = 15;

    /// <summary>
    /// Penalty points for accessing suspicious paths
    /// </summary>
    public int SuspiciousPathPenalty { get; set; } = 20;

    /// <summary>
    /// Penalty points for high request frequency
    /// </summary>
    public int HighFrequencyPenalty { get; set; } = 25;

    /// <summary>
    /// Penalty points for accessing excessive different endpoints
    /// </summary>
    public int ExcessiveEndpointsPenalty { get; set; } = 15;

    /// <summary>
    /// Penalty points for frequent user agent changes
    /// </summary>
    public int UserAgentVariationPenalty { get; set; } = 10;

    /// <summary>
    /// Maximum number of unique endpoints allowed in pattern analysis window
    /// </summary>
    public int MaxUniqueEndpointsThreshold { get; set; } = 20;

    /// <summary>
    /// Maximum number of user agent changes allowed in pattern analysis window
    /// </summary>
    public int MaxUserAgentChangesThreshold { get; set; } = 3;

    /// <summary>
    /// Enable or disable DDoS protection middleware
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enable detailed logging of DDoS protection activities
    /// </summary>
    public bool DetailedLogging { get; set; } = false;
}