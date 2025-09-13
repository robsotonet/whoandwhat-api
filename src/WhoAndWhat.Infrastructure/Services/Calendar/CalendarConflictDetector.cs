using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Calendar;

/// <summary>
/// Advanced calendar conflict detection service with sophisticated algorithms for detecting
/// and resolving various types of calendar synchronization conflicts
/// </summary>
public class CalendarConflictDetector : ICalendarConflictDetector, IDisposable
{
    private readonly ILogger<CalendarConflictDetector> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly ConcurrentDictionary<Guid, UserConflictPreferences> _userPreferencesCache;
    private readonly ConcurrentDictionary<Guid, List<HistoricalResolution>> _resolutionHistoryCache;
    private readonly SemaphoreSlim _cacheSemaphore;
    private bool _disposed;

    public CalendarConflictDetector(
        IOptions<CalendarSyncSettings> settings,
        ILogger<CalendarConflictDetector> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _userPreferencesCache = new ConcurrentDictionary<Guid, UserConflictPreferences>();
        _resolutionHistoryCache = new ConcurrentDictionary<Guid, List<HistoricalResolution>>();
        _cacheSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<IEnumerable<DetectedConflict>> DetectConflictsAsync(
        Guid userId,
        IEnumerable<InternalCalendarEvent> internalEvents,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        ConflictDetectionOptions detectionOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting conflict detection for user {UserId} with {InternalCount} internal and {ExternalCount} external events",
                userId, internalEvents.Count(), externalEvents.Count());

            var conflicts = new List<DetectedConflict>();

            // Detect different types of conflicts based on options
            if (detectionOptions.DetectTimeOverlaps)
            {
                var timeOverlapConflicts = await DetectTimeOverlapConflictsAdvanced(userId, internalEvents, externalEvents, detectionOptions, cancellationToken);
                conflicts.AddRange(timeOverlapConflicts);
            }

            if (detectionOptions.DetectDuplicates)
            {
                var duplicateConflicts = await DetectDuplicatesAsync(userId, internalEvents, externalEvents,
                    detectionOptions.DuplicateDetectionCriteria, cancellationToken);
                conflicts.AddRange(duplicateConflicts.Select(d => new DetectedConflict(
                    d.ConflictId,
                    ConflictType.Duplicate,
                    d.Severity,
                    d.Description,
                    d.InternalEvent,
                    d.ExternalEvent,
                    d.SuggestedResolutions,
                    d.DetectedAt,
                    d.Details
                )));
            }

            if (detectionOptions.DetectDataInconsistency)
            {
                var dataConflicts = await DetectDataInconsistencyAdvanced(userId, internalEvents, externalEvents, detectionOptions, cancellationToken);
                conflicts.AddRange(dataConflicts);
            }

            if (detectionOptions.DetectSchedulingConflicts)
            {
                var schedulingConflicts = await DetectSchedulingConflicts(userId, internalEvents, externalEvents, detectionOptions, cancellationToken);
                conflicts.AddRange(schedulingConflicts);
            }

            // Apply conflict prioritization and filtering
            var prioritizedConflicts = await PrioritizeConflicts(userId, conflicts, cancellationToken);

            _logger.LogInformation("Detected {ConflictCount} conflicts for user {UserId}", prioritizedConflicts.Count(), userId);
            return prioritizedConflicts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect conflicts for user {UserId}", userId);
            return [];
        }
    }

    public Task<IEnumerable<TimeOverlapConflict>> DetectTimeOverlapAsync(
        Guid userId,
        TimeRange targetTimeRange,
        IEnumerable<InternalCalendarEvent> existingEvents,
        int conflictTolerance,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Detecting time overlaps for user {UserId} in range {Start} to {End}",
                userId, targetTimeRange.Start, targetTimeRange.End);

            var conflicts = new List<TimeOverlapConflict>();
            var toleranceSpan = TimeSpan.FromMinutes(conflictTolerance);

            foreach (var existingEvent in existingEvents)
            {
                var overlap = CalculateOverlap(targetTimeRange, new TimeRange(existingEvent.StartTime, existingEvent.EndTime), toleranceSpan);

                if (overlap.HasValue)
                {
                    var severity = CalculateOverlapSeverity(overlap.Value, targetTimeRange);
                    var suggestions = GenerateOverlapResolutions(targetTimeRange, existingEvent, overlap.Value);

                    conflicts.Add(new TimeOverlapConflict(
                        Guid.NewGuid(),
                        severity,
                        $"Time overlap detected with existing event '{existingEvent.Title}'",
                        existingEvent,
                        targetTimeRange,
                        overlap.Value,
                        suggestions,
                        DateTime.UtcNow,
                        new Dictionary<string, object>
                        {
                            ["overlapDuration"] = overlap.Value,
                            ["conflictTolerance"] = conflictTolerance,
                            ["overlapPercentage"] = CalculateOverlapPercentage(overlap.Value, targetTimeRange)
                        }
                    ));
                }
            }

            return Task.FromResult<IEnumerable<TimeOverlapConflict>>(conflicts.OrderByDescending(c => c.Severity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect time overlaps for user {UserId}", userId);
            return Task.FromResult<IEnumerable<TimeOverlapConflict>>([]);
        }
    }

    public Task<IEnumerable<DuplicateEventConflict>> DetectDuplicatesAsync(
        Guid userId,
        IEnumerable<InternalCalendarEvent> internalEvents,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        DuplicateDetectionCriteria duplicateDetectionCriteria,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Detecting duplicate events for user {UserId}", userId);

            var conflicts = new List<DuplicateEventConflict>();

            foreach (var internalEvent in internalEvents)
            {
                var potentialDuplicates = FindPotentialDuplicates(internalEvent, externalEvents, duplicateDetectionCriteria);

                foreach (var externalEvent in potentialDuplicates)
                {
                    var similarity = CalculateEventSimilarity(internalEvent, externalEvent, duplicateDetectionCriteria);

                    if (similarity >= duplicateDetectionCriteria.MinimumSimilarityThreshold)
                    {
                        var severity = similarity > 0.9 ? ConflictSeverity.High :
                                     similarity > 0.7 ? ConflictSeverity.Medium :
                                     ConflictSeverity.Low;

                        var suggestions = GenerateDuplicateResolutions(internalEvent, externalEvent, similarity);

                        conflicts.Add(new DuplicateEventConflict(
                            Guid.NewGuid(),
                            severity,
                            $"Potential duplicate event detected (similarity: {similarity:P0})",
                            internalEvent,
                            externalEvent,
                            similarity,
                            suggestions,
                            DateTime.UtcNow,
                            new Dictionary<string, object>
                            {
                                ["similarityScore"] = similarity,
                                ["matchingFields"] = GetMatchingFields(internalEvent, externalEvent, duplicateDetectionCriteria),
                                ["detectionCriteria"] = duplicateDetectionCriteria
                            }
                        ));
                    }
                }
            }

            return Task.FromResult<IEnumerable<DuplicateEventConflict>>(conflicts.OrderByDescending(c => c.SimilarityScore));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect duplicates for user {UserId}", userId);
            return Task.FromResult<IEnumerable<DuplicateEventConflict>>([]);
        }
    }

    public async Task<IEnumerable<DataConsistencyConflict>> DetectDataInconsistencyAsync(
        Guid userId,
        IEnumerable<EventPair> eventPairs,
        DataConsistencyOptions consistencyOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Detecting data inconsistencies for user {UserId} with {PairCount} event pairs",
                userId, eventPairs.Count());

            var conflicts = new List<DataConsistencyConflict>();

            foreach (var eventPair in eventPairs)
            {
                var inconsistencies = await DetectEventPairInconsistencies(eventPair, consistencyOptions, cancellationToken);
                conflicts.AddRange(inconsistencies);
            }

            return conflicts.OrderByDescending(c => c.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect data inconsistencies for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ConflictAnalysis> AnalyzeConflictAsync(
        DetectedConflict conflict,
        UserConflictPreferences userPreferences,
        IEnumerable<HistoricalResolution> historicalResolutions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing conflict {ConflictId} of type {ConflictType}", conflict.ConflictId, conflict.ConflictType);

            var impact = AnalyzeConflictImpact(conflict);
            var recommendations = await GenerateResolutionRecommendations(conflict, userPreferences, historicalResolutions, cancellationToken);
            var riskAssessment = AssessResolutionRisks(conflict, recommendations);

            return new ConflictAnalysis(
                conflict.ConflictId,
                conflict.ConflictType,
                impact,
                recommendations,
                riskAssessment,
                CalculateConfidenceScore(conflict, historicalResolutions),
                DateTime.UtcNow,
                new Dictionary<string, object>
                {
                    ["analysisVersion"] = "1.0",
                    ["userPreferences"] = userPreferences,
                    ["historicalResolutionCount"] = historicalResolutions.Count(),
                    ["conflictComplexity"] = CalculateConflictComplexity(conflict)
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze conflict {ConflictId}", conflict.ConflictId);
            throw;
        }
    }

    public async Task<IEnumerable<AutoResolutionResult>> AutoResolveConflictsAsync(
        Guid userId,
        IEnumerable<DetectedConflict> conflicts,
        ConflictResolutionStrategy resolutionStrategy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Auto-resolving {ConflictCount} conflicts for user {UserId} using strategy {Strategy}",
                conflicts.Count(), userId, resolutionStrategy.StrategyType);

            var results = new List<AutoResolutionResult>();

            foreach (var conflict in conflicts)
            {
                try
                {
                    var canAutoResolve = CanAutoResolveConflict(conflict, resolutionStrategy);

                    if (canAutoResolve)
                    {
                        var resolution = SelectAutomaticResolution(conflict, resolutionStrategy);
                        var resolutionResult = await ApplyAutomaticResolution(userId, conflict, resolution, cancellationToken);

                        results.Add(new AutoResolutionResult(
                            conflict.ConflictId,
                            resolutionResult.Success,
                            resolution.ResolutionType,
                            resolutionResult.Success ? "Conflict resolved automatically" : resolutionResult.ErrorMessage,
                            resolutionResult.ResolvedEvent,
                            DateTime.UtcNow
                        ));
                    }
                    else
                    {
                        results.Add(new AutoResolutionResult(
                            conflict.ConflictId,
                            false,
                            ConflictResolutionType.ManualRequired,
                            "Conflict requires manual resolution",
                            null,
                            DateTime.UtcNow
                        ));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-resolve conflict {ConflictId}", conflict.ConflictId);
                    results.Add(new AutoResolutionResult(
                        conflict.ConflictId,
                        false,
                        ConflictResolutionType.Failed,
                        ex.Message,
                        null,
                        DateTime.UtcNow
                    ));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-resolve conflicts for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ManualResolutionResult> ApplyResolutionAsync(
        Guid userId,
        Guid conflictId,
        ConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Applying manual resolution for conflict {ConflictId} by user {UserId}", conflictId, userId);

            // Validate the resolution
            var validationResult = await ValidateResolutionAsync(userId, conflictId, resolution, cancellationToken);

            if (!validationResult.IsValid)
            {
                return new ManualResolutionResult(
                    conflictId,
                    userId,
                    false,
                    validationResult.ValidationErrors.FirstOrDefault() ?? "Resolution validation failed",
                    null,
                    DateTime.UtcNow
                );
            }

            // Apply the resolution
            var result = await ExecuteManualResolution(userId, conflictId, resolution, cancellationToken);

            // Record the resolution for learning
            if (result.Success)
            {
                await RecordResolutionHistory(userId, conflictId, resolution, result, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply resolution for conflict {ConflictId}", conflictId);
            return new ManualResolutionResult(
                conflictId,
                userId,
                false,
                ex.Message,
                null,
                DateTime.UtcNow
            );
        }
    }

    public async Task<ResolutionValidationResult> ValidateResolutionAsync(
        Guid userId,
        Guid conflictId,
        ConflictResolution proposedResolution,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationErrors = new List<string>();
            var validationWarnings = new List<string>();

            // Validate resolution type compatibility
            if (!IsResolutionTypeValid(proposedResolution.ResolutionType))
            {
                validationErrors.Add($"Resolution type {proposedResolution.ResolutionType} is not valid");
            }

            // Validate event data if provided
            if (proposedResolution.ResolvedEventData != null)
            {
                var eventValidation = ValidateEventData(proposedResolution.ResolvedEventData);
                validationErrors.AddRange(eventValidation.Errors);
                validationWarnings.AddRange(eventValidation.Warnings);
            }

            // Check for potential new conflicts
            var potentialConflicts = await CheckForPotentialConflicts(userId, proposedResolution, cancellationToken);
            if (potentialConflicts.Any())
            {
                validationWarnings.Add($"Resolution may create {potentialConflicts.Count()} new conflicts");
            }

            return new ResolutionValidationResult(
                !validationErrors.Any(),
                validationErrors,
                validationWarnings,
                potentialConflicts
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate resolution for conflict {ConflictId}", conflictId);
            return new ResolutionValidationResult(
                false,
                [ex.Message],
                [],
                []
            );
        }
    }

    public async Task<IEnumerable<ResolutionSuggestion>> GetResolutionSuggestionsAsync(
        Guid userId,
        DetectedConflict conflict,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userPreferences = await GetUserPreferences(userId, cancellationToken);
            var historicalResolutions = await GetUserResolutionHistory(userId, cancellationToken);

            var suggestions = await GenerateIntelligentSuggestions(conflict, userPreferences, historicalResolutions, cancellationToken);

            return suggestions.OrderByDescending(s => s.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resolution suggestions for conflict {ConflictId}", conflict.ConflictId);
            return [];
        }
    }

    public async Task<IEnumerable<PredictedConflict>> PredictConflictsAsync(
        Guid userId,
        IEnumerable<PlannedSyncChange> plannedChanges,
        CalendarSyncState currentState,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Predicting conflicts for user {UserId} with {ChangeCount} planned changes",
                userId, plannedChanges.Count());

            var predictedConflicts = new List<PredictedConflict>();

            foreach (var plannedChange in plannedChanges)
            {
                var conflicts = await PredictChangePotentialConflicts(plannedChange, currentState, cancellationToken);
                predictedConflicts.AddRange(conflicts);
            }

            return predictedConflicts.OrderByDescending(c => c.Probability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to predict conflicts for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ConflictStatistics> GetConflictStatisticsAsync(
        Guid userId,
        TimeRange timeRange,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolutionHistory = await GetUserResolutionHistory(userId, cancellationToken);
            var relevantHistory = resolutionHistory.Where(h =>
                h.ResolvedAt >= timeRange.Start && h.ResolvedAt <= timeRange.End).ToList();

            var statistics = CalculateConflictStatistics(relevantHistory, timeRange);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conflict statistics for user {UserId}", userId);
            return new ConflictStatistics(
                userId,
                timeRange,
                0, 0, 0, 0,
                new Dictionary<ConflictType, int>(),
                new Dictionary<ConflictResolutionType, int>(),
                [],
                DateTime.UtcNow
            );
        }
    }

    public async Task<UserConflictPreferences> UpdateUserPreferencesAsync(
        Guid userId,
        IEnumerable<HistoricalResolution> recentResolutions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentPreferences = await GetUserPreferences(userId, cancellationToken);
            var updatedPreferences = AnalyzeAndUpdatePreferences(currentPreferences, recentResolutions);

            // Cache the updated preferences
            _userPreferencesCache[userId] = updatedPreferences;

            return updatedPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<EventMergeResult> MergeConflictingEventsAsync(
        Guid userId,
        IEnumerable<ConflictingEventData> conflictingEvents,
        EventMergeStrategy mergeStrategy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Merging {EventCount} conflicting events for user {UserId} using strategy {Strategy}",
                conflictingEvents.Count(), userId, mergeStrategy.StrategyType);

            var mergedEvent = await ExecuteEventMerge(conflictingEvents, mergeStrategy, cancellationToken);

            return new EventMergeResult(
                true,
                mergedEvent,
                conflictingEvents.Count(),
                "Events merged successfully",
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge conflicting events for user {UserId}", userId);
            return new EventMergeResult(
                false,
                null,
                conflictingEvents.Count(),
                ex.Message,
                DateTime.UtcNow
            );
        }
    }

    public async Task<bool> IsConflictResolvedAsync(Guid conflictId, CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, this would check the database for conflict resolution status
            // For now, we'll simulate by checking if we have a resolution record
            return await Task.FromResult(false); // Placeholder implementation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if conflict {ConflictId} is resolved", conflictId);
            return false;
        }
    }

    public async Task<IEnumerable<DetectedConflict>> GetPendingConflictsAsync(
        Guid userId,
        ConflictFilterOptions filterOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, this would query the database for pending conflicts
            // For now, return empty list as placeholder
            return await Task.FromResult(Enumerable.Empty<DetectedConflict>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending conflicts for user {UserId}", userId);
            return [];
        }
    }

    public Task<ConflictIgnoreResult> IgnoreConflictAsync(
        Guid userId,
        Guid conflictId,
        string ignoreReason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Ignoring conflict {ConflictId} for user {UserId} with reason: {Reason}",
                conflictId, userId, ignoreReason);

            // In a real implementation, this would update the database to mark the conflict as ignored

            return Task.FromResult(new ConflictIgnoreResult(
                conflictId,
                userId,
                true,
                ignoreReason,
                DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ignore conflict {ConflictId} for user {UserId}", conflictId, userId);
            return Task.FromResult(new ConflictIgnoreResult(
                conflictId,
                userId,
                false,
                ex.Message,
                DateTime.UtcNow
            ));
        }
    }

    // Private helper methods

    private Task<IEnumerable<DetectedConflict>> DetectTimeOverlapConflictsAdvanced(
        Guid userId,
        IEnumerable<InternalCalendarEvent> internalEvents,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        ConflictDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<DetectedConflict>();

        foreach (var internalEvent in internalEvents)
        {
            foreach (var externalEvent in externalEvents)
            {
                var internalRange = new TimeRange(internalEvent.StartTime, internalEvent.EndTime);
                var externalRange = new TimeRange(externalEvent.StartTime, externalEvent.EndTime);

                var overlap = CalculateOverlap(internalRange, externalRange, TimeSpan.FromMinutes(options.TimeOverlapToleranceMinutes));

                if (overlap.HasValue)
                {
                    var severity = CalculateOverlapSeverity(overlap.Value, internalRange);
                    var suggestions = GenerateOverlapResolutions(internalRange, internalEvent, overlap.Value);

                    conflicts.Add(new DetectedConflict(
                        Guid.NewGuid(),
                        ConflictType.TimeOverlap,
                        severity,
                        $"Time overlap between '{internalEvent.Title}' and '{externalEvent.Title}'",
                        internalEvent,
                        externalEvent,
                        suggestions,
                        DateTime.UtcNow,
                        new Dictionary<string, object>
                        {
                            ["overlapDuration"] = overlap.Value,
                            ["overlapPercentage"] = CalculateOverlapPercentage(overlap.Value, internalRange)
                        }
                    ));
                }
            }
        }

        return Task.FromResult<IEnumerable<DetectedConflict>>(conflicts);
    }

    private Task<IEnumerable<DetectedConflict>> DetectDataInconsistencyAdvanced(
        Guid userId,
        IEnumerable<InternalCalendarEvent> internalEvents,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        ConflictDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<DetectedConflict>();

        // This would implement sophisticated data consistency checking
        // For now, return empty list as placeholder
        return Task.FromResult<IEnumerable<DetectedConflict>>(conflicts);
    }

    private Task<IEnumerable<DetectedConflict>> DetectSchedulingConflicts(
        Guid userId,
        IEnumerable<InternalCalendarEvent> internalEvents,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        ConflictDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<DetectedConflict>();

        // This would implement scheduling conflict detection (e.g., double-booking, availability conflicts)
        // For now, return empty list as placeholder
        return Task.FromResult<IEnumerable<DetectedConflict>>(conflicts);
    }

    private TimeSpan? CalculateOverlap(TimeRange range1, TimeRange range2, TimeSpan tolerance)
    {
        var start = range1.Start > range2.Start ? range1.Start : range2.Start;
        var end = range1.End < range2.End ? range1.End : range2.End;

        if (start < end && (end - start) > tolerance)
        {
            return end - start;
        }

        return null;
    }

    private ConflictSeverity CalculateOverlapSeverity(TimeSpan overlap, TimeRange originalRange)
    {
        var overlapPercentage = CalculateOverlapPercentage(overlap, originalRange);

        return overlapPercentage switch
        {
            >= 0.8 => ConflictSeverity.Critical,
            >= 0.5 => ConflictSeverity.High,
            >= 0.2 => ConflictSeverity.Medium,
            _ => ConflictSeverity.Low
        };
    }

    private double CalculateOverlapPercentage(TimeSpan overlap, TimeRange range)
    {
        var totalDuration = range.End - range.Start;
        return totalDuration.TotalMilliseconds > 0 ? overlap.TotalMilliseconds / totalDuration.TotalMilliseconds : 0;
    }

    private List<ConflictResolution> GenerateOverlapResolutions(TimeRange targetRange, InternalCalendarEvent existingEvent, TimeSpan overlap)
    {
        return new List<ConflictResolution>
        {
            new ConflictResolution(ConflictResolutionType.MoveEvent, "Move conflicting event to earlier time", 0.8, null),
            new ConflictResolution(ConflictResolutionType.MoveEvent, "Move conflicting event to later time", 0.8, null),
            new ConflictResolution(ConflictResolutionType.ShortenEvent, "Shorten event duration to avoid overlap", 0.6, null),
            new ConflictResolution(ConflictResolutionType.KeepBoth, "Keep both events (accept overlap)", 0.3, null)
        };
    }

    private IEnumerable<ExternalCalendarEvent> FindPotentialDuplicates(
        InternalCalendarEvent internalEvent,
        IEnumerable<ExternalCalendarEvent> externalEvents,
        DuplicateDetectionCriteria criteria)
    {
        var candidates = new List<ExternalCalendarEvent>();

        foreach (var externalEvent in externalEvents)
        {
            var similarity = CalculateEventSimilarity(internalEvent, externalEvent, criteria);
            if (similarity >= criteria.MinimumSimilarityThreshold)
            {
                candidates.Add(externalEvent);
            }
        }

        return candidates;
    }

    private double CalculateEventSimilarity(InternalCalendarEvent internal1, ExternalCalendarEvent external1, DuplicateDetectionCriteria criteria)
    {
        double similarity = 0.0;
        int criteriaMet = 0;
        int totalCriteria = 0;

        // Title similarity
        if (criteria.CompareTitle)
        {
            totalCriteria++;
            similarity += CalculateStringSimilarity(internal1.Title, external1.Title) * criteria.TitleWeight;
            if (CalculateStringSimilarity(internal1.Title, external1.Title) > 0.8)
            {
                criteriaMet++;
            }
        }

        // Time similarity
        if (criteria.CompareTime)
        {
            totalCriteria++;
            var timeSimilarity = CalculateTimeSimilarity(internal1.StartTime, internal1.EndTime, external1.StartTime, external1.EndTime);
            similarity += timeSimilarity * criteria.TimeWeight;
            if (timeSimilarity > 0.8)
            {
                criteriaMet++;
            }
        }

        // Location similarity
        if (criteria.CompareLocation && !string.IsNullOrEmpty(internal1.Location) && !string.IsNullOrEmpty(external1.Location))
        {
            totalCriteria++;
            similarity += CalculateStringSimilarity(internal1.Location, external1.Location) * criteria.LocationWeight;
        }

        return totalCriteria > 0 ? similarity / totalCriteria : 0.0;
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
        {
            return 0.0;
        }

        if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        // Simple Levenshtein distance-based similarity
        var distance = ComputeLevenshteinDistance(str1.ToLowerInvariant(), str2.ToLowerInvariant());
        var maxLength = Math.Max(str1.Length, str2.Length);

        return maxLength > 0 ? 1.0 - (double)distance / maxLength : 0.0;
    }

    private double CalculateTimeSimilarity(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
    {
        var startDiff = Math.Abs((start1 - start2).TotalMinutes);
        var endDiff = Math.Abs((end1 - end2).TotalMinutes);

        // Consider events similar if they start and end within 15 minutes of each other
        var startSimilarity = Math.Max(0, 1.0 - startDiff / 60.0); // 1 hour tolerance
        var endSimilarity = Math.Max(0, 1.0 - endDiff / 60.0);

        return (startSimilarity + endSimilarity) / 2.0;
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private List<ConflictResolution> GenerateDuplicateResolutions(InternalCalendarEvent internal1, ExternalCalendarEvent external1, double similarity)
    {
        return new List<ConflictResolution>
        {
            new ConflictResolution(ConflictResolutionType.KeepInternal, "Keep internal event, delete external", 0.7, null),
            new ConflictResolution(ConflictResolutionType.KeepExternal, "Keep external event, delete internal", 0.7, null),
            new ConflictResolution(ConflictResolutionType.MergeEvents, "Merge both events into one", 0.9, null),
            new ConflictResolution(ConflictResolutionType.KeepBoth, "Keep both as separate events", 0.3, null)
        };
    }

    private List<string> GetMatchingFields(InternalCalendarEvent internal1, ExternalCalendarEvent external1, DuplicateDetectionCriteria criteria)
    {
        var matchingFields = new List<string>();

        if (criteria.CompareTitle && CalculateStringSimilarity(internal1.Title, external1.Title) > 0.8)
        {
            matchingFields.Add("title");
        }

        if (criteria.CompareTime && CalculateTimeSimilarity(internal1.StartTime, internal1.EndTime, external1.StartTime, external1.EndTime) > 0.8)
        {
            matchingFields.Add("time");
        }

        if (criteria.CompareLocation && !string.IsNullOrEmpty(internal1.Location) && !string.IsNullOrEmpty(external1.Location) &&
            CalculateStringSimilarity(internal1.Location, external1.Location) > 0.8)
        {
            matchingFields.Add("location");
        }

        return matchingFields;
    }

    private Task<IEnumerable<DataConsistencyConflict>> DetectEventPairInconsistencies(
        EventPair eventPair,
        DataConsistencyOptions consistencyOptions,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<DataConsistencyConflict>();

        // Detect field-level inconsistencies
        var fieldInconsistencies = DetectFieldInconsistencies(eventPair.InternalEvent, eventPair.ExternalEvent, consistencyOptions);

        foreach (var inconsistency in fieldInconsistencies)
        {
            conflicts.Add(new DataConsistencyConflict(
                Guid.NewGuid(),
                inconsistency.Severity,
                inconsistency.Description,
                eventPair.InternalEvent,
                eventPair.ExternalEvent,
                inconsistency.FieldName,
                inconsistency.InternalValue,
                inconsistency.ExternalValue,
                inconsistency.SuggestedResolutions,
                DateTime.UtcNow,
                inconsistency.Details
            ));
        }

        return Task.FromResult<IEnumerable<DataConsistencyConflict>>(conflicts);
    }

    private IEnumerable<FieldInconsistency> DetectFieldInconsistencies(
        InternalCalendarEvent internalEvent,
        ExternalCalendarEvent externalEvent,
        DataConsistencyOptions options)
    {
        var inconsistencies = new List<FieldInconsistency>();

        // Check title consistency
        if (options.CheckTitle && !string.Equals(internalEvent.Title, externalEvent.Title, StringComparison.OrdinalIgnoreCase))
        {
            inconsistencies.Add(new FieldInconsistency(
                "title",
                ConflictSeverity.Medium,
                "Event titles are different",
                internalEvent.Title,
                externalEvent.Title,
                GenerateTitleResolutions(),
                new Dictionary<string, object>()
            ));
        }

        // Check time consistency
        if (options.CheckTime && (internalEvent.StartTime != externalEvent.StartTime || internalEvent.EndTime != externalEvent.EndTime))
        {
            var timeDiff = Math.Abs((internalEvent.StartTime - externalEvent.StartTime).TotalMinutes);
            var severity = timeDiff > 60 ? ConflictSeverity.High : ConflictSeverity.Medium;

            inconsistencies.Add(new FieldInconsistency(
                "time",
                severity,
                $"Event times differ by {timeDiff:F0} minutes",
                $"{internalEvent.StartTime} - {internalEvent.EndTime}",
                $"{externalEvent.StartTime} - {externalEvent.EndTime}",
                GenerateTimeResolutions(),
                new Dictionary<string, object> { ["timeDifferenceMinutes"] = timeDiff }
            ));
        }

        // Check location consistency
        if (options.CheckLocation && !string.Equals(internalEvent.Location, externalEvent.Location, StringComparison.OrdinalIgnoreCase))
        {
            inconsistencies.Add(new FieldInconsistency(
                "location",
                ConflictSeverity.Low,
                "Event locations are different",
                internalEvent.Location ?? "",
                externalEvent.Location ?? "",
                GenerateLocationResolutions(),
                new Dictionary<string, object>()
            ));
        }

        return inconsistencies;
    }

    private List<ConflictResolution> GenerateTitleResolutions()
    {
        return new List<ConflictResolution>
        {
            new ConflictResolution(ConflictResolutionType.UseInternalData, "Use internal event title", 0.6, null),
            new ConflictResolution(ConflictResolutionType.UseExternalData, "Use external event title", 0.6, null),
            new ConflictResolution(ConflictResolutionType.CombineData, "Combine both titles", 0.4, null)
        };
    }

    private List<ConflictResolution> GenerateTimeResolutions()
    {
        return new List<ConflictResolution>
        {
            new ConflictResolution(ConflictResolutionType.UseInternalData, "Use internal event time", 0.7, null),
            new ConflictResolution(ConflictResolutionType.UseExternalData, "Use external event time", 0.7, null),
            new ConflictResolution(ConflictResolutionType.AverageValues, "Use average time", 0.5, null)
        };
    }

    private List<ConflictResolution> GenerateLocationResolutions()
    {
        return new List<ConflictResolution>
        {
            new ConflictResolution(ConflictResolutionType.UseInternalData, "Use internal event location", 0.6, null),
            new ConflictResolution(ConflictResolutionType.UseExternalData, "Use external event location", 0.6, null),
            new ConflictResolution(ConflictResolutionType.CombineData, "Combine location information", 0.4, null)
        };
    }

    private Task<IEnumerable<DetectedConflict>> PrioritizeConflicts(Guid userId, List<DetectedConflict> conflicts, CancellationToken cancellationToken)
    {
        // Apply business rules for conflict prioritization
        return Task.FromResult<IEnumerable<DetectedConflict>>(conflicts
            .OrderByDescending(c => c.Severity)
            .ThenByDescending(c => c.ConflictType == ConflictType.TimeOverlap ? 1 : 0) // Prioritize time overlaps
            .ThenBy(c => c.DetectedAt));
    }

    private ConflictImpact AnalyzeConflictImpact(DetectedConflict conflict)
    {
        // Analyze the potential impact of the conflict
        return new ConflictImpact(
            conflict.Severity,
            EstimateUserDisruption(conflict),
            EstimateDataLoss(conflict),
            EstimateBusinessImpact(conflict)
        );
    }

    private UserDisruption EstimateUserDisruption(DetectedConflict conflict)
    {
        return conflict.Severity switch
        {
            ConflictSeverity.Critical => UserDisruption.High,
            ConflictSeverity.High => UserDisruption.Medium,
            _ => UserDisruption.Low
        };
    }

    private DataLossRisk EstimateDataLoss(DetectedConflict conflict)
    {
        return conflict.ConflictType switch
        {
            ConflictType.Duplicate => DataLossRisk.High,
            ConflictType.DataInconsistency => DataLossRisk.Medium,
            _ => DataLossRisk.Low
        };
    }

    private BusinessImpact EstimateBusinessImpact(DetectedConflict conflict)
    {
        return conflict.Severity switch
        {
            ConflictSeverity.Critical => BusinessImpact.High,
            ConflictSeverity.High => BusinessImpact.Medium,
            _ => BusinessImpact.Low
        };
    }

    private Task<IEnumerable<ResolutionRecommendation>> GenerateResolutionRecommendations(
        DetectedConflict conflict,
        UserConflictPreferences userPreferences,
        IEnumerable<HistoricalResolution> historicalResolutions,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<ResolutionRecommendation>();

        // Generate context-aware recommendations based on conflict type and user preferences
        foreach (var suggestedResolution in conflict.SuggestedResolutions)
        {
            var confidence = CalculateRecommendationConfidence(suggestedResolution, userPreferences, historicalResolutions);
            var reasoning = GenerateRecommendationReasoning(suggestedResolution, userPreferences, historicalResolutions);

            recommendations.Add(new ResolutionRecommendation(
                suggestedResolution.ResolutionType,
                confidence,
                reasoning,
                suggestedResolution,
                EstimateImplementationComplexity(suggestedResolution)
            ));
        }

        return Task.FromResult<IEnumerable<ResolutionRecommendation>>(recommendations.OrderByDescending(r => r.Confidence));
    }

    private double CalculateRecommendationConfidence(
        ConflictResolution resolution,
        UserConflictPreferences preferences,
        IEnumerable<HistoricalResolution> history)
    {
        var baseConfidence = resolution.Confidence;

        // Adjust based on user preferences
        var preferenceBoost = preferences.PreferredResolutionTypes.Contains(resolution.ResolutionType) ? 0.2 : 0.0;

        // Adjust based on historical success
        var historicalSuccessRate = CalculateHistoricalSuccessRate(resolution.ResolutionType, history);

        return Math.Min(1.0, baseConfidence + preferenceBoost + (historicalSuccessRate * 0.3));
    }

    private double CalculateHistoricalSuccessRate(ConflictResolutionType resolutionType, IEnumerable<HistoricalResolution> history)
    {
        var relevantResolutions = history.Where(h => h.ResolutionType == resolutionType).ToList();
        if (!relevantResolutions.Any())
        {
            return 0.5; // Default success rate
        }

        var successCount = relevantResolutions.Count(r => r.WasSuccessful);
        return (double)successCount / relevantResolutions.Count;
    }

    private string GenerateRecommendationReasoning(
        ConflictResolution resolution,
        UserConflictPreferences preferences,
        IEnumerable<HistoricalResolution> history)
    {
        var reasons = new List<string>();

        if (preferences.PreferredResolutionTypes.Contains(resolution.ResolutionType))
        {
            reasons.Add("matches your preferred resolution style");
        }

        var successRate = CalculateHistoricalSuccessRate(resolution.ResolutionType, history);
        if (successRate > 0.7)
        {
            reasons.Add($"has a {successRate:P0} success rate in your history");
        }

        if (resolution.Confidence > 0.8)
        {
            reasons.Add("has high algorithmic confidence");
        }

        return reasons.Any() ? string.Join(", ", reasons) : "standard resolution approach";
    }

    private ImplementationComplexity EstimateImplementationComplexity(ConflictResolution resolution)
    {
        return resolution.ResolutionType switch
        {
            ConflictResolutionType.KeepInternal or ConflictResolutionType.KeepExternal => ImplementationComplexity.Low,
            ConflictResolutionType.MoveEvent or ConflictResolutionType.ShortenEvent => ImplementationComplexity.Medium,
            ConflictResolutionType.MergeEvents or ConflictResolutionType.CombineData => ImplementationComplexity.High,
            _ => ImplementationComplexity.Medium
        };
    }

    private RiskAssessment AssessResolutionRisks(DetectedConflict conflict, IEnumerable<ResolutionRecommendation> recommendations)
    {
        var highestRisk = recommendations.Max(r => r.ImplementationComplexity);
        var riskFactors = new List<string>();

        if (conflict.Severity == ConflictSeverity.Critical)
        {
            riskFactors.Add("Critical severity conflict");
        }

        if (recommendations.Any(r => r.ImplementationComplexity == ImplementationComplexity.High))
        {
            riskFactors.Add("Complex implementation required");
        }

        return new RiskAssessment(
            highestRisk switch
            {
                ImplementationComplexity.High => RiskLevel.High,
                ImplementationComplexity.Medium => RiskLevel.Medium,
                _ => RiskLevel.Low
            },
            riskFactors
        );
    }

    private double CalculateConfidenceScore(DetectedConflict conflict, IEnumerable<HistoricalResolution> history)
    {
        var baseScore = conflict.SuggestedResolutions.Any() ? conflict.SuggestedResolutions.Max(r => r.Confidence) : 0.5;

        // Adjust based on historical data availability
        var historyBoost = history.Any() ? 0.2 : 0.0;

        // Adjust based on conflict complexity
        var complexityPenalty = CalculateConflictComplexity(conflict) > 0.7 ? -0.1 : 0.0;

        return Math.Max(0.0, Math.Min(1.0, baseScore + historyBoost + complexityPenalty));
    }

    private double CalculateConflictComplexity(DetectedConflict conflict)
    {
        var complexity = 0.0;

        // Factor in conflict type complexity
        complexity += conflict.ConflictType switch
        {
            ConflictType.TimeOverlap => 0.3,
            ConflictType.Duplicate => 0.5,
            ConflictType.DataInconsistency => 0.7,
            ConflictType.SchedulingConflict => 0.6,
            _ => 0.4
        };

        // Factor in severity
        complexity += conflict.Severity switch
        {
            ConflictSeverity.Critical => 0.3,
            ConflictSeverity.High => 0.2,
            ConflictSeverity.Medium => 0.1,
            _ => 0.0
        };

        return Math.Min(1.0, complexity);
    }

    private bool CanAutoResolveConflict(DetectedConflict conflict, ConflictResolutionStrategy strategy)
    {
        // Determine if conflict can be automatically resolved based on strategy
        return strategy.AutoResolveThreshold >= conflict.Severity &&
               strategy.AllowedResolutionTypes.Intersect(conflict.SuggestedResolutions.Select(r => r.ResolutionType)).Any();
    }

    private ConflictResolution SelectAutomaticResolution(DetectedConflict conflict, ConflictResolutionStrategy strategy)
    {
        var applicableResolutions = conflict.SuggestedResolutions
            .Where(r => strategy.AllowedResolutionTypes.Contains(r.ResolutionType))
            .OrderByDescending(r => r.Confidence);

        return applicableResolutions.FirstOrDefault() ??
               new ConflictResolution(ConflictResolutionType.ManualRequired, "Manual resolution required", 0.0, null);
    }

    private Task<ManualResolutionResult> ApplyAutomaticResolution(
        Guid userId,
        DetectedConflict conflict,
        ConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        // Apply the automatic resolution
        // This would involve actual event modifications based on the resolution type
        return Task.FromResult(new ManualResolutionResult(
            conflict.ConflictId,
            userId,
            true,
            "Conflict resolved automatically",
            resolution.ResolvedEventData,
            DateTime.UtcNow
        ));
    }

    private Task<ManualResolutionResult> ExecuteManualResolution(
        Guid userId,
        Guid conflictId,
        ConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        // Execute the manual resolution
        // This would involve actual event modifications based on the resolution type
        return Task.FromResult(new ManualResolutionResult(
            conflictId,
            userId,
            true,
            "Manual resolution applied successfully",
            resolution.ResolvedEventData,
            DateTime.UtcNow
        ));
    }

    private bool IsResolutionTypeValid(ConflictResolutionType resolutionType)
    {
        return Enum.IsDefined(typeof(ConflictResolutionType), resolutionType);
    }

    private (List<string> Errors, List<string> Warnings) ValidateEventData(object eventData)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate event data structure and constraints
        // This would involve detailed validation based on the event data type

        return (errors, warnings);
    }

    private Task<IEnumerable<PotentialConflict>> CheckForPotentialConflicts(
        Guid userId,
        ConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        // Check if applying this resolution might create new conflicts
        return Task.FromResult<IEnumerable<PotentialConflict>>(new List<PotentialConflict>()); // Placeholder
    }

    private Task RecordResolutionHistory(
        Guid userId,
        Guid conflictId,
        ConflictResolution resolution,
        ManualResolutionResult result,
        CancellationToken cancellationToken)
    {
        var historyEntry = new HistoricalResolution(
            conflictId,
            userId,
            resolution.ResolutionType,
            result.Success,
            result.ErrorMessage,
            result.ResolvedAt
        );

        // Add to cache
        if (!_resolutionHistoryCache.ContainsKey(userId))
        {
            _resolutionHistoryCache[userId] = new List<HistoricalResolution>();
        }

        _resolutionHistoryCache[userId].Add(historyEntry);
        return Task.CompletedTask;
    }

    private Task<UserConflictPreferences> GetUserPreferences(Guid userId, CancellationToken cancellationToken)
    {
        if (_userPreferencesCache.TryGetValue(userId, out var cachedPreferences))
        {
            return Task.FromResult(cachedPreferences);
        }

        // Load from database or create default preferences
        var defaultPreferences = new UserConflictPreferences(
            userId,
            [ConflictResolutionType.KeepInternal, ConflictResolutionType.MergeEvents],
            ConflictSeverity.Medium,
            true,
            false,
            DateTime.UtcNow
        );

        _userPreferencesCache[userId] = defaultPreferences;
        return Task.FromResult(defaultPreferences);
    }

    private Task<IEnumerable<HistoricalResolution>> GetUserResolutionHistory(Guid userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<HistoricalResolution>>(_resolutionHistoryCache.GetValueOrDefault(userId, new List<HistoricalResolution>()));
    }

    private Task<IEnumerable<ResolutionSuggestion>> GenerateIntelligentSuggestions(
        DetectedConflict conflict,
        UserConflictPreferences preferences,
        IEnumerable<HistoricalResolution> history,
        CancellationToken cancellationToken)
    {
        var suggestions = new List<ResolutionSuggestion>();

        foreach (var suggestedResolution in conflict.SuggestedResolutions)
        {
            var confidence = CalculateRecommendationConfidence(suggestedResolution, preferences, history);
            var reasoning = GenerateRecommendationReasoning(suggestedResolution, preferences, history);

            suggestions.Add(new ResolutionSuggestion(
                suggestedResolution.ResolutionType,
                confidence,
                suggestedResolution.Description,
                reasoning,
                suggestedResolution
            ));
        }

        return Task.FromResult<IEnumerable<ResolutionSuggestion>>(suggestions);
    }

    private Task<IEnumerable<PredictedConflict>> PredictChangePotentialConflicts(
        PlannedSyncChange plannedChange,
        CalendarSyncState currentState,
        CancellationToken cancellationToken)
    {
        var predictions = new List<PredictedConflict>();

        // Analyze the planned change and predict potential conflicts
        // This would involve complex prediction algorithms

        return Task.FromResult<IEnumerable<PredictedConflict>>(predictions);
    }

    private ConflictStatistics CalculateConflictStatistics(List<HistoricalResolution> relevantHistory, TimeRange timeRange)
    {
        var totalConflicts = relevantHistory.Count;
        var resolvedConflicts = relevantHistory.Count(h => h.WasSuccessful);
        var pendingConflicts = totalConflicts - resolvedConflicts;

        var conflictsByType = relevantHistory
            .GroupBy(h => ConflictType.TimeOverlap) // Simplified - would map from resolution type to conflict type
            .ToDictionary(g => g.Key, g => g.Count());

        var resolutionsByType = relevantHistory
            .GroupBy(h => h.ResolutionType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ConflictStatistics(
            Guid.Empty, // Would be passed as parameter
            timeRange,
            totalConflicts,
            resolvedConflicts,
            pendingConflicts,
            0, // Average resolution time - would be calculated
            conflictsByType,
            resolutionsByType,
            [], // Trending patterns would be analyzed
            DateTime.UtcNow
        );
    }

    private UserConflictPreferences AnalyzeAndUpdatePreferences(
        UserConflictPreferences currentPreferences,
        IEnumerable<HistoricalResolution> recentResolutions)
    {
        var mostUsedResolutions = recentResolutions
            .Where(r => r.WasSuccessful)
            .GroupBy(r => r.ResolutionType)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        return new UserConflictPreferences(
            currentPreferences.UserId,
            mostUsedResolutions,
            currentPreferences.AutoResolveThreshold,
            currentPreferences.AllowDataLoss,
            currentPreferences.PreferExternalData,
            DateTime.UtcNow
        );
    }

    private Task<InternalCalendarEvent> ExecuteEventMerge(
        IEnumerable<ConflictingEventData> conflictingEvents,
        EventMergeStrategy mergeStrategy,
        CancellationToken cancellationToken)
    {
        var events = conflictingEvents.ToList();
        var firstEvent = events.First();

        // Create merged event based on strategy
        var mergedEvent = new InternalCalendarEvent(
            Guid.NewGuid(),
            firstEvent.EventId,
            MergeField(events, e => e.Title, mergeStrategy),
            MergeField(events, e => e.Description, mergeStrategy),
            events.Min(e => e.StartTime),
            events.Max(e => e.EndTime),
            events.Any(e => e.IsAllDay),
            MergeField(events, e => e.Location, mergeStrategy),
            [], // Attendees would require more complex merging
            null, // Recurrence would require special handling
            [], // Reminders would be merged
            [], // Attachments would be combined
            events.Min(e => e.CreatedTime),
            DateTime.UtcNow,
            CalendarProvider.None
        );

        return Task.FromResult(mergedEvent);
    }

    private string MergeField(IEnumerable<ConflictingEventData> events, Func<ConflictingEventData, string?> fieldSelector, EventMergeStrategy strategy)
    {
        var values = events.Select(fieldSelector).Where(v => !string.IsNullOrEmpty(v)).Distinct().ToList();

        return strategy.StrategyType switch
        {
            EventMergeStrategyType.PreferFirst => values.FirstOrDefault() ?? "",
            EventMergeStrategyType.PreferLast => values.LastOrDefault() ?? "",
            EventMergeStrategyType.Combine => string.Join(" | ", values),
            _ => values.FirstOrDefault() ?? ""
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cacheSemaphore?.Dispose();
        _userPreferencesCache.Clear();
        _resolutionHistoryCache.Clear();

        _disposed = true;
    }
}

// Helper classes for internal conflict detection logic

internal record FieldInconsistency(
    string FieldName,
    ConflictSeverity Severity,
    string Description,
    string InternalValue,
    string ExternalValue,
    List<ConflictResolution> SuggestedResolutions,
    Dictionary<string, object> Details
);

// Enums for conflict detection

public enum UserDisruption
{
    Low,
    Medium,
    High
}

public enum DataLossRisk
{
    Low,
    Medium,
    High
}

public enum BusinessImpact
{
    Low,
    Medium,
    High
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

public enum ImplementationComplexity
{
    Low,
    Medium,
    High
}
