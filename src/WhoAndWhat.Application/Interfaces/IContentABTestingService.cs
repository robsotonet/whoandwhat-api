using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service interface for A/B testing of motivational content
/// </summary>
public interface IContentABTestingService
{
    /// <summary>
    /// Creates a new A/B test configuration for content testing
    /// </summary>
    Task<ABTestConfiguration> CreateABTestAsync(string testName, string description, List<Guid> contentIds, Dictionary<string, double>? groupWeights = null, TimeSpan? testDuration = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyzes current A/B test results with statistical significance
    /// </summary>
    Task<ABTestResults> AnalyzeABTestAsync(string testName, bool includeStatisticalAnalysis = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Promotes the winning variant and disables losing variants
    /// </summary>
    Task<bool> PromoteWinnerAsync(string testName, string? winnerGroup = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all currently active A/B tests
    /// </summary>
    Task<List<ABTestSummary>> GetActiveABTestsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops an active A/B test
    /// </summary>
    Task<bool> StopABTestAsync(string testName, CancellationToken cancellationToken = default);
}

/// <summary>
/// A/B test configuration model
/// </summary>
public class ABTestConfiguration
{
    public string TestName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> GroupNames { get; set; } = new();
    public Dictionary<Guid, string> ContentIds { get; set; } = new();
    public Dictionary<string, double> GroupWeights { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int MinimumSampleSize { get; set; }
    public double SignificanceLevel { get; set; }
}

/// <summary>
/// A/B test results model
/// </summary>
public class ABTestResults
{
    public string TestName { get; set; } = string.Empty;
    public Dictionary<string, ABTestGroupResults> GroupResults { get; set; } = new();
    public StatisticalAnalysis? StatisticalAnalysis { get; set; }
    public ABTestGroupResults? Winner { get; set; }
    public ABTestStatus Status { get; set; }
    public DateTime AnalysisDate { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// A/B test group results
/// </summary>
public class ABTestGroupResults
{
    public string GroupName { get; set; } = string.Empty;
    public int SampleSize { get; set; }
    public int UniqueUsers { get; set; }
    public int TotalEngagements { get; set; }
    public double EngagementRate { get; set; }
    public double AverageEngagementScore { get; set; }
    public double ConversionRate { get; set; }
    public double RevenueImpact { get; set; }
    public Dictionary<string, int> EngagementDistribution { get; set; } = new();
    public ConfidenceInterval ConfidenceInterval { get; set; } = new();
}

/// <summary>
/// Statistical analysis results
/// </summary>
public class StatisticalAnalysis
{
    public bool IsSignificant { get; set; }
    public double PValue { get; set; }
    public double EffectSize { get; set; }
    public double ConfidenceLevel { get; set; }
    public string TestType { get; set; } = string.Empty;
    public PowerAnalysis PowerAnalysis { get; set; } = new();
}

/// <summary>
/// Power analysis for statistical tests
/// </summary>
public class PowerAnalysis
{
    public double EstimatedPower { get; set; }
    public string SampleSizeAdequacy { get; set; } = string.Empty;
    public int RecommendedSampleSize { get; set; }
}

/// <summary>
/// Confidence interval for statistical measurements
/// </summary>
public class ConfidenceInterval
{
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
    public double ConfidenceLevel { get; set; } = 0.95;
}

/// <summary>
/// A/B test summary information
/// </summary>
public class ABTestSummary
{
    public string TestName { get; set; } = string.Empty;
    public int GroupCount { get; set; }
    public List<Guid> ContentItems { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ABTestStatus Status { get; set; }
    public int CurrentSampleSize { get; set; }
    public int TargetSampleSize { get; set; }
}

/// <summary>
/// A/B test status enumeration
/// </summary>
public enum ABTestStatus
{
    Running = 0,
    SignificantResult = 1,
    InsufficientData = 2,
    NoData = 3,
    Stopped = 4,
    Error = 5
}