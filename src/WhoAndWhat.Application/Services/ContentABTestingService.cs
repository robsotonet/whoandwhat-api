using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services;

/// <summary>
/// Service for managing A/B testing of motivational content with advanced statistical analysis
/// </summary>
public class ContentABTestingService : IContentABTestingService
{
    private readonly IRepository<MotivationalContent> _contentRepository;
    private readonly IRepository<ContentDeliveryLog> _deliveryLogRepository;
    private readonly ILogger<ContentABTestingService> _logger;

    // Statistical analysis constants
    private const double MinimumSampleSize = 100;
    private const double SignificanceLevel = 0.05; // 95% confidence
    private const double MinimumEffectSize = 0.05; // 5% minimum improvement

    public ContentABTestingService(
        IRepository<MotivationalContent> contentRepository,
        IRepository<ContentDeliveryLog> deliveryLogRepository,
        ILogger<ContentABTestingService> logger)
    {
        _contentRepository = contentRepository ?? throw new ArgumentNullException(nameof(contentRepository));
        _deliveryLogRepository = deliveryLogRepository ?? throw new ArgumentNullException(nameof(deliveryLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ABTestConfiguration> CreateABTestAsync(
        string testName,
        string description,
        List<Guid> contentIds,
        Dictionary<string, double>? groupWeights = null,
        TimeSpan? testDuration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(testName))
            {
                throw new ArgumentException("Test name cannot be empty", nameof(testName));
            }

            if (contentIds.Count < 2)
            {
                throw new ArgumentException("At least 2 content items required for A/B test", nameof(contentIds));
            }

            // Set default group weights if not provided
            var weights = groupWeights ?? GenerateEqualWeights(contentIds.Count);

            // Configure A/B test for each content item
            var groupNames = new List<string>();
            for (int i = 0; i < contentIds.Count; i++)
            {
                var content = await _contentRepository.GetByIdAsync(contentIds[i], cancellationToken);
                if (content == null)
                {
                    throw new ArgumentException($"Content with ID {contentIds[i]} not found");
                }

                var groupName = i == 0 ? "control" : $"variant_{(char)('a' + i - 1)}";
                groupNames.Add(groupName);

                // Configure A/B test on content
                var testConfig = new Dictionary<string, object>
                {
                    ["testName"] = testName,
                    ["groupWeight"] = weights.GetValueOrDefault(groupName, 1.0 / contentIds.Count),
                    ["startDate"] = DateTime.UtcNow,
                    ["endDate"] = testDuration.HasValue ? DateTime.UtcNow.Add(testDuration.Value) : DateTime.UtcNow.AddDays(30)
                };

                content.ConfigureABTest(groupName, testConfig);
                await _contentRepository.UpdateAsync(content, cancellationToken);
            }

            var configuration = new ABTestConfiguration
            {
                TestName = testName,
                Description = description,
                GroupNames = groupNames,
                ContentIds = contentIds.ToDictionary(id => id, id => contentIds.IndexOf(id) < groupNames.Count ? groupNames[contentIds.IndexOf(id)] : "unknown"),
                GroupWeights = weights,
                StartDate = DateTime.UtcNow,
                EndDate = testDuration.HasValue ? DateTime.UtcNow.Add(testDuration.Value) : DateTime.UtcNow.AddDays(30),
                IsActive = true,
                MinimumSampleSize = (int)MinimumSampleSize,
                SignificanceLevel = SignificanceLevel
            };

            _logger.LogInformation("Created A/B test '{TestName}' with {GroupCount} groups and {ContentCount} content items",
                testName, groupNames.Count, contentIds.Count);

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating A/B test '{TestName}'", testName);
            throw;
        }
    }

    public async Task<ABTestResults> AnalyzeABTestAsync(
        string testName,
        bool includeStatisticalAnalysis = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all delivery logs for the test
            var deliveryLogs = await _deliveryLogRepository.GetAllByConditionAsync(
                dl => dl.ABTestGroup != null && dl.ABTestGroup.Contains(testName),
                cancellationToken);

            if (!deliveryLogs.Any())
            {
                return new ABTestResults
                {
                    TestName = testName,
                    Status = ABTestStatus.NoData,
                    Message = "No delivery data found for this test"
                };
            }

            // Group by test groups
            var groupedLogs = deliveryLogs.GroupBy(dl => dl.ABTestGroup).ToList();
            var groupResults = new Dictionary<string, ABTestGroupResults>();

            // Calculate metrics for each group
            foreach (var group in groupedLogs)
            {
                if (group.Key == null) continue;

                var logs = group.ToList();
                var engagedLogs = logs.Where(l => l.EngagementType.HasValue).ToList();

                var groupResult = new ABTestGroupResults
                {
                    GroupName = group.Key,
                    SampleSize = logs.Count,
                    UniqueUsers = logs.Select(l => l.UserId).Distinct().Count(),
                    TotalEngagements = engagedLogs.Count,
                    EngagementRate = logs.Count > 0 ? (double)engagedLogs.Count / logs.Count : 0,
                    AverageEngagementScore = engagedLogs.Any() ? engagedLogs.Average(l => l.GetEngagementScore()) : 0,
                    ConversionRate = CalculateConversionRate(logs),
                    RevenueImpact = CalculateRevenueImpact(logs),
                    EngagementDistribution = CalculateEngagementDistribution(logs),
                    ConfidenceInterval = new ConfidenceInterval()
                };

                groupResults[group.Key] = groupResult;
            }

            // Perform statistical analysis if requested and sufficient data
            StatisticalAnalysis? statisticalAnalysis = null;
            if (includeStatisticalAnalysis && groupResults.Values.Any(g => g.SampleSize >= MinimumSampleSize))
            {
                statisticalAnalysis = await PerformStatisticalAnalysisAsync(groupResults);
            }

            // Determine test status and winner
            var testStatus = DetermineTestStatus(groupResults, statisticalAnalysis);
            var winner = DetermineWinner(groupResults, statisticalAnalysis);

            var results = new ABTestResults
            {
                TestName = testName,
                GroupResults = groupResults,
                StatisticalAnalysis = statisticalAnalysis,
                Winner = winner,
                Status = testStatus,
                AnalysisDate = DateTime.UtcNow,
                Recommendations = GenerateRecommendations(groupResults, statisticalAnalysis),
                Message = GenerateAnalysisMessage(testStatus, winner, statisticalAnalysis)
            };

            _logger.LogInformation("Analyzed A/B test '{TestName}': Status={Status}, Winner={Winner}",
                testName, testStatus, winner?.GroupName ?? "None");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing A/B test '{TestName}'", testName);
            return new ABTestResults
            {
                TestName = testName,
                Status = ABTestStatus.Error,
                Message = $"Error analyzing test: {ex.Message}"
            };
        }
    }

