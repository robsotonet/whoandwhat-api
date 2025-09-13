namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// AI planning service configuration settings
/// </summary>
public class AISettings
{
    public const string SectionName = "AI";

    /// <summary>
    /// Enable AI planning service (can be disabled for testing/development)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Primary AI provider to use for planning services
    /// </summary>
    public AIProvider Provider { get; set; } = AIProvider.OpenAI;

    /// <summary>
    /// Fallback AI provider when primary fails
    /// </summary>
    public AIProvider? FallbackProvider { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for failed AI requests
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Request timeout in milliseconds for AI service calls
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Rate limiting configuration for AI requests
    /// </summary>
    public AIRateLimitSettings RateLimit { get; set; } = new();

    /// <summary>
    /// Caching configuration for AI responses
    /// </summary>
    public AICacheSettings Cache { get; set; } = new();

    /// <summary>
    /// OpenAI provider configuration
    /// </summary>
    public OpenAISettings OpenAI { get; set; } = new();

    /// <summary>
    /// Azure OpenAI provider configuration
    /// </summary>
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();

    /// <summary>
    /// Google Gemini provider configuration
    /// </summary>
    public GoogleGeminiSettings GoogleGemini { get; set; } = new();

    /// <summary>
    /// Anthropic Claude provider configuration
    /// </summary>
    public AnthropicClaudeSettings AnthropicClaude { get; set; } = new();

    /// <summary>
    /// Monitoring and health check settings
    /// </summary>
    public AIMonitoringSettings Monitoring { get; set; } = new();

    /// <summary>
    /// Feature flags for different AI capabilities
    /// </summary>
    public AIFeatureFlags Features { get; set; } = new();
}

/// <summary>
/// Rate limiting settings for AI requests
/// </summary>
public class AIRateLimitSettings
{
    /// <summary>
    /// Maximum requests per minute per user
    /// </summary>
    public int RequestsPerMinutePerUser { get; set; } = 10;

    /// <summary>
    /// Maximum requests per hour per user
    /// </summary>
    public int RequestsPerHourPerUser { get; set; } = 100;

    /// <summary>
    /// Global rate limit for all users (requests per minute)
    /// </summary>
    public int GlobalRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Enable burst allowance for short periods
    /// </summary>
    public bool EnableBurstAllowance { get; set; } = true;

    /// <summary>
    /// Burst allowance multiplier
    /// </summary>
    public double BurstMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Caching settings for AI responses
/// </summary>
public class AICacheSettings
{
    /// <summary>
    /// Enable caching of AI responses
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default cache expiration in minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Cache expiration for day plans in minutes
    /// </summary>
    public int DayPlanExpirationMinutes { get; set; } = 180; // 3 hours

    /// <summary>
    /// Cache expiration for priority suggestions in minutes
    /// </summary>
    public int PrioritySuggestionExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Cache expiration for productivity insights in minutes
    /// </summary>
    public int ProductivityInsightsExpirationMinutes { get; set; } = 360; // 6 hours

    /// <summary>
    /// Enable cache warming for frequently accessed data
    /// </summary>
    public bool EnableCacheWarming { get; set; } = false;

    /// <summary>
    /// Maximum cache size per user in MB
    /// </summary>
    public int MaxCacheSizePerUserMB { get; set; } = 10;
}

/// <summary>
/// OpenAI provider settings
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI API endpoint URL
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// Model to use for planning tasks
    /// </summary>
    public string PlanningModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Model to use for text analysis tasks
    /// </summary>
    public string AnalysisModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Organization ID (optional)
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Maximum tokens per request
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for response generation (0.0 to 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top-p parameter for response generation
    /// </summary>
    public double TopP { get; set; } = 1.0;
}

/// <summary>
/// Azure OpenAI provider settings
/// </summary>
public class AzureOpenAISettings
{
    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API version to use
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-01";

