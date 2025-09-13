using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for calendar conflict resolution business logic
/// </summary>
public class CalendarConflictResolutionService
{
    /// <summary>
    /// Analyzes a conflict and provides resolution recommendations
    /// </summary>
    public ConflictAnalysisResult AnalyzeConflict(CalendarConflict conflict, CalendarIntegration? integration = null)
    {
        if (conflict == null)
            return ConflictAnalysisResult.CreateEmpty();

        var recommendations = new List<ResolutionRecommendation>();
        var requiredUserDecisions = new List<string>();
        var confidence = 0.0;
        var complexity = ConflictComplexity.Simple;

        switch ((ConflictType)conflict.ConflictType)
        {
            case ConflictType.TimeOverlap:
                return AnalyzeTimeOverlapConflict(conflict, integration);

            case ConflictType.DuplicateEvent:
                return AnalyzeDuplicateEventConflict(conflict, integration);

            case ConflictType.DataInconsistency:
                return AnalyzeDataInconsistencyConflict(conflict, integration);

            case ConflictType.DeletedEvent:
                return AnalyzeDeletedEventConflict(conflict, integration);

            case ConflictType.ModifiedEvent:
                return AnalyzeModifiedEventConflict(conflict, integration);

            default:
                recommendations.Add(new ResolutionRecommendation(
                    ConflictResolutionAction.UserDecision,
                    "Unknown conflict type requires manual resolution",
                    0.1f,
                    new Dictionary<string, object>()
                ));
                requiredUserDecisions.Add("Determine appropriate action for unknown conflict type");
                complexity = ConflictComplexity.RequiresUserInput;
                break;
        }

        return new ConflictAnalysisResult(
            conflict.Id,
            complexity,
            recommendations,
            confidence,
            requiredUserDecisions,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Determines if a conflict can be automatically resolved
    /// </summary>
    public bool CanAutoResolve(CalendarConflict conflict, CalendarIntegration? integration = null)
    {
        if (conflict == null || !conflict.IsActive || conflict.IsIgnored)
            return false;

        // High severity conflicts should not be auto-resolved
        if (conflict.Severity >= (int)ConflictSeverity.High)
            return false;

        var analysis = AnalyzeConflict(conflict, integration);
        
        // Must have high confidence recommendation
        if (analysis.AutoResolutionConfidence < 0.8)
            return false;

        // Must not require user decisions
        if (analysis.RequiredUserDecisions.Any())
            return false;

        // Must have a clear recommended action
        var topRecommendation = analysis.Recommendations.OrderByDescending(r => r.Confidence).FirstOrDefault();
        if (topRecommendation == null || topRecommendation.Action == ConflictResolutionAction.UserDecision)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves a conflict automatically if possible
    /// </summary>
    public ConflictResolutionResult AttemptAutoResolution(CalendarConflict conflict, CalendarIntegration? integration = null)
    {
        if (!CanAutoResolve(conflict, integration))
        {
            return ConflictResolutionResult.Failed(
                conflict.Id,
                "Conflict cannot be automatically resolved"
            );
        }

        var analysis = AnalyzeConflict(conflict, integration);
        var recommendedAction = analysis.Recommendations.OrderByDescending(r => r.Confidence).First();

        try
        {
            var resolutionSteps = GenerateResolutionSteps(recommendedAction, conflict);
            
            return ConflictResolutionResult.Success(
                conflict.Id,
                recommendedAction.Action,
                resolutionSteps,
                "Automatically resolved based on analysis",
                analysis.AutoResolutionConfidence
            );
        }
        catch (Exception ex)
        {
            return ConflictResolutionResult.Failed(
                conflict.Id,
                $"Auto-resolution failed: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Validates a proposed resolution
    /// </summary>
    public ResolutionValidationResult ValidateResolution(CalendarConflict conflict, 
        ConflictResolutionAction proposedAction, Dictionary<string, object>? parameters = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (conflict == null)
        {
            errors.Add("Conflict is required");
            return new ResolutionValidationResult(false, errors, warnings);
        }

        if (!conflict.IsActive)
        {
            errors.Add("Cannot resolve inactive conflict");
        }

        if (conflict.IsIgnored)
        {
            warnings.Add("Conflict is currently ignored");
        }

        // Validate action-specific requirements
        switch (proposedAction)
        {
            case ConflictResolutionAction.Merge:
                ValidateMergeAction(conflict, parameters, errors, warnings);
                break;

            case ConflictResolutionAction.Reschedule:
                ValidateRescheduleAction(conflict, parameters, errors, warnings);
                break;

            case ConflictResolutionAction.CreateBoth:
                ValidateCreateBothAction(conflict, parameters, errors, warnings);
                break;

            case ConflictResolutionAction.DeleteBoth:
                ValidateDeleteBothAction(conflict, parameters, errors, warnings);
                break;
        }

        return new ResolutionValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Gets suitable resolution options for a conflict
    /// </summary>
    public List<ConflictResolutionOption> GetResolutionOptions(CalendarConflict conflict)
    {
        if (conflict == null)
            return new List<ConflictResolutionOption>();

        var options = new List<ConflictResolutionOption>();

        switch ((ConflictType)conflict.ConflictType)
        {
            case ConflictType.TimeOverlap:
                options.AddRange(GetTimeOverlapOptions(conflict));
                break;

            case ConflictType.DuplicateEvent:
                options.AddRange(GetDuplicateEventOptions(conflict));
                break;

            case ConflictType.DataInconsistency:
                options.AddRange(GetDataInconsistencyOptions(conflict));
                break;

            case ConflictType.DeletedEvent:
                options.AddRange(GetDeletedEventOptions(conflict));
                break;

            case ConflictType.ModifiedEvent:
                options.AddRange(GetModifiedEventOptions(conflict));
                break;
        }

        // Always add manual resolution option
        options.Add(new ConflictResolutionOption(
            ConflictResolutionAction.UserDecision,
            "Manual resolution - defer to user",
            1.0f,
            false,
            "Let the user decide how to resolve this conflict"
        ));

        return options.OrderByDescending(o => o.Confidence).ToList();
    }

    /// <summary>
    /// Calculates conflict severity based on various factors
    /// </summary>
    public ConflictSeverity CalculateConflictSeverity(CalendarConflict conflict)
    {
        if (conflict == null)
            return ConflictSeverity.Low;

        var severityScore = 0;

        // Base severity on conflict type
        severityScore += (ConflictType)conflict.ConflictType switch
        {
            ConflictType.TimeOverlap => 30,
            ConflictType.DuplicateEvent => 20,
            ConflictType.DataInconsistency => 25,
            ConflictType.DeletedEvent => 35,
            ConflictType.ModifiedEvent => 25,
            ConflictType.PermissionDenied => 40,
            ConflictType.SyncFailure => 15,
            _ => 20
        };

        // Add severity for time conflicts
        if (conflict.OverlapMinutes.HasValue)
        {
            severityScore += conflict.OverlapMinutes.Value switch
            {
                > 120 => 30, // > 2 hours
                > 60 => 20,  // > 1 hour
                > 30 => 15,  // > 30 minutes
                > 15 => 10,  // > 15 minutes
                _ => 5
            };
        }

        // Add severity for event importance
        if (conflict.InternalEvent != null)
        {
            severityScore += conflict.InternalEvent.Priority switch
            {
                3 => 25, // Priority.Urgent
                2 => 20, // Priority.High
                1 => 10, // Priority.Medium
                _ => 0
            };

            // Events with attendees are more critical
            if (conflict.InternalEvent.HasAttendees)
                severityScore += 15;

            // Upcoming events are more critical
            if (conflict.InternalEvent.IsUpcoming)
            {
                var timeUntilEvent = conflict.InternalEvent.StartTime - DateTime.UtcNow;
                if (timeUntilEvent <= TimeSpan.FromHours(2))
                    severityScore += 25;
                else if (timeUntilEvent <= TimeSpan.FromHours(24))
                    severityScore += 15;
                else if (timeUntilEvent <= TimeSpan.FromDays(7))
                    severityScore += 10;
            }
        }

        return severityScore switch
        {
            >= 80 => ConflictSeverity.Critical,
            >= 60 => ConflictSeverity.High,
            >= 40 => ConflictSeverity.Medium,
            _ => ConflictSeverity.Low
        };
    }

    private ConflictAnalysisResult AnalyzeTimeOverlapConflict(CalendarConflict conflict, CalendarIntegration? integration)
    {
        var recommendations = new List<ResolutionRecommendation>();
        var requiredDecisions = new List<string>();
        var complexity = ConflictComplexity.Simple;

        var overlapMinutes = conflict.OverlapMinutes ?? 0;

        if (overlapMinutes <= 15)
        {
            // Small overlap - can likely reschedule automatically
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.Reschedule,
                "Reschedule one event to avoid overlap",
                0.85f,
                new Dictionary<string, object> { ["suggestedOffset"] = TimeSpan.FromMinutes(overlapMinutes + 15) }
            ));
            complexity = ConflictComplexity.Simple;
        }
        else if (overlapMinutes <= 60)
        {
            // Medium overlap - might need user decision
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.Reschedule,
                "Reschedule to resolve significant overlap",
                0.7f,
                new Dictionary<string, object>()
            ));
            
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.CreateBoth,
                "Keep both events as scheduled (user will manage manually)",
                0.4f,
                new Dictionary<string, object>()
            ));

            requiredDecisions.Add("Choose which event to reschedule");
            complexity = ConflictComplexity.Moderate;
        }
        else
        {
            // Large overlap - definitely needs user input
            requiredDecisions.Add("Resolve significant time conflict manually");
            complexity = ConflictComplexity.RequiresUserInput;

            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.UserDecision,
                "Manual resolution required for large overlap",
                0.9f,
                new Dictionary<string, object>()
            ));
        }