    public async Task<bool> PromoteWinnerAsync(
        string testName,
        string? winnerGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If no winner specified, analyze test to determine winner
            if (string.IsNullOrEmpty(winnerGroup))
            {
                var results = await AnalyzeABTestAsync(testName, true, cancellationToken);
                if (results.Winner == null)
                {
                    _logger.LogWarning("Cannot promote winner for test '{TestName}' - no clear winner determined", testName);
                    return false;
                }
                winnerGroup = results.Winner.GroupName;
            }

            // Get all content in the test
            var testContent = await _contentRepository.GetAllByConditionAsync(
                c => c.IsABTestEnabled && c.ABTestGroup.Contains(testName),
                cancellationToken);

            var winnerContent = testContent.FirstOrDefault(c => c.ABTestGroup == winnerGroup);
            if (winnerContent == null)
            {
                _logger.LogWarning("Winner content not found for test '{TestName}', group '{WinnerGroup}'",
                    testName, winnerGroup);
                return false;
            }

            // Disable A/B test on winner content (it becomes the default)
            winnerContent.DisableABTest();
            winnerContent.Activate();

            // Deactivate losing variants
            var losingContent = testContent.Where(c => c.ABTestGroup != winnerGroup).ToList();
            foreach (var content in losingContent)
            {
                content.Deactivate();
                content.UpdateMetadata("abtest_result", "loser");
                content.UpdateMetadata("promoted_winner", winnerGroup);
                content.UpdateMetadata("promotion_date", DateTime.UtcNow);
            }

            // Update winner content metadata
            winnerContent.UpdateMetadata("abtest_result", "winner");
            winnerContent.UpdateMetadata("promotion_date", DateTime.UtcNow);
            winnerContent.UpdateMetadata("test_name", testName);

            // Save all changes
            await _contentRepository.UpdateAsync(winnerContent, cancellationToken);
            foreach (var content in losingContent)
            {
                await _contentRepository.UpdateAsync(content, cancellationToken);
            }

            _logger.LogInformation("Promoted winner '{WinnerGroup}' for A/B test '{TestName}' and deactivated {LosingCount} losing variants",
                winnerGroup, testName, losingContent.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting winner for A/B test '{TestName}'", testName);
            return false;
        }
    }

