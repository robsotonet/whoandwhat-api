using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// AI planning service implementation with multi-provider support and fallback mechanisms
/// Follows 2025 best practices with async/await, proper error handling, and security
/// </summary>
public class AIPlanningService : IAIPlanningService
{
    private readonly AISettings _aiSettings;
    private readonly ILogger<AIPlanningService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IAICacheService _cacheService;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly Dictionary<AIProvider, DateTime> _providerLastFailure;
    private readonly object _lockObject = new();

    public AIPlanningService(
        IOptions<AISettings> aiSettings,
        ILogger<AIPlanningService> logger,
        HttpClient httpClient,
        IAICacheService cacheService)
    {
        _aiSettings = aiSettings.Value;
        _logger = logger;
        _httpClient = httpClient;
        _cacheService = cacheService;
        _rateLimitSemaphore = new SemaphoreSlim(_aiSettings.RateLimit.GlobalRequestsPerMinute, _aiSettings.RateLimit.GlobalRequestsPerMinute);
        _providerLastFailure = new Dictionary<AIProvider, DateTime>();

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public async Task<AIGeneratedPlan?> GenerateDayPlanAsync(
        Guid userId,
        DateTime planDate,
        UserPlanningPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableDayPlanning)
        {
            _logger.LogInformation("Day planning is disabled. Skipping AI plan generation for user {UserId}", userId);
            return null;
        }

        try
        {
            // Check cache first
            var cacheKey = $"day-plan:{userId}:{planDate:yyyy-MM-dd}";
            var cachedPlan = await _cacheService.GetCachedDayPlanAsync(userId, planDate, cancellationToken);
            if (cachedPlan != null)
            {
                _logger.LogDebug("Retrieved day plan from cache for user {UserId}, date {PlanDate}", userId, planDate);
                return cachedPlan;
            }

            // Rate limiting
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Generating AI day plan for user {UserId}, date {PlanDate}", userId, planDate);

                var prompt = BuildDayPlanPrompt(userId, planDate, preferences);
                var response = await CallAIServiceAsync(prompt, AIRequestType.DayPlanning, cancellationToken);

                if (response == null)
                {
                    _logger.LogWarning("Failed to generate day plan for user {UserId}, date {PlanDate}", userId, planDate);
                    return null;
                }

                var plan = ParseDayPlanResponse(response, userId, planDate);
                if (plan != null)
                {
                    // Cache the result
                    await _cacheService.CacheDayPlanAsync(plan, _aiSettings.Cache.DayPlanExpirationMinutes, cancellationToken);
                    _logger.LogInformation("Successfully generated and cached day plan for user {UserId}", userId);
                }

                return plan;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating day plan for user {UserId}, date {PlanDate}", userId, planDate);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskPrioritySuggestion>?> GetTaskPrioritySuggestionsAsync(
        Guid userId,
        IEnumerable<TaskAnalysisContext> taskContexts,
        PriorityAnalysisContext analysisContext,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnablePrioritySuggestions)
        {
            _logger.LogInformation("Priority suggestions are disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Getting AI priority suggestions for user {UserId}, {TaskCount} tasks", userId, taskContexts.Count());

                var prompt = BuildPrioritySuggestionsPrompt(userId, taskContexts, analysisContext);
                var response = await CallAIServiceAsync(prompt, AIRequestType.PrioritySuggestions, cancellationToken);

                if (response == null)
                {
                    _logger.LogWarning("Failed to get priority suggestions for user {UserId}", userId);
                    return null;
                }

                var suggestions = ParsePrioritySuggestionsResponse(response, taskContexts);

                // Cache individual suggestions
                if (suggestions != null)
                {
                    foreach (var suggestion in suggestions)
                    {
                        await _cacheService.CachePrioritySuggestionAsync(
                            suggestion,
                            _aiSettings.Cache.PrioritySuggestionExpirationMinutes,
                            cancellationToken);
                    }
                    _logger.LogInformation("Successfully generated {Count} priority suggestions for user {UserId}", suggestions.Count(), userId);
                }

                return suggestions;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting priority suggestions for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ScheduleOptimizationResult?> GenerateScheduleOptimizationsAsync(
        Guid userId,
        IEnumerable<TimeSlot> timeSlots,
        ScheduleOptimizationPreferences optimizationPreferences,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableScheduleOptimization)
        {
            _logger.LogInformation("Schedule optimization is disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Generating schedule optimizations for user {UserId}", userId);

                var prompt = BuildScheduleOptimizationPrompt(userId, timeSlots, optimizationPreferences);
                var response = await CallAIServiceAsync(prompt, AIRequestType.ScheduleOptimization, cancellationToken);

                var result = ParseScheduleOptimizationResponse(response, userId);
                if (result != null)
                {
                    _logger.LogInformation("Successfully generated schedule optimizations for user {UserId}", userId);
                }

                return result;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule optimizations for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BreakRecommendation>?> GetBreakRecommendationsAsync(
        Guid userId,
        WorkloadAnalysis workloadAnalysis,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableBreakRecommendations)
        {
            _logger.LogInformation("Break recommendations are disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Getting break recommendations for user {UserId}", userId);

                var prompt = BuildBreakRecommendationsPrompt(userId, workloadAnalysis);
                var response = await CallAIServiceAsync(prompt, AIRequestType.BreakRecommendations, cancellationToken);

                var recommendations = ParseBreakRecommendationsResponse(response);
                if (recommendations != null)
                {
                    _logger.LogInformation("Successfully generated {Count} break recommendations for user {UserId}", recommendations.Count(), userId);
                }

                return recommendations;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting break recommendations for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProductivityInsights?> GetProductivityInsightsAsync(
        Guid userId,
        TimeframeAnalysis analysisTimeframe,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableProductivityInsights)
        {
            _logger.LogInformation("Productivity insights are disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            // Check cache first
            var cachedInsights = await _cacheService.GetCachedProductivityInsightsAsync(userId, analysisTimeframe, cancellationToken);
            if (cachedInsights != null)
            {
                _logger.LogDebug("Retrieved productivity insights from cache for user {UserId}", userId);
                return cachedInsights;
            }

            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Getting productivity insights for user {UserId}", userId);

                var prompt = BuildProductivityInsightsPrompt(userId, analysisTimeframe);
                var response = await CallAIServiceAsync(prompt, AIRequestType.ProductivityInsights, cancellationToken);

                var insights = ParseProductivityInsightsResponse(response, userId, analysisTimeframe.StartDate);
                if (insights != null)
                {
                    // Cache the insights
                    await _cacheService.CacheProductivityInsightsAsync(
                        insights,
                        _aiSettings.Cache.ProductivityInsightsExpirationMinutes,
                        cancellationToken);
                    _logger.LogInformation("Successfully generated and cached productivity insights for user {UserId}", userId);
                }

                return insights;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity insights for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CategorySuggestion>?> GetTaskCategorizationSuggestionsAsync(
        Guid userId,
        string taskContent,
        UserCategoryHistory userCategoryHistory,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableTaskCategorization)
        {
            _logger.LogInformation("Task categorization is disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Getting task categorization suggestions for user {UserId}", userId);

                var prompt = BuildCategorizationPrompt(taskContent, userCategoryHistory);
                var response = await CallAIServiceAsync(prompt, AIRequestType.TaskCategorization, cancellationToken);

                var suggestions = ParseCategorizationResponse(response);
                if (suggestions != null)
                {
                    _logger.LogInformation("Successfully generated {Count} categorization suggestions for user {UserId}", suggestions.Count(), userId);
                }

                return suggestions;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categorization suggestions for user {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAIServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled)
        {
            return false;
        }

        try
        {
            var healthStatus = await GetAIServiceHealthAsync(cancellationToken);
            return healthStatus.IsHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking AI service availability");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AIServiceHealthStatus> GetAIServiceHealthAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var healthChecks = new List<HealthCheckResult>();
        var availableCapabilities = new List<string>();

        try
        {
            // Check primary provider
            var primaryProviderHealthy = await CheckProviderHealthAsync(_aiSettings.Provider, cancellationToken);
            healthChecks.Add(new HealthCheckResult(
                CheckName: $"Primary Provider ({_aiSettings.Provider})",
                Passed: primaryProviderHealthy,
                ErrorMessage: primaryProviderHealthy ? null : "Provider not responding",
                Duration: DateTime.UtcNow - startTime,
                Metadata: new Dictionary<string, object> { ["provider"] = _aiSettings.Provider.ToString() }
            ));

            // Check fallback provider if configured
            if (_aiSettings.FallbackProvider.HasValue)
            {
                var fallbackProviderHealthy = await CheckProviderHealthAsync(_aiSettings.FallbackProvider.Value, cancellationToken);
                healthChecks.Add(new HealthCheckResult(
                    CheckName: $"Fallback Provider ({_aiSettings.FallbackProvider.Value})",
                    Passed: fallbackProviderHealthy,
                    ErrorMessage: fallbackProviderHealthy ? null : "Fallback provider not responding",
                    Duration: DateTime.UtcNow - startTime,
                    Metadata: new Dictionary<string, object> { ["provider"] = _aiSettings.FallbackProvider.Value.ToString() }
                ));
            }

            // Check enabled capabilities
            if (_aiSettings.Features.EnableDayPlanning)
            {
                availableCapabilities.Add("DayPlanning");
            }

            if (_aiSettings.Features.EnablePrioritySuggestions)
            {
                availableCapabilities.Add("PrioritySuggestions");
            }

            if (_aiSettings.Features.EnableScheduleOptimization)
            {
                availableCapabilities.Add("ScheduleOptimization");
            }

            if (_aiSettings.Features.EnableBreakRecommendations)
            {
                availableCapabilities.Add("BreakRecommendations");
            }

            if (_aiSettings.Features.EnableProductivityInsights)
            {
                availableCapabilities.Add("ProductivityInsights");
            }

            if (_aiSettings.Features.EnableTaskCategorization)
            {
                availableCapabilities.Add("TaskCategorization");
            }

            if (_aiSettings.Features.EnableTimeEstimation)
            {
                availableCapabilities.Add("TimeEstimation");
            }

            var responseTime = DateTime.UtcNow - startTime;
            var isHealthy = healthChecks.Any(h => h.Passed) && _aiSettings.Enabled;

            return new AIServiceHealthStatus(
                IsHealthy: isHealthy,
                ResponseTime: responseTime,
                ServiceVersion: "1.0.0", // This could be dynamically determined
                AvailableCapabilities: availableCapabilities,
                DetailedChecks: healthChecks,
                CheckTimestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing AI service health check");
            return new AIServiceHealthStatus(
                IsHealthy: false,
                ResponseTime: DateTime.UtcNow - startTime,
                ServiceVersion: "1.0.0",
                AvailableCapabilities: new List<string>(),
                DetailedChecks: new List<HealthCheckResult>
                {
                    new HealthCheckResult(
                        CheckName: "Health Check",
                        Passed: false,
                        ErrorMessage: ex.Message,
                        Duration: DateTime.UtcNow - startTime,
                        Metadata: new Dictionary<string, object>()
                    )
                },
                CheckTimestamp: DateTime.UtcNow
            );
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskTimeEstimate>?> GenerateTaskTimeEstimatesAsync(
        Guid userId,
        IEnumerable<TaskEstimationRequest> taskEstimationRequests,
        UserHistoricalPerformance historicalPerformance,
        CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.Enabled || !_aiSettings.Features.EnableTimeEstimation)
        {
            _logger.LogInformation("Time estimation is disabled. Skipping for user {UserId}", userId);
            return null;
        }

        try
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Generating time estimates for user {UserId}, {TaskCount} tasks", userId, taskEstimationRequests.Count());

                var prompt = BuildTimeEstimationPrompt(taskEstimationRequests, historicalPerformance);
                var response = await CallAIServiceAsync(prompt, AIRequestType.TimeEstimation, cancellationToken);

                var estimates = ParseTimeEstimationResponse(response, taskEstimationRequests);
                if (estimates != null)
                {
                    _logger.LogInformation("Successfully generated {Count} time estimates for user {UserId}", estimates.Count(), userId);
                }

                return estimates;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time estimates for user {UserId}", userId);
            return null;
        }
    }

    #region Private Helper Methods

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_aiSettings.RequestTimeoutMs);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-API/1.0");
    }

    private async Task<string?> CallAIServiceAsync(string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        var provider = _aiSettings.Provider;

        // Check if primary provider recently failed and use fallback
        if (ShouldUseFallback(provider) && _aiSettings.FallbackProvider.HasValue)
        {
            provider = _aiSettings.FallbackProvider.Value;
            _logger.LogInformation("Using fallback provider {Provider} for request type {RequestType}", provider, requestType);
        }

        for (int attempt = 0; attempt <= _aiSettings.MaxRetryAttempts; attempt++)
        {
            try
            {
                var response = await CallProviderAsync(provider, prompt, requestType, cancellationToken);
                if (!string.IsNullOrEmpty(response))
                {
                    // Reset failure tracking on success
                    lock (_lockObject)
                    {
                        _providerLastFailure.Remove(provider);
                    }
                    return response;
                }
            }
            catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.Timeout)
            {
                _logger.LogWarning("AI service request timeout on attempt {Attempt} for provider {Provider}", attempt + 1, provider);
                RecordProviderFailure(provider);

                if (attempt < _aiSettings.MaxRetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI service request failed on attempt {Attempt} for provider {Provider}", attempt + 1, provider);
                RecordProviderFailure(provider);

                if (attempt < _aiSettings.MaxRetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), cancellationToken);
                }
            }
        }

        // Try fallback provider if primary failed
        if (provider == _aiSettings.Provider && _aiSettings.FallbackProvider.HasValue)
        {
            _logger.LogInformation("Primary provider failed, attempting fallback provider {Provider}", _aiSettings.FallbackProvider.Value);
            return await CallProviderAsync(_aiSettings.FallbackProvider.Value, prompt, requestType, cancellationToken);
        }

        return null;
    }

    private async Task<string?> CallProviderAsync(AIProvider provider, string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        return provider switch
        {
            AIProvider.OpenAI => await CallOpenAIAsync(prompt, requestType, cancellationToken),
            AIProvider.AzureOpenAI => await CallAzureOpenAIAsync(prompt, requestType, cancellationToken),
            AIProvider.GoogleGemini => await CallGoogleGeminiAsync(prompt, requestType, cancellationToken),
            AIProvider.AnthropicClaude => await CallAnthropicClaudeAsync(prompt, requestType, cancellationToken),
            _ => throw new NotSupportedException($"Provider {provider} is not supported")
        };
    }

    private async Task<bool> CheckProviderHealthAsync(AIProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var healthCheckPrompt = "Hello, please respond with 'OK' to confirm you are working.";
            var response = await CallProviderAsync(provider, healthCheckPrompt, AIRequestType.HealthCheck, cancellationToken);
            return !string.IsNullOrEmpty(response);
        }
        catch
        {
            return false;
        }
    }

    private bool ShouldUseFallback(AIProvider provider)
    {
        lock (_lockObject)
        {
            if (_providerLastFailure.TryGetValue(provider, out var lastFailure))
            {
                // Use fallback if provider failed within the last 5 minutes
                return DateTime.UtcNow - lastFailure < TimeSpan.FromMinutes(5);
            }
        }
        return false;
    }

    private void RecordProviderFailure(AIProvider provider)
    {
        lock (_lockObject)
        {
            _providerLastFailure[provider] = DateTime.UtcNow;
        }
    }

    // Provider-specific implementation methods (simplified for now)
    private async Task<string?> CallOpenAIAsync(string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        // Implementation for OpenAI API calls
        // This would contain the actual HTTP request logic for OpenAI
        _logger.LogDebug("Calling OpenAI for request type {RequestType}", requestType);

        // Placeholder implementation - would contain actual OpenAI API integration
        await Task.Delay(100, cancellationToken); // Simulate API call
        return "OpenAI response placeholder";
    }

    private async Task<string?> CallAzureOpenAIAsync(string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Calling Azure OpenAI for request type {RequestType}", requestType);
        await Task.Delay(100, cancellationToken);
        return "Azure OpenAI response placeholder";
    }

    private async Task<string?> CallGoogleGeminiAsync(string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Calling Google Gemini for request type {RequestType}", requestType);
        await Task.Delay(100, cancellationToken);
        return "Google Gemini response placeholder";
    }

    private async Task<string?> CallAnthropicClaudeAsync(string prompt, AIRequestType requestType, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Calling Anthropic Claude for request type {RequestType}", requestType);
        await Task.Delay(100, cancellationToken);
        return "Anthropic Claude response placeholder";
    }

    // Prompt building methods (simplified for now)
    private string BuildDayPlanPrompt(Guid userId, DateTime planDate, UserPlanningPreferences preferences)
    {
        return $"Generate a day plan for user {userId} on {planDate:yyyy-MM-dd} with preferences: {JsonSerializer.Serialize(preferences)}";
    }

    private string BuildPrioritySuggestionsPrompt(Guid userId, IEnumerable<TaskAnalysisContext> taskContexts, PriorityAnalysisContext analysisContext)
    {
        return $"Analyze and suggest priorities for {taskContexts.Count()} tasks for user {userId}";
    }

    private string BuildScheduleOptimizationPrompt(Guid userId, IEnumerable<TimeSlot> timeSlots, ScheduleOptimizationPreferences preferences)
    {
        return $"Optimize schedule for user {userId} with {timeSlots.Count()} time slots";
    }

    private string BuildBreakRecommendationsPrompt(Guid userId, WorkloadAnalysis workloadAnalysis)
    {
        return $"Recommend breaks for user {userId} based on workload analysis";
    }

    private string BuildProductivityInsightsPrompt(Guid userId, TimeframeAnalysis analysisTimeframe)
    {
        return $"Generate productivity insights for user {userId} from {analysisTimeframe.StartDate} to {analysisTimeframe.EndDate}";
    }

    private string BuildCategorizationPrompt(string taskContent, UserCategoryHistory userCategoryHistory)
    {
        return $"Categorize task: {taskContent} based on user history";
    }

    private string BuildTimeEstimationPrompt(IEnumerable<TaskEstimationRequest> requests, UserHistoricalPerformance historicalPerformance)
    {
        return $"Estimate time for {requests.Count()} tasks based on historical performance";
    }

    // Response parsing methods (simplified for now - would contain actual JSON parsing logic)
    private AIGeneratedPlan? ParseDayPlanResponse(string response, Guid userId, DateTime planDate)
    {
        // Placeholder implementation - would contain actual response parsing
        return new AIGeneratedPlan(
            UserId: userId,
            PlanDate: planDate,
            ScheduledTasks: new List<ScheduledTaskBlock>(),
            TimeBlocks: new List<TimeBlockRecommendation>(),
            ProductivityTips: new List<string> { "Sample tip from AI" },
            AnalysisMetadata: new AIAnalysisMetadata(
                ModelUsed: _aiSettings.Provider.ToString(),
                ModelVersion: "1.0",
                DataSourcesUsed: new List<string> { "user_preferences", "task_history" },
                ProcessingStartTime: DateTime.UtcNow.AddSeconds(-1),
                ProcessingDuration: TimeSpan.FromSeconds(1),
                ModelParameters: new Dictionary<string, object>()
            ),
            ConfidenceScore: 0.85,
            GeneratedAt: DateTime.UtcNow
        );
    }

    private IEnumerable<TaskPrioritySuggestion>? ParsePrioritySuggestionsResponse(string response, IEnumerable<TaskAnalysisContext> contexts)
    {
        // Placeholder implementation
        return contexts.Select(ctx => new TaskPrioritySuggestion(
            TaskId: ctx.TaskId,
            SuggestedPriority: "High",
            ConfidenceScore: 0.8,
            AIReasoning: "Based on urgency and importance",
            InfluencingFactors: new List<string> { "Due date", "Category importance" },
            SuggestionCreatedAt: DateTime.UtcNow
        ));
    }

    private ScheduleOptimizationResult? ParseScheduleOptimizationResponse(string response, Guid userId)
    {
        // Placeholder implementation
        return new ScheduleOptimizationResult(
            UserId: userId,
            OptimizationDate: DateTime.UtcNow,
            Optimizations: new List<ScheduleOptimization>(),
            Insights: new List<ProductivityInsight>(),
            EstimatedImpact: new EstimatedImpact(
                ProductivityImprovement: 0.15,
                TimeSaved: TimeSpan.FromMinutes(30),
                StressReduction: 0.1,
                QualitativeBenefits: new List<string> { "Better work-life balance" }
            ),
            GeneratedAt: DateTime.UtcNow
        );
    }

    private IEnumerable<BreakRecommendation>? ParseBreakRecommendationsResponse(string response)
    {
        // Placeholder implementation
        return new List<BreakRecommendation>
        {
            new BreakRecommendation(
                RecommendedTime: TimeSpan.FromHours(10.5),
                Duration: TimeSpan.FromMinutes(15),
                BreakType: BreakType.Short,
                ActivitySuggestion: "Take a short walk",
                Reasoning: "You've been working for 2 hours",
                ImportanceScore: 0.7
            )
        };
    }

    private ProductivityInsights? ParseProductivityInsightsResponse(string response, Guid userId, DateTime analysisDate)
    {
        // Placeholder implementation
        return new ProductivityInsights(
            UserId: userId,
            AnalysisDate: analysisDate,
            IdentifiedPatterns: new List<ProductivityPattern>(),
            ActionableRecommendations: new List<string> { "Consider time blocking", "Take regular breaks" },
            PerformanceMetrics: new Dictionary<string, double> { ["focus_score"] = 0.75, ["efficiency"] = 0.8 },
            TrendAnalysis: new ProductivityTrendAnalysis(
                OverallTrend: TrendDirection.Improving,
                DataPoints: new List<TrendDataPoint>(),
                TrendDrivers: new List<string> { "Consistent schedule", "Regular breaks" },
                Recommendations: new List<string> { "Maintain current patterns" }
            )
        );
    }

    private IEnumerable<CategorySuggestion>? ParseCategorizationResponse(string response)
    {
        // Placeholder implementation
        return new List<CategorySuggestion>
        {
            new CategorySuggestion(
                SuggestedCategory: "Work",
                ConfidenceScore: 0.85,
                Reasoning: "Based on task content and keywords",
                AlternativeCategories: new List<string> { "Project", "Administrative" },
                CategoryProbabilities: new Dictionary<string, double> { ["Work"] = 0.85, ["Project"] = 0.1, ["Administrative"] = 0.05 }
            )
        };
    }

    private IEnumerable<TaskTimeEstimate>? ParseTimeEstimationResponse(string response, IEnumerable<TaskEstimationRequest> requests)
    {
        // Placeholder implementation
        return requests.Select(req => new TaskTimeEstimate(
            TaskId: req.TaskId,
            EstimatedDuration: TimeSpan.FromHours(2),
            MinDuration: TimeSpan.FromMinutes(90),
            MaxDuration: TimeSpan.FromHours(3),
            ConfidenceLevel: 0.75,
            EstimationFactors: new List<string> { "Task complexity", "User experience", "Historical data" }
        ));
    }

    #endregion

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
        // HttpClient should not be disposed here as it's managed by the DI container and HttpClientFactory
        // Disposing it manually can interfere with connection pooling and affect other HttpClient instances
    }
}

/// <summary>
/// Types of AI requests for categorization and handling
/// </summary>
public enum AIRequestType
{
    DayPlanning,
    PrioritySuggestions,
    ScheduleOptimization,
    BreakRecommendations,
    ProductivityInsights,
    TaskCategorization,
    TimeEstimation,
    HealthCheck
}
