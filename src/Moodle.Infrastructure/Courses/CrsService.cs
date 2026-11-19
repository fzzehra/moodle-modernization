
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Abstractions;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;

namespace Moodle.Infrastructure.Courses;

public sealed class CrsService(CrsDbContext db)
    : ICourseDirectory, IEnrolmentQuery, IAssignmentLearnerQuery
{
    private readonly CrsQueryCache cache = new();

    private bool BypassCache => db.Database.CurrentTransaction is not null ||
        System.Transactions.Transaction.Current is not null ||
        db.GetService<IDbContextTransactionManager>() is ITransactionEnlistmentManager { EnlistedTransaction: not null };

    private async Task<CrsQueryCache.Stamp> RevisionAsync(bool enrolments, int? courseId, CancellationToken ct)
    {
        var kinds = enrolments
            ? new[] { "enrolment.created", "enrolment.updated", "enrolment.deleted" }
            : new[] { "course.created", "course.updated", "course.deleted" };
        var row = await db.Database.SqlQuery<CacheRevision>($"""
            SELECT count(*) AS "Count", COALESCE(max("Id"), 0) AS "LastId"
            FROM crs_events WHERE "Kind" = ANY ({kinds})
                AND ({courseId}::integer IS NULL OR "CourseId" = {courseId})
            """).SingleAsync(ct);
        return new(row.Count, row.LastId);
    }

    private sealed class CacheRevision
    {
        public long Count { get; set; }
        public long LastId { get; set; }
    }

    private static Course? CopyCourse(Course? x) => x is null ? null : new()
    {
        Id = x.Id, CategoryId = x.CategoryId, FullName = x.FullName, ShortName = x.ShortName,
        IdNumber = x.IdNumber, Visible = x.Visible, StartDate = x.StartDate, EndDate = x.EndDate
    };

    private static CourseEnrolment? CopyEnrolment(CourseEnrolment? x) => x is null ? null : new()
    {
        Id = x.Id, CourseId = x.CourseId, UserId = x.UserId, Suspended = x.Suspended,
        Role = x.Role, StartsAt = x.StartsAt, EndsAt = x.EndsAt
    };

    public Task<CourseCategory?> FindCategoryAsync(int id, CancellationToken ct = default) =>
        db.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Course?> FindCourseAsync(int id, CancellationToken ct = default) =>
        cache.ReadAsync($"course:{id}", BypassCache,
            () => RevisionAsync(false, id, ct),
            () => db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct), CopyCourse);

    public Task<CourseEnrolment?> FindEnrolmentAsync(int courseId, int id, CancellationToken ct = default) =>
        cache.ReadAsync($"enrolment:{courseId}:{id}", BypassCache,
            () => RevisionAsync(true, courseId, ct),
            () => db.Enrolments.AsNoTracking().SingleOrDefaultAsync(x => x.CourseId == courseId && x.Id == id, ct), CopyEnrolment);

    public async Task<CourseInfo?> FindAsync(
        int courseId, CancellationToken ct = default)
    {
        var course = await FindCourseAsync(courseId, ct);
        return course is null ? null : new CourseInfo(course.Id, course.CategoryId, course.FullName,
            course.ShortName, course.Visible, course.StartDate, course.EndDate);
    }

    public async Task<bool> IsActivelyEnrolledAsync(
        int userId, int courseId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Enrolments.AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                x.CourseId == courseId &&
                !x.Suspended &&
                (!x.StartsAt.HasValue || x.StartsAt <= now) &&
                (!x.EndsAt.HasValue || x.EndsAt > now), ct);
    }

    public async Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Enrolments.AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.CourseId == courseId &&
                !x.Suspended &&
                (!x.StartsAt.HasValue || x.StartsAt <= now) &&
                (!x.EndsAt.HasValue || x.EndsAt > now))
            .Select(x => x.Role)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(
        int courseId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Enrolments.AsNoTracking()
            .Where(x =>
                x.CourseId == courseId &&
                !x.Suspended &&
                (!x.StartsAt.HasValue || x.StartsAt <= now) &&
                (!x.EndsAt.HasValue || x.EndsAt > now))
            .Select(x => x.UserId)
            .ToListAsync(ct);
    }

    // ASG individual submissions: active Student enrolments only, never all enrolled roles.
    public async Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(int courseId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Enrolments.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.Role == "Student" && !x.Suspended
                && (!x.StartsAt.HasValue || x.StartsAt <= now)
                && (!x.EndsAt.HasValue || x.EndsAt > now))
            .Select(x => x.UserId).Distinct().OrderBy(id => id).ToListAsync(ct);
    }

    public Task<List<Course>> ListCoursesAsync(
        CancellationToken ct = default) =>
        cache.ReadAsync("courses", BypassCache,
            () => RevisionAsync(false, null, ct),
            () => db.Courses.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct),
            list => list.Select(x => CopyCourse(x)!).ToList());

    public Task<List<CourseCategory>> ListCategoriesAsync(
        CancellationToken ct = default) =>
        db.Categories.AsNoTracking()
            .OrderBy(x => x.Name).ToListAsync(ct);

    public Task<List<CourseEnrolment>> ListEnrolmentsAsync(
        int courseId, CancellationToken ct = default) =>
        cache.ReadAsync($"enrolments:{courseId}", BypassCache,
            () => RevisionAsync(true, courseId, ct),
            () => db.Enrolments.AsNoTracking().Where(x => x.CourseId == courseId).ToListAsync(ct),
            list => list.Select(x => CopyEnrolment(x)!).ToList());

    public async Task<CourseCategory> SaveCategoryAsync(
        CourseCategory category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(category.Name) || category.Name.Length > 255 ||
            category.Name.Any(char.IsControl))
            throw new ArgumentException("Geçersiz kategori adı.");

        if (category.ParentId == category.Id && category.Id != 0)
            throw new ArgumentException(
                "Kategori kendisinin üst kategorisi olamaz.");

        if (category.ParentId.HasValue &&
            !await db.Categories.AnyAsync(
                x => x.Id == category.ParentId.Value, ct))
            throw new ArgumentException("Üst kategori bulunamadı.");

        var parentId = category.ParentId;
        var visited = new HashSet<int>();
        while (parentId.HasValue)
        {
            if (parentId == category.Id || !visited.Add(parentId.Value))
                throw new ArgumentException("Kategori hiyerarşisi döngü içeremez.");
            parentId = await db.Categories.Where(x => x.Id == parentId.Value)
                .Select(x => x.ParentId).SingleAsync(ct);
        }

        if (category.Id == 0)
            db.Categories.Add(category);
        else
        {
            var current = await db.Categories.FindAsync(
                [category.Id], ct)
                ?? throw new KeyNotFoundException(
                    "Kategori bulunamadı.");

            current.Name = category.Name;
            current.ParentId = category.ParentId;
            category = current;
        }

        await db.SaveChangesAsync(ct);
        return category;
    }

    public async Task DeleteCategoryAsync(
        int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Kategori bulunamadı.");

        if (await db.Courses.AnyAsync(
                x => x.CategoryId == id, ct) ||
            await db.Categories.AnyAsync(
                x => x.ParentId == id, ct))
        {
            // Preserve the existing service exception contract for callers.
            var conflict = new CrsConflictException("category_not_empty", "Kategori boş değil.");
            throw new InvalidOperationException(conflict.Message, conflict);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    // YZ destekli: ders oluşturma ve güncelleme.
    public async Task<Course> SaveCourseAsync(
        Course course, CancellationToken ct = default)
    {
        CourseValidation.Validate(
            course.FullName, course.ShortName, course.IdNumber,
            course.StartDate, course.EndDate);

        course.IdNumber = string.IsNullOrWhiteSpace(course.IdNumber) ? null : course.IdNumber;
        course.StartDate = course.StartDate?.ToUniversalTime();
        course.EndDate = course.EndDate?.ToUniversalTime();

        if (!await db.Categories.AnyAsync(
            x => x.Id == course.CategoryId, ct))
            throw new ArgumentException("Kategori bulunamadı.");

        if (await db.Courses.AnyAsync(
            x => x.Id != course.Id &&
                 x.ShortName == course.ShortName, ct))
            throw new CrsConflictException("course_short_name_conflict", "Kısa ad benzersiz olmalıdır.");

        if (!string.IsNullOrWhiteSpace(course.IdNumber) &&
            await db.Courses.AnyAsync(
                x => x.Id != course.Id &&
                     x.IdNumber == course.IdNumber, ct))
            throw new CrsConflictException("course_id_number_conflict", "Kimlik numarası benzersiz olmalıdır.");

        if (course.Id == 0)
            db.Courses.Add(course);
        else
        {
            var current = await db.Courses.FindAsync(
                [course.Id], ct)
                ?? throw new KeyNotFoundException("Ders bulunamadı.");

            current.CategoryId = course.CategoryId;
            current.FullName = course.FullName;
            current.ShortName = course.ShortName;
            current.IdNumber = course.IdNumber;
            current.Visible = course.Visible;
            current.StartDate = course.StartDate;
            current.EndDate = course.EndDate;
            course = current;
        }

        await db.SaveChangesAsync(ct);
        return course;
    }

    public async Task DeleteCourseAsync(
        int id, CancellationToken ct = default)
    {
        var course = await db.Courses.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        db.Courses.Remove(course);
        await db.SaveChangesAsync(ct);
    }

    // YZ taslağıdır; elle geliştirilmiş olarak raporlanmamalıdır.
    public async Task<CourseEnrolment> EnrolAsync(
        CourseEnrolment enrolment, CancellationToken ct = default)
    {
        CourseValidation.ValidateRole(enrolment.Role);
        enrolment.StartsAt = enrolment.StartsAt?.ToUniversalTime();
        enrolment.EndsAt = enrolment.EndsAt?.ToUniversalTime();
        if (enrolment.UserId <= 0)
            throw new ArgumentException("Geçersiz kullanıcı.");

        if (!await db.Courses.AnyAsync(
            x => x.Id == enrolment.CourseId, ct))
            throw new ArgumentException("Ders bulunamadı.");

        if (enrolment.StartsAt.HasValue &&
            enrolment.EndsAt.HasValue &&
            enrolment.EndsAt <= enrolment.StartsAt)
            throw new ArgumentException("Kayıt tarihleri geçersiz.");

        if (await db.Enrolments.AnyAsync(
            x => x.CourseId == enrolment.CourseId &&
                 x.UserId == enrolment.UserId, ct))
            throw new CrsConflictException("enrolment_conflict", "Kullanıcı zaten kayıtlı.");

        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync(ct);
        return enrolment;
    }

    public async Task UpdateEnrolmentAsync(
        int id, bool suspended, string role,
        CancellationToken ct = default)
    {
        var enrolment = await db.Enrolments.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

        CourseValidation.ValidateRole(role);

        enrolment.Suspended = suspended;
        enrolment.Role = role;
        await db.SaveChangesAsync(ct);
    }

    public async Task UnenrolAsync(
        int id, CancellationToken ct = default)
    {
        var enrolment = await db.Enrolments.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

        db.Enrolments.Remove(enrolment);
        await db.SaveChangesAsync(ct);
    }
}