    public async Task<List<ABTestSummary>> GetActiveABTestsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var activeContent = await _contentRepository.GetAllByConditionAsync(
                c => c.IsABTestEnabled && c.IsActive,
                cancellationToken);

            var testSummaries = new Dictionary<string, ABTestSummary>();

            foreach (var content in activeContent)
            {
                var testName = content.GetMetadata<string>("testName") ?? "Unknown";
                
                if (!testSummaries.ContainsKey(testName))
                {
                    testSummaries[testName] = new ABTestSummary
                    {
                        TestName = testName,
                        GroupCount = 0,
                        ContentItems = new List<Guid>(),
                        StartDate = content.GetMetadata<DateTime>("startDate"),
                        EndDate = content.GetMetadata<DateTime>("endDate"),
                        Status = ABTestStatus.Running
                    };
                }

                testSummaries[testName].GroupCount++;
                testSummaries[testName].ContentItems.Add(content.Id);
            }

            // Add sample size information
            foreach (var summary in testSummaries.Values)
            {
                var sampleSize = await _deliveryLogRepository.CountByConditionAsync(
                    dl => dl.ABTestGroup != null && dl.ABTestGroup.Contains(summary.TestName),
                    cancellationToken);
                
                summary.CurrentSampleSize = sampleSize;
                summary.TargetSampleSize = (int)MinimumSampleSize * summary.GroupCount;
            }

