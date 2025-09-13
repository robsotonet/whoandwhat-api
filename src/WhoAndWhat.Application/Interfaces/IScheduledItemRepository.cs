using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for ScheduledItem entities
/// Provides data access methods for smart scheduling items
/// </summary>
public interface IScheduledItemRepository : IRepository<ScheduledItem>
{
    /// <summary>
    /// Gets scheduled items for a user within a date range
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items in date range</returns>
    Task<IEnumerable<ScheduledItem>> GetByUserAndDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items for a specific date
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="date">The specific date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items for the date</returns>
    Task<IEnumerable<ScheduledItem>> GetByUserAndDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active (in progress) scheduled items for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currently active scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetActiveItemsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming scheduled items for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="hoursAhead">Hours to look ahead (default: 24)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upcoming scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetUpcomingItemsAsync(Guid userId, int hoursAhead = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue scheduled items for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Overdue scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetOverdueItemsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items by item type
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="itemType">The item type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items of the specified type</returns>
    Task<IEnumerable<ScheduledItem>> GetByItemTypeAsync(Guid userId, int itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items by category
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="category">The category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items in the category</returns>
    Task<IEnumerable<ScheduledItem>> GetByCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items associated with a specific task
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items for the task</returns>
    Task<IEnumerable<ScheduledItem>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items from external calendar events
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="calendarSource">The calendar source (e.g., "Google", "Outlook")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>External calendar scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetExternalCalendarItemsAsync(Guid userId, string? calendarSource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for time conflicts with existing scheduled items
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startTime">Start time of the potential item</param>
    /// <param name="endTime">End time of the potential item</param>
    /// <param name="excludeItemId">Item ID to exclude from conflict check (for updates)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conflicting scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetConflictingItemsAsync(Guid userId, DateTime startTime, DateTime endTime, Guid? excludeItemId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets flexible scheduled items that can be rescheduled
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date for search range</param>
    /// <param name="endDate">End date for search range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Flexible scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetFlexibleItemsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items by time block
    /// </summary>
    /// <param name="timeBlockId">The time block ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduled items in the time block</returns>
    Task<IEnumerable<ScheduledItem>> GetByTimeBlockAsync(Guid timeBlockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items with low confidence scores (may need reoptimization)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="maxConfidenceScore">Maximum confidence score threshold</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Low confidence scheduled items</returns>
    Task<IEnumerable<ScheduledItem>> GetLowConfidenceItemsAsync(Guid userId, double maxConfidenceScore = 0.5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates completion status for multiple scheduled items
    /// </summary>
    /// <param name="itemIds">The scheduled item IDs</param>
    /// <param name="isCompleted">Completion status</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items updated</returns>
    Task<int> BulkUpdateCompletionStatusAsync(IEnumerable<Guid> itemIds, bool isCompleted, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules multiple items to new time slots
    /// </summary>
    /// <param name="reschedulingData">Dictionary of item ID to new start/end times</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items rescheduled</returns>
    Task<int> BulkRescheduleAsync(Dictionary<Guid, (DateTime StartTime, DateTime EndTime)> reschedulingData, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes scheduled items older than specified date
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="olderThan">Delete items older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items deleted</returns>
    Task<int> DeleteOldItemsAsync(Guid userId, DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduling statistics for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduling statistics</returns>
    Task<Dictionary<string, object>> GetSchedulingStatisticsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets productivity correlations for scheduled items
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity correlation data</returns>
    Task<Dictionary<string, double>> GetProductivityCorrelationsAsync(Guid userId, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets optimal time slots based on historical completion rates
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="category">Task category</param>
    /// <param name="duration">Required duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimal time slots with success rates</returns>
    Task<IEnumerable<(TimeSpan StartTime, TimeSpan EndTime, double SuccessRate)>> GetOptimalTimeSlotsAsync(Guid userId, string category, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a scheduled item belongs to the specified user
    /// </summary>
    /// <param name="itemId">The scheduled item ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the item belongs to the user</returns>
    Task<bool> ItemBelongsToUserAsync(Guid itemId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduling trends for machine learning analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="days">Number of days to analyze (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scheduling trends data</returns>
    Task<Dictionary<string, object>> GetSchedulingTrendsAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default);
}