        var confidence = complexity == ConflictComplexity.Simple ? 0.8 : 0.5;

        return new ConflictAnalysisResult(
            conflict.Id,
            complexity,
            recommendations,
            confidence,
            requiredDecisions,
            new Dictionary<string, object> { ["overlapMinutes"] = overlapMinutes }
        );
    }

    private ConflictAnalysisResult AnalyzeDuplicateEventConflict(CalendarConflict conflict, CalendarIntegration? integration)
    {
        var recommendations = new List<ResolutionRecommendation>();
        var similarity = conflict.SimilarityScore ?? 0.0;

        if (similarity >= 0.95)
        {
            // High confidence duplicate - safe to merge
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.KeepInternal,
                "Keep internal event, remove external duplicate",
                0.9f,
                new Dictionary<string, object>()
            ));
        }
        else if (similarity >= 0.8)
        {
            // Likely duplicate but less certain
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.Merge,
                "Merge event data to preserve information",
                0.7f,
                new Dictionary<string, object>()
            ));
        }
        else
        {
            // Low similarity - might not be duplicate
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.CreateBoth,
                "Keep both events as they may be different",
                0.6f,
                new Dictionary<string, object>()
            ));
        }

        var complexity = similarity >= 0.9 ? ConflictComplexity.Simple : ConflictComplexity.Moderate;
        var confidence = Math.Max(0.1, similarity);

        return new ConflictAnalysisResult(
            conflict.Id,
            complexity,
            recommendations,
            confidence,
            similarity < 0.9 ? new List<string> { "Verify events are actually duplicates" } : new List<string>(),
            new Dictionary<string, object> { ["similarityScore"] = similarity }
        );
    }

    private ConflictAnalysisResult AnalyzeDataInconsistencyConflict(CalendarConflict conflict, CalendarIntegration? integration)
    {
        var recommendations = new List<ResolutionRecommendation>();
        var conflictingFields = conflict.GetConflictingFields();
        var requiredDecisions = new List<string>();

        var criticalFields = new[] { "StartTime", "EndTime", "Title" };
        var hasCriticalConflicts = conflictingFields.Intersect(criticalFields).Any();

        if (hasCriticalConflicts)
        {
            requiredDecisions.Add("Resolve critical field conflicts manually");
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.UserDecision,
                "Critical fields conflict - manual resolution required",
                0.9f,
                new Dictionary<string, object>()
            ));
        }
        else
        {
            // Non-critical fields can potentially be merged
            recommendations.Add(new ResolutionRecommendation(
                ConflictResolutionAction.Merge,
                "Merge non-critical field differences",
                0.8f,
                new Dictionary<string, object>()
            ));

            var strategy = integration != null ? (ConflictResolutionStrategy)integration.ConflictResolutionStrategy : ConflictResolutionStrategy.UserResolves;
            
            if (strategy == ConflictResolutionStrategy.LastModifiedWins)
            {
                recommendations.Add(new ResolutionRecommendation(
                    ConflictResolutionAction.KeepExternal,
                    "Use most recently modified version",
                    0.7f,
                    new Dictionary<string, object>()
                ));
            }
        }

        var complexity = hasCriticalConflicts ? ConflictComplexity.RequiresUserInput : ConflictComplexity.Moderate;
        var confidence = hasCriticalConflicts ? 0.3 : 0.7;

        return new ConflictAnalysisResult(
            conflict.Id,
            complexity,
            recommendations,
            confidence,
            requiredDecisions,
            new Dictionary<string, object> { ["conflictingFields"] = conflictingFields }
        );
    }

    private ConflictAnalysisResult AnalyzeDeletedEventConflict(CalendarConflict conflict, CalendarIntegration? integration)
    {
        var recommendations = new List<ResolutionRecommendation>
        {
            new(ConflictResolutionAction.DeleteBoth, "Delete from both calendars", 0.8f, new Dictionary<string, object>()),
            new(ConflictResolutionAction.KeepInternal, "Keep internal copy only", 0.6f, new Dictionary<string, object>()),
            new(ConflictResolutionAction.KeepExternal, "Keep external copy only", 0.6f, new Dictionary<string, object>())
        };

        return new ConflictAnalysisResult(
            conflict.Id,
            ConflictComplexity.Moderate,
            recommendations,
            0.6,
            new List<string> { "Confirm deletion is intentional" },
            new Dictionary<string, object>()
        );
    }

    private ConflictAnalysisResult AnalyzeModifiedEventConflict(CalendarConflict conflict, CalendarIntegration? integration)
    {
        var recommendations = new List<ResolutionRecommendation>
        {
            new(ConflictResolutionAction.Merge, "Merge modifications", 0.7f, new Dictionary<string, object>()),
            new(ConflictResolutionAction.KeepExternal, "Keep external modifications", 0.6f, new Dictionary<string, object>()),
            new(ConflictResolutionAction.KeepInternal, "Keep internal modifications", 0.6f, new Dictionary<string, object>())
        };

        return new ConflictAnalysisResult(
            conflict.Id,
            ConflictComplexity.Moderate,
            recommendations,
            0.6,
            new List<string> { "Review modifications to choose best version" },
            new Dictionary<string, object>()
        );
    }

    private List<ConflictResolutionOption> GetTimeOverlapOptions(CalendarConflict conflict)
    {
        return new List<ConflictResolutionOption>
        {
            new(ConflictResolutionAction.Reschedule, "Reschedule one event", 0.8f, true, "Move one event to avoid overlap"),
            new(ConflictResolutionAction.CreateBoth, "Keep both events", 0.6f, false, "Allow overlap, user manages manually"),
            new(ConflictResolutionAction.Merge, "Merge into one event", 0.4f, false, "Combine events if they're related")
        };
    }

    private List<ConflictResolutionOption> GetDuplicateEventOptions(CalendarConflict conflict)
    {
        return new List<ConflictResolutionOption>
        {
            new(ConflictResolutionAction.KeepInternal, "Keep internal event", 0.8f, true, "Remove external duplicate"),
            new(ConflictResolutionAction.KeepExternal, "Keep external event", 0.7f, false, "Remove internal duplicate"),
            new(ConflictResolutionAction.Merge, "Merge event data", 0.6f, false, "Combine information from both")
        };
    }

    private List<ConflictResolutionOption> GetDataInconsistencyOptions(CalendarConflict conflict)
    {
        return new List<ConflictResolutionOption>
        {
            new(ConflictResolutionAction.Merge, "Merge differences", 0.7f, true, "Combine data from both versions"),
            new(ConflictResolutionAction.KeepExternal, "Use external version", 0.6f, false, "External calendar wins"),
            new(ConflictResolutionAction.KeepInternal, "Use internal version", 0.6f, false, "Internal calendar wins")
        };
    }

    private List<ConflictResolutionOption> GetDeletedEventOptions(CalendarConflict conflict)
    {
        return new List<ConflictResolutionOption>
        {
            new(ConflictResolutionAction.DeleteBoth, "Delete from both", 0.8f, true, "Sync the deletion"),
            new(ConflictResolutionAction.KeepInternal, "Restore to external", 0.6f, false, "Un-delete in external calendar"),
            new(ConflictResolutionAction.KeepExternal, "Delete from internal", 0.6f, false, "Complete the deletion")
        };
    }

    private List<ConflictResolutionOption> GetModifiedEventOptions(CalendarConflict conflict)
    {
        return new List<ConflictResolutionOption>
        {
            new(ConflictResolutionAction.Merge, "Merge changes", 0.7f, true, "Combine modifications"),
            new(ConflictResolutionAction.KeepExternal, "Use external changes", 0.6f, false, "External modifications win"),
            new(ConflictResolutionAction.KeepInternal, "Use internal changes", 0.6f, false, "Internal modifications win")
        };
    }

    private void ValidateMergeAction(CalendarConflict conflict, Dictionary<string, object>? parameters, 
        List<string> errors, List<string> warnings)
    {
        if (conflict.ConflictType == (int)ConflictType.TimeOverlap)
        {
            errors.Add("Cannot merge time-overlapping events");
        }
    }

    private void ValidateRescheduleAction(CalendarConflict conflict, Dictionary<string, object>? parameters,
        List<string> errors, List<string> warnings)
    {
        if (conflict.ConflictType != (int)ConflictType.TimeOverlap)
        {
            warnings.Add("Rescheduling may not resolve this type of conflict");
        }
    }

    private void ValidateCreateBothAction(CalendarConflict conflict, Dictionary<string, object>? parameters,
        List<string> errors, List<string> warnings)
    {
        if (conflict.ConflictType == (int)ConflictType.DuplicateEvent)
        {
            warnings.Add("Creating both events may result in actual duplicates");
        }
    }

    private void ValidateDeleteBothAction(CalendarConflict conflict, Dictionary<string, object>? parameters,
        List<string> errors, List<string> warnings)
    {
        warnings.Add("This action will permanently delete events from both calendars");
    }

    private List<string> GenerateResolutionSteps(ResolutionRecommendation recommendation, CalendarConflict conflict)
    {
        return recommendation.Action switch
        {
            ConflictResolutionAction.KeepInternal => new List<string> { "Delete external event", "Mark conflict as resolved" },
            ConflictResolutionAction.KeepExternal => new List<string> { "Delete internal event", "Mark conflict as resolved" },
            ConflictResolutionAction.Merge => new List<string> { "Merge event data", "Update both calendars", "Mark conflict as resolved" },
            ConflictResolutionAction.CreateBoth => new List<string> { "Keep both events", "Mark conflict as resolved" },
            ConflictResolutionAction.DeleteBoth => new List<string> { "Delete internal event", "Delete external event", "Mark conflict as resolved" },
            ConflictResolutionAction.Reschedule => new List<string> { "Reschedule conflicting event", "Update calendar", "Mark conflict as resolved" },
            _ => new List<string> { "Manual resolution required" }
        };
    }
}

