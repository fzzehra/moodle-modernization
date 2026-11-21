
using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Courses;

namespace Moodle.Infrastructure.Courses;

public sealed class CrsDbContext(
    DbContextOptions<CrsDbContext> options) : DbContext(options)
{
    public DbSet<CourseCategory> Categories => Set<CourseCategory>();
    public DbSet<CourseSection> Sections => Set<CourseSection>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseEnrolment> Enrolments => Set<CourseEnrolment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CourseCategory>(e =>
        {
            e.ToTable("crs_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.HasOne<CourseCategory>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Course>(e =>
        {
            e.ToTable("crs_courses");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ShortName).HasMaxLength(100).IsRequired();
            e.Property(x => x.IdNumber).HasMaxLength(100);
            e.HasIndex(x => x.ShortName).IsUnique();
            e.HasIndex(x => x.IdNumber).IsUnique();
            e.HasOne<CourseCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<CourseSection>(e =>
        {
            e.ToTable("crs_sections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.CourseId);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<CourseEnrolment>(e =>
        {
            e.ToTable("crs_enrolments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CourseId, x.UserId }).IsUnique();
            e.HasOne<Course>()
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
        });
    }
}
