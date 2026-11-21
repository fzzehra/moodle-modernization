using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Infrastructure.Persistence;

namespace Moodle.Infrastructure.Grading;

/// <summary>Runs before ASG deletes assignment identities; local transaction is safe to replay.</summary>
public sealed class GrdCourseDeletedExtension(GrdDbContext configured, MoodleDbContext configuredAsg) : ICrsEventExtension
{
    public string Id => "grd.course-deleted.v1";
    public int Order => 50;
    public async Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default)
    {
        if (notification.Kind != "course.deleted") return;
        if (notification.Version != 1 || notification.CourseId <= 0 || notification.Id <= 0)
            throw new InvalidOperationException("Unsupported CRS deletion event.");
        await using var db = new GrdDbContext(new DbContextOptionsBuilder<GrdDbContext>()
            .UseNpgsql(configured.Database.GetConnectionString()).Options);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({0x41534743},{notification.CourseId})", ct);
        await using var asg = new MoodleDbContext(new DbContextOptionsBuilder<MoodleDbContext>()
            .UseNpgsql(configuredAsg.Database.GetConnectionString()).Options);
        var ids = await asg.Assignments.Where(x => x.CourseId == notification.CourseId).Select(x => x.Id).ToArrayAsync(ct);
        var itemAssignments = await db.Items.Where(x => x.CourseId == notification.CourseId && x.AssignmentId != null)
            .Select(x => x.AssignmentId!.Value).ToArrayAsync(ct);
        var assignments = ids.Concat(itemAssignments).Distinct().ToArray();
        await db.AssignmentGrades.Where(x => assignments.Contains(x.AssignmentId)).ExecuteDeleteAsync(ct);
        await db.Items.Where(x => x.CourseId == notification.CourseId).ExecuteDeleteAsync(ct);
        await db.Categories.Where(x => x.CourseId == notification.CourseId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentId, (int?)null), ct);
        await db.Categories.Where(x => x.CourseId == notification.CourseId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }
}