/// <summary>
/// Result of conflict analysis
/// </summary>
public sealed record ConflictAnalysisResult(
    Guid ConflictId,
    ConflictComplexity Complexity,
    List<ResolutionRecommendation> Recommendations,
    double AutoResolutionConfidence,
    List<string> RequiredUserDecisions,
    Dictionary<string, object> AnalysisMetadata)
{
    public static ConflictAnalysisResult CreateEmpty() => new(
        Guid.Empty,
        ConflictComplexity.Simple,
        new List<ResolutionRecommendation>(),
        0.0,
        new List<string>(),
        new Dictionary<string, object>()
    );
}

/// <summary>
/// Resolution recommendation
/// </summary>
public sealed record ResolutionRecommendation(
    ConflictResolutionAction Action,
    string Description,
    float Confidence,
    Dictionary<string, object> Parameters);

/// <summary>
/// Result of conflict resolution
/// </summary>
public sealed record ConflictResolutionResult
{
    private ConflictResolutionResult(Guid conflictId, bool success, ConflictResolutionAction? action, 
        List<string>? steps, string? message, double? confidence)
    {
        ConflictId = conflictId;
        IsSuccess = success;
        Action = action;
        ResolutionSteps = steps ?? new List<string>();
        Message = message;
        Confidence = confidence;
    }

