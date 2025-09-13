using Microsoft.Extensions.Diagnostics.HealthChecks;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;

namespace WhoAndWhat.API.Configuration;

/// <summary>
/// Extensions for configuring AI planning services in the DI container
/// </summary>
public static class AIPlanningServiceCollectionExtensions
{
    /// <summary>
    /// Configure AI planning and analytics services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddAIPlanningServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure AI settings
        services.Configure<AISettings>(configuration.GetSection(AISettings.SectionName));

        // Configure HttpClient for AI service
        services.AddHttpClient<AIPlanningService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-API/1.0");
            // Additional headers will be configured by the service based on provider
        });

        // Register AI planning service
        services.AddScoped<IAIPlanningService, AIPlanningService>();

        // Register AI cache service
        services.AddScoped<IAICacheService, AICacheService>();

        // Add AI service health checks
        services.AddHealthChecks()
            .AddCheck<AIServiceHealthCheck>("ai_service",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "ai", "external" });

        return services;
    }

    /// <summary>
    /// Configure AI planning services with custom settings
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action for AI settings</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddAIPlanningServices(this IServiceCollection services, Action<AISettings> configureOptions)
    {
        // Configure AI settings with custom action
        services.Configure(configureOptions);

        // Configure HttpClient for AI service
        services.AddHttpClient<AIPlanningService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-API/1.0");
        });

        // Register AI planning service
        services.AddScoped<IAIPlanningService, AIPlanningService>();

        // Register AI cache service
        services.AddScoped<IAICacheService, AICacheService>();

        // Add AI service health checks
        services.AddHealthChecks()
            .AddCheck<AIServiceHealthCheck>("ai_service",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "ai", "external" });

        return services;
    }

    /// <summary>
    /// Configure AI planning services for testing environment
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddAIPlanningServicesForTesting(this IServiceCollection services)
    {
        // Configure AI settings for testing (disabled by default)
        services.Configure<AISettings>(options =>
        {
            options.Enabled = false;
            options.Provider = AIProvider.OpenAI;
            options.RequestTimeoutMs = 5000; // Shorter timeout for tests
            options.MaxRetryAttempts = 1; // Fewer retries for tests
            options.Cache.Enabled = false; // Disable cache for testing
        });

        // Use in-memory HttpClient for testing
        services.AddHttpClient<AIPlanningService>();

        // Register services
        services.AddScoped<IAIPlanningService, AIPlanningService>();
        services.AddScoped<IAICacheService, AICacheService>();

        return services;
    }

    /// <summary>
    /// Configure AI planning services with mock implementation for development
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddMockAIPlanningServices(this IServiceCollection services)
    {
        // Configure AI settings for mock mode
        services.Configure<AISettings>(options =>
        {
            options.Enabled = true;
            options.Provider = AIProvider.OpenAI;
            options.RequestTimeoutMs = 1000; // Very short timeout for mock
            options.MaxRetryAttempts = 0; // No retries for mock
            options.Features.EnableDayPlanning = true;
            options.Features.EnablePrioritySuggestions = true;
            options.Features.EnableScheduleOptimization = true;
            options.Features.EnableBreakRecommendations = true;
            options.Features.EnableProductivityInsights = true;
            options.Features.EnableTaskCategorization = true;
            options.Features.EnableTimeEstimation = true;
        });

        services.AddHttpClient<AIPlanningService>();

        // Register mock AI planning service (would implement mock responses)
        services.AddScoped<IAIPlanningService, MockAIPlanningService>();
        services.AddScoped<IAICacheService, AICacheService>();

        return services;
    }
}

/// <summary>
/// Health check for AI planning service
/// </summary>
public class AIServiceHealthCheck : IHealthCheck
{
    private readonly IAIPlanningService _aiService;
    private readonly ILogger<AIServiceHealthCheck> _logger;

    public AIServiceHealthCheck(IAIPlanningService aiService, ILogger<AIServiceHealthCheck> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _aiService.IsAIServiceAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                return HealthCheckResult.Healthy("AI planning service is available and responding");
            }

            // Get detailed health status for more information
            var healthStatus = await _aiService.GetAIServiceHealthAsync(cancellationToken);
            var failedChecks = healthStatus.DetailedChecks.Where(c => !c.Passed).ToList();

            if (failedChecks.Any())
            {
                var errorMessage = $"AI service health checks failed: {string.Join(", ", failedChecks.Select(c => c.ErrorMessage))}";
                return HealthCheckResult.Degraded(errorMessage, data: new Dictionary<string, object>
                {
                    ["response_time"] = healthStatus.ResponseTime.TotalMilliseconds,
                    ["failed_checks"] = failedChecks.Count,
                    ["available_capabilities"] = healthStatus.AvailableCapabilities.Count
                });
            }

            return HealthCheckResult.Degraded("AI planning service is not responding properly");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service health check failed with exception");
            return HealthCheckResult.Unhealthy("AI planning service health check failed", ex);
        }
    }
}

/// <summary>
/// Mock implementation of AI planning service for development/testing
/// </summary>
public class MockAIPlanningService : IAIPlanningService
{
    private readonly ILogger<MockAIPlanningService> _logger;

    public MockAIPlanningService(ILogger<MockAIPlanningService> logger)
    {
        _logger = logger;
    }

