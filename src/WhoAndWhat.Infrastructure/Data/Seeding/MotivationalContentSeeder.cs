using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Infrastructure.Data.Seeding;

/// <summary>
/// Seeds initial motivational content into the database
/// </summary>
public class MotivationalContentSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MotivationalContentSeeder> _logger;

    public MotivationalContentSeeder(ApplicationDbContext context, ILogger<MotivationalContentSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seeds motivational content if none exists
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if content already exists
            var existingContentCount = await _context.MotivationalContents.CountAsync(cancellationToken);
            if (existingContentCount > 0)
            {
                _logger.LogInformation("Motivational content already exists ({Count} items), skipping seeding", existingContentCount);
                return;
            }

            _logger.LogInformation("Starting motivational content seeding...");

            // Seed different categories of content
            var contents = new List<MotivationalContent>();

            // Achievement & Success Content
            contents.AddRange(CreateAchievementContent());
            
            // Productivity & Focus Content  
            contents.AddRange(CreateProductivityContent());
            
            // Wellness & Balance Content
            contents.AddRange(CreateWellnessContent());
            
            // Learning & Growth Content
            contents.AddRange(CreateLearningContent());
            
            // Streak & Milestone Content
            contents.AddRange(CreateStreakContent());
            
            // Encouragement & Support Content
            contents.AddRange(CreateEncouragementContent());

            // Add all content to context
            await _context.MotivationalContents.AddRangeAsync(contents, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully seeded {Count} motivational content items", contents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding motivational content");
            throw;
        }
    }

    private List<MotivationalContent> CreateAchievementContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "🎉 Task Completed!",
                message: "Great job completing that task! You're building momentum one accomplishment at a time.",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateBasicTargeting(UserExperienceLevel.Beginner),
                priority: 100
            ),
            
            MotivationalContent.Create(
                title: "✨ Outstanding Progress!",
                message: "Your consistency is paying off! You've completed several tasks today - that's the kind of dedication that leads to success.",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateBasicTargeting(UserExperienceLevel.Intermediate),
                priority: 90
            ),

            MotivationalContent.Create(
                title: "🚀 Productivity Master!",
                message: "Incredible! You've completed multiple high-priority tasks. Your systematic approach to getting things done is truly impressive.",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateAdvancedTargeting(UserExperienceLevel.Expert, 0.8),
                priority: 95,
                actionText: "View Analytics",
                actionUrl: "/dashboard/analytics"
            )
        };
    }

    private List<MotivationalContent> CreateProductivityContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "🎯 Focus Time",
                message: "Ready to tackle your next task? Choose one important item and give it your full attention for the next 25 minutes.",
                MotivationalContentType.Insight,
                MotivationalContentCategory.Productivity,
                CreateBasicTargeting(UserExperienceLevel.Beginner),
                priority: 80
            ),

            MotivationalContent.Create(
                title: "⚡ Peak Performance Tip",
                message: "Break large tasks into smaller, manageable chunks. You're more likely to complete them and feel accomplished along the way!",
                MotivationalContentType.Insight,
                MotivationalContentCategory.Productivity,
                CreateBasicTargeting(UserExperienceLevel.Intermediate),
                priority: 85
            ),

            MotivationalContent.Create(
                title: "🧠 Smart Prioritization",
                message: "Try the 'Two-Minute Rule': If a task takes less than two minutes, do it immediately. For larger tasks, schedule them.",
                MotivationalContentType.Insight,
                MotivationalContentCategory.Productivity,
                CreateAdvancedTargeting(UserExperienceLevel.Expert, 0.7),
                priority: 90,
                actionText: "Set Priorities",
                actionUrl: "/tasks?sort=priority"
            )
        };
    }

    private List<MotivationalContent> CreateWellnessContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "🌱 Balance Check",
                message: "Remember to take breaks! Even a 5-minute walk or stretch can refresh your mind and boost creativity.",
                MotivationalContentType.Reminder,
                MotivationalContentCategory.Wellness,
                CreateBasicTargeting(UserExperienceLevel.Beginner),
                priority: 70
            ),

            MotivationalContent.Create(
                title: "🧘 Mindful Moment",
                message: "Take a deep breath. You're doing great work, and it's okay to pause and appreciate your progress.",
                MotivationalContentType.Encouragement,
                MotivationalContentCategory.Wellness,
                CreateBasicTargeting(UserExperienceLevel.Intermediate),
                priority: 75
            ),

            MotivationalContent.Create(
                title: "🌟 Work-Life Harmony",
                message: "High performers know when to push and when to rest. Honor both your ambition and your need for restoration.",
                MotivationalContentType.Insight,
                MotivationalContentCategory.Wellness,
                CreateAdvancedTargeting(UserExperienceLevel.Expert, 0.9),
                priority: 85
            )
        };
    }

    private List<MotivationalContent> CreateLearningContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "📚 Growth Mindset",
                message: "Every task you complete teaches you something new. What did you learn from your recent accomplishments?",
                MotivationalContentType.Reflection,
                MotivationalContentCategory.Learning,
                CreateBasicTargeting(UserExperienceLevel.Beginner),
                priority: 60
            ),

            MotivationalContent.Create(
                title: "🎓 Skill Building",
                message: "Consider adding a learning goal to your tasks. What skill would help you work more effectively?",
                MotivationalContentType.Suggestion,
                MotivationalContentCategory.Learning,
                CreateBasicTargeting(UserExperienceLevel.Intermediate),
                priority: 65,
                actionText: "Add Learning Task",
                actionUrl: "/tasks/create?category=learning"
            ),

            MotivationalContent.Create(
                title: "🏆 Mastery Path",
                message: "You're becoming a master of your craft. Each completed project adds to your expertise and opens new opportunities.",
                MotivationalContentType.Encouragement,
                MotivationalContentCategory.Learning,
                CreateAdvancedTargeting(UserExperienceLevel.Expert, 0.85),
                priority: 80
            )
        };
    }

    private List<MotivationalContent> CreateStreakContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "🔥 Streak Started!",
                message: "You've begun a productivity streak! Consistency is the key to lasting success. Keep the momentum going!",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateStreakTargeting(3),
                priority: 95
            ),

            MotivationalContent.Create(
                title: "⚡ Week Strong!",
                message: "7 days of consistent progress! You're proving that small daily actions create remarkable results.",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateStreakTargeting(7),
                priority: 100,
                actionText: "Share Achievement",
                actionUrl: "/social/share-streak"
            ),

            MotivationalContent.Create(
                title: "💎 Streak Master!",
                message: "30 days of excellence! You've transformed consistency into a superpower. This is how success is built!",
                MotivationalContentType.Achievement,
                MotivationalContentCategory.Productivity,
                CreateStreakTargeting(30),
                priority: 110,
                actionText: "View Milestone",
                actionUrl: "/achievements/30-day-streak"
            )
        };
    }

    private List<MotivationalContent> CreateEncouragementContent()
    {
        return new List<MotivationalContent>
        {
            MotivationalContent.Create(
                title: "💪 You've Got This!",
                message: "Feeling stuck? That's normal! Take a moment to appreciate how far you've come, then take one small step forward.",
                MotivationalContentType.Encouragement,
                MotivationalContentCategory.Wellness,
                CreateLowActivityTargeting(),
                priority: 90
            ),

            MotivationalContent.Create(
                title: "🌈 Progress Over Perfection",
                message: "Done is better than perfect. You're making progress, even if it doesn't feel like it right now. Keep going!",
                MotivationalContentType.Encouragement,
                MotivationalContentCategory.Wellness,
                CreateBasicTargeting(UserExperienceLevel.Beginner),
                priority: 85
            ),

            MotivationalContent.Create(
                title: "🎨 Your Unique Path",
                message: "Your approach to productivity is uniquely yours. Trust your process and celebrate your individual wins.",
                MotivationalContentType.Encouragement,
                MotivationalContentCategory.Learning,
                CreateBasicTargeting(UserExperienceLevel.Expert),
                priority: 75
            )
        };
    }

    private Dictionary<string, object> CreateBasicTargeting(UserExperienceLevel experienceLevel)
    {
        return new Dictionary<string, object>
        {
            ["experienceLevel"] = experienceLevel,
            ["minCompletionRate"] = experienceLevel switch
            {
                UserExperienceLevel.Beginner => 0.3,
                UserExperienceLevel.Intermediate => 0.5,
                UserExperienceLevel.Expert => 0.7,
                _ => 0.5
            }
        };
    }

    private Dictionary<string, object> CreateAdvancedTargeting(UserExperienceLevel experienceLevel, double minCompletionRate)
    {
        return new Dictionary<string, object>
        {
            ["experienceLevel"] = experienceLevel,
            ["minCompletionRate"] = minCompletionRate,
            ["categories"] = new[] { "Productivity", "Learning" }
        };
    }

    private Dictionary<string, object> CreateStreakTargeting(int minStreakDays)
    {
        return new Dictionary<string, object>
        {
            ["minStreakDays"] = minStreakDays,
            ["experienceLevel"] = UserExperienceLevel.Beginner
        };
    }

    private Dictionary<string, object> CreateLowActivityTargeting()
    {
        return new Dictionary<string, object>
        {
            ["experienceLevel"] = UserExperienceLevel.Beginner,
            ["maxRecentActivity"] = TimeSpan.FromDays(2),
            ["minCompletionRate"] = 0.2
        };
    }
}