    public Guid ConflictId { get; }
    public bool IsSuccess { get; }
    public ConflictResolutionAction? Action { get; }
    public List<string> ResolutionSteps { get; }
    public string? Message { get; }
    public double? Confidence { get; }

    public static ConflictResolutionResult Success(Guid conflictId, ConflictResolutionAction action, 
        List<string> steps, string message, double confidence) =>
        new(conflictId, true, action, steps, message, confidence);

    public static ConflictResolutionResult Failed(Guid conflictId, string message) =>
        new(conflictId, false, null, null, message, null);
}

/// <summary>
/// Validation result for conflict resolution
/// </summary>
public sealed record ResolutionValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings)
{
    public bool HasErrors => Errors.Any();
    public bool HasWarnings => Warnings.Any();
}

/// <summary>
/// Conflict complexity levels
/// </summary>
public enum ConflictComplexity
{
    Simple = 0,             // Can be auto-resolved with high confidence
    Moderate = 1,           // May require some user input or confirmation
    Complex = 2,            // Requires careful analysis and user decisions
    RequiresUserInput = 3   // Must be resolved manually by user
}

/// <summary>
/// Resolution option for conflicts
/// </summary>
public sealed record ConflictResolutionOption(
    ConflictResolutionAction Action,
    string Description,
    float Confidence,
    bool IsRecommended,
    string? AdditionalInfo = null);