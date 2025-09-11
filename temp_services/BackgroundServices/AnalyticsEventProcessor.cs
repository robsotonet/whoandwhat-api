using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Services.Analytics;

namespace WhoAndWhat.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for processing analytics events in real-time
/// Handles task completion, creation, and other events that trigger analytics updates
/// </summary>
public class AnalyticsEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsEventProcessor> _logger;
    private readonly ConcurrentQueue<AnalyticsEvent> _eventQueue = new();
    private readonly SemaphoreSlim _processingThrottle = new(10); // Limit concurrent processing

    public AnalyticsEventProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Queues an analytics event for processing
    /// </summary>
    public void QueueEvent(Guid userId, string eventType, Dictionary<string, object>? eventData = null)
    {
        var analyticsEvent = new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            EventData = eventData ?? new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow,
            RetryCount = 0
        };

        _eventQueue.Enqueue(analyticsEvent);
        _logger.LogDebug("Queued analytics event {EventType} for user {UserId} (Queue size: {QueueSize})",
            eventType, userId, _eventQueue.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analytics event processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_eventQueue.TryDequeue(out var analyticsEvent))
                {
                    // Use throttling to prevent overwhelming the system
                    await _processingThrottle.WaitAsync(stoppingToken);

                    // Process event in background without blocking the queue
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessEventAsync(analyticsEvent, stoppingToken);
                        }
                        finally
                        {
                            _processingThrottle.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No events to process, wait a short time
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Analytics event processor is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in analytics event processor main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessEventAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<IAnalyticsProcessingService>();

            _logger.LogDebug("Processing analytics event {EventId} ({EventType}) for user {UserId}",
                analyticsEvent.Id, analyticsEvent.EventType, analyticsEvent.UserId);

            var startTime = DateTime.UtcNow;

            await processingService.ProcessTaskEventAsync(
                analyticsEvent.UserId,
                analyticsEvent.EventType,
                analyticsEvent.EventData,
                cancellationToken);

            var processingTime = DateTime.UtcNow - startTime;

            _logger.LogDebug("Completed processing analytics event {EventId} in {Duration}ms",
                analyticsEvent.Id, processingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            analyticsEvent.RetryCount++;

            if (analyticsEvent.RetryCount <= maxRetries)
            {
                _logger.LogWarning(ex, "Failed to process analytics event {EventId} (attempt {Attempt}/{MaxRetries}), retrying",
                    analyticsEvent.Id, analyticsEvent.RetryCount, maxRetries);

                // Re-queue with exponential backoff
                var delay = TimeSpan.FromSeconds(Math.Pow(2, analyticsEvent.RetryCount));
                _ = Task.Delay(delay, cancellationToken).ContinueWith(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _eventQueue.Enqueue(analyticsEvent);
                    }
                }, cancellationToken);
            }
            else
            {
                _logger.LogError(ex, "Failed to process analytics event {EventId} after {MaxRetries} attempts, dropping event",
                    analyticsEvent.Id, maxRetries);

                // Could optionally store failed events in a dead letter queue
            }
        }
    }

    /// <summary>
    /// Gets current queue statistics
    /// </summary>
    public AnalyticsEventProcessorStats GetStats()
    {
        return new AnalyticsEventProcessorStats
        {
            QueueSize = _eventQueue.Count,
            ActiveProcessors = 10 - _processingThrottle.CurrentCount,
            IsHealthy = _eventQueue.Count < 1000 // Arbitrary threshold
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analytics event processor is stopping, processing remaining {QueueSize} events", _eventQueue.Count);

        try
        {
            // Process remaining events with a timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var remainingEvents = new List<AnalyticsEvent>();
            while (_eventQueue.TryDequeue(out var evt))
            {
                remainingEvents.Add(evt);
            }

            if (remainingEvents.Count > 0)
            {
                _logger.LogInformation("Processing {Count} remaining analytics events", remainingEvents.Count);

                var tasks = remainingEvents.Select(evt => ProcessEventAsync(evt, timeoutCts.Token)).ToArray();
                await Task.WhenAll(tasks);
            }

            await base.StopAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analytics event processor stop operation was cancelled, some events may have been lost");
        }

        _logger.LogInformation("Analytics event processor stopped");
    }
}

/// <summary>
/// Represents an analytics event to be processed
/// </summary>
public class AnalyticsEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Dictionary<string, object> EventData { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// Statistics about the analytics event processor
/// </summary>
public record AnalyticsEventProcessorStats
{
    public int QueueSize { get; init; }
    public int ActiveProcessors { get; init; }
    public bool IsHealthy { get; init; }
}
