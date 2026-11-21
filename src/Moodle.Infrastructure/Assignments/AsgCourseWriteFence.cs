using Microsoft.EntityFrameworkCore;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Persistence;

namespace Moodle.Infrastructure.Assignments;

/// <summary>ASG-owned lock namespace; all ASG writes and course deletion acquire course before assignment locks.</summary>
internal static class AsgCourseWriteFence
{
    private const int Namespace = 0x41534743;

    public static Task LockAsync(MoodleDbContext db, int courseId, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Namespace}, {courseId})", ct);

    public static async Task RequireLiveAsync(MoodleDbContext db, int courseId, CancellationToken ct)
    {
        await LockAsync(db, courseId, ct);
        if (await db.DeletedCourses.AnyAsync(x => x.CourseId == courseId, ct))
            throw AppException.NotFound();
    }
}