    public async Task<AIGeneratedPlan?> GenerateDayPlanAsync(Guid userId, DateTime planDate, UserPlanningPreferences preferences, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Generating day plan for user {UserId}, date {PlanDate}", userId, planDate);
        await Task.Delay(100, cancellationToken); // Simulate API call delay

        return new AIGeneratedPlan(
            UserId: userId,
            PlanDate: planDate,
            ScheduledTasks: new List<ScheduledTaskBlock>
            {
                new ScheduledTaskBlock(
                    TaskId: Guid.NewGuid(),
                    TaskTitle: "Mock Task 1",
                    StartTime: TimeSpan.FromHours(9),
                    EndTime: TimeSpan.FromHours(10),
                    Category: "Work",
                    Priority: "High",
                    PreparationNeeded: new List<string> { "Review materials" }
                )
            },
            TimeBlocks: new List<TimeBlockRecommendation>
            {
                new TimeBlockRecommendation(
                    StartTime: TimeSpan.FromHours(9),
                    EndTime: TimeSpan.FromHours(12),
                    BlockType: TimeBlockType.Deep_Work,
                    Purpose: "Focus work",
                    Description: "Dedicated time for important tasks"
                )
            },
            ProductivityTips: new List<string> { "Take breaks every 90 minutes", "Start with your most important task" },
            AnalysisMetadata: new AIAnalysisMetadata(
                ModelUsed: "Mock",
                ModelVersion: "1.0",
                DataSourcesUsed: new List<string> { "user_preferences", "task_history" },
                ProcessingStartTime: DateTime.UtcNow.AddMilliseconds(-100),
                ProcessingDuration: TimeSpan.FromMilliseconds(100),
                ModelParameters: new Dictionary<string, object> { ["mock"] = true }
            ),
            ConfidenceScore: 0.95,
            GeneratedAt: DateTime.UtcNow
        );
    }

    public async Task<IEnumerable<TaskPrioritySuggestion>?> GetTaskPrioritySuggestionsAsync(Guid userId, IEnumerable<TaskAnalysisContext> taskContexts, PriorityAnalysisContext analysisContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Getting priority suggestions for user {UserId}, {TaskCount} tasks", userId, taskContexts.Count());
        await Task.Delay(50, cancellationToken);

        return taskContexts.Select(task => new TaskPrioritySuggestion(
            TaskId: task.TaskId,
            SuggestedPriority: "High",
            ConfidenceScore: 0.85,
            AIReasoning: "Mock reasoning: This task appears urgent based on keywords and due date",
            InfluencingFactors: new List<string> { "Due date proximity", "Task category importance" },
            SuggestionCreatedAt: DateTime.UtcNow
        ));
    }

    // Implement other interface methods with mock responses...
    public Task<ScheduleOptimizationResult?> GenerateScheduleOptimizationsAsync(Guid userId, IEnumerable<TimeSlot> timeSlots, ScheduleOptimizationPreferences optimizationPreferences, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ScheduleOptimizationResult?>(null); // Mock implementation
    }

    public Task<IEnumerable<BreakRecommendation>?> GetBreakRecommendationsAsync(Guid userId, WorkloadAnalysis workloadAnalysis, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<BreakRecommendation>?>(new List<BreakRecommendation>
        {
            new BreakRecommendation(
                RecommendedTime: TimeSpan.FromHours(10.5),
                Duration: TimeSpan.FromMinutes(15),
                BreakType: BreakType.Active,
                ActivitySuggestion: "Take a short walk",
                Reasoning: "Mock: You've been working for 2 hours",
                ImportanceScore: 0.8
            )
        });
    }

    public Task<ProductivityInsights?> GetProductivityInsightsAsync(Guid userId, TimeframeAnalysis analysisTimeframe, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProductivityInsights?>(null); // Mock implementation
    }

    public Task<IEnumerable<CategorySuggestion>?> GetTaskCategorizationSuggestionsAsync(Guid userId, string taskContent, UserCategoryHistory userCategoryHistory, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<CategorySuggestion>?>(new List<CategorySuggestion>
        {
            new CategorySuggestion(
                SuggestedCategory: "Work",
                ConfidenceScore: 0.9,
                Reasoning: "Mock: Task contains work-related keywords",
                AlternativeCategories: new List<string> { "Project", "Administrative" },
                CategoryProbabilities: new Dictionary<string, double> { ["Work"] = 0.9, ["Project"] = 0.08, ["Administrative"] = 0.02 }
            )
        });
    }

    public Task<bool> IsAIServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true); // Mock is always available
    }

    public Task<AIServiceHealthStatus> GetAIServiceHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AIServiceHealthStatus(
            IsHealthy: true,
            ResponseTime: TimeSpan.FromMilliseconds(10),
            ServiceVersion: "Mock-1.0",
            AvailableCapabilities: new List<string> { "DayPlanning", "PrioritySuggestions", "BreakRecommendations", "TaskCategorization" },
            DetailedChecks: new List<HealthCheckResult>
            {
                new HealthCheckResult(
                    CheckName: "Mock Provider",
                    Passed: true,
                    ErrorMessage: null,
                    Duration: TimeSpan.FromMilliseconds(1),
                    Metadata: new Dictionary<string, object> { ["mock"] = true }
                )
            },
            CheckTimestamp: DateTime.UtcNow
        ));
    }

    public Task<IEnumerable<TaskTimeEstimate>?> GenerateTaskTimeEstimatesAsync(Guid userId, IEnumerable<TaskEstimationRequest> taskEstimationRequests, UserHistoricalPerformance historicalPerformance, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<TaskTimeEstimate>?>(taskEstimationRequests.Select(req => new TaskTimeEstimate(
            TaskId: req.TaskId,
            EstimatedDuration: TimeSpan.FromHours(2),
            MinDuration: TimeSpan.FromMinutes(90),
            MaxDuration: TimeSpan.FromHours(3),
            ConfidenceLevel: 0.75,
            EstimationFactors: new List<string> { "Mock estimation based on task complexity" }
        )));
    }
}
