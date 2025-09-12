using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        // TODO: Domain event dispatching will be implemented here when business logic requires it
        // The IDomainEventDispatcher infrastructure is available but not yet used by domain entities
    }

    public DbSet<User> Users { get; set; }
    public DbSet<AppTask> Tasks { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<OAuthAccount> OAuthAccounts { get; set; }

    // Motivational Content entities
    public DbSet<MotivationalContent> MotivationalContents { get; set; }
    public DbSet<UserContentPreferences> UserContentPreferences { get; set; }
    public DbSet<ContentDeliveryLog> ContentDeliveryLogs { get; set; }

    // Analytics entities (temporarily commented out for initial migration)
    // public DbSet<UserAnalytics> UserAnalytics { get; set; }
    // public DbSet<ProductivityStreak> ProductivityStreaks { get; set; }
    // public DbSet<TaskMetrics> TaskMetrics { get; set; }
    // public DbSet<AnalyticsSnapshot> AnalyticsSnapshots { get; set; }

    // Archive entities
    public DbSet<ArchivedAppTask> ArchivedAppTasks { get; set; }
    public DbSet<ArchivedProject> ArchivedProjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);

            entity.Property(e => e.PreferredLanguage)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => (Language)Enum.Parse(typeof(Language), v));

            // Configure ignore for private collections used by EF Core relationships
            entity.Ignore(e => e.RefreshTokens);
            entity.Ignore(e => e.OAuthAccounts);
        });

        // Task Configuration
        modelBuilder.Entity<AppTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.User).WithMany(u => u.Tasks).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Project).WithMany(p => p.Tasks).HasForeignKey(e => e.ProjectId).IsRequired(false);
            entity.HasMany(e => e.Subtasks).WithOne().HasForeignKey("ParentTaskId").IsRequired(false);

            // Indexes for performance
            entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            entity.HasIndex(e => new { e.UserId, e.Status, e.IsDeleted });
            entity.HasIndex(e => new { e.UserId, e.Category, e.IsDeleted });
            entity.HasIndex(e => new { e.UserId, e.DueDate, e.IsDeleted });

            // Global query filter for soft delete
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Contact Configuration
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.RelationshipType).HasConversion<int>();

            entity.HasOne(e => e.User).WithMany(u => u.Contacts).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Tasks).WithMany(t => t.Contacts);

            // Soft delete configuration
            entity.HasIndex(e => e.IsDeleted);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Project Configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();

            entity.HasOne(e => e.User).WithMany(u => u.Projects).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Contacts).WithMany();

            // Soft delete configuration
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Event Configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Type).IsRequired();

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Tasks).WithMany();
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.CreatedByIp).IsRequired().HasMaxLength(45);
            entity.Property(e => e.RevokedByIp).HasMaxLength(45);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient queries
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
            entity.HasIndex(e => e.ExpiresAt);
        });

        // OAuthAccount Configuration
        modelBuilder.Entity<OAuthAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint for provider + external ID combination
            entity.HasIndex(e => new { e.Provider, e.ExternalId }).IsUnique();
            // Index for efficient user queries
            entity.HasIndex(e => e.UserId);
        });

        // ArchivedAppTask Configuration
        modelBuilder.Entity<ArchivedAppTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.ArchiveReason).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProjectName).HasMaxLength(100);
            entity.Property(e => e.ParentAppTaskTitle).HasMaxLength(200);

            // JSON columns for serialized data
            entity.Property(e => e.SubtasksJson).HasColumnType("jsonb");
            entity.Property(e => e.ContactsJson).HasColumnType("jsonb");
            entity.Property(e => e.AttachmentsJson).HasColumnType("jsonb");

            // Indexes for efficient querying
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OriginalAppTaskId).IsUnique();
            entity.HasIndex(e => e.ArchivedAt);
            entity.HasIndex(e => new { e.UserId, e.ArchivedAt });
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.Category });

            // Foreign key relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referential relationships for archived tasks
            entity.HasIndex(e => e.ProjectId).HasFilter("project_id IS NOT NULL");
            entity.HasIndex(e => e.ParentAppTaskId).HasFilter("parent_app_task_id IS NOT NULL");
        });

        // ArchivedProject Configuration
        modelBuilder.Entity<ArchivedProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ArchiveReason).IsRequired().HasMaxLength(50);

            // JSON columns for serialized data
            entity.Property(e => e.TasksJson).HasColumnType("jsonb");
            entity.Property(e => e.ContactsJson).HasColumnType("jsonb");
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");

            // Indexes for efficient querying
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OriginalProjectId).IsUnique();
            entity.HasIndex(e => e.ArchivedAt);
            entity.HasIndex(e => new { e.UserId, e.ArchivedAt });
            entity.HasIndex(e => new { e.UserId, e.Status });

            // Foreign key relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MotivationalContent Configuration
        modelBuilder.Entity<MotivationalContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasConversion<int>();
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.ABTestGroup).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionText).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsABTestEnabled).HasDefaultValue(false);
            entity.Property(e => e.Priority).HasDefaultValue(0);

            // JSON columns for complex objects
            entity.Property(e => e.TargetConditions).HasColumnType("jsonb");
            entity.Property(e => e.ABTestConfiguration).HasColumnType("jsonb");
            entity.Property(e => e.SchedulingRules).HasColumnType("jsonb");
            entity.Property(e => e.Metadata).HasColumnType("jsonb");

            // Indexes for performance
            entity.HasIndex(e => e.ContentType);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => new { e.IsActive, e.Priority });
            entity.HasIndex(e => new { e.IsABTestEnabled, e.ABTestGroup });
            entity.HasIndex(e => new { e.StartDate, e.EndDate });

            // Soft delete configuration
            entity.HasIndex(e => e.IsDeleted);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // UserContentPreferences Configuration
        modelBuilder.Entity<UserContentPreferences>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.IsContentEnabled).HasDefaultValue(true);
            entity.Property(e => e.PreferredFrequency).HasConversion<int>().HasDefaultValue(1); // Moderate
            entity.Property(e => e.MaxDailyContent).HasDefaultValue(3);
            entity.Property(e => e.MaxWeeklyContent).HasDefaultValue(15);
            entity.Property(e => e.AllowWeekends).HasDefaultValue(true);
            entity.Property(e => e.AllowAfterHours).HasDefaultValue(false);
            entity.Property(e => e.TimeZone).IsRequired().HasMaxLength(50).HasDefaultValue("UTC");

            // JSON columns for complex collections
            entity.Property(e => e.PreferredContentTypes).HasColumnType("jsonb");
            entity.Property(e => e.PreferredCategories).HasColumnType("jsonb");
            entity.Property(e => e.PreferredChannels).HasColumnType("jsonb");
            entity.Property(e => e.PreferredDeliveryTimes).HasColumnType("jsonb");
            entity.Property(e => e.PersonalizationSettings).HasColumnType("jsonb");
            entity.Property(e => e.EngagementHistory).HasColumnType("jsonb");

            // Foreign key to User
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsContentEnabled });
            entity.HasIndex(e => e.ContentPausedUntil).HasFilter("content_paused_until IS NOT NULL");
        });

        // ContentDeliveryLog Configuration
        modelBuilder.Entity<ContentDeliveryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.MotivationalContentId).IsRequired();
            entity.Property(e => e.DeliveredAt).IsRequired();
            entity.Property(e => e.DeliveryChannel).HasConversion<int>();
            entity.Property(e => e.ABTestGroup).HasMaxLength(100);
            entity.Property(e => e.EngagementType).HasConversion<int?>();
            entity.Property(e => e.DeliveryMethod).HasMaxLength(100);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.DeviceType).HasMaxLength(50);
            entity.Property(e => e.WasPersonalized).HasDefaultValue(false);

            // JSON columns for metadata
            entity.Property(e => e.DeliveryContext).HasColumnType("jsonb");
            entity.Property(e => e.EngagementMetadata).HasColumnType("jsonb");

            // Foreign key relationships
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MotivationalContent).WithMany().HasForeignKey(e => e.MotivationalContentId).OnDelete(DeleteBehavior.Restrict);

            // Indexes for analytics and performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.MotivationalContentId);
            entity.HasIndex(e => e.DeliveredAt);
            entity.HasIndex(e => new { e.UserId, e.DeliveredAt });
            entity.HasIndex(e => new { e.MotivationalContentId, e.DeliveredAt });
            entity.HasIndex(e => new { e.ABTestGroup, e.DeliveredAt });
            entity.HasIndex(e => new { e.EngagementType, e.EngagementAt });
            entity.HasIndex(e => new { e.DeliveryChannel, e.DeliveredAt });
        });

        // Analytics entities configurations (temporarily commented out for initial migration)
        /*
        // UserAnalytics Configuration
        modelBuilder.Entity<UserAnalytics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.AnalyticsPeriodStart).IsRequired();
            entity.Property(e => e.AnalyticsPeriodEnd).IsRequired();
            entity.Property(e => e.TotalTasksCompleted).HasDefaultValue(0);
            entity.Property(e => e.TotalTasksCreated).HasDefaultValue(0);
            entity.Property(e => e.OverallEfficiencyScore).HasPrecision(5, 4);
            entity.Property(e => e.ProductivityScore).HasPrecision(5, 4);

            // JSON columns for complex data
            entity.Property(e => e.TaskCategoryBreakdown).HasColumnType("jsonb");
            entity.Property(e => e.ProductivityPatterns).HasColumnType("jsonb");
            entity.Property(e => e.GoalProgressMetrics).HasColumnType("jsonb");
            entity.Property(e => e.EngagementMetrics).HasColumnType("jsonb");

            // Foreign key to User
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.AnalyticsPeriodStart, e.AnalyticsPeriodEnd });
            entity.HasIndex(e => e.AnalyticsPeriodStart);
        });

        // ProductivityStreak Configuration
        modelBuilder.Entity<ProductivityStreak>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.StreakLength).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.StreakType).HasMaxLength(50).HasDefaultValue("Daily");

            // JSON columns
            entity.Property(e => e.MilestoneRewards).HasColumnType("jsonb");
            entity.Property(e => e.StreakMetadata).HasColumnType("jsonb");

            // Foreign key to User
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.LastActivityDate);
        });

        // TaskMetrics Configuration
        modelBuilder.Entity<TaskMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TaskId).IsRequired();
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MetricValue).HasPrecision(10, 4);
            entity.Property(e => e.RecordedAt).IsRequired();

            // JSON column for additional data
            entity.Property(e => e.AdditionalData).HasColumnType("jsonb");

            // Foreign key relationships
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppTask>().WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => new { e.UserId, e.MetricType, e.RecordedAt });
            entity.HasIndex(e => e.RecordedAt);
        });

        // AnalyticsSnapshot Configuration
        modelBuilder.Entity<AnalyticsSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.SnapshotDate).IsRequired();
            entity.Property(e => e.SnapshotType).IsRequired().HasMaxLength(50);

            // JSON column for snapshot data
            entity.Property(e => e.SnapshotData).HasColumnType("jsonb");
            entity.Property(e => e.ComputedMetrics).HasColumnType("jsonb");

            // Foreign key to User
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.SnapshotType, e.SnapshotDate });
            entity.HasIndex(e => e.SnapshotDate);
        });
        */

        // Explicitly ignore any type named 'Task' to prevent EF auto-discovery conflicts
        try
        {
            var taskType = Type.GetType("WhoAndWhat.Domain.Entities.Task");
            if (taskType != null)
            {
                modelBuilder.Ignore(taskType);
            }
        }
        catch
        {
            // Ignore any errors - the type might not exist
        }
    }
}
