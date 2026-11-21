using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Domain.Courses;

namespace Moodle.Infrastructure.Courses;

public sealed class CourseSectionService(CrsDbContext db) : ICourseSectionQuery
{
    public Task<bool> BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct = default) =>
        db.Sections.AsNoTracking().AnyAsync(x => x.Id == sectionId && x.CourseId == courseId, ct);

    public async Task<Course> RequireCourseAsync(int courseId, CancellationToken ct) =>
        await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct) ?? throw AppException.NotFound();

    public async Task<IReadOnlyList<CourseSection>> ListAsync(int courseId, CancellationToken ct) =>
        await db.Sections.AsNoTracking().Where(x => x.CourseId == courseId).OrderBy(x => x.Id).ToArrayAsync(ct);

    public async Task<CourseSection> CreateAsync(int courseId, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 255)
            throw AppException.Validation("invalid_section_name", "Bölüm adı 1–255 karakter olmalı.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize section creation with real CRS deletion; never create a section under a vanished course.
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM crs_courses WHERE \"Id\"={courseId} FOR UPDATE", ct);
        await RequireCourseAsync(courseId, ct);
        var section = new CourseSection { CourseId = courseId, Name = name.Trim() };
        db.Sections.Add(section);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return section;
    }
}
