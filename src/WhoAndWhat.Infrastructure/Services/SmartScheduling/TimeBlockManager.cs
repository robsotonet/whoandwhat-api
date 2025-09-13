using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Service for generating and optimizing time blocks for enhanced productivity
/// </summary>
public sealed class TimeBlockManager : ITimeBlockManager
{
    private readonly ILogger<TimeBlockManager> _logger;
    private readonly SmartSchedulingSettings _settings;

    public TimeBlockManager(
        ILogger<TimeBlockManager> logger,
        IOptions<SmartSchedulingSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<List<TimeBlockSuggestion>> GenerateTimeBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating time blocks for user {UserId} with {ItemCount} scheduled items", 
            userId, scheduledItems.Count);

        try
        {
            var timeBlocks = new List<TimeBlockSuggestion>();
            var workingHours = preferences.PreferredWorkingHours;
            
            // Find gaps in the schedule for time blocks
            var gaps = FindScheduleGaps(scheduledItems, workingHours);
            
            foreach (var gap in gaps)
            {
                if (gap.Duration >= TimeSpan.FromMinutes(30)) // Minimum time block size
                {
                    var blockPurpose = DetermineOptimalBlockPurpose(gap, scheduledItems, preferences);
                    var suggestion = CreateTimeBlockSuggestion(gap, blockPurpose, preferences);
                    
                    if (suggestion != null)
                    {
                        timeBlocks.Add(suggestion);
                    }
                }
            }

            // Add strategic time blocks
            timeBlocks.AddRange(await GenerateStrategicTimeBlocksAsync(userId, scheduledItems, preferences, cancellationToken));
            
            // Sort by start time and score
            timeBlocks = timeBlocks
                .OrderByDescending(tb => tb.ProductivityScore)
                .ThenBy(tb => tb.StartTime)
                .Take(10) // Limit to top 10 suggestions
                .ToList();

            _logger.LogInformation("Generated {BlockCount} time block suggestions for user {UserId}", 
                timeBlocks.Count, userId);

            return timeBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time blocks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimeBlockSuggestion>> GenerateTimeBlockRecommendationsAsync(
        Guid userId,
        DateTime date,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating time block recommendations for user {UserId} on {Date}", 
            userId, date.Date);

        try
        {
            var recommendations = new List<TimeBlockSuggestion>();
            var workingHours = preferences.PreferredWorkingHours;
            
            // Morning deep work block
            if (preferences.PreferMorningTasks)
            {
                var morningBlock = CreateDeepWorkBlock(
                    date.Date.Add(workingHours.StartTime).AddMinutes(30),
                    TimeSpan.FromHours(2),
                    "Morning Deep Work Session"
                );
                recommendations.Add(morningBlock);
            }

            // Afternoon planning block
            var planningBlock = CreatePlanningBlock(
                date.Date.Add(workingHours.EndTime).AddHours(-1),
                TimeSpan.FromMinutes(30),
                "Daily Planning & Review"
            );
            recommendations.Add(planningBlock);

            // Creative time block
            var creativeTime = preferences.ProductivityPattern switch
            {
                ProductivityPatterns.MorningPerson => date.Date.AddHours(10),
                ProductivityPatterns.NightOwl => date.Date.AddHours(15),
                ProductivityPatterns.MidDay => date.Date.AddHours(13),
                _ => date.Date.AddHours(11)
            };

            var creativeBlock = CreateCreativeBlock(creativeTime, TimeSpan.FromMinutes(90), "Creative Work Block");
            recommendations.Add(creativeBlock);

            // Add break blocks if buffer time is required
            if (preferences.RequireBufferTime)
            {
                recommendations.AddRange(GenerateBreakBlocks(date, workingHours, preferences.BufferDuration));
            }

            _logger.LogInformation("Generated {RecommendationCount} time block recommendations for user {UserId}", 
                recommendations.Count, userId);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time block recommendations for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimeBlockSuggestion>> OptimizeTimeBlocksAsync(
        Guid userId,
        List<TimeBlockSuggestion> currentTimeBlocks,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing {BlockCount} time blocks for user {UserId}", 
            currentTimeBlocks.Count, userId);

        try
        {
            var optimizedBlocks = new List<TimeBlockSuggestion>();

            foreach (var block in currentTimeBlocks)
            {
                var optimizedBlock = await OptimizeIndividualTimeBlockAsync(block, preferences, cancellationToken);
                optimizedBlocks.Add(optimizedBlock);
            }

            // Apply global optimizations
            optimizedBlocks = ApplyGlobalOptimizations(optimizedBlocks, preferences);

            _logger.LogInformation("Optimized time blocks for user {UserId}", userId);

            return optimizedBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing time blocks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimeBlockSuggestion>> CreateDeepWorkBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var deepWorkBlocks = new List<TimeBlockSuggestion>();

        foreach (var timeSlot in availableTime.Where(t => t.IsAvailable))
        {
            var duration = timeSlot.EndTime - timeSlot.StartTime;
            
            // Deep work blocks need at least 90 minutes
            if (duration >= TimeSpan.FromMinutes(90))
            {
                var optimalDuration = CalculateOptimalDeepWorkDuration(duration, preferences);
                var block = CreateDeepWorkBlock(timeSlot.StartTime, optimalDuration, "Deep Work Session");
                
                // Adjust productivity score based on time of day and user patterns
                var timeQuality = CalculateTimeQuality(timeSlot.StartTime, preferences);
                block = block with { ProductivityScore = block.ProductivityScore * timeQuality };
                
                deepWorkBlocks.Add(block);
            }
        }

        var maxBlocks = Math.Max(1, _settings.MaxDeepWorkBlocksPerDay);
        return deepWorkBlocks
            .OrderByDescending(b => b.ProductivityScore)
            .Take(maxBlocks)
            .ToList();
    }

    public async Task<List<TimeBlockSuggestion>> CreateAdministrativeBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var adminBlocks = new List<TimeBlockSuggestion>();

        // Schedule admin work during lower-energy periods
        var lowerEnergySlots = availableTime.Where(t => 
            t.IsAvailable && 
            IsLowerEnergyTime(t.StartTime, preferences)).ToList();

        foreach (var timeSlot in lowerEnergySlots)
        {
            var duration = timeSlot.EndTime - timeSlot.StartTime;
            
            if (duration >= TimeSpan.FromMinutes(30))
            {
                var optimalDuration = Math.Min(duration, TimeSpan.FromHours(1.5)); // Max 1.5 hours for admin
                
                var activities = new List<string>
                {
                    "Email processing and responses",
                    "Calendar management and scheduling",
                    "Document organization and filing",
                    "Administrative task completion",
                    "Status updates and reporting"
                };

                var block = new TimeBlockSuggestion(
                    Id: Guid.NewGuid(),
                    Title: "Administrative Work Block",
                    StartTime: timeSlot.StartTime,
                    EndTime: timeSlot.StartTime.Add(optimalDuration),
                    Purpose: TimeBlockPurpose.Administrative,
                    Description: "Dedicated time for handling administrative tasks and communication",
                    SuggestedActivities: activities,
                    ProductivityScore: 0.6, // Lower score for admin work
                    Reasoning: "Scheduled during lower-energy period to preserve peak hours for more demanding work"
                );

                adminBlocks.Add(block);
            }
        }

        var maxAdminBlocks = Math.Max(1, _settings.MaxAdminBlocksPerDay);
        return adminBlocks.Take(maxAdminBlocks).ToList();
    }

    public async Task<List<TimeBlockSuggestion>> CreateBufferBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        TimeSpan bufferDuration,
        CancellationToken cancellationToken = default)
    {
        var bufferBlocks = new List<TimeBlockSuggestion>();

        // Create buffers between consecutive high-priority tasks
        var sortedItems = scheduledItems.OrderBy(item => item.StartTime).ToList();

        for (int i = 0; i < sortedItems.Count - 1; i++)
        {
            var currentItem = sortedItems[i];
            var nextItem = sortedItems[i + 1];

            // Check if both items are high priority and close together
            if (currentItem.Priority.Value >= 3 && nextItem.Priority.Value >= 3)
            {
                var gapStart = currentItem.EndTime;
                var gapEnd = nextItem.StartTime;
                var gapDuration = gapEnd - gapStart;

                if (gapDuration >= bufferDuration && gapDuration <= TimeSpan.FromMinutes(30))
                {
                    var bufferBlock = new TimeBlockSuggestion(
                        Id: Guid.NewGuid(),
                        Title: "Buffer Time",
                        StartTime: gapStart,
                        EndTime: gapStart.Add(bufferDuration),
                        Purpose: TimeBlockPurpose.Buffer,
                        Description: "Buffer time between high-priority tasks",
                        SuggestedActivities: new List<string> 
                        { 
                            "Quick break and mental reset",
                            "Review notes from previous task",
                            "Prepare for upcoming task",
                            "Brief walk or stretching"
                        },
                        ProductivityScore: 0.7,
                        Reasoning: $"Buffer between '{currentItem.Title}' and '{nextItem.Title}' to prevent fatigue"
                    );

                    bufferBlocks.Add(bufferBlock);
                }
            }
        }

        return bufferBlocks;
    }

