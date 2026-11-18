using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Grading;

namespace Moodle.Infrastructure.Grading;

/// <summary>
/// GRD veri modeli (WBS-2 §3.5.1). crs_ tablolarına yabancı anahtar kurulmaz;
/// ders, kayıt ve ödev ilişkileri arayüzlerle doğrulanır.
/// </summary>
public sealed class GrdDbContext(
    DbContextOptions<GrdDbContext> options) : DbContext(options)
{
    private const int ScorePrecision = 10;
    private const int ScoreScale = 5;

    public DbSet<AssignmentGrade> AssignmentGrades => Set<AssignmentGrade>();
    public DbSet<GradeCategory> Categories => Set<GradeCategory>();
    public DbSet<GradeItem> Items => Set<GradeItem>();
    public DbSet<LearnerGrade> LearnerGrades => Set<LearnerGrade>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AssignmentGrade>(e =>
        {
            e.ToTable("grd_assignment_grades");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AssignmentId, x.LearnerId, x.Attempt }).IsUnique();
            e.Property(x => x.Score).HasPrecision(ScorePrecision, ScoreScale);
            e.Property(x => x.Version).IsConcurrencyToken();
        });

        model.Entity<GradeCategory>(e =>
        {
            e.ToTable("grd_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Aggregation).HasConversion<string>().HasMaxLength(30);
            // Ebeveyn aynı derste olmalı: (CourseId, ParentId) → (CourseId, Id).
            e.HasOne<GradeCategory>()
                .WithMany()
                .HasForeignKey(x => new { x.CourseId, x.ParentId })
                .HasPrincipalKey(x => new { x.CourseId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<GradeItem>(e =>
        {
            e.ToTable("grd_items", t => t.HasCheckConstraint(
                "CK_grd_items_score_range", "\"MinScore\" <= \"MaxScore\""));
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.MinScore).HasPrecision(ScorePrecision, ScoreScale);
            e.Property(x => x.MaxScore).HasPrecision(ScorePrecision, ScoreScale);
            // Bir ödev için tek ana kalem.
            e.HasIndex(x => x.AssignmentId).IsUnique()
                .HasFilter("\"AssignmentId\" IS NOT NULL");
            // Kategori aynı derste olmalı: (CourseId, CategoryId) → (CourseId, Id).
            e.HasOne<GradeCategory>()
                .WithMany()
                .HasForeignKey(x => new { x.CourseId, x.CategoryId })
                .HasPrincipalKey(x => new { x.CourseId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<LearnerGrade>(e =>
        {
            e.ToTable("grd_grades");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ItemId, x.LearnerId }).IsUnique();
            e.Property(x => x.RawScore).HasPrecision(ScorePrecision, ScoreScale);
            e.Property(x => x.FinalScore).HasPrecision(ScorePrecision, ScoreScale);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<GradeItem>()
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