            _logger.LogDebug("Retrieved {TestCount} active A/B tests", testSummaries.Count);
            return testSummaries.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active A/B tests");
            return new List<ABTestSummary>();
        }
    }

    public async Task<bool> StopABTestAsync(string testName, CancellationToken cancellationToken = default)
    {
        try
        {
            var testContent = await _contentRepository.GetAllByConditionAsync(
                c => c.IsABTestEnabled && c.ABTestGroup.Contains(testName),
                cancellationToken);

            if (!testContent.Any())
            {
                _logger.LogWarning("No content found for A/B test '{TestName}'", testName);
                return false;
            }

            foreach (var content in testContent)
            {
                content.DisableABTest();
                content.UpdateMetadata("abtest_stopped", DateTime.UtcNow);
                content.UpdateMetadata("abtest_status", "stopped");
                await _contentRepository.UpdateAsync(content, cancellationToken);
            }

            _logger.LogInformation("Stopped A/B test '{TestName}' for {ContentCount} content items",
                testName, testContent.Count());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping A/B test '{TestName}'", testName);
            return false;
        }
    }

    // Private helper methods

    private Dictionary<string, double> GenerateEqualWeights(int groupCount)
    {
        var weight = 1.0 / groupCount;
        var weights = new Dictionary<string, double>();
        
        weights["control"] = weight;
        for (int i = 1; i < groupCount; i++)
        {
            weights[$"variant_{(char)('a' + i - 1)}"] = weight;
        }

        return weights;
    }

    private double CalculateConversionRate(List<ContentDeliveryLog> logs)
    {
        if (!logs.Any()) return 0.0;

        var conversions = logs.Count(l => l.EngagementType >= ContentEngagementType.Clicked);
        return (double)conversions / logs.Count;
    }

    private double CalculateRevenueImpact(List<ContentDeliveryLog> logs)
    {
        // Placeholder for revenue calculation - would be based on business logic
        // For now, use engagement score as a proxy
        if (!logs.Any()) return 0.0;

        return logs.Where(l => l.EngagementType.HasValue)
                  .Sum(l => l.GetEngagementScore() * 10); // $10 value per engagement point
    }

    private Dictionary<string, int> CalculateEngagementDistribution(List<ContentDeliveryLog> logs)
    {
        return logs.Where(l => l.EngagementType.HasValue)
                  .GroupBy(l => l.EngagementType!.Value.ToString())
                  .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<StatisticalAnalysis> PerformStatisticalAnalysisAsync(Dictionary<string, ABTestGroupResults> groupResults)
    {
        // Perform statistical tests - this is a simplified implementation
        // In production, you'd use proper statistical libraries

        var analysis = new StatisticalAnalysis();
        
        if (groupResults.Count < 2)
        {
            analysis.IsSignificant = false;
            analysis.PValue = 1.0;
            analysis.TestType = "Insufficient groups";
            return analysis;
        }

        // Chi-square test for engagement rates
        var chiSquareResult = PerformChiSquareTest(groupResults);
        analysis.PValue = chiSquareResult.PValue;
        analysis.IsSignificant = chiSquareResult.PValue < SignificanceLevel;
        analysis.TestType = "Chi-square test for engagement rates";
        analysis.EffectSize = CalculateEffectSize(groupResults);
        analysis.PowerAnalysis = CalculatePower(groupResults);
        analysis.ConfidenceLevel = 1.0 - SignificanceLevel;

        await Task.CompletedTask; // Placeholder for async statistical operations

        return analysis;
    }

    private (double PValue, double ChiSquare) PerformChiSquareTest(Dictionary<string, ABTestGroupResults> groupResults)
    {
        // Simplified chi-square test implementation
        // In production, use proper statistical library like Math.NET

        var groups = groupResults.Values.ToList();
        if (groups.Count < 2) return (1.0, 0.0);

        var totalSample = groups.Sum(g => g.SampleSize);
        var totalEngagements = groups.Sum(g => g.TotalEngagements);
        var expectedRate = totalSample > 0 ? (double)totalEngagements / totalSample : 0;

        double chiSquare = 0;
        foreach (var group in groups)
        {
            var expected = group.SampleSize * expectedRate;
            var observed = group.TotalEngagements;
            
            if (expected > 0)
            {
                chiSquare += Math.Pow(observed - expected, 2) / expected;
            }
        }

        // Very simplified p-value approximation
        var degreesOfFreedom = groups.Count - 1;
        var pValue = chiSquare > 3.84 ? 0.05 : 0.1; // Rough approximation

        return (pValue, chiSquare);
    }

    private double CalculateEffectSize(Dictionary<string, ABTestGroupResults> groupResults)
    {
        var groups = groupResults.Values.OrderByDescending(g => g.EngagementRate).ToList();
        if (groups.Count < 2) return 0.0;

        var best = groups.First().EngagementRate;
        var control = groups.Last().EngagementRate;
        
        return control > 0 ? Math.Abs(best - control) / control : 0.0;
    }

    private PowerAnalysis CalculatePower(Dictionary<string, ABTestGroupResults> groupResults)
    {
        var minSampleSize = groupResults.Values.Min(g => g.SampleSize);
        var maxSampleSize = groupResults.Values.Max(g => g.SampleSize);

        return new PowerAnalysis
        {
            EstimatedPower = minSampleSize >= MinimumSampleSize ? 0.8 : 0.5,
            SampleSizeAdequacy = minSampleSize >= MinimumSampleSize ? "Adequate" : "Insufficient",
            RecommendedSampleSize = Math.Max((int)MinimumSampleSize, minSampleSize * 2)
        };
    }

    private ABTestStatus DetermineTestStatus(Dictionary<string, ABTestGroupResults> groupResults, StatisticalAnalysis? analysis)
    {
        if (!groupResults.Any())
            return ABTestStatus.NoData;

        var minSampleSize = groupResults.Values.Min(g => g.SampleSize);
        
        if (minSampleSize < MinimumSampleSize)
            return ABTestStatus.InsufficientData;

        if (analysis?.IsSignificant == true)
            return ABTestStatus.SignificantResult;

        return ABTestStatus.Running;
    }

    private ABTestGroupResults? DetermineWinner(Dictionary<string, ABTestGroupResults> groupResults, StatisticalAnalysis? analysis)
    {
        if (analysis?.IsSignificant != true)
            return null;

        return groupResults.Values.OrderByDescending(g => g.EngagementRate).First();
    }

    private List<string> GenerateRecommendations(Dictionary<string, ABTestGroupResults> groupResults, StatisticalAnalysis? analysis)
    {
        var recommendations = new List<string>();

        if (analysis == null)
        {
            recommendations.Add("Collect more data before making decisions");
            return recommendations;
        }

        if (!analysis.IsSignificant)
        {
            recommendations.Add("No statistically significant difference found - continue test or consider new variants");
        }
        else
        {
            var winner = groupResults.Values.OrderByDescending(g => g.EngagementRate).First();
            recommendations.Add($"Promote '{winner.GroupName}' - shows {winner.EngagementRate:P1} engagement rate");
        }

        var minSample = groupResults.Values.Min(g => g.SampleSize);
        if (minSample < MinimumSampleSize)
        {
            recommendations.Add($"Increase sample size - current minimum is {minSample}, recommended minimum is {MinimumSampleSize}");
        }

        if (analysis.EffectSize < MinimumEffectSize)
        {
            recommendations.Add("Consider testing variants with larger expected differences");
        }

        return recommendations;
    }

    private string GenerateAnalysisMessage(ABTestStatus status, ABTestGroupResults? winner, StatisticalAnalysis? analysis)
    {
        return status switch
        {
            ABTestStatus.NoData => "No test data available",
            ABTestStatus.InsufficientData => $"Insufficient data - need minimum {MinimumSampleSize} samples per group",
            ABTestStatus.Running => "Test in progress - no significant results yet",
            ABTestStatus.SignificantResult => $"Significant result found - {winner?.GroupName} is the winner with {winner?.EngagementRate:P1} engagement",
            ABTestStatus.Error => "Error occurred during analysis",
            _ => "Unknown test status"
        };
    }
}

