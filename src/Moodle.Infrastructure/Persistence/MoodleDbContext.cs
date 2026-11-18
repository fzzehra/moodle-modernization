using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Assignments;
namespace Moodle.Infrastructure.Persistence;

/// <summary>ASG schema; other modules add their mappings when implemented.</summary>
public sealed class MoodleDbContext(DbContextOptions<MoodleDbContext> options) : DbContext(options)
{
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionRule> SubmissionRules => Set<SubmissionRule>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var a = model.Entity<Assignment>();
        a.ToTable("Assignments", t => {
            t.HasCheckConstraint("CK_Assignment_Dates", """("OpensAt" IS NULL OR "DueAt" IS NULL OR "OpensAt" <= "DueAt") AND ("DueAt" IS NULL OR "CutoffAt" IS NULL OR "DueAt" <= "CutoffAt") AND ("OpensAt" IS NULL OR "CutoffAt" IS NULL OR "OpensAt" <= "CutoffAt")""");
            t.HasCheckConstraint("CK_Assignment_Title", """length(btrim("Title")) > 0""");
            t.HasCheckConstraint("CK_Assignment_References", """("CourseId" > 0 AND "SectionId" > 0 AND "Version" > 0)""");
        });
        a.HasKey(x => x.Id);
        a.Property(x => x.Version).IsConcurrencyToken();
        a.HasIndex(x => new { x.CourseId, x.SectionId });
        var s = model.Entity<Submission>();
        s.ToTable("Submissions", t => {
            t.HasCheckConstraint("CK_Submission_State", """("Status" = 'draft' AND "SubmittedAt" IS NULL) OR ("Status" = 'submitted' AND "SubmittedAt" IS NOT NULL)""");
            t.HasCheckConstraint("CK_Submission_Values", """("Attempt" = 0 AND "LearnerId" > 0 AND "Version" > 0 AND length(btrim("Text")) > 0)""");
        });
        s.HasKey(x => x.Id);
        s.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        s.HasIndex(x => new { x.AssignmentId, x.LearnerId, x.Attempt }).IsUnique();
        s.Property(x => x.Version).IsConcurrencyToken();
        var r = model.Entity<SubmissionRule>();
        r.ToTable("SubmissionRules", t => t.HasCheckConstraint("CK_Rule_Learner", """("LearnerId" > 0)"""));
        r.HasKey(x => new { x.AssignmentId, x.LearnerId });
        r.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
