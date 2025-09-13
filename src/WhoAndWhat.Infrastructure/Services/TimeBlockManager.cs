using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Advanced time block manager for optimizing productivity through intelligent time segmentation
/// </summary>
public class TimeBlockManager : ITimeBlockManager
{
    private readonly ILogger<TimeBlockManager> _logger;
    private readonly SmartSchedulingSettings _settings;
    private readonly IUserSchedulingPreferenceService _userPreferenceService;

    public TimeBlockManager(
        IOptions<SmartSchedulingSettings> settings,
        IUserSchedulingPreferenceService userPreferenceService,
        ILogger<TimeBlockManager> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _userPreferenceService = userPreferenceService ?? throw new ArgumentNullException(nameof(userPreferenceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<TimeBlockSuggestion>> GenerateTimeBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating time blocks for user {UserId} with {ItemCount} scheduled items",
                userId, scheduledItems.Count);

            var timeBlocks = new List<TimeBlockSuggestion>();

            // Step 1: Identify available time slots between scheduled items
            var availableSlots = IdentifyAvailableSlots(scheduledItems, preferences);

            // Step 2: Generate deep work blocks
            var deepWorkBlocks = await CreateDeepWorkBlocksAsync(userId, availableSlots, preferences, cancellationToken);
            timeBlocks.AddRange(deepWorkBlocks);

            // Step 3: Generate administrative blocks
            var adminBlocks = await CreateAdministrativeBlocksAsync(userId, availableSlots, preferences, cancellationToken);
            timeBlocks.AddRange(adminBlocks);

            // Step 4: Generate buffer blocks
            var bufferBlocks = await CreateBufferBlocksAsync(userId, scheduledItems, preferences.BufferDuration, cancellationToken);
            timeBlocks.AddRange(bufferBlocks);

            // Step 5: Generate break blocks
            var breakBlocks = CreateBreakBlocks(scheduledItems, preferences);
            timeBlocks.AddRange(breakBlocks);

            // Step 6: Optimize time block placement
            timeBlocks = await OptimizeTimeBlocksAsync(userId, timeBlocks, preferences, cancellationToken);

            _logger.LogInformation("Generated {TimeBlockCount} time blocks for user {UserId}", timeBlocks.Count, userId);

            return timeBlocks.OrderBy(tb => tb.StartTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time blocks for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<List<TimeBlockSuggestion>> GenerateTimeBlockRecommendationsAsync(
        Guid userId,
        DateTime date,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating time block recommendations for user {UserId} on {Date}", userId, date);

            var recommendations = new List<TimeBlockSuggestion>();
            var workingHours = preferences.PreferredWorkingHours;
            var startTime = date.Date.Add(workingHours.StartTime);
            var endTime = date.Date.Add(workingHours.EndTime);

            // Get user patterns to inform recommendations
            var patterns = await _userPreferenceService.GetUserSchedulingPatternsAsync(userId, cancellationToken);

            // Generate recommendations based on productivity patterns
            recommendations.AddRange(await GenerateProductivityBasedBlocks(userId, startTime, endTime, preferences, patterns));

            // Add standard recommendations
            recommendations.AddRange(GenerateStandardTimeBlocks(startTime, endTime, preferences));

            _logger.LogInformation("Generated {RecommendationCount} time block recommendations for user {UserId}",
                recommendations.Count, userId);

            return recommendations.OrderBy(r => r.StartTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time block recommendations for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<List<TimeBlockSuggestion>> OptimizeTimeBlocksAsync(
        Guid userId,
        List<TimeBlockSuggestion> currentTimeBlocks,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Optimizing {TimeBlockCount} time blocks for user {UserId}",
                currentTimeBlocks.Count, userId);

            var optimizedBlocks = new List<TimeBlockSuggestion>(currentTimeBlocks);

            // Step 1: Remove overlapping blocks
            optimizedBlocks = ResolveTimeBlockOverlaps(optimizedBlocks);

            // Step 2: Optimize block durations
            optimizedBlocks = await OptimizeBlockDurations(userId, optimizedBlocks, cancellationToken);

            // Step 3: Optimize block sequence
            optimizedBlocks = OptimizeBlockSequence(optimizedBlocks, preferences);

            // Step 4: Ensure minimum gaps between blocks
            optimizedBlocks = EnsureMinimumGaps(optimizedBlocks, preferences.BufferDuration);

            _logger.LogInformation("Optimized time blocks for user {UserId}", userId);

            return optimizedBlocks.OrderBy(tb => tb.StartTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing time blocks for user {UserId}", userId);
            return currentTimeBlocks;
        }
    }

    public async Task<List<TimeBlockSuggestion>> CreateDeepWorkBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deepWorkBlocks = new List<TimeBlockSuggestion>();
            var optimalDuration = await GetOptimalBlockDurationAsync(userId, TimeBlockPurpose.DeepWork, cancellationToken);

            // Find slots suitable for deep work (longer duration, peak productivity times)
            var suitableSlots = availableTime.Where(slot =>
                (slot.EndTime - slot.StartTime) >= optimalDuration.RecommendedDuration &&
                IsOptimalForDeepWork(slot.StartTime, preferences.ProductivityPattern))
                .OrderByDescending(slot => GetProductivityScoreForTime(slot.StartTime, preferences.ProductivityPattern))
                .Take(Math.Min(3, _settings.MaxDeepWorkBlocksPerDay)); // Limit to 3 deep work blocks per day

            foreach (var slot in suitableSlots)
            {
                var blockDuration = TimeSpan.FromMinutes(
                    Math.Min(optimalDuration.RecommendedDuration.TotalMinutes, (slot.EndTime - slot.StartTime).TotalMinutes));

                deepWorkBlocks.Add(new TimeBlockSuggestion(
                    Guid.NewGuid(),
                    "Deep Work Session",
                    slot.StartTime,
                    slot.StartTime.Add(blockDuration),
                    TimeBlockPurpose.DeepWork,
                    "Focused work time for complex tasks requiring sustained attention",
                    new List<string>
                    {
                        "Turn off notifications",
                        "Work on your most challenging tasks",
                        "Avoid meetings and interruptions",
                        "Focus on single-tasking"
                    },
                    GetProductivityScoreForTime(slot.StartTime, preferences.ProductivityPattern),
                    "Scheduled during your peak productivity hours for maximum focus"
                ));
            }

            return deepWorkBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deep work blocks for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<List<TimeBlockSuggestion>> CreateAdministrativeBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var adminBlocks = new List<TimeBlockSuggestion>();
            var optimalDuration = await GetOptimalBlockDurationAsync(userId, TimeBlockPurpose.Administrative, cancellationToken);

            // Find slots suitable for administrative work (shorter duration, any time)
            var suitableSlots = availableTime.Where(slot =>
                (slot.EndTime - slot.StartTime) >= optimalDuration.MinimumDuration)
                .OrderBy(slot => GetProductivityScoreForTime(slot.StartTime, preferences.ProductivityPattern)) // Use lower energy times
                .Take(Math.Min(2, _settings.MaxAdminBlocksPerDay)); // Limit to 2 admin blocks per day

            foreach (var slot in suitableSlots)
            {
                var blockDuration = TimeSpan.FromMinutes(
                    Math.Min(optimalDuration.RecommendedDuration.TotalMinutes, (slot.EndTime - slot.StartTime).TotalMinutes));

                adminBlocks.Add(new TimeBlockSuggestion(
                    Guid.NewGuid(),
                    "Administrative Tasks",
                    slot.StartTime,
                    slot.StartTime.Add(blockDuration),
                    TimeBlockPurpose.Administrative,
                    "Time for emails, scheduling, and routine administrative work",
                    new List<string>
                    {
                        "Process emails",
                        "Schedule meetings",
                        "Update calendars",
                        "Handle routine paperwork",
                        "Review and organize tasks"
                    },
                    0.6, // Lower productivity score as these are routine tasks
                    "Scheduled during lower energy periods for routine administrative work"
                ));
            }

            return adminBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating administrative blocks for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<List<TimeBlockSuggestion>> CreateBufferBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        TimeSpan bufferDuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bufferBlocks = new List<TimeBlockSuggestion>();

            // Create buffer blocks between consecutive scheduled items
            var sortedItems = scheduledItems.OrderBy(item => item.StartTime).ToList();

            for (int i = 0; i < sortedItems.Count - 1; i++)
            {
                var currentItem = sortedItems[i];
                var nextItem = sortedItems[i + 1];
                var gap = nextItem.StartTime - currentItem.EndTime;

                // Only create buffer if gap is larger than required buffer duration
                if (gap > bufferDuration)
                {
                    var bufferStartTime = currentItem.EndTime;
                    var bufferEndTime = bufferStartTime.Add(bufferDuration);

                    bufferBlocks.Add(new TimeBlockSuggestion(
                        Guid.NewGuid(),
                        "Transition Buffer",
                        bufferStartTime,
                        bufferEndTime,
                        TimeBlockPurpose.Buffer,
                        $"Buffer time between '{currentItem.Title}' and '{nextItem.Title}'",
                        new List<string>
                        {
                            "Prepare for next task",
                            "Quick mental reset",
                            "Review next task requirements",
                            "Take a short break if needed"
                        },
                        0.7,
                        "Provides transition time between tasks to reduce context switching stress"
                    ));
                }
            }

            return bufferBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating buffer blocks for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<TimeBlockAnalysis> AnalyzeTimeBlockEffectivenessAsync(
        Guid userId,
        List<TimeBlockSuggestion> timeBlocks,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing time block effectiveness for user {UserId} with {BlockCount} blocks",
                userId, timeBlocks.Count);

            // Calculate overall effectiveness
            var overallEffectiveness = timeBlocks.Average(tb => tb.ProductivityScore);

            // Calculate effectiveness by purpose
            var effectivenessByPurpose = timeBlocks
                .GroupBy(tb => tb.Purpose)
                .ToDictionary(g => g.Key, g => g.Average(tb => tb.ProductivityScore));

            // Generate insights
            var insights = GenerateTimeBlockInsights(timeBlocks);

            // Generate recommendations
            var recommendations = GenerateTimeBlockRecommendations(timeBlocks, effectivenessByPurpose);

            // Calculate metrics
            var metrics = CalculateTimeBlockMetrics(timeBlocks);

            return new TimeBlockAnalysis(
                userId,
                DateTime.UtcNow,
                overallEffectiveness,
                effectivenessByPurpose,
                insights,
                recommendations,
                metrics
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing time block effectiveness for user {UserId}", userId);
            throw;
        }
    }

    public async Task<TimeBlockDurationRecommendation> GetOptimalBlockDurationAsync(
        Guid userId,
        TimeBlockPurpose blockPurpose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user-specific patterns if available
            var patterns = await _userPreferenceService.GetUserSchedulingPatternsAsync(userId, cancellationToken);
            
            var recommendation = blockPurpose switch
            {
                TimeBlockPurpose.DeepWork => new TimeBlockDurationRecommendation(
                    TimeBlockPurpose.DeepWork,
                    TimeSpan.FromMinutes(90), // Recommended duration
                    TimeSpan.FromMinutes(45),  // Minimum duration
                    TimeSpan.FromMinutes(180), // Maximum duration
                    0.85,
                    new List<string> { "Research on focus cycles", "User productivity patterns" },
                    "Deep work sessions are most effective in 90-minute blocks aligned with natural focus cycles"
                ),

                TimeBlockPurpose.Administrative => new TimeBlockDurationRecommendation(
                    TimeBlockPurpose.Administrative,
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(60),
                    0.80,
                    new List<string> { "Task complexity analysis", "Administrative task patterns" },
                    "Administrative tasks are efficiently handled in shorter, focused blocks"
                ),

                TimeBlockPurpose.Creative => new TimeBlockDurationRecommendation(
                    TimeBlockPurpose.Creative,
                    TimeSpan.FromMinutes(60),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromMinutes(120),
                    0.75,
                    new List<string> { "Creative process research", "User creative patterns" },
                    "Creative work benefits from uninterrupted time blocks with flexibility for inspiration"
                ),

                TimeBlockPurpose.Break => new TimeBlockDurationRecommendation(
                    TimeBlockPurpose.Break,
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    0.90,
                    new List<string> { "Recovery time research", "Productivity break patterns" },
                    "Short breaks help maintain focus and prevent burnout"
                ),

                _ => new TimeBlockDurationRecommendation(
                    blockPurpose,
                    TimeSpan.FromMinutes(45),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(90),
                    0.70,
                    new List<string> { "General productivity research" },
                    "Standard time block duration based on general productivity guidelines"
                )
            };

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimal block duration for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if time block manager is properly configured
            return _settings.EnableTimeBlocking && await _userPreferenceService.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking time block manager availability");
            return false;
        }
    }

    // Private helper methods

    private List<TimeSlot> IdentifyAvailableSlots(List<SmartScheduledItem> scheduledItems, SmartSchedulingPreferences preferences)
    {
        var availableSlots = new List<TimeSlot>();
        var workingHours = preferences.PreferredWorkingHours;

        if (!scheduledItems.Any())
        {
            // If no scheduled items, the entire working day is available
            var startTime = DateTime.Today.Add(workingHours.StartTime);
            var endTime = DateTime.Today.Add(workingHours.EndTime);
            
            availableSlots.Add(new TimeSlot(startTime, endTime, true, null, TimeSlotType.Work));
            return availableSlots;
        }

        var sortedItems = scheduledItems.OrderBy(item => item.StartTime).ToList();
        var workStartTime = DateTime.Today.Add(workingHours.StartTime);
        var workEndTime = DateTime.Today.Add(workingHours.EndTime);

        // Check for available slot before first scheduled item
        if (sortedItems.First().StartTime > workStartTime)
        {
            availableSlots.Add(new TimeSlot(workStartTime, sortedItems.First().StartTime, true, null, TimeSlotType.Work));
        }

        // Check for available slots between scheduled items
        for (int i = 0; i < sortedItems.Count - 1; i++)
        {
            var currentEnd = sortedItems[i].EndTime;
            var nextStart = sortedItems[i + 1].StartTime;

            if (nextStart > currentEnd)
            {
                availableSlots.Add(new TimeSlot(currentEnd, nextStart, true, null, TimeSlotType.Work));
            }
        }

        // Check for available slot after last scheduled item
        if (sortedItems.Last().EndTime < workEndTime)
        {
            availableSlots.Add(new TimeSlot(sortedItems.Last().EndTime, workEndTime, true, null, TimeSlotType.Work));
        }

        return availableSlots.Where(slot => (slot.EndTime - slot.StartTime).TotalMinutes >= 15).ToList(); // Minimum 15-minute slots
    }

    private List<TimeBlockSuggestion> CreateBreakBlocks(List<SmartScheduledItem> scheduledItems, SmartSchedulingPreferences preferences)
    {
        var breakBlocks = new List<TimeBlockSuggestion>();

        // Create break blocks at preferred break times
        foreach (var breakTime in preferences.PreferredBreakTimes)
        {
            var breakDateTime = DateTime.Today.Add(breakTime);
            
            // Check if break time conflicts with scheduled items
            var hasConflict = scheduledItems.Any(item => 
                breakDateTime >= item.StartTime && breakDateTime <= item.EndTime);

            if (!hasConflict)
            {
                breakBlocks.Add(new TimeBlockSuggestion(
                    Guid.NewGuid(),
                    "Scheduled Break",
                    breakDateTime,
                    breakDateTime.AddMinutes(15), // 15-minute breaks
                    TimeBlockPurpose.Break,
                    "Scheduled break for mental refreshment",
                    new List<string>
                    {
                        "Take a walk",
                        "Practice deep breathing",
                        "Hydrate",
                        "Stretch",
                        "Step away from screens"
                    },
                    0.8,
                    "Regular breaks help maintain focus and energy throughout the day"
                ));
            }
        }

        return breakBlocks;
    }

    private async Task<List<TimeBlockSuggestion>> GenerateProductivityBasedBlocks(
        Guid userId,
        DateTime startTime,
        DateTime endTime,
        SmartSchedulingPreferences preferences,
        UserSchedulingPatternsResponse patterns)
    {
        var blocks = new List<TimeBlockSuggestion>();

        // Generate blocks based on productivity patterns
        switch (preferences.ProductivityPattern)
        {
            case ProductivityPatterns.MorningPerson:
                blocks.Add(CreateProductivityBlock("Peak Focus Morning", startTime, TimeSpan.FromMinutes(120), TimeBlockPurpose.DeepWork));
                blocks.Add(CreateProductivityBlock("Administrative Morning", startTime.AddHours(2.5), TimeSpan.FromMinutes(45), TimeBlockPurpose.Administrative));
                break;

            case ProductivityPatterns.AfternoonPeak:
                blocks.Add(CreateProductivityBlock("Afternoon Deep Work", startTime.AddHours(4), TimeSpan.FromMinutes(90), TimeBlockPurpose.DeepWork));
                blocks.Add(CreateProductivityBlock("Morning Admin", startTime.AddHours(1), TimeSpan.FromMinutes(60), TimeBlockPurpose.Administrative));
                break;

            case ProductivityPatterns.NightOwl:
                blocks.Add(CreateProductivityBlock("Late Morning Focus", startTime.AddHours(3), TimeSpan.FromMinutes(90), TimeBlockPurpose.DeepWork));
                blocks.Add(CreateProductivityBlock("Early Admin", startTime, TimeSpan.FromMinutes(45), TimeBlockPurpose.Administrative));
                break;

            default:
                blocks.Add(CreateProductivityBlock("Mid-Morning Focus", startTime.AddHours(2), TimeSpan.FromMinutes(90), TimeBlockPurpose.DeepWork));
                blocks.Add(CreateProductivityBlock("Early Admin", startTime.AddMinutes(30), TimeSpan.FromMinutes(30), TimeBlockPurpose.Administrative));
                break;
        }

        return blocks.Where(b => b.StartTime >= startTime && b.EndTime <= endTime).ToList();
    }

    private TimeBlockSuggestion CreateProductivityBlock(string title, DateTime startTime, TimeSpan duration, TimeBlockPurpose purpose)
    {
        return new TimeBlockSuggestion(
            Guid.NewGuid(),
            title,
            startTime,
            startTime.Add(duration),
            purpose,
            $"Optimized {purpose.ToString().ToLower()} time block",
            GetSuggestedActivitiesForPurpose(purpose),
            0.85,
            "Scheduled during optimal productivity period based on your patterns"
        );
    }

    private List<TimeBlockSuggestion> GenerateStandardTimeBlocks(DateTime startTime, DateTime endTime, SmartSchedulingPreferences preferences)
    {
        var blocks = new List<TimeBlockSuggestion>();

        // Add standard planning block at start of day
        blocks.Add(new TimeBlockSuggestion(
            Guid.NewGuid(),
            "Daily Planning",
            startTime,
            startTime.AddMinutes(15),
            TimeBlockPurpose.Planning,
            "Daily planning and priority setting",
            new List<string>
            {
                "Review daily goals",
                "Prioritize tasks",
                "Check calendar",
                "Set intentions for the day"
            },
            0.75,
            "Start your day with clear priorities and intentions"
        ));

        // Add end-of-day review block
        blocks.Add(new TimeBlockSuggestion(
            Guid.NewGuid(),
            "Daily Review",
            endTime.AddMinutes(-15),
            endTime,
            TimeBlockPurpose.Planning,
            "End-of-day review and tomorrow's preparation",
            new List<string>
            {
                "Review completed tasks",
                "Note lessons learned",
                "Prepare for tomorrow",
                "Celebrate achievements"
            },
            0.70,
            "End your day with reflection and preparation for tomorrow"
        ));

        return blocks;
    }

    private bool IsOptimalForDeepWork(DateTime startTime, ProductivityPatterns pattern)
    {
        var hour = startTime.Hour;
        
        return pattern switch
        {
            ProductivityPatterns.MorningPerson => hour >= 8 && hour <= 11,
            ProductivityPatterns.AfternoonPeak => hour >= 13 && hour <= 16,
            ProductivityPatterns.NightOwl => hour >= 14 && hour <= 17,
            _ => hour >= 9 && hour <= 12
        };
    }

    private double GetProductivityScoreForTime(DateTime time, ProductivityPatterns pattern)
    {
        var hour = time.Hour;
        
        return pattern switch
        {
            ProductivityPatterns.MorningPerson => hour <= 12 ? 1.0 - (hour - 8) * 0.1 : 0.5,
            ProductivityPatterns.AfternoonPeak => hour >= 13 && hour <= 16 ? 1.0 : 0.6,
            ProductivityPatterns.NightOwl => hour >= 14 ? 1.0 - (hour - 14) * 0.05 : 0.4,
            _ => hour >= 9 && hour <= 15 ? 0.8 : 0.6
        };
    }

    private List<TimeBlockSuggestion> ResolveTimeBlockOverlaps(List<TimeBlockSuggestion> timeBlocks)
    {
        var resolvedBlocks = new List<TimeBlockSuggestion>();
        var sortedBlocks = timeBlocks.OrderBy(tb => tb.StartTime).ToList();

        foreach (var block in sortedBlocks)
        {
            var currentBlock = block;
            
            // Check for overlaps with already resolved blocks
            foreach (var resolvedBlock in resolvedBlocks)
            {
                if (currentBlock.StartTime < resolvedBlock.EndTime && resolvedBlock.StartTime < currentBlock.EndTime)
                {
                    // Move the current block after the resolved block
                    var newStartTime = resolvedBlock.EndTime;
                    var duration = currentBlock.EndTime - currentBlock.StartTime;
                    currentBlock = currentBlock with 
                    { 
                        StartTime = newStartTime, 
                        EndTime = newStartTime.Add(duration) 
                    };
                }
            }

            resolvedBlocks.Add(currentBlock);
        }

        return resolvedBlocks;
    }

    private async Task<List<TimeBlockSuggestion>> OptimizeBlockDurations(
        Guid userId,
        List<TimeBlockSuggestion> timeBlocks,
        CancellationToken cancellationToken)
    {
        var optimizedBlocks = new List<TimeBlockSuggestion>();

        foreach (var block in timeBlocks)
        {
            var optimalDuration = await GetOptimalBlockDurationAsync(userId, block.Purpose, cancellationToken);
            var currentDuration = block.EndTime - block.StartTime;
            
            // Adjust duration if it's significantly different from optimal
            if (currentDuration < optimalDuration.MinimumDuration || currentDuration > optimalDuration.MaximumDuration)
            {
                var newDuration = optimalDuration.RecommendedDuration;
                optimizedBlocks.Add(block with { EndTime = block.StartTime.Add(newDuration) });
            }
            else
            {
                optimizedBlocks.Add(block);
            }
        }

        return optimizedBlocks;
    }

    private List<TimeBlockSuggestion> OptimizeBlockSequence(List<TimeBlockSuggestion> timeBlocks, SmartSchedulingPreferences preferences)
    {
        // Order blocks by productivity pattern optimal sequence
        var optimizedSequence = timeBlocks.OrderBy(tb => GetOptimalSequenceScore(tb, preferences.ProductivityPattern)).ToList();
        
        // Adjust start times based on optimal sequence
        var currentTime = timeBlocks.Min(tb => tb.StartTime);
        var resequencedBlocks = new List<TimeBlockSuggestion>();

        foreach (var block in optimizedSequence)
        {
            var duration = block.EndTime - block.StartTime;
            resequencedBlocks.Add(block with 
            { 
                StartTime = currentTime, 
                EndTime = currentTime.Add(duration) 
            });
            currentTime = currentTime.Add(duration);
        }

        return resequencedBlocks;
    }

    private List<TimeBlockSuggestion> EnsureMinimumGaps(List<TimeBlockSuggestion> timeBlocks, TimeSpan minimumGap)
    {
        var adjustedBlocks = new List<TimeBlockSuggestion>();
        var sortedBlocks = timeBlocks.OrderBy(tb => tb.StartTime).ToList();

        for (int i = 0; i < sortedBlocks.Count; i++)
        {
            var currentBlock = sortedBlocks[i];
            
            if (i > 0)
            {
                var previousBlock = adjustedBlocks[i - 1];
                var gap = currentBlock.StartTime - previousBlock.EndTime;
                
                if (gap < minimumGap)
                {
                    var newStartTime = previousBlock.EndTime.Add(minimumGap);
                    var duration = currentBlock.EndTime - currentBlock.StartTime;
                    currentBlock = currentBlock with 
                    { 
                        StartTime = newStartTime, 
                        EndTime = newStartTime.Add(duration) 
                    };
                }
            }
            
            adjustedBlocks.Add(currentBlock);
        }

        return adjustedBlocks;
    }

    private int GetOptimalSequenceScore(TimeBlockSuggestion timeBlock, ProductivityPatterns pattern)
    {
        // Return sequence priority (lower number = earlier in sequence)
        return timeBlock.Purpose switch
        {
            TimeBlockPurpose.Planning => 1,
            TimeBlockPurpose.DeepWork => pattern == ProductivityPatterns.MorningPerson ? 2 : 4,
            TimeBlockPurpose.Administrative => pattern == ProductivityPatterns.MorningPerson ? 4 : 2,
            TimeBlockPurpose.Creative => 3,
            TimeBlockPurpose.Communication => 5,
            TimeBlockPurpose.Break => 6,
            _ => 7
        };
    }

    private List<string> GetSuggestedActivitiesForPurpose(TimeBlockPurpose purpose)
    {
        return purpose switch
        {
            TimeBlockPurpose.DeepWork => new List<string>
            {
                "Complex problem solving",
                "Strategic planning",
                "Creative work",
                "Writing and analysis",
                "Learning new skills"
            },
            TimeBlockPurpose.Administrative => new List<string>
            {
                "Email processing",
                "Calendar management",
                "File organization",
                "Routine tasks",
                "Status updates"
            },
            TimeBlockPurpose.Creative => new List<string>
            {
                "Brainstorming",
                "Design work",
                "Innovation projects",
                "Creative writing",
                "Artistic endeavors"
            },
            TimeBlockPurpose.Planning => new List<string>
            {
                "Goal setting",
                "Priority planning",
                "Schedule review",
                "Progress tracking",
                "Strategy development"
            },
            TimeBlockPurpose.Communication => new List<string>
            {
                "Team meetings",
                "Client calls",
                "Collaboration",
                "Networking",
                "Presentations"
            },
            _ => new List<string> { "General productivity activities" }
        };
    }

    private List<TimeBlockInsight> GenerateTimeBlockInsights(List<TimeBlockSuggestion> timeBlocks)
    {
        var insights = new List<TimeBlockInsight>();

        // Analyze time block distribution
        var purposeDistribution = timeBlocks.GroupBy(tb => tb.Purpose).ToDictionary(g => g.Key, g => g.Count());
        
        if (purposeDistribution.GetValueOrDefault(TimeBlockPurpose.DeepWork, 0) < 2)
        {
            insights.Add(new TimeBlockInsight(
                "InsufficientDeepWork",
                "You may benefit from more deep work time blocks for complex tasks",
                0.7,
                new List<string> { "Add more deep work blocks", "Protect existing deep work time" },
                timeBlocks.Where(tb => tb.Purpose == TimeBlockPurpose.DeepWork).Select(tb => tb.Id).ToList()
            ));
        }

        if (purposeDistribution.GetValueOrDefault(TimeBlockPurpose.Break, 0) == 0)
        {
            insights.Add(new TimeBlockInsight(
                "NoBreaks",
                "Consider adding break blocks to maintain energy throughout the day",
                0.6,
                new List<string> { "Schedule regular breaks", "Include short rest periods" },
                new List<Guid>()
            ));
        }

        return insights;
    }

    private List<string> GenerateTimeBlockRecommendations(
        List<TimeBlockSuggestion> timeBlocks,
        Dictionary<TimeBlockPurpose, double> effectivenessByPurpose)
    {
        var recommendations = new List<string>();

        // Analyze effectiveness and make recommendations
        foreach (var purposeScore in effectivenessByPurpose.Where(ps => ps.Value < 0.7))
        {
            recommendations.Add($"Consider optimizing your {purposeScore.Key} time blocks for better effectiveness");
        }

        if (!effectivenessByPurpose.ContainsKey(TimeBlockPurpose.Break))
        {
            recommendations.Add("Add regular break blocks to maintain productivity throughout the day");
        }

        if (timeBlocks.Count < 3)
        {
            recommendations.Add("Consider using more time blocks to better structure your day");
        }

        return recommendations;
    }

    private TimeBlockMetrics CalculateTimeBlockMetrics(List<TimeBlockSuggestion> timeBlocks)
    {
        var blocksByPurpose = timeBlocks.GroupBy(tb => tb.Purpose).ToDictionary(g => g.Key, g => g.Count());
        var totalDuration = timeBlocks.Aggregate(TimeSpan.Zero, (total, block) => total.Add(block.EndTime - block.StartTime));
        var averageDuration = timeBlocks.Any() ? 
            TimeSpan.FromMinutes(totalDuration.TotalMinutes / timeBlocks.Count) : 
            TimeSpan.Zero;

        return new TimeBlockMetrics(
            timeBlocks.Count,
            blocksByPurpose,
            averageDuration,
            totalDuration,
            0.75, // Placeholder utilization rate
            timeBlocks.Count, // Assume all blocks are completed for now
            0 // No interrupted blocks in this implementation
        );
    }
}