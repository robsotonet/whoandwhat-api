using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service interface for managing and delivering personalized motivational content
/// </summary>
public interface IMotivationalContentService
{
    /// <summary>
    /// Gets personalized content for a user based on their analytics and preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="contentContext">Optional context for content selection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Personalized content or null if no suitable content available</returns>
    Task<PersonalizedContentResult?> GetPersonalizedContentAsync(Guid userId, 
        ContentSelectionContext? contentContext = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple personalized content items for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="maxItems">Maximum number of items to return</param>
    /// <param name="contentContext">Optional context for content selection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of personalized content items</returns>
    Task<IEnumerable<PersonalizedContentResult>> GetPersonalizedContentBatchAsync(Guid userId, 
        int maxItems, 
        ContentSelectionContext? contentContext = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs user engagement with delivered content
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="contentId">Content identifier</param>
    /// <param name="engagementType">Type of engagement</param>
    /// <param name="deliveryLogId">Delivery log identifier</param>
    /// <param name="engagementMetadata">Optional engagement metadata</param>
    /// <param name="viewDuration">Optional view duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if engagement was logged successfully</returns>
    Task<bool> LogContentEngagementAsync(Guid userId, 
        Guid contentId, 
        ContentEngagementType engagementType,
        Guid? deliveryLogId = null,
        Dictionary<string, object>? engagementMetadata = null,
        TimeSpan? viewDuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records content delivery for tracking and analytics
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="contentId">Content identifier</param>
    /// <param name="deliveryChannel">Delivery channel used</param>
    /// <param name="deliveryContext">Optional delivery context</param>
    /// <param name="personalizationScore">Personalization score for this delivery</param>
    /// <param name="abTestGroup">A/B test group if applicable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delivery log ID for tracking engagement</returns>
    Task<Guid> RecordContentDeliveryAsync(Guid userId, 
        Guid contentId, 
        ContentDeliveryChannel deliveryChannel,
        Dictionary<string, object>? deliveryContext = null,
        double? personalizationScore = null,
        string? abTestGroup = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns user to A/B test group for content experiments
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="testName">Name of the A/B test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assigned test group</returns>
    Task<string> AssignABTestGroupAsync(Guid userId, 
        string testName, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets A/B test effectiveness metrics for content
    /// </summary>
    /// <param name="testName">Name of the A/B test</param>
    /// <param name="startDate">Start date for metrics</param>
    /// <param name="endDate">End date for metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A/B test effectiveness metrics</returns>
    Task<ABTestMetrics> GetABTestMetricsAsync(string testName, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content performance analytics
    /// </summary>
    /// <param name="contentId">Content identifier</param>
    /// <param name="startDate">Start date for analytics</param>
    /// <param name="endDate">End date for analytics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Content performance metrics</returns>
    Task<ContentPerformanceMetrics> GetContentPerformanceAsync(Guid contentId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user content preferences based on engagement patterns
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if preferences were updated</returns>
    Task<bool> UpdateUserPreferencesFromEngagementAsync(Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if user has reached content delivery limits
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="timeWindow">Time window to check (daily/weekly)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has reached limits</returns>
    Task<bool> HasReachedContentLimitsAsync(Guid userId, 
        ContentLimitTimeWindow timeWindow, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content recommendation score for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="contentId">Content identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recommendation score (0-1)</returns>
    Task<double> GetContentRecommendationScoreAsync(Guid userId, 
        Guid contentId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes content for better engagement based on historical data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of content items optimized</returns>
    Task<int> OptimizeContentForEngagementAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result model for personalized content delivery
/// </summary>
public class PersonalizedContentResult
{
    public MotivationalContentDisplay Content { get; set; } = new();
    public double PersonalizationScore { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public Dictionary<string, object> PersonalizationFactors { get; set; } = new();
    public string? ABTestGroup { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ContentDeliveryChannel RecommendedChannel { get; set; }
    public DateTime? OptimalDeliveryTime { get; set; }
}

/// <summary>
/// Context information for content selection
/// </summary>
public class ContentSelectionContext
{
    public MotivationalContentType? PreferredType { get; set; }
    public ContentCategory? PreferredCategory { get; set; }
    public ContentDeliveryChannel? DeliveryChannel { get; set; }
    public string? TriggerEvent { get; set; }
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
    public bool ExcludeRecentContent { get; set; } = true;
    public TimeSpan? RecentContentWindow { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// A/B test metrics for content effectiveness analysis
/// </summary>
public class ABTestMetrics
{
    public string TestName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Dictionary<string, ABTestGroupMetrics> GroupMetrics { get; set; } = new();
    public string? WinningGroup { get; set; }
    public double? StatisticalSignificance { get; set; }
    public bool IsStatisticallySignificant => StatisticalSignificance >= 0.95;
    public string Recommendation { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metrics for a specific A/B test group
/// </summary>
public class ABTestGroupMetrics
{
    public string GroupName { get; set; } = string.Empty;
    public int TotalDeliveries { get; set; }
    public int UniqueUsers { get; set; }
    public int TotalEngagements { get; set; }
    public double EngagementRate { get; set; }
    public double AverageEngagementScore { get; set; }
    public TimeSpan AverageEngagementLatency { get; set; }
    public Dictionary<string, int> EngagementTypeBreakdown { get; set; } = new();
}

/// <summary>
/// Performance metrics for individual content
/// </summary>
public class ContentPerformanceMetrics
{
    public Guid ContentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDeliveries { get; set; }
    public int UniqueUsers { get; set; }
    public int TotalEngagements { get; set; }
    public double EngagementRate { get; set; }
    public double AverageEngagementScore { get; set; }
    public double AveragePersonalizationScore { get; set; }
    public TimeSpan AverageEngagementLatency { get; set; }
    public Dictionary<string, int> ChannelPerformance { get; set; } = new();
    public Dictionary<string, int> EngagementTypeBreakdown { get; set; } = new();
    public Dictionary<string, double> UserSegmentPerformance { get; set; } = new();
    public ContentPerformanceTrend Trend { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Content performance trend analysis
/// </summary>
public class ContentPerformanceTrend
{
    public string TrendDirection { get; set; } = "Stable"; // Improving, Declining, Stable
    public double TrendMagnitude { get; set; }
    public string TrendConfidence { get; set; } = "Medium"; // High, Medium, Low
    public Dictionary<string, double> FactorAnalysis { get; set; } = new();
}

/// <summary>
/// Time window enumeration for content limits
/// </summary>
public enum ContentLimitTimeWindow
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2
}