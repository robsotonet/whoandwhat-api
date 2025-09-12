using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Tests.Builders;

/// <summary>
/// Test builder for MotivationalContent entity
/// </summary>
public class MotivationalContentBuilder
{
    private string _title = "Default Motivational Title";
    private string _message = "Default motivational message for testing purposes.";
    private MotivationalContentType _contentType = MotivationalContentType.Insight;
    private ContentCategory _category = ContentCategory.Productivity;
    private Dictionary<string, object>? _targetConditions = null;
    private int _priority = 100;
    private bool _isActive = true;
    private bool _isDeleted = false;
    private DateTime? _scheduledFor = null;
    private bool _isABTestEnabled = false;
    private Dictionary<string, object>? _abTestConfiguration = null;
    private double _currentScore = 0.0;
    private DateTime? _lastOptimized = null;

    /// <summary>
    /// Creates a new MotivationalContentBuilder with default values
    /// </summary>
    public static MotivationalContentBuilder New() => new();

    /// <summary>
    /// Sets the title for the motivational content
    /// </summary>
    public MotivationalContentBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the message for the motivational content
    /// </summary>
    public MotivationalContentBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets the content type
    /// </summary>
    public MotivationalContentBuilder WithContentType(MotivationalContentType contentType)
    {
        _contentType = contentType;
        return this;
    }

    /// <summary>
    /// Sets the category
    /// </summary>
    public MotivationalContentBuilder WithCategory(ContentCategory category)
    {
        _category = category;
        return this;
    }

    /// <summary>
    /// Sets the target conditions
    /// </summary>
    public MotivationalContentBuilder WithTargetConditions(Dictionary<string, object> targetConditions)
    {
        _targetConditions = targetConditions;
        return this;
    }

    /// <summary>
    /// Sets the priority
    /// </summary>
    public MotivationalContentBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Marks the content as inactive
    /// </summary>
    public MotivationalContentBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    /// <summary>
    /// Marks the content as deleted
    /// </summary>
    public MotivationalContentBuilder AsDeleted()
    {
        _isDeleted = true;
        return this;
    }

    /// <summary>
    /// Sets a scheduled delivery time
    /// </summary>
    public MotivationalContentBuilder ScheduledFor(DateTime scheduledFor)
    {
        _scheduledFor = scheduledFor;
        return this;
    }

    /// <summary>
    /// Enables A/B testing with configuration
    /// </summary>
    public MotivationalContentBuilder WithABTesting(Dictionary<string, object>? configuration = null)
    {
        _isABTestEnabled = true;
        _abTestConfiguration = configuration ?? new Dictionary<string, object>();
        return this;
    }

    /// <summary>
    /// Sets the current engagement score
    /// </summary>
    public MotivationalContentBuilder WithScore(double score)
    {
        _currentScore = score;
        return this;
    }

    /// <summary>
    /// Sets the last optimized timestamp
    /// </summary>
    public MotivationalContentBuilder LastOptimizedAt(DateTime lastOptimized)
    {
        _lastOptimized = lastOptimized;
        return this;
    }

    /// <summary>
    /// Creates a basic achievement content
    /// </summary>
    public MotivationalContentBuilder AsAchievement()
    {
        _title = "🎉 Great Achievement!";
        _message = "You've accomplished something remarkable today! Keep up the excellent work.";
        _contentType = MotivationalContentType.Achievement;
        _category = ContentCategory.Achievement;
        _priority = 90;
        return this;
    }

    /// <summary>
    /// Creates a basic productivity tip
    /// </summary>
    public MotivationalContentBuilder AsProductivityTip()
    {
        _title = "💡 Productivity Tip";
        _message = "Break large tasks into smaller, manageable chunks for better focus and completion rates.";
        _contentType = MotivationalContentType.Tip;
        _category = ContentCategory.Productivity;
        _priority = 75;
        return this;
    }

    /// <summary>
    /// Creates a basic encouragement message
    /// </summary>
    public MotivationalContentBuilder AsEncouragement()
    {
        _title = "💪 You've Got This!";
        _message = "Every step forward is progress. You're doing better than you think!";
        _contentType = MotivationalContentType.Encouragement;
        _category = ContentCategory.Motivation;
        _priority = 80;
        return this;
    }

    /// <summary>
    /// Creates a streak celebration
    /// </summary>
    public MotivationalContentBuilder AsStreakCelebration(int streakDays = 7)
    {
        _title = $"🔥 {streakDays}-Day Streak!";
        _message = $"Incredible! You've maintained your productivity streak for {streakDays} days. That's real dedication!";
        _contentType = MotivationalContentType.Streak;
        _category = ContentCategory.Achievement;
        _priority = 95;
        _targetConditions = new Dictionary<string, object>
        {
            ["minStreakDays"] = streakDays
        };
        return this;
    }

    /// <summary>
    /// Creates content targeted at specific experience level
    /// </summary>
    public MotivationalContentBuilder ForExperienceLevel(UserExperienceLevel level)
    {
        _targetConditions ??= new Dictionary<string, object>();
        _targetConditions["experienceLevel"] = level;
        
        // Adjust content based on experience level
        switch (level)
        {
            case UserExperienceLevel.Beginner:
                _title = "🌱 Getting Started";
                _message = "Every expert was once a beginner. You're on the right path!";
                break;
            case UserExperienceLevel.Intermediate:
                _title = "📈 Building Momentum";
                _message = "You're developing great habits! Keep refining your approach.";
                break;
            case UserExperienceLevel.Expert:
                _title = "🏆 Mastery in Action";
                _message = "Your expertise shows in every task you complete. Inspiring work!";
                break;
        }
        return this;
    }

    /// <summary>
    /// Creates wellness-focused content
    /// </summary>
    public MotivationalContentBuilder AsWellnessReminder()
    {
        _title = "🌿 Balance Check";
        _message = "Remember to take breaks and care for your wellbeing. You're more than your productivity!";
        _contentType = MotivationalContentType.Reminder;
        _category = ContentCategory.Wellness;
        _priority = 70;
        return this;
    }

    /// <summary>
    /// Builds the MotivationalContent instance
    /// </summary>
    public MotivationalContent Build()
    {
        var content = MotivationalContent.Create(
            _title,
            _message,
            _contentType,
            _category,
            _targetConditions,
            _priority);

        // Set additional properties using reflection or by calling methods
        if (!_isActive)
        {
            content.Deactivate();
        }

        if (_isDeleted)
        {
            content.SoftDelete();
        }

        if (_scheduledFor.HasValue)
        {
            content.ScheduleFor(_scheduledFor.Value);
        }

        if (_isABTestEnabled)
        {
            content.ConfigureABTest("testGroup", _abTestConfiguration ?? new Dictionary<string, object>());
        }

        // Note: Some properties like CurrentScore and LastOptimized might need to be set 
        // through methods or properties depending on the entity's API

        return content;
    }

    /// <summary>
    /// Builds multiple instances with incremental variations
    /// </summary>
    public List<MotivationalContent> BuildMany(int count)
    {
        var contents = new List<MotivationalContent>();
        
        for (int i = 0; i < count; i++)
        {
            var builder = New()
                .WithTitle($"{_title} #{i + 1}")
                .WithMessage($"{_message} (Instance {i + 1})")
                .WithContentType(_contentType)
                .WithCategory(_category)
                .WithPriority(_priority + i);

            contents.Add(builder.Build());
        }

        return contents;
    }
}