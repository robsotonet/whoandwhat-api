using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.API.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates including analytics, metrics, and productivity data
/// </summary>
[Authorize]
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;
    private readonly IDashboardCacheService _dashboardCacheService;

    public DashboardHub(
        ILogger<DashboardHub> logger,
        IDashboardCacheService dashboardCacheService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dashboardCacheService = dashboardCacheService ?? throw new ArgumentNullException(nameof(dashboardCacheService));
    }

    /// <summary>
    /// Handle client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                // Add user to their personal dashboard group
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserDashboardGroupName(userId.Value));

                _logger.LogInformation("User {UserId} connected to dashboard hub with connection {ConnectionId}",
                    userId.Value, Context.ConnectionId);

                // Notify client that connection is established
                await Clients.Caller.SendAsync("DashboardConnected", new { userId = userId.Value, timestamp = DateTime.UtcNow });
            }
            else
            {
                _logger.LogWarning("Anonymous user attempted to connect to dashboard hub with connection {ConnectionId}",
                    Context.ConnectionId);
                Context.Abort();
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dashboard hub connection for {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                // Remove user from their personal dashboard group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserDashboardGroupName(userId.Value));

                _logger.LogInformation("User {UserId} disconnected from dashboard hub with connection {ConnectionId}",
                    userId.Value, Context.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dashboard hub disconnection for {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific dashboard view (e.g., analytics, tasks, productivity)
    /// </summary>
    /// <param name="viewType">Type of dashboard view</param>
    public async Task JoinDashboardView(string viewType)
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var groupName = GetUserDashboardViewGroupName(userId.Value, viewType);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug("User {UserId} joined dashboard view '{ViewType}' with connection {ConnectionId}",
                userId.Value, viewType, Context.ConnectionId);

            await Clients.Caller.SendAsync("JoinedDashboardView", new { viewType, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining dashboard view '{ViewType}' for connection {ConnectionId}",
                viewType, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to join dashboard view");
        }
    }

    /// <summary>
    /// Leave a specific dashboard view
    /// </summary>
    /// <param name="viewType">Type of dashboard view</param>
    public async Task LeaveDashboardView(string viewType)
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var groupName = GetUserDashboardViewGroupName(userId.Value, viewType);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug("User {UserId} left dashboard view '{ViewType}' with connection {ConnectionId}",
                userId.Value, viewType, Context.ConnectionId);

            await Clients.Caller.SendAsync("LeftDashboardView", new { viewType, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving dashboard view '{ViewType}' for connection {ConnectionId}",
                viewType, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to leave dashboard view");
        }
    }

    /// <summary>
    /// Request current dashboard data
    /// </summary>
    public async Task RequestDashboardData()
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            // Get cached dashboard data if available
            var dashboardSummary = await _dashboardCacheService.GetCachedDashboardSummaryAsync(userId.Value);
            
            if (dashboardSummary != null)
            {
                await Clients.Caller.SendAsync("DashboardDataUpdate", new
                {
                    type = "summary",
                    data = dashboardSummary,
                    timestamp = DateTime.UtcNow,
                    source = "cache"
                });

                _logger.LogDebug("Sent cached dashboard data to user {UserId} via connection {ConnectionId}",
                    userId.Value, Context.ConnectionId);
            }
            else
            {
                // No cached data available, client should fetch from API
                await Clients.Caller.SendAsync("DashboardDataUnavailable", new
                {
                    message = "Dashboard data not cached, please fetch from API",
                    timestamp = DateTime.UtcNow
                });

                _logger.LogDebug("No cached dashboard data available for user {UserId}", userId.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting dashboard data for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to retrieve dashboard data");
        }
    }

    /// <summary>
    /// Request productivity metrics for a specific period
    /// </summary>
    /// <param name="period">Time period for metrics</param>
    public async Task RequestProductivityMetrics(string period)
    {
        try
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            // Validate period parameter
            if (string.IsNullOrWhiteSpace(period))
            {
                await Clients.Caller.SendAsync("Error", "Invalid period specified");
                return;
            }

            // Get cached productivity metrics if available
            var metrics = await _dashboardCacheService.GetCachedProductivityMetricsAsync(userId.Value, period);
            
            if (metrics != null)
            {
                await Clients.Caller.SendAsync("ProductivityMetricsUpdate", new
                {
                    type = "metrics",
                    period,
                    data = metrics,
                    timestamp = DateTime.UtcNow,
                    source = "cache"
                });

                _logger.LogDebug("Sent cached productivity metrics for period '{Period}' to user {UserId} via connection {ConnectionId}",
                    period, userId.Value, Context.ConnectionId);
            }
            else
            {
                await Clients.Caller.SendAsync("ProductivityMetricsUnavailable", new
                {
                    period,
                    message = "Productivity metrics not cached for this period",
                    timestamp = DateTime.UtcNow
                });

                _logger.LogDebug("No cached productivity metrics available for user {UserId}, period '{Period}'",
                    userId.Value, period);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting productivity metrics for period '{Period}', connection {ConnectionId}",
                period, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to retrieve productivity metrics");
        }
    }

    /// <summary>
    /// Get the authenticated user's ID from the hub context
    /// </summary>
    private Guid? GetUserIdFromContext()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Get the group name for a user's dashboard
    /// </summary>
    private static string GetUserDashboardGroupName(Guid userId)
        => $"dashboard:user:{userId}";

    /// <summary>
    /// Get the group name for a user's specific dashboard view
    /// </summary>
    private static string GetUserDashboardViewGroupName(Guid userId, string viewType)
        => $"dashboard:user:{userId}:view:{viewType}";
}

/// <summary>
/// Extension methods for the DashboardHub to send notifications from outside the hub
/// </summary>
public static class DashboardHubExtensions
{
    /// <summary>
    /// Send dashboard summary update to a specific user
    /// </summary>
    public static async Task SendDashboardSummaryUpdate(this IHubContext<DashboardHub> hubContext, 
        Guid userId, object dashboardSummary)
    {
        var groupName = $"dashboard:user:{userId}";
        await hubContext.Clients.Group(groupName).SendAsync("DashboardDataUpdate", new
        {
            type = "summary",
            data = dashboardSummary,
            timestamp = DateTime.UtcNow,
            source = "realtime"
        });
    }

    /// <summary>
    /// Send task completion update to user's dashboard
    /// </summary>
    public static async Task SendTaskCompletionUpdate(this IHubContext<DashboardHub> hubContext,
        Guid userId, object taskUpdate)
    {
        var groupName = $"dashboard:user:{userId}";
        await hubContext.Clients.Group(groupName).SendAsync("TaskCompletionUpdate", new
        {
            type = "taskCompletion",
            data = taskUpdate,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send productivity streak update to user's dashboard
    /// </summary>
    public static async Task SendProductivityStreakUpdate(this IHubContext<DashboardHub> hubContext,
        Guid userId, object streakUpdate)
    {
        var groupName = $"dashboard:user:{userId}";
        await hubContext.Clients.Group(groupName).SendAsync("ProductivityStreakUpdate", new
        {
            type = "streakUpdate",
            data = streakUpdate,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send productivity metrics update to users in a specific dashboard view
    /// </summary>
    public static async Task SendProductivityMetricsUpdate(this IHubContext<DashboardHub> hubContext,
        Guid userId, string period, object metricsUpdate)
    {
        var groupName = $"dashboard:user:{userId}:view:analytics";
        await hubContext.Clients.Group(groupName).SendAsync("ProductivityMetricsUpdate", new
        {
            type = "metrics",
            period,
            data = metricsUpdate,
            timestamp = DateTime.UtcNow,
            source = "realtime"
        });
    }

    /// <summary>
    /// Send analytics snapshot update to user's dashboard
    /// </summary>
    public static async Task SendAnalyticsSnapshotUpdate(this IHubContext<DashboardHub> hubContext,
        Guid userId, object snapshotUpdate)
    {
        var groupName = $"dashboard:user:{userId}";
        await hubContext.Clients.Group(groupName).SendAsync("AnalyticsSnapshotUpdate", new
        {
            type = "analyticsSnapshot",
            data = snapshotUpdate,
            timestamp = DateTime.UtcNow
        });
    }
}