    public async Task<TimeBlockAnalysis> AnalyzeTimeBlockEffectivenessAsync(
        Guid userId,
        List<TimeBlockSuggestion> timeBlocks,
        CancellationToken cancellationToken = default)
    {
        var effectivenessByPurpose = new Dictionary<TimeBlockPurpose, double>();
        var insights = new List<TimeBlockInsight>();
        var recommendations = new List<string>();

        // Analyze effectiveness by purpose
        var purposeGroups = timeBlocks.GroupBy(tb => tb.Purpose);
        
        foreach (var group in purposeGroups)
        {
            var avgScore = group.Average(tb => tb.ProductivityScore);
            effectivenessByPurpose[group.Key] = avgScore;

            if (avgScore < 0.5)
            {
                insights.Add(new TimeBlockInsight(
                    InsightType: "LowEffectiveness",
                    Description: $"{group.Key} time blocks show low effectiveness",
                    ImpactScore: 0.3,
                    RecommendedActions: new List<string> 
                    { 
                        $"Review timing for {group.Key} blocks",
                        "Consider adjusting duration or activities",
                        "Check for scheduling conflicts"
                    },
                    AffectedTimeBlocks: group.Select(g => g.Id).ToList()
                ));
            }
        }

        // Generate overall recommendations
        var overallEffectiveness = effectivenessByPurpose.Values.Average();
        
        if (overallEffectiveness < 0.6)
        {
            recommendations.Add("Consider rescheduling time blocks to better align with your energy patterns");
            recommendations.Add("Reduce the number of time blocks to avoid over-scheduling");
        }

        if (timeBlocks.Count > 8)
        {
            recommendations.Add("Too many time blocks may cause stress - consider consolidating similar activities");
        }

        var metrics = new TimeBlockMetrics(
            TotalTimeBlocks: timeBlocks.Count,
            BlocksByPurpose: effectivenessByPurpose.Keys.ToDictionary(k => k, k => effectivenessByPurpose[k]),
            AverageBlockDuration: TimeSpan.FromMinutes(timeBlocks.Average(tb => (tb.EndTime - tb.StartTime).TotalMinutes)),
            TotalTimeBlocked: TimeSpan.FromMinutes(timeBlocks.Sum(tb => (tb.EndTime - tb.StartTime).TotalMinutes)),
            UtilizationRate: Math.Min(1.0, timeBlocks.Sum(tb => (tb.EndTime - tb.StartTime).TotalHours) / 8.0),
            CompletedBlocks: timeBlocks.Count, // Assume all are completed for this analysis
            InterruptedBlocks: 0
        );

        return new TimeBlockAnalysis(
            UserId: userId,
            AnalysisDate: DateTime.UtcNow,
            OverallEffectiveness: overallEffectiveness,
            EffectivenessByPurpose: effectivenessByPurpose,
            Insights: insights,
            Recommendations: recommendations,
            Metrics: metrics
        );
    }

