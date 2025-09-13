using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for AI-powered task planning, prioritization, and scheduling optimization
/// </summary>
public interface IAIPlanningService
{
    /// <summary>
    /// Generate an AI-optimized day plan for a user based on their tasks and preferences
    /// </summary>
    /// <param name="userId">User ID to generate plan for</param>
    /// <param name="planDate">Date to generate the plan for</param>
    /// <param name="preferences">User preferences and constraints for planning</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated day plan with suggested task scheduling and time blocks</returns>
    public Task<AIGeneratedPlan?> GenerateDayPlanAsync(Guid userId, DateTime planDate, UserPlanningPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI-powered task priority suggestions based on task content, context, and user patterns
    /// </summary>
    /// <param name="userId">User ID for personalized suggestions</param>
    /// <param name="taskContexts">List of tasks to analyze and prioritize</param>
    /// <param name="analysisContext">Additional context for priority analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of task priority suggestions with AI reasoning</returns>
    public Task<IEnumerable<TaskPrioritySuggestion>?> GetTaskPrioritySuggestionsAsync(Guid userId, IEnumerable<TaskAnalysisContext> taskContexts, PriorityAnalysisContext analysisContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate AI-powered schedule optimizations for better productivity and time management
    /// </summary>
    /// <param name="userId">User ID for personalized optimization</param>
    /// <param name="timeSlots">Available time slots and current schedule</param>
    /// <param name="optimizationPreferences">User preferences for schedule optimization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schedule optimization suggestions with productivity insights</returns>
    public Task<ScheduleOptimizationResult?> GenerateScheduleOptimizationsAsync(Guid userId, IEnumerable<TimeSlot> timeSlots, ScheduleOptimizationPreferences optimizationPreferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI-generated break and rest recommendations based on user activity patterns
    /// </summary>
    /// <param name="userId">User ID for personalized recommendations</param>
    /// <param name="workloadAnalysis">Current workload and productivity analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Break recommendations with timing and activity suggestions</returns>
    public Task<IEnumerable<BreakRecommendation>?> GetBreakRecommendationsAsync(Guid userId, WorkloadAnalysis workloadAnalysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI-powered productivity insights and patterns analysis
    /// </summary>
    /// <param name="userId">User ID for analysis</param>
    /// <param name="analysisTimeframe">Timeframe for productivity analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity insights with actionable recommendations</returns>
    public Task<ProductivityInsights?> GetProductivityInsightsAsync(Guid userId, TimeframeAnalysis analysisTimeframe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate smart task categorization suggestions using AI content analysis
    /// </summary>
    /// <param name="userId">User ID for personalized categorization</param>
    /// <param name="taskContent">Task content to analyze for categorization</param>
    /// <param name="userCategoryHistory">User's historical categorization patterns</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category suggestions with confidence scores</returns>
    public Task<IEnumerable<CategorySuggestion>?> GetTaskCategorizationSuggestionsAsync(Guid userId, string taskContent, UserCategoryHistory userCategoryHistory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if AI planning service is properly configured and available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if AI service is ready and responding</returns>
    public Task<bool> IsAIServiceAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI service health status with detailed diagnostics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed health status including response times and service capabilities</returns>
    public Task<AIServiceHealthStatus> GetAIServiceHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate AI-powered task completion time estimates
    /// </summary>
    /// <param name="userId">User ID for personalized estimates</param>
    /// <param name="taskEstimationRequests">Tasks to estimate completion time for</param>
    /// <param name="historicalPerformance">User's historical task completion performance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time estimates with confidence intervals and factors affecting estimation</returns>
    public Task<IEnumerable<TaskTimeEstimate>?> GenerateTaskTimeEstimatesAsync(Guid userId, IEnumerable<TaskEstimationRequest> taskEstimationRequests, UserHistoricalPerformance historicalPerformance, CancellationToken cancellationToken = default);
}