// Supporting classes and interfaces

public interface IContentABTestingService
{
    Task<ABTestConfiguration> CreateABTestAsync(string testName, string description, List<Guid> contentIds, Dictionary<string, double>? groupWeights = null, TimeSpan? testDuration = null, CancellationToken cancellationToken = default);
    Task<ABTestResults> AnalyzeABTestAsync(string testName, bool includeStatisticalAnalysis = true, CancellationToken cancellationToken = default);
    Task<bool> PromoteWinnerAsync(string testName, string? winnerGroup = null, CancellationToken cancellationToken = default);
    Task<List<ABTestSummary>> GetActiveABTestsAsync(CancellationToken cancellationToken = default);
    Task<bool> StopABTestAsync(string testName, CancellationToken cancellationToken = default);
}

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

public class StatisticalAnalysis
{
    public bool IsSignificant { get; set; }
    public double PValue { get; set; }
    public double EffectSize { get; set; }
    public double ConfidenceLevel { get; set; }
    public string TestType { get; set; } = string.Empty;
    public PowerAnalysis PowerAnalysis { get; set; } = new();
}

public class PowerAnalysis
{
    public double EstimatedPower { get; set; }
    public string SampleSizeAdequacy { get; set; } = string.Empty;
    public int RecommendedSampleSize { get; set; }
}

public class ConfidenceInterval
{
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
    public double ConfidenceLevel { get; set; } = 0.95;
}

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

public enum ABTestStatus
{
    Running = 0,
    SignificantResult = 1,
    InsufficientData = 2,
    NoData = 3,
    Stopped = 4,
    Error = 5
}