    /// <summary>
    /// Deployment name for planning model
    /// </summary>
    public string PlanningDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for analysis model
    /// </summary>
    public string AnalysisDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens per request
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}

/// <summary>
/// Google Gemini provider settings
/// </summary>
public class GoogleGeminiSettings
{
    /// <summary>
    /// Google AI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gemini API endpoint URL
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Model version to use
    /// </summary>
    public string Model { get; set; } = "gemini-1.5-pro";

    /// <summary>
    /// Maximum output tokens
    /// </summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top-K parameter
    /// </summary>
    public int TopK { get; set; } = 40;

    /// <summary>
    /// Top-P parameter
    /// </summary>
    public double TopP { get; set; } = 0.95;
}

/// <summary>
/// Anthropic Claude provider settings
/// </summary>
public class AnthropicClaudeSettings
{
    /// <summary>
    /// Anthropic API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Anthropic API endpoint URL
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://api.anthropic.com/v1";

    /// <summary>
    /// Model version to use
    /// </summary>
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";

    /// <summary>
    /// Maximum tokens per request
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// API version
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";
}

/// <summary>
/// AI service monitoring settings
/// </summary>
public class AIMonitoringSettings
{
    /// <summary>
    /// Enable detailed monitoring and metrics
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Log all AI requests for debugging
    /// </summary>
    public bool LogRequests { get; set; } = false;

    /// <summary>
    /// Log AI response details
    /// </summary>
    public bool LogResponses { get; set; } = false;

    /// <summary>
    /// Enable performance metrics tracking
    /// </summary>
    public bool TrackPerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Alert thresholds for AI service monitoring
    /// </summary>
    public AIAlertThresholds Alerts { get; set; } = new();
}

/// <summary>
/// Alert thresholds for AI service monitoring
/// </summary>
public class AIAlertThresholds
{
    /// <summary>
    /// Response time threshold in milliseconds for alerts
    /// </summary>
    public int ResponseTimeThresholdMs { get; set; } = 10000; // 10 seconds

    /// <summary>
    /// Error rate threshold percentage for alerts
    /// </summary>
    public double ErrorRateThresholdPercent { get; set; } = 5.0;

    /// <summary>
    /// Failed requests threshold count for alerts
    /// </summary>
    public int FailedRequestsThreshold { get; set; } = 10;

    /// <summary>
    /// Time window for threshold evaluation in minutes
    /// </summary>
    public int ThresholdTimeWindowMinutes { get; set; } = 15;
}

/// <summary>
/// Feature flags for AI capabilities
/// </summary>
public class AIFeatureFlags
{
    /// <summary>
    /// Enable day planning functionality
    /// </summary>
    public bool EnableDayPlanning { get; set; } = true;

    /// <summary>
    /// Enable task priority suggestions
    /// </summary>
    public bool EnablePrioritySuggestions { get; set; } = true;

    /// <summary>
    /// Enable schedule optimization
    /// </summary>
    public bool EnableScheduleOptimization { get; set; } = true;

    /// <summary>
    /// Enable break recommendations
    /// </summary>
    public bool EnableBreakRecommendations { get; set; } = true;

    /// <summary>
    /// Enable productivity insights
    /// </summary>
    public bool EnableProductivityInsights { get; set; } = true;

    /// <summary>
    /// Enable task categorization suggestions
    /// </summary>
    public bool EnableTaskCategorization { get; set; } = true;

    /// <summary>
    /// Enable task time estimation
    /// </summary>
    public bool EnableTimeEstimation { get; set; } = true;

    /// <summary>
    /// Enable experimental features (beta testing)
    /// </summary>
    public bool EnableExperimentalFeatures { get; set; } = false;
}

/// <summary>
/// Supported AI providers
/// </summary>
public enum AIProvider
{
    OpenAI,
    AzureOpenAI,
    GoogleGemini,
    AnthropicClaude,
    Local, // For future local model support
    Custom  // For custom provider implementations
}