    public async Task<TimeBlockDurationRecommendation> GetOptimalBlockDurationAsync(
        Guid userId,
        TimeBlockPurpose blockPurpose,
        CancellationToken cancellationToken = default)
    {
        var recommendations = blockPurpose switch
        {
            TimeBlockPurpose.DeepWork => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(120),
                MinimumDuration: TimeSpan.FromMinutes(90),
                MaximumDuration: TimeSpan.FromMinutes(180),
                ConfidenceScore: 0.9,
                FactorsConsidered: new List<string> { "Cognitive research", "Focus sustainability", "Quality output" },
                Reasoning: "Deep work requires extended focus periods for meaningful progress"
            ),
            
            TimeBlockPurpose.Administrative => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(60),
                MinimumDuration: TimeSpan.FromMinutes(30),
                MaximumDuration: TimeSpan.FromMinutes(90),
                ConfidenceScore: 0.8,
                FactorsConsidered: new List<string> { "Task switching overhead", "Email batching", "Efficiency patterns" },
                Reasoning: "Administrative tasks benefit from batching but shouldn't consume prime productivity hours"
            ),
            
            TimeBlockPurpose.Creative => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(90),
                MinimumDuration: TimeSpan.FromMinutes(60),
                MaximumDuration: TimeSpan.FromMinutes(150),
                ConfidenceScore: 0.85,
                FactorsConsidered: new List<string> { "Creative flow states", "Inspiration cycles", "Innovation research" },
                Reasoning: "Creative work needs time to develop flow state but benefits from natural breaking points"
            ),
            
            TimeBlockPurpose.Planning => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(30),
                MinimumDuration: TimeSpan.FromMinutes(15),
                MaximumDuration: TimeSpan.FromMinutes(45),
                ConfidenceScore: 0.9,
                FactorsConsidered: new List<string> { "Decision fatigue", "Planning effectiveness", "Time investment ROI" },
                Reasoning: "Planning sessions should be focused and decisive to maintain momentum"
            ),
            
            TimeBlockPurpose.Communication => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(45),
                MinimumDuration: TimeSpan.FromMinutes(30),
                MaximumDuration: TimeSpan.FromMinutes(90),
                ConfidenceScore: 0.75,
                FactorsConsidered: new List<string> { "Response time expectations", "Message batching", "Communication quality" },
                Reasoning: "Communication blocks allow for thoughtful responses while maintaining productivity flow"
            ),
            
            TimeBlockPurpose.Break => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(15),
                MinimumDuration: TimeSpan.FromMinutes(10),
                MaximumDuration: TimeSpan.FromMinutes(30),
                ConfidenceScore: 0.8,
                FactorsConsidered: new List<string> { "Recovery research", "Attention restoration", "Energy management" },
                Reasoning: "Short breaks optimize recovery without losing momentum from previous work"
            ),
            
            TimeBlockPurpose.Buffer => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(10),
                MinimumDuration: TimeSpan.FromMinutes(5),
                MaximumDuration: TimeSpan.FromMinutes(20),
                ConfidenceScore: 0.7,
                FactorsConsidered: new List<string> { "Context switching", "Mental preparation", "Schedule flexibility" },
                Reasoning: "Buffer blocks provide transition time between different types of work"
            ),
            
            _ => new TimeBlockDurationRecommendation(
                Purpose: blockPurpose,
                RecommendedDuration: TimeSpan.FromMinutes(60),
                MinimumDuration: TimeSpan.FromMinutes(30),
                MaximumDuration: TimeSpan.FromMinutes(120),
                ConfidenceScore: 0.6,
                FactorsConsidered: new List<string> { "General productivity patterns", "Default recommendations" },
                Reasoning: "Standard time block duration based on general productivity principles"
            )
        };

        return await Task.FromResult(recommendations);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(true);
    }

    // Private helper methods

    private List<TimeGap> FindScheduleGaps(List<SmartScheduledItem> scheduledItems, WorkingHours workingHours)
    {
        var gaps = new List<TimeGap>();
        var sortedItems = scheduledItems
            .Where(item => item.StartTime != DateTime.MinValue)
            .OrderBy(item => item.StartTime)
            .ToList();

        if (!sortedItems.Any())
        {
            // If no items scheduled, entire working day is available
            var today = DateTime.Today;
            gaps.Add(new TimeGap(
                StartTime: today.Add(workingHours.StartTime),
                EndTime: today.Add(workingHours.EndTime),
                Duration: workingHours.EndTime - workingHours.StartTime
            ));
            return gaps;
        }

        // Gap before first item
        var firstItem = sortedItems.First();
        var workDayStart = firstItem.StartTime.Date.Add(workingHours.StartTime);
        if (firstItem.StartTime > workDayStart)
        {
            gaps.Add(new TimeGap(
                StartTime: workDayStart,
                EndTime: firstItem.StartTime,
                Duration: firstItem.StartTime - workDayStart
            ));
        }

        // Gaps between items
        for (int i = 0; i < sortedItems.Count - 1; i++)
        {
            var currentEnd = sortedItems[i].EndTime;
            var nextStart = sortedItems[i + 1].StartTime;

            if (nextStart > currentEnd)
            {
                gaps.Add(new TimeGap(
                    StartTime: currentEnd,
                    EndTime: nextStart,
                    Duration: nextStart - currentEnd
                ));
            }
        }

        // Gap after last item
        var lastItem = sortedItems.Last();
        var workDayEnd = lastItem.EndTime.Date.Add(workingHours.EndTime);
        if (lastItem.EndTime < workDayEnd)
        {
            gaps.Add(new TimeGap(
                StartTime: lastItem.EndTime,
                EndTime: workDayEnd,
                Duration: workDayEnd - lastItem.EndTime
            ));
        }

        return gaps.Where(g => g.Duration >= TimeSpan.FromMinutes(15)).ToList();
    }

    private TimeBlockPurpose DetermineOptimalBlockPurpose(
        TimeGap gap, 
        List<SmartScheduledItem> scheduledItems, 
        SmartSchedulingPreferences preferences)
    {
        var gapHour = gap.StartTime.Hour;
        var gapDuration = gap.Duration;

        // Long gaps in peak hours = Deep work
        if (gapDuration >= TimeSpan.FromMinutes(90) && 
            ((gapHour >= 9 && gapHour <= 11) || (gapHour >= 14 && gapHour <= 16)))
        {
            return TimeBlockPurpose.DeepWork;
        }

        // Short gaps between high-priority tasks = Buffer
        if (gapDuration <= TimeSpan.FromMinutes(20) && 
            HasHighPriorityTasksAround(gap, scheduledItems))
        {
            return TimeBlockPurpose.Buffer;
        }

        // Afternoon gaps = Administrative
        if (gapHour >= 15 && gapDuration <= TimeSpan.FromHours(1))
        {
            return TimeBlockPurpose.Administrative;
        }

        // Morning gaps = Creative (if user prefers mornings)
        if (preferences.PreferMorningTasks && gapHour <= 11 && gapDuration >= TimeSpan.FromMinutes(60))
        {
            return TimeBlockPurpose.Creative;
        }

        // Default to planning for medium gaps
        return TimeBlockPurpose.Planning;
    }

    private TimeBlockSuggestion? CreateTimeBlockSuggestion(
        TimeGap gap, 
        TimeBlockPurpose purpose, 
        SmartSchedulingPreferences preferences)
    {
        var optimalDuration = CalculateOptimalDuration(gap.Duration, purpose);
        var endTime = gap.StartTime.Add(optimalDuration);

        var activities = GetSuggestedActivities(purpose);
        var title = GetTimeBlockTitle(purpose);
        var description = GetTimeBlockDescription(purpose);
        var reasoning = GetTimeBlockReasoning(gap, purpose);

        var productivityScore = CalculateProductivityScore(gap.StartTime, purpose, preferences);

        return new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: title,
            StartTime: gap.StartTime,
            EndTime: endTime,
            Purpose: purpose,
            Description: description,
            SuggestedActivities: activities,
            ProductivityScore: productivityScore,
            Reasoning: reasoning
        );
    }

    private async Task<List<TimeBlockSuggestion>> GenerateStrategicTimeBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken)
    {
        var strategicBlocks = new List<TimeBlockSuggestion>();

        // Daily review block at end of day
        var workingHours = preferences.PreferredWorkingHours;
        var reviewTime = DateTime.Today.Add(workingHours.EndTime).AddMinutes(-30);
        
        if (!HasConflict(reviewTime, TimeSpan.FromMinutes(15), scheduledItems))
        {
            strategicBlocks.Add(new TimeBlockSuggestion(
                Id: Guid.NewGuid(),
                Title: "Daily Review",
                StartTime: reviewTime,
                EndTime: reviewTime.AddMinutes(15),
                Purpose: TimeBlockPurpose.Planning,
                Description: "Review today's accomplishments and plan tomorrow",
                SuggestedActivities: new List<string>
                {
                    "Review completed tasks",
                    "Identify lessons learned",
                    "Plan next day's priorities",
                    "Update project status"
                },
                ProductivityScore: 0.8,
                Reasoning: "End-of-day review improves learning and next-day preparation"
            ));
        }

        // Weekly planning block (if it's Monday)
        if (DateTime.Today.DayOfWeek == DayOfWeek.Monday)
        {
            var planningTime = DateTime.Today.Add(workingHours.StartTime);
            
            if (!HasConflict(planningTime, TimeSpan.FromMinutes(30), scheduledItems))
            {
                strategicBlocks.Add(new TimeBlockSuggestion(
                    Id: Guid.NewGuid(),
                    Title: "Weekly Planning",
                    StartTime: planningTime,
                    EndTime: planningTime.AddMinutes(30),
                    Purpose: TimeBlockPurpose.Planning,
                    Description: "Strategic planning for the week ahead",
                    SuggestedActivities: new List<string>
                    {
                        "Review weekly goals",
                        "Identify key priorities",
                        "Schedule important tasks",
                        "Plan major deliverables"
                    },
                    ProductivityScore: 0.9,
                    Reasoning: "Weekly planning at start of week sets clear direction and priorities"
                ));
            }
        }

        return strategicBlocks;
    }

    private async Task<TimeBlockSuggestion> OptimizeIndividualTimeBlockAsync(
        TimeBlockSuggestion block,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken)
    {
        // Optimize timing based on user preferences and productivity patterns
        var optimalTime = FindOptimalTimeForPurpose(block.Purpose, block.StartTime.Date, preferences);
        
        if (optimalTime.HasValue && Math.Abs((optimalTime.Value - block.StartTime).TotalHours) > 1)
        {
            var duration = block.EndTime - block.StartTime;
            var newProductivityScore = CalculateProductivityScore(optimalTime.Value, block.Purpose, preferences);
            
            if (newProductivityScore > block.ProductivityScore)
            {
                return block with
                {
                    StartTime = optimalTime.Value,
                    EndTime = optimalTime.Value.Add(duration),
                    ProductivityScore = newProductivityScore,
                    Reasoning = block.Reasoning + " (Optimized for peak productivity time)"
                };
            }
        }

        return block;
    }

    private List<TimeBlockSuggestion> ApplyGlobalOptimizations(
        List<TimeBlockSuggestion> timeBlocks,
        SmartSchedulingPreferences preferences)
    {
        // Ensure proper spacing between blocks
        var sortedBlocks = timeBlocks.OrderBy(b => b.StartTime).ToList();
        var optimizedBlocks = new List<TimeBlockSuggestion>();

        TimeBlockSuggestion? previousBlock = null;
        
        foreach (var block in sortedBlocks)
        {
            var optimizedBlock = block;
            
            if (previousBlock != null)
            {
                var gap = block.StartTime - previousBlock.EndTime;
                
                // Ensure minimum gap between different types of blocks
                var requiredGap = GetRequiredGapBetweenBlocks(previousBlock.Purpose, block.Purpose);
                
                if (gap < requiredGap)
                {
                    var adjustment = requiredGap - gap;
                    optimizedBlock = block with
                    {
                        StartTime = block.StartTime.Add(adjustment),
                        EndTime = block.EndTime.Add(adjustment)
                    };
                }
            }
            
            optimizedBlocks.Add(optimizedBlock);
            previousBlock = optimizedBlock;
        }

        return optimizedBlocks;
    }

    private TimeBlockSuggestion CreateDeepWorkBlock(DateTime startTime, TimeSpan duration, string title)
    {
        return new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: title,
            StartTime: startTime,
            EndTime: startTime.Add(duration),
            Purpose: TimeBlockPurpose.DeepWork,
            Description: "Uninterrupted time for focused, high-value work",
            SuggestedActivities: new List<string>
            {
                "Complex problem solving",
                "Strategic thinking and planning",
                "Creative work and innovation",
                "Important project advancement",
                "Learning and skill development"
            },
            ProductivityScore: 0.9,
            Reasoning: "Deep work blocks maximize output on high-value activities"
        );
    }

    private TimeBlockSuggestion CreatePlanningBlock(DateTime startTime, TimeSpan duration, string title)
    {
        return new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: title,
            StartTime: startTime,
            EndTime: startTime.Add(duration),
            Purpose: TimeBlockPurpose.Planning,
            Description: "Strategic planning and goal setting time",
            SuggestedActivities: new List<string>
            {
                "Review and update goals",
                "Plan upcoming tasks and projects",
                "Analyze progress and metrics",
                "Schedule important activities",
                "Reflect on priorities and direction"
            },
            ProductivityScore: 0.8,
            Reasoning: "Planning blocks ensure strategic alignment and clear direction"
        );
    }

    private TimeBlockSuggestion CreateCreativeBlock(DateTime startTime, TimeSpan duration, string title)
    {
        return new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: title,
            StartTime: startTime,
            EndTime: startTime.Add(duration),
            Purpose: TimeBlockPurpose.Creative,
            Description: "Dedicated time for creative and innovative thinking",
            SuggestedActivities: new List<string>
            {
                "Brainstorming and ideation",
                "Creative problem solving",
                "Innovation and experimentation",
                "Design and conceptual work",
                "Artistic and creative pursuits"
            },
            ProductivityScore: 0.85,
            Reasoning: "Creative blocks foster innovation and out-of-the-box thinking"
        );
    }

    private List<TimeBlockSuggestion> GenerateBreakBlocks(DateTime date, WorkingHours workingHours, TimeSpan bufferDuration)
    {
        var breakBlocks = new List<TimeBlockSuggestion>();

        // Morning break
        var morningBreak = date.Date.AddHours(10).AddMinutes(30);
        breakBlocks.Add(new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: "Morning Break",
            StartTime: morningBreak,
            EndTime: morningBreak.Add(bufferDuration),
            Purpose: TimeBlockPurpose.Break,
            Description: "Short break for refreshment and mental reset",
            SuggestedActivities: new List<string> { "Stretch", "Hydrate", "Brief walk", "Deep breathing" },
            ProductivityScore: 0.7,
            Reasoning: "Regular breaks maintain energy and focus throughout the day"
        ));

        // Afternoon break
        var afternoonBreak = date.Date.AddHours(15);
        breakBlocks.Add(new TimeBlockSuggestion(
            Id: Guid.NewGuid(),
            Title: "Afternoon Break",
            StartTime: afternoonBreak,
            EndTime: afternoonBreak.Add(bufferDuration),
            Purpose: TimeBlockPurpose.Break,
            Description: "Afternoon energy boost break",
            SuggestedActivities: new List<string> { "Light snack", "Stretch", "Fresh air", "Quick meditation" },
            ProductivityScore: 0.7,
            Reasoning: "Afternoon breaks combat energy dips and restore focus"
        ));

        return breakBlocks;
    }

    // Helper calculation methods

    private TimeSpan CalculateOptimalDeepWorkDuration(TimeSpan availableDuration, SmartSchedulingPreferences preferences)
    {
        // Optimal deep work is typically 90-120 minutes
        var optimalDuration = TimeSpan.FromMinutes(105); // 1h 45m
        
        if (availableDuration < optimalDuration)
        {
            return TimeSpan.FromMinutes(Math.Max(90, availableDuration.TotalMinutes - 15)); // Leave 15min buffer
        }
        
        return optimalDuration;
    }

    private double CalculateTimeQuality(DateTime startTime, SmartSchedulingPreferences preferences)
    {
        var hour = startTime.Hour;
        
        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => hour <= 12 ? 1.0 : 0.6,
            ProductivityPatterns.NightOwl => hour >= 14 ? 1.0 : 0.6,
            ProductivityPatterns.MidDay => hour >= 10 && hour <= 15 ? 1.0 : 0.7,
            _ => 0.8 // Consistent pattern
        };
    }

    private bool IsLowerEnergyTime(DateTime startTime, SmartSchedulingPreferences preferences)
    {
        var hour = startTime.Hour;
        
        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => hour >= 15,
            ProductivityPatterns.NightOwl => hour <= 10,
            ProductivityPatterns.MidDay => hour <= 9 || hour >= 16,
            _ => hour <= 8 || hour >= 17 // Outside normal hours
        };
    }

    private bool HasHighPriorityTasksAround(TimeGap gap, List<SmartScheduledItem> scheduledItems)
    {
        var beforeTask = scheduledItems
            .Where(item => item.EndTime <= gap.StartTime)
            .OrderBy(item => item.EndTime)
            .LastOrDefault();

        var afterTask = scheduledItems
            .Where(item => item.StartTime >= gap.EndTime)
            .OrderBy(item => item.StartTime)
            .FirstOrDefault();

        return (beforeTask?.Priority.Value >= 3) && (afterTask?.Priority.Value >= 3);
    }

    private TimeSpan CalculateOptimalDuration(TimeSpan availableDuration, TimeBlockPurpose purpose)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => TimeSpan.FromMinutes(Math.Min(120, Math.Max(90, availableDuration.TotalMinutes - 15))),
            TimeBlockPurpose.Administrative => TimeSpan.FromMinutes(Math.Min(90, Math.Max(30, availableDuration.TotalMinutes - 10))),
            TimeBlockPurpose.Creative => TimeSpan.FromMinutes(Math.Min(120, Math.Max(60, availableDuration.TotalMinutes - 15))),
            TimeBlockPurpose.Planning => TimeSpan.FromMinutes(Math.Min(45, Math.Max(15, availableDuration.TotalMinutes - 5))),
            TimeBlockPurpose.Communication => TimeSpan.FromMinutes(Math.Min(60, Math.Max(30, availableDuration.TotalMinutes - 10))),
            TimeBlockPurpose.Break => TimeSpan.FromMinutes(Math.Min(20, Math.Max(10, availableDuration.TotalMinutes - 5))),
            TimeBlockPurpose.Buffer => TimeSpan.FromMinutes(Math.Min(15, availableDuration.TotalMinutes)),
            _ => TimeSpan.FromMinutes(Math.Min(60, availableDuration.TotalMinutes - 10))
        };
    }

    private List<string> GetSuggestedActivities(TimeBlockPurpose purpose)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => new List<string> { "Complex analysis", "Strategic planning", "Creative work", "Important projects" },
            TimeBlockPurpose.Administrative => new List<string> { "Email processing", "Document organization", "Scheduling", "Status updates" },
            TimeBlockPurpose.Creative => new List<string> { "Brainstorming", "Design work", "Innovation", "Problem solving" },
            TimeBlockPurpose.Planning => new List<string> { "Goal setting", "Task prioritization", "Schedule review", "Progress analysis" },
            TimeBlockPurpose.Communication => new List<string> { "Team meetings", "Client calls", "Feedback sessions", "Collaboration" },
            TimeBlockPurpose.Break => new List<string> { "Rest and recovery", "Stretching", "Fresh air", "Mindfulness" },
            TimeBlockPurpose.Buffer => new List<string> { "Preparation", "Transition", "Quick tasks", "Mental reset" },
            _ => new List<string> { "Focused work", "Task completion", "Productivity activities" }
        };
    }

    private string GetTimeBlockTitle(TimeBlockPurpose purpose)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => "Deep Work Session",
            TimeBlockPurpose.Administrative => "Administrative Tasks",
            TimeBlockPurpose.Creative => "Creative Time",
            TimeBlockPurpose.Planning => "Planning Session",
            TimeBlockPurpose.Communication => "Communication Block",
            TimeBlockPurpose.Break => "Break Time",
            TimeBlockPurpose.Buffer => "Buffer Time",
            _ => "Focus Block"
        };
    }

    private string GetTimeBlockDescription(TimeBlockPurpose purpose)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => "Uninterrupted time for complex, high-value work",
            TimeBlockPurpose.Administrative => "Handle routine tasks and organizational activities",
            TimeBlockPurpose.Creative => "Dedicated time for creative thinking and innovation",
            TimeBlockPurpose.Planning => "Strategic planning and goal-setting session",
            TimeBlockPurpose.Communication => "Time for meetings, calls, and collaboration",
            TimeBlockPurpose.Break => "Rest and recovery time to maintain energy",
            TimeBlockPurpose.Buffer => "Transition time between different activities",
            _ => "Focused work session"
        };
    }

    private string GetTimeBlockReasoning(TimeGap gap, TimeBlockPurpose purpose)
    {
        return $"Optimal {purpose.ToString().ToLower()} time based on {gap.Duration.TotalMinutes:F0}-minute availability window";
    }

    private double CalculateProductivityScore(DateTime startTime, TimeBlockPurpose purpose, SmartSchedulingPreferences preferences)
    {
        var timeQuality = CalculateTimeQuality(startTime, preferences);
        var purposeMultiplier = purpose switch
        {
            TimeBlockPurpose.DeepWork => 1.0,
            TimeBlockPurpose.Creative => 0.9,
            TimeBlockPurpose.Planning => 0.85,
            TimeBlockPurpose.Communication => 0.75,
            TimeBlockPurpose.Administrative => 0.6,
            TimeBlockPurpose.Break => 0.7,
            TimeBlockPurpose.Buffer => 0.5,
            _ => 0.7
        };

        return Math.Min(1.0, timeQuality * purposeMultiplier);
    }

    private bool HasConflict(DateTime startTime, TimeSpan duration, List<SmartScheduledItem> scheduledItems)
    {
        var endTime = startTime.Add(duration);
        return scheduledItems.Any(item => 
            item.StartTime < endTime && startTime < item.EndTime);
    }

    private DateTime? FindOptimalTimeForPurpose(TimeBlockPurpose purpose, DateTime date, SmartSchedulingPreferences preferences)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => preferences.PreferMorningTasks ? date.AddHours(9) : date.AddHours(14),
            TimeBlockPurpose.Administrative => date.AddHours(15),
            TimeBlockPurpose.Creative => date.AddHours(10),
            TimeBlockPurpose.Planning => date.AddHours(8),
            TimeBlockPurpose.Communication => date.AddHours(13),
            _ => null
        };
    }

    private TimeSpan GetRequiredGapBetweenBlocks(TimeBlockPurpose previousPurpose, TimeBlockPurpose nextPurpose)
    {
        // Deep work needs more buffer time
        if (previousPurpose == TimeBlockPurpose.DeepWork || nextPurpose == TimeBlockPurpose.DeepWork)
        {
            return TimeSpan.FromMinutes(10);
        }

        // Administrative to creative needs transition time
        if ((previousPurpose == TimeBlockPurpose.Administrative && nextPurpose == TimeBlockPurpose.Creative) ||
            (previousPurpose == TimeBlockPurpose.Creative && nextPurpose == TimeBlockPurpose.Administrative))
        {
            return TimeSpan.FromMinutes(15);
        }

        return TimeSpan.FromMinutes(5); // Default minimum gap
    }

    // Helper record for internal use
    private sealed record TimeGap(DateTime StartTime, DateTime EndTime, TimeSpan Duration);
}