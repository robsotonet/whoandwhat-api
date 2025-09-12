using Microsoft.Extensions.Logging;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Service for aggregating and summarizing analytics metrics across different time periods
/// </summary>
public class MetricsAggregationService : IMetricsAggregationService
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly IAnalyticsCalculationService _calculationService;
    private readonly IProductivityStreakService _streakService;
    private readonly ILogger<MetricsAggregationService> _logger;

    public MetricsAggregationService(
        IAnalyticsRepository analyticsRepository,
        IAnalyticsCalculationService calculationService,
        IProductivityStreakService streakService,
        ILogger<MetricsAggregationService> logger)
    {
        _analyticsRepository = analyticsRepository;
        _calculationService = calculationService;
        _streakService = streakService;
        _logger = logger;
    }

    public async Task<WeeklyMetricsSummary> AggregateWeeklyMetricsAsync(Guid userId, DateTime weekStartDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Aggregating weekly metrics for user {UserId} week starting {WeekStart}", userId, weekStartDate);

            var weeklyMetrics = await _calculationService.CalculateWeeklyMetricsAsync(userId, weekStartDate, cancellationToken);
            var weekEndDate = weekStartDate.AddDays(6);

            // Calculate previous week for comparison
            var previousWeekStart = weekStartDate.AddDays(-7);
            var previousWeekMetrics = await _calculationService.CalculateWeeklyMetricsAsync(userId, previousWeekStart, cancellationToken);

            var currentTotal = Convert.ToInt32(weeklyMetrics["totalCompletedTasks"]);
            var previousTotal = Convert.ToInt32(previousWeekMetrics["totalCompletedTasks"]);
            var weekOverWeekChange = previousTotal > 0 ? ((double)(currentTotal - previousTotal) / previousTotal) * 100 : 0.0;

            // Generate achievements
            var achievements = GenerateWeeklyAchievements(weeklyMetrics);

            // Determine trend
            var trend = weekOverWeekChange switch
            {
                > 10 => "Significantly Improving",
                > 0 => "Improving",
                < -10 => "Declining",
                < 0 => "Slightly Declining",
                _ => "Stable"
            };

            return new WeeklyMetricsSummary
            {
                UserId = userId,
                WeekStartDate = weekStartDate.Date,
                WeekEndDate = weekEndDate.Date,
                TotalTasksCompleted = currentTotal,
                TotalTasksCreated = Convert.ToInt32(weeklyMetrics["totalCreatedTasks"]),
                WeeklyCompletionRate = Convert.ToDouble(weeklyMetrics["weeklyCompletionRate"]),
                ProductiveDays = Convert.ToInt32(weeklyMetrics["productiveDays"]),
                AverageEfficiencyScore = Convert.ToDouble(weeklyMetrics["averageEfficiencyScore"]),
                TotalProductiveHours = Convert.ToInt32(weeklyMetrics["totalProductiveHours"]),
                CategoryBreakdown = ExtractCategoryBreakdown(weeklyMetrics),
                PriorityBreakdown = ExtractPriorityBreakdown(weeklyMetrics),
                TopAchievements = achievements,
                WeeklyTrend = trend,
                WeekOverWeekChange = weekOverWeekChange
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating weekly metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<MonthlyMetricsSummary> AggregateMonthlyMetricsAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Aggregating monthly metrics for user {UserId} for {Year}-{Month}", userId, year, month);

            var monthlyMetrics = await _calculationService.CalculateMonthlyMetricsAsync(userId, year, month, cancellationToken);
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            // Generate weekly summaries for the month
            var weeklySummaries = new List<WeeklyMetricsSummary>();
            var currentWeekStart = GetWeekStart(monthStart);

            while (currentWeekStart <= monthEnd)
            {
                if (currentWeekStart.Month == month || currentWeekStart.AddDays(6).Month == month)
                {
                    var weekSummary = await AggregateWeeklyMetricsAsync(userId, currentWeekStart, cancellationToken);
                    weeklySummaries.Add(weekSummary);
                }
                currentWeekStart = currentWeekStart.AddDays(7);
            }

            // Calculate previous month for comparison
            var previousMonth = month == 1 ? 12 : month - 1;
            var previousYear = month == 1 ? year - 1 : year;
            var previousMonthMetrics = await _calculationService.CalculateMonthlyMetricsAsync(userId, previousYear, previousMonth, cancellationToken);

            var currentCompleted = Convert.ToInt32(monthlyMetrics["totalTasksCompleted"]);
            var previousCompleted = Convert.ToInt32(previousMonthMetrics["totalTasksCompleted"]);
            var monthOverMonthChange = previousCompleted > 0 ? ((double)(currentCompleted - previousCompleted) / previousCompleted) * 100 : 0.0;

            var trend = monthOverMonthChange switch
            {
                > 15 => "Strong Growth",
                > 5 => "Growing",
                < -15 => "Sharp Decline",
                < -5 => "Declining",
                _ => "Stable"
            };

            var highlights = GenerateMonthlyHighlights(monthlyMetrics, weeklySummaries);

            return new MonthlyMetricsSummary
            {
                UserId = userId,
                Year = year,
                Month = month,
                TotalTasksCompleted = currentCompleted,
                TotalTasksCreated = Convert.ToInt32(monthlyMetrics["totalTasksCreated"]),
                MonthlyCompletionRate = Convert.ToDouble(monthlyMetrics["monthlyCompletionRate"]),
                ProductiveDays = weeklySummaries.Sum(w => w.ProductiveDays),
                AverageEfficiencyScore = Convert.ToDouble(monthlyMetrics["monthlyEfficiencyScore"]),
                TotalProductiveHours = weeklySummaries.Sum(w => w.TotalProductiveHours),
                CategoryPerformance = (Dictionary<string, CategoryPerformance>)monthlyMetrics["categoryPerformance"],
                PriorityPerformance = (Dictionary<string, PriorityPerformance>)monthlyMetrics["priorityPerformance"],
                WeeklySummaries = weeklySummaries,
                MonthlyTrend = trend,
                MonthOverMonthChange = monthOverMonthChange,
                MonthlyHighlights = highlights
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating monthly metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<QuarterlyMetricsSummary> AggregateQuarterlyMetricsAsync(Guid userId, int year, int quarter, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Aggregating quarterly metrics for user {UserId} for Q{Quarter} {Year}", userId, quarter, year);

            var quarterStartMonth = (quarter - 1) * 3 + 1;
            var monthlySummaries = new List<MonthlyMetricsSummary>();

            // Aggregate monthly summaries for the quarter
            for (int monthOffset = 0; monthOffset < 3; monthOffset++)
            {
                var currentMonth = quarterStartMonth + monthOffset;
                var monthSummary = await AggregateMonthlyMetricsAsync(userId, year, currentMonth, cancellationToken);
                monthlySummaries.Add(monthSummary);
            }

            var totalCompleted = monthlySummaries.Sum(m => m.TotalTasksCompleted);
            var totalCreated = monthlySummaries.Sum(m => m.TotalTasksCreated);
            var quarterlyCompletionRate = totalCreated > 0 ? (double)totalCompleted / totalCreated * 100 : 0.0;
            var avgEfficiency = monthlySummaries.Average(m => m.AverageEfficiencyScore);
            var productiveDays = monthlySummaries.Sum(m => m.ProductiveDays);

            // Calculate quarter-over-quarter change
            var previousQuarter = quarter == 1 ? 4 : quarter - 1;
            var previousYear = quarter == 1 ? year - 1 : year;
            var previousQuarterData = await AggregateQuarterlyMetricsAsync(userId, previousYear, previousQuarter, cancellationToken);
            var quarterOverQuarterChange = previousQuarterData.TotalTasksCompleted > 0
                ? ((double)(totalCompleted - previousQuarterData.TotalTasksCompleted) / previousQuarterData.TotalTasksCompleted) * 100
                : 0.0;

            var trend = quarterOverQuarterChange switch
            {
                > 20 => "Exceptional Growth",
                > 10 => "Strong Growth",
                > 0 => "Positive Growth",
                < -20 => "Significant Decline",
                < -10 => "Declining",
                _ => "Stable Performance"
            };

            var achievements = GenerateQuarterlyAchievements(monthlySummaries, totalCompleted);
            var goals = GenerateQuarterlyGoals(userId, quarter, avgEfficiency);

            return new QuarterlyMetricsSummary
            {
                UserId = userId,
                Year = year,
                Quarter = quarter,
                TotalTasksCompleted = totalCompleted,
                TotalTasksCreated = totalCreated,
                QuarterlyCompletionRate = quarterlyCompletionRate,
                ProductiveDays = productiveDays,
                AverageEfficiencyScore = avgEfficiency,
                MonthlySummaries = monthlySummaries,
                QuarterlyTrend = trend,
                QuarterOverQuarterChange = quarterOverQuarterChange,
                QuarterlyGoals = goals,
                QuarterlyAchievements = achievements
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating quarterly metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<AnnualMetricsSummary> AggregateAnnualMetricsAsync(Guid userId, int year, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Aggregating annual metrics for user {UserId} for year {Year}", userId, year);

            var quarterlySummaries = new List<QuarterlyMetricsSummary>();

            // Aggregate quarterly summaries for the year
            for (int quarter = 1; quarter <= 4; quarter++)
            {
                var quarterSummary = await AggregateQuarterlyMetricsAsync(userId, year, quarter, cancellationToken);
                quarterlySummaries.Add(quarterSummary);
            }

            var totalCompleted = quarterlySummaries.Sum(q => q.TotalTasksCompleted);
            var totalCreated = quarterlySummaries.Sum(q => q.TotalTasksCreated);
            var annualCompletionRate = totalCreated > 0 ? (double)totalCompleted / totalCreated * 100 : 0.0;
            var avgEfficiency = quarterlySummaries.Average(q => q.AverageEfficiencyScore);
            var productiveDays = quarterlySummaries.Sum(q => q.ProductiveDays);

            // Calculate year-over-year change
            var previousYearData = await AggregateAnnualMetricsAsync(userId, year - 1, cancellationToken);
            var yearOverYearChange = previousYearData.TotalTasksCompleted > 0
                ? ((double)(totalCompleted - previousYearData.TotalTasksCompleted) / previousYearData.TotalTasksCompleted) * 100
                : 0.0;

            var trend = DetermineAnnualTrend(quarterlySummaries);
            var grade = CalculateProductivityGrade(annualCompletionRate, avgEfficiency, productiveDays);
            var highlights = GenerateYearHighlights(quarterlySummaries, totalCompleted);
            var goals = await GenerateAnnualGoals(userId, year, cancellationToken);

            return new AnnualMetricsSummary
            {
                UserId = userId,
                Year = year,
                TotalTasksCompleted = totalCompleted,
                TotalTasksCreated = totalCreated,
                AnnualCompletionRate = annualCompletionRate,
                ProductiveDays = productiveDays,
                AverageEfficiencyScore = avgEfficiency,
                QuarterlySummaries = quarterlySummaries,
                AnnualTrend = trend,
                YearOverYearChange = yearOverYearChange,
                AnnualGoals = goals,
                YearHighlights = highlights,
                ProductivityGrade = grade
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating annual metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<AnalyticsSnapshot> CreateSnapshotAsync(Guid userId, DateTime date, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating {SnapshotType} analytics snapshot for user {UserId} on {Date}", snapshotType, userId, date);

            var metricsData = new Dictionary<string, object>();

            switch (snapshotType)
            {
                case SnapshotType.Daily:
                    var dailyMetrics = await _calculationService.CalculateDailyMetricsAsync(userId, date, cancellationToken);
                    metricsData = ExtractMetricsData(dailyMetrics);
                    break;

                case SnapshotType.Weekly:
                    var weekStart = GetWeekStart(date);
                    var weeklyMetrics = await AggregateWeeklyMetricsAsync(userId, weekStart, cancellationToken);
                    metricsData = ExtractMetricsData(weeklyMetrics);
                    break;

                case SnapshotType.Monthly:
                    var monthlyMetrics = await AggregateMonthlyMetricsAsync(userId, date.Year, date.Month, cancellationToken);
                    metricsData = ExtractMetricsData(monthlyMetrics);
                    break;

                case SnapshotType.Quarterly:
                    var quarter = (date.Month - 1) / 3 + 1;
                    var quarterlyMetrics = await AggregateQuarterlyMetricsAsync(userId, date.Year, quarter, cancellationToken);
                    metricsData = ExtractMetricsData(quarterlyMetrics);
                    break;

                case SnapshotType.Annual:
                    var annualMetrics = await AggregateAnnualMetricsAsync(userId, date.Year, cancellationToken);
                    metricsData = ExtractMetricsData(annualMetrics);
                    break;

                case SnapshotType.Milestone:
                    // Custom milestone snapshot - combine multiple metrics
                    metricsData = await CreateMilestoneSnapshot(userId, date, cancellationToken);
                    break;
            }

            var snapshot = AnalyticsSnapshot.Create(userId, date, snapshotType, metricsData);

            // Add comparison with previous snapshot of same type
            var previousSnapshots = await _analyticsRepository.GetAnalyticsSnapshotsAsync(userId, snapshotType, 2, cancellationToken);
            var previousSnapshot = previousSnapshots.FirstOrDefault(s => s.SnapshotDate < date);
            snapshot.AddComparisonData(previousSnapshot);

            await _analyticsRepository.AddAnalyticsSnapshotAsync(snapshot, cancellationToken);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating analytics snapshot for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ComparativeMetrics> AggregateComparativeMetricsAsync(List<Guid> userIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Aggregating comparative metrics for {UserCount} users from {StartDate} to {EndDate}",
                userIds.Count, startDate, endDate);

            var userSummaries = new Dictionary<Guid, UserMetricsSummary>();

            foreach (var userId in userIds)
            {
                var efficiency = await _calculationService.CalculateEfficiencyScoreAsync(userId, startDate, endDate, cancellationToken);
                var streakStats = await _streakService.GetStreakStatisticsAsync(userId, cancellationToken);

                // This would typically involve more detailed calculations
                var summary = new UserMetricsSummary
                {
                    UserId = userId,
                    TasksCompleted = 50, // Placeholder - would calculate from actual data
                    CompletionRate = 75.0, // Placeholder
                    EfficiencyScore = efficiency,
                    StreakDays = streakStats.CurrentBestStreakDays,
                    ProductiveDays = 20, // Placeholder
                    CategoryScores = new Dictionary<string, double>()
                };

                userSummaries[userId] = summary;
            }

            // Calculate averages
            var averageMetrics = new UserMetricsSummary
            {
                TasksCompleted = (int)userSummaries.Values.Average(s => s.TasksCompleted),
                CompletionRate = userSummaries.Values.Average(s => s.CompletionRate),
                EfficiencyScore = userSummaries.Values.Average(s => s.EfficiencyScore),
                StreakDays = (int)userSummaries.Values.Average(s => s.StreakDays),
                ProductiveDays = (int)userSummaries.Values.Average(s => s.ProductiveDays)
            };

            // Generate rankings
            var rankings = GenerateRankings(userSummaries);

            return new ComparativeMetrics
            {
                UserIds = userIds,
                StartDate = startDate,
                EndDate = endDate,
                UserSummaries = userSummaries,
                AverageMetrics = averageMetrics,
                Rankings = rankings,
                GroupInsights = GenerateGroupInsights(userSummaries)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating comparative metrics");
            throw;
        }
    }

    public async Task<RollingAverages> CalculateRollingAveragesAsync(Guid userId, int windowDays, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating {WindowDays}-day rolling averages for user {UserId} ending {EndDate}",
                windowDays, userId, endDate);

            var completionRates = new Dictionary<DateTime, double>();
            var efficiencyScores = new Dictionary<DateTime, double>();
            var tasksPerDay = new Dictionary<DateTime, double>();
            var productiveHours = new Dictionary<DateTime, double>();

            // Calculate rolling averages for each day in the analysis period
            var analysisStart = endDate.AddDays(-30); // Analyze last 30 days
            var currentDate = analysisStart;

            while (currentDate <= endDate)
            {
                var windowStart = currentDate.AddDays(-windowDays + 1);
                var windowMetrics = new List<TaskMetrics>();

                // Collect metrics for the rolling window
                for (var d = windowStart; d <= currentDate; d = d.AddDays(1))
                {
                    var dayMetrics = await _calculationService.CalculateDailyMetricsAsync(userId, d, cancellationToken);
                    windowMetrics.Add(dayMetrics);
                }

                // Calculate averages for the window
                completionRates[currentDate] = windowMetrics.Average(m => m.GetCompletionRate());
                efficiencyScores[currentDate] = windowMetrics.Average(m => m.EfficiencyScore);
                tasksPerDay[currentDate] = windowMetrics.Average(m => m.CompletedTasks);
                productiveHours[currentDate] = windowMetrics.Average(m => m.ProductiveHours);

                currentDate = currentDate.AddDays(1);
            }

            // Analyze trend
            var trendDirection = CalculateTrendDirection(efficiencyScores);
            var trendStrength = CalculateTrendStrength(efficiencyScores);

            return new RollingAverages
            {
                UserId = userId,
                WindowDays = windowDays,
                CompletionRateAverage = completionRates,
                EfficiencyScoreAverage = efficiencyScores,
                TasksPerDayAverage = tasksPerDay,
                ProductiveHoursAverage = productiveHours,
                TrendDirection = trendDirection,
                TrendStrength = trendStrength
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating rolling averages for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PatternAnalysis> AnalyzePatternsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing patterns for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

            var patterns = new List<ProductivityPattern>();
            var anomalies = new List<Anomaly>();
            var cyclicalTrends = new Dictionary<string, double>();
            var behavioralInsights = new Dictionary<string, object>();

            // Analyze weekly patterns
            patterns.AddRange(await IdentifyWeeklyPatterns(userId, startDate, endDate, cancellationToken));

            // Analyze monthly patterns  
            patterns.AddRange(await IdentifyMonthlyPatterns(userId, startDate, endDate, cancellationToken));

            // Detect anomalies
            anomalies.AddRange(await DetectAnomalies(userId, startDate, endDate, cancellationToken));

            // Analyze cyclical trends
            cyclicalTrends = await IdentifyCyclicalTrends(userId, startDate, endDate, cancellationToken);

            // Generate behavioral insights
            behavioralInsights = await GenerateBehavioralInsights(userId, patterns, anomalies, cancellationToken);

            // Generate recommendations based on patterns
            var recommendations = GeneratePatternRecommendations(patterns, anomalies);

            return new PatternAnalysis
            {
                UserId = userId,
                AnalysisStartDate = startDate,
                AnalysisEndDate = endDate,
                IdentifiedPatterns = patterns,
                DetectedAnomalies = anomalies,
                CyclicalTrends = cyclicalTrends,
                BehavioralInsights = behavioralInsights,
                Recommendations = recommendations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing patterns for user {UserId}", userId);
            throw;
        }
    }

    public async Task<DashboardKPIs> CalculateKPIsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating KPIs for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

            var currentKPIs = new Dictionary<string, KPIValue>();
            var previousPeriodStart = startDate.AddDays(-(endDate - startDate).Days - 1);
            var previousKPIs = new Dictionary<string, KPIValue>();
            var changes = new Dictionary<string, double>();
            var alerts = new List<string>();
            var trends = new Dictionary<string, string>();

            // Calculate efficiency score
            var efficiency = await _calculationService.CalculateEfficiencyScoreAsync(userId, startDate, endDate, cancellationToken);
            var previousEfficiency = await _calculationService.CalculateEfficiencyScoreAsync(userId, previousPeriodStart, startDate.AddDays(-1), cancellationToken);

            currentKPIs["efficiency"] = new KPIValue
            {
                Value = efficiency * 100,
                Unit = "%",
                DisplayFormat = "F1",
                HealthStatus = efficiency >= 0.8 ? "Green" : efficiency >= 0.6 ? "Yellow" : "Red",
                Target = 80.0,
                Description = "Overall task completion efficiency"
            };

            previousKPIs["efficiency"] = new KPIValue { Value = previousEfficiency * 100 };
            changes["efficiency"] = currentKPIs["efficiency"].Value - previousKPIs["efficiency"].Value;
            trends["efficiency"] = changes["efficiency"] > 0 ? "Improving" : changes["efficiency"] < 0 ? "Declining" : "Stable";

            // Add more KPIs...
            await AddTaskCompletionKPI(currentKPIs, previousKPIs, changes, trends, userId, startDate, endDate, previousPeriodStart, cancellationToken);
            await AddStreakKPI(currentKPIs, previousKPIs, changes, trends, userId, cancellationToken);

            // Generate alerts
            alerts.AddRange(GenerateKPIAlerts(currentKPIs, changes));

            return new DashboardKPIs
            {
                UserId = userId,
                CalculationDate = DateTime.UtcNow,
                CurrentPeriodKPIs = currentKPIs,
                PreviousPeriodKPIs = previousKPIs,
                PeriodOverPeriodChanges = changes,
                KPIAlerts = alerts,
                KPITrends = trends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating KPIs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ExecutiveSummary> GenerateExecutiveSummaryAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating executive summary for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

            var efficiency = await _calculationService.CalculateEfficiencyScoreAsync(userId, startDate, endDate, cancellationToken);
            var streakStats = await _streakService.GetStreakStatisticsAsync(userId, cancellationToken);
            var userAnalytics = await _calculationService.CalculateUserAnalyticsAsync(userId, cancellationToken);

            var grade = CalculateProductivityGrade(userAnalytics.GetCompletionRate(), efficiency * 100, userAnalytics.ProductiveDaysCount);
            var achievements = GenerateExecutiveAchievements(userAnalytics, streakStats);
            var improvements = GenerateImprovementAreas(userAnalytics, efficiency);
            var recommendations = GenerateStrategicRecommendations(userAnalytics, streakStats);

            var highLevelMetrics = new Dictionary<string, double>
            {
                ["completionRate"] = userAnalytics.GetCompletionRate(),
                ["efficiencyScore"] = efficiency * 100,
                ["currentStreak"] = streakStats.CurrentBestStreakDays,
                ["totalTasksCompleted"] = userAnalytics.TotalTasksCompleted,
                ["productivityConsistency"] = userAnalytics.GetProductivityConsistency()
            };

            var trend = userAnalytics.GetRecentProductivityTrend();
            var goalProgress = await CalculateGoalProgress(userId, userAnalytics, cancellationToken);

            return new ExecutiveSummary
            {
                UserId = userId,
                ReportDate = DateTime.UtcNow,
                PeriodStart = startDate,
                PeriodEnd = endDate,
                OverallPerformanceGrade = grade,
                KeyAchievements = achievements,
                AreasForImprovement = improvements,
                HighLevelMetrics = highLevelMetrics,
                ProductivityTrend = trend,
                StrategicRecommendations = recommendations,
                GoalProgress = goalProgress
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating executive summary for user {UserId}", userId);
            throw;
        }
    }

    #region Private Helper Methods

    private DateTime GetWeekStart(DateTime date)
    {
        var daysToSubtract = (int)date.DayOfWeek;
        return date.AddDays(-daysToSubtract).Date;
    }

    private List<string> GenerateWeeklyAchievements(Dictionary<string, object> weeklyMetrics)
    {
        var achievements = new List<string>();
        var productiveDays = Convert.ToInt32(weeklyMetrics["productiveDays"]);
        var completionRate = Convert.ToDouble(weeklyMetrics["weeklyCompletionRate"]);

        if (productiveDays >= 6)
        {
            achievements.Add("Exceptional week - 6+ productive days!");
        }

        if (completionRate >= 90)
        {
            achievements.Add("Outstanding completion rate!");
        }

        if (productiveDays >= 4 && completionRate >= 75)
        {
            achievements.Add("Consistent high performance!");
        }

        return achievements;
    }

    private Dictionary<string, int> ExtractCategoryBreakdown(Dictionary<string, object> metrics)
    {
        // This would extract category data from the metrics - simplified for now
        return new Dictionary<string, int>();
    }

    private Dictionary<string, int> ExtractPriorityBreakdown(Dictionary<string, object> metrics)
    {
        // This would extract priority data from the metrics - simplified for now
        return new Dictionary<string, int>();
    }

    private List<string> GenerateMonthlyHighlights(Dictionary<string, object> monthlyMetrics, List<WeeklyMetricsSummary> weeklySummaries)
    {
        var highlights = new List<string>();
        var completionRate = Convert.ToDouble(monthlyMetrics["monthlyCompletionRate"]);
        var bestWeek = weeklySummaries.OrderByDescending(w => w.TotalTasksCompleted).FirstOrDefault();

        if (completionRate >= 80)
        {
            highlights.Add("Excellent monthly completion rate!");
        }

        if (bestWeek != null)
        {
            highlights.Add($"Peak performance in week of {bestWeek.WeekStartDate:MMM dd}");
        }

        return highlights;
    }

    private List<string> GenerateQuarterlyAchievements(List<MonthlyMetricsSummary> monthlySummaries, int totalCompleted)
    {
        var achievements = new List<string>();
        var avgCompletionRate = monthlySummaries.Average(m => m.MonthlyCompletionRate);

        if (totalCompleted >= 100)
        {
            achievements.Add("Century achiever - 100+ tasks completed!");
        }

        if (avgCompletionRate >= 85)
        {
            achievements.Add("Quarterly excellence in task completion!");
        }

        return achievements;
    }

    private Dictionary<string, object> GenerateQuarterlyGoals(Guid userId, int quarter, double avgEfficiency)
    {
        return new Dictionary<string, object>
        {
            ["targetEfficiency"] = Math.Min(100.0, avgEfficiency + 5),
            ["targetTasksCompleted"] = 120,
            ["targetStreakDays"] = 30
        };
    }

    private string DetermineAnnualTrend(List<QuarterlyMetricsSummary> quarterlySummaries)
    {
        var quarterlyTotals = quarterlySummaries.Select(q => q.TotalTasksCompleted).ToList();

        if (quarterlyTotals.Count < 2)
        {
            return "Insufficient data";
        }

        var improvements = 0;
        for (int i = 1; i < quarterlyTotals.Count; i++)
        {
            if (quarterlyTotals[i] > quarterlyTotals[i - 1])
            {
                improvements++;
            }
        }

        return improvements switch
        {
            3 => "Consistently Improving",
            2 => "Generally Improving",
            1 => "Mixed Performance",
            0 => "Declining Performance",
            _ => "Stable"
        };
    }

    private string CalculateProductivityGrade(double completionRate, double efficiency, int productiveDays)
    {
        var score = (completionRate * 0.4 + efficiency * 0.4 + Math.Min(productiveDays / 200.0 * 100, 100) * 0.2);

        return score switch
        {
            >= 90 => "A+",
            >= 85 => "A",
            >= 80 => "A-",
            >= 75 => "B+",
            >= 70 => "B",
            >= 65 => "B-",
            >= 60 => "C+",
            >= 55 => "C",
            _ => "C-"
        };
    }

    private List<string> GenerateYearHighlights(List<QuarterlyMetricsSummary> quarterlySummaries, int totalCompleted)
    {
        var highlights = new List<string>();
        var bestQuarter = quarterlySummaries.OrderByDescending(q => q.TotalTasksCompleted).FirstOrDefault();

        if (totalCompleted >= 500)
        {
            highlights.Add("Productivity champion - 500+ tasks completed!");
        }

        if (bestQuarter != null)
        {
            highlights.Add($"Peak performance in Q{bestQuarter.Quarter}");
        }

        return highlights;
    }

    private Task<Dictionary<string, object>> GenerateAnnualGoals(Guid userId, int year, CancellationToken cancellationToken)
    {
        // This would generate personalized goals based on user history
        return Task.FromResult(new Dictionary<string, object>
        {
            ["targetTasksCompleted"] = 600,
            ["targetEfficiencyScore"] = 85.0,
            ["targetStreakDays"] = 100
        });
    }

    private async Task<Dictionary<string, object>> CreateMilestoneSnapshot(Guid userId, DateTime date, CancellationToken cancellationToken)
    {
        // Create a comprehensive milestone snapshot combining multiple metrics
        var dailyMetrics = await _calculationService.CalculateDailyMetricsAsync(userId, date, cancellationToken);
        var streakStats = await _streakService.GetStreakStatisticsAsync(userId, cancellationToken);
        var userAnalytics = await _calculationService.CalculateUserAnalyticsAsync(userId, cancellationToken);

        return new Dictionary<string, object>
        {
            ["milestoneType"] = "Achievement Milestone",
            ["totalTasksCompleted"] = userAnalytics.TotalTasksCompleted,
            ["longestStreak"] = streakStats.LongestStreakDays,
            ["overallEfficiency"] = userAnalytics.OverallEfficiencyScore,
            ["experienceLevel"] = userAnalytics.GetExperienceLevel().ToString(),
            ["keyInsights"] = userAnalytics.GetPersonalizedInsights()
        };
    }

    private Dictionary<string, object> ExtractMetricsData(object metricsObject)
    {
        // This would extract relevant metrics data from various summary objects
        // Simplified implementation
        return new Dictionary<string, object>
        {
            ["extractedFrom"] = metricsObject.GetType().Name,
            ["timestamp"] = DateTime.UtcNow
        };
    }

    private List<UserRanking> GenerateRankings(Dictionary<Guid, UserMetricsSummary> userSummaries)
    {
        var rankings = new List<UserRanking>();
        var userList = userSummaries.Values.ToList();

        // Rank by completion rate
        var completionRanking = userList.OrderByDescending(u => u.CompletionRate).ToList();
        for (int i = 0; i < completionRanking.Count; i++)
        {
            rankings.Add(new UserRanking
            {
                UserId = completionRanking[i].UserId,
                MetricName = "Completion Rate",
                MetricValue = completionRanking[i].CompletionRate,
                Rank = i + 1,
                TotalUsers = completionRanking.Count,
                Percentile = (double)(completionRanking.Count - i) / completionRanking.Count * 100
            });
        }

        return rankings;
    }

    private Dictionary<string, object> GenerateGroupInsights(Dictionary<Guid, UserMetricsSummary> userSummaries)
    {
        return new Dictionary<string, object>
        {
            ["totalUsers"] = userSummaries.Count,
            ["averageCompletionRate"] = userSummaries.Values.Average(s => s.CompletionRate),
            ["topPerformerCount"] = userSummaries.Values.Count(s => s.CompletionRate >= 90),
            ["consistentPerformers"] = userSummaries.Values.Count(s => s.StreakDays >= 14)
        };
    }

    private string CalculateTrendDirection(Dictionary<DateTime, double> values)
    {
        if (values.Count < 2)
        {
            return "Unknown";
        }

        var orderedValues = values.OrderBy(v => v.Key).ToList();
        var first = orderedValues.First().Value;
        var last = orderedValues.Last().Value;

        var change = (last - first) / first * 100;

        return change switch
        {
            > 5 => "Strongly Improving",
            > 0 => "Improving",
            < -5 => "Declining",
            < 0 => "Slightly Declining",
            _ => "Stable"
        };
    }

    private double CalculateTrendStrength(Dictionary<DateTime, double> values)
    {
        if (values.Count < 3)
        {
            return 0.0;
        }

        // Calculate correlation coefficient for trend strength
        var orderedValues = values.OrderBy(v => v.Key).ToList();
        var n = orderedValues.Count;
        var xValues = Enumerable.Range(1, n).ToList();
        var yValues = orderedValues.Select(v => v.Value).ToList();

        var avgX = xValues.Average();
        var avgY = yValues.Average();

        var numerator = xValues.Zip(yValues, (x, y) => (x - avgX) * (y - avgY)).Sum();
        var denominator = Math.Sqrt(xValues.Sum(x => Math.Pow(x - avgX, 2)) * yValues.Sum(y => Math.Pow(y - avgY, 2)));

        return denominator != 0 ? Math.Abs(numerator / denominator) : 0.0;
    }

    private Task<List<ProductivityPattern>> IdentifyWeeklyPatterns(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // Simplified pattern identification
        return Task.FromResult(new List<ProductivityPattern>());
    }

    private Task<List<ProductivityPattern>> IdentifyMonthlyPatterns(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // Simplified pattern identification
        return Task.FromResult(new List<ProductivityPattern>());
    }

    private Task<List<Anomaly>> DetectAnomalies(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // Simplified anomaly detection
        return Task.FromResult(new List<Anomaly>());
    }

    private Task<Dictionary<string, double>> IdentifyCyclicalTrends(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, double>
        {
            ["weeklyMondayEffect"] = 0.1,
            ["monthEndRush"] = 0.15
        });
    }

    private Task<Dictionary<string, object>> GenerateBehavioralInsights(Guid userId, List<ProductivityPattern> patterns, List<Anomaly> anomalies, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["patternCount"] = patterns.Count,
            ["anomalyCount"] = anomalies.Count,
            ["behaviorType"] = "Consistent"
        });
    }

    private List<string> GeneratePatternRecommendations(List<ProductivityPattern> patterns, List<Anomaly> anomalies)
    {
        var recommendations = new List<string>();

        if (patterns.Any())
        {
            recommendations.Add("Leverage your identified productivity patterns");
        }

        if (anomalies.Any())
        {
            recommendations.Add("Address factors that caused productivity anomalies");
        }

        return recommendations;
    }

    private Task AddTaskCompletionKPI(Dictionary<string, KPIValue> currentKPIs, Dictionary<string, KPIValue> previousKPIs,
        Dictionary<string, double> changes, Dictionary<string, string> trends,
        Guid userId, DateTime startDate, DateTime endDate, DateTime previousStart, CancellationToken cancellationToken)
    {
        // Implementation for task completion KPI
        var current = 50; // Placeholder
        var previous = 45; // Placeholder

        currentKPIs["taskCompletion"] = new KPIValue
        {
            Value = current,
            Unit = "tasks",
            DisplayFormat = "F0",
            HealthStatus = current >= 40 ? "Green" : current >= 25 ? "Yellow" : "Red",
            Target = 50,
            Description = "Tasks completed in period"
        };

        previousKPIs["taskCompletion"] = new KPIValue { Value = previous };
        changes["taskCompletion"] = current - previous;
        trends["taskCompletion"] = changes["taskCompletion"] > 0 ? "Improving" : "Declining";
        return Task.CompletedTask;
    }

    private async Task AddStreakKPI(Dictionary<string, KPIValue> currentKPIs, Dictionary<string, KPIValue> previousKPIs,
        Dictionary<string, double> changes, Dictionary<string, string> trends,
        Guid userId, CancellationToken cancellationToken)
    {
        var streakStats = await _streakService.GetStreakStatisticsAsync(userId, cancellationToken);

        currentKPIs["currentStreak"] = new KPIValue
        {
            Value = streakStats.CurrentBestStreakDays,
            Unit = "days",
            DisplayFormat = "F0",
            HealthStatus = streakStats.CurrentBestStreakDays >= 14 ? "Green" : streakStats.CurrentBestStreakDays >= 7 ? "Yellow" : "Red",
            Target = 30,
            Description = "Current productivity streak"
        };
    }

    private List<string> GenerateKPIAlerts(Dictionary<string, KPIValue> kpis, Dictionary<string, double> changes)
    {
        var alerts = new List<string>();

        foreach (var kpi in kpis)
        {
            if (kpi.Value.HealthStatus == "Red")
            {
                alerts.Add($"{kpi.Key} is below target - attention needed");
            }
        }

        return alerts;
    }

    private List<string> GenerateExecutiveAchievements(UserAnalytics analytics, StreakStatistics streakStats)
    {
        var achievements = new List<string>();

        if (analytics.GetCompletionRate() >= 90)
        {
            achievements.Add("Outstanding task completion rate");
        }

        if (streakStats.LongestStreakDays >= 50)
        {
            achievements.Add("Exceptional productivity consistency");
        }

        if (analytics.TotalTasksCompleted >= 1000)
        {
            achievements.Add("Milestone achievement - 1000+ tasks completed");
        }

        return achievements;
    }

    private List<string> GenerateImprovementAreas(UserAnalytics analytics, double efficiency)
    {
        var improvements = new List<string>();

        if (analytics.GetCompletionRate() < 70)
        {
            improvements.Add("Task completion consistency");
        }

        if (efficiency < 0.6)
        {
            improvements.Add("Task execution efficiency");
        }

        if (analytics.GetOverdueRate() > 25)
        {
            improvements.Add("Due date management");
        }

        return improvements;
    }

    private List<string> GenerateStrategicRecommendations(UserAnalytics analytics, StreakStatistics streakStats)
    {
        var recommendations = new List<string>();

        recommendations.Add("Focus on maintaining consistent daily productivity");
        if (streakStats.ActiveStreaks == 0)
        {
            recommendations.Add("Consider starting a new productivity streak");
        }

        if (analytics.GetProductivityConsistency() < 50)
        {
            recommendations.Add("Develop more consistent work habits");
        }

        return recommendations;
    }

    private Task<Dictionary<string, object>> CalculateGoalProgress(Guid userId, UserAnalytics analytics, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["annualTaskGoal"] = new { Target = 500, Current = analytics.TotalTasksCompleted, Progress = analytics.TotalTasksCompleted / 5.0 },
            ["efficiencyGoal"] = new { Target = 0.85, Current = analytics.OverallEfficiencyScore, Progress = analytics.OverallEfficiencyScore / 0.85 * 100 }
        });
    }

    #endregion
}

/// <summary>
/// Extended interface for analytics repository to support aggregation operations
/// </summary>
public partial interface IAnalyticsRepository
{
    public Task<List<AnalyticsSnapshot>> GetAnalyticsSnapshotsAsync(Guid userId, SnapshotType snapshotType, int limit, CancellationToken cancellationToken = default);
    public Task AddAnalyticsSnapshotAsync(AnalyticsSnapshot snapshot, CancellationToken cancellationToken = default);
}
