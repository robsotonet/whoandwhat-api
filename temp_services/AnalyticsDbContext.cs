using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Repositories.Analytics;

namespace WhoAndWhat.Infrastructure.Data;

/// <summary>
/// Dedicated database context for analytics operations
/// Optimized for time-series data and analytical queries
/// </summary>
public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options)
    {
    }

    // Analytics Entity Sets
    public DbSet<TaskMetrics> TaskMetrics { get; set; }
    public DbSet<ProductivityStreak> ProductivityStreaks { get; set; }
    public DbSet<UserAnalytics> UserAnalytics { get; set; }
    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TaskMetrics Configuration
        modelBuilder.Entity<TaskMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.TasksCompleted).IsRequired();
            entity.Property(e => e.TasksOverdue).IsRequired();
            entity.Property(e => e.TotalTasks).IsRequired();
            entity.Property(e => e.TasksCreated).IsRequired();
            entity.Property(e => e.ProductiveHours).HasPrecision(5, 2);
            entity.Property(e => e.EfficiencyScore).HasPrecision(5, 2);

            // JSON columns for breakdown data
            entity.Property(e => e.CategoryBreakdown).HasColumnType("jsonb");
            entity.Property(e => e.PriorityBreakdown).HasColumnType("jsonb");

            // Unique constraint on user and date
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();

            // Performance indexes for analytics queries
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.UserId, e.Date, e.TasksCompleted });
            entity.HasIndex(e => new { e.Date, e.TasksCompleted }).HasDatabaseName("IX_TaskMetrics_Global_Performance");

            // Partitioning-ready index for time-series data
            entity.HasIndex(e => new { e.Date, e.UserId }).HasDatabaseName("IX_TaskMetrics_TimeSeries");
        });

        // ProductivityStreak Configuration
        modelBuilder.Entity<ProductivityStreak>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CurrentLength).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();

            // Value object conversion for StreakType
            entity.Property(e => e.StreakType)
                .HasConversion(
                    v => v.Name,
                    v => StreakType.FromName(v))
                .IsRequired()
                .HasMaxLength(20);

            // Enum conversion for AchievementLevel
            entity.Property(e => e.AchievementLevel)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            // JSON columns for metadata
            entity.Property(e => e.Metadata).HasColumnType("jsonb");

            // Indexes for streak queries
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.StreakType });
            entity.HasIndex(e => new { e.UserId, e.CurrentLength }).HasDatabaseName("IX_ProductivityStreak_UserLength");
            entity.HasIndex(e => new { e.StreakType, e.CurrentLength, e.IsActive }).HasDatabaseName("IX_ProductivityStreak_Leaderboard");

            // Global leaderboard queries
            entity.HasIndex(e => new { e.IsActive, e.CurrentLength }).HasDatabaseName("IX_ProductivityStreak_GlobalLeaderboard");
        });

        // UserAnalytics Configuration
        modelBuilder.Entity<UserAnalytics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalTasksCompleted).IsRequired();
            entity.Property(e => e.TotalProductiveHours).HasPrecision(8, 2);
            entity.Property(e => e.AverageCompletionRate).HasPrecision(5, 2);
            entity.Property(e => e.CurrentStreak).IsRequired();
            entity.Property(e => e.LongestStreak).IsRequired();
            entity.Property(e => e.ProductiveDaysCount).IsRequired();
            entity.Property(e => e.LastActiveDate).IsRequired();

            // Enum conversions
            entity.Property(e => e.ExperienceLevel)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.PrimaryCategory)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            // JSON columns for complex data
            entity.Property(e => e.CategoryStats).HasColumnType("jsonb");
            entity.Property(e => e.MonthlyTrends).HasColumnType("jsonb");
            entity.Property(e => e.Achievements).HasColumnType("jsonb");

            // Unique constraint per user
            entity.HasIndex(e => e.UserId).IsUnique();

            // Performance indexes
            entity.HasIndex(e => e.TotalTasksCompleted).HasDatabaseName("IX_UserAnalytics_TasksCompleted");
            entity.HasIndex(e => e.CurrentStreak).HasDatabaseName("IX_UserAnalytics_CurrentStreak");
            entity.HasIndex(e => e.AverageCompletionRate).HasDatabaseName("IX_UserAnalytics_CompletionRate");
            entity.HasIndex(e => e.LastActiveDate).HasDatabaseName("IX_UserAnalytics_LastActive");
        });

        // AnalyticsSnapshot Configuration
        modelBuilder.Entity<AnalyticsSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SnapshotDate).IsRequired();

            // Enum conversion for SnapshotType
            entity.Property(e => e.SnapshotType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            // JSON columns for snapshot data
            entity.Property(e => e.MetricsData).HasColumnType("jsonb");
            entity.Property(e => e.ComparisonData).HasColumnType("jsonb");
            entity.Property(e => e.TrendData).HasColumnType("jsonb");

            // Unique constraint on user, date, and type
            entity.HasIndex(e => new { e.UserId, e.SnapshotDate, e.SnapshotType }).IsUnique();

            // Time-series indexes
            entity.HasIndex(e => new { e.UserId, e.SnapshotType, e.SnapshotDate }).HasDatabaseName("IX_AnalyticsSnapshot_UserTypeDate");
            entity.HasIndex(e => e.SnapshotDate).HasDatabaseName("IX_AnalyticsSnapshot_Date");
            entity.HasIndex(e => new { e.SnapshotType, e.SnapshotDate }).HasDatabaseName("IX_AnalyticsSnapshot_TypeDate");
        });

        // Configure table names with schema
        modelBuilder.Entity<TaskMetrics>().ToTable("TaskMetrics", "analytics");
        modelBuilder.Entity<ProductivityStreak>().ToTable("ProductivityStreaks", "analytics");
        modelBuilder.Entity<UserAnalytics>().ToTable("UserAnalytics", "analytics");
        modelBuilder.Entity<AnalyticsSnapshot>().ToTable("AnalyticsSnapshots", "analytics");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Configure for analytics workload optimization
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging(false); // Disabled for production
        }
    }
}
