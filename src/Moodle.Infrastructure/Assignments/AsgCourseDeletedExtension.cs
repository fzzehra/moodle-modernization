using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Courses;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Files;
using Moodle.Infrastructure.Persistence;
using Npgsql;

namespace Moodle.Infrastructure.Assignments;

/// <summary>ASG-owned post-commit cleanup; retry-safe local transaction, no physical file deletion.</summary>
public sealed class AsgCourseDeletedExtension(MoodleDbContext configuredAsg, FilesDbContext configuredFiles, TimeProvider clock) : ICrsEventExtension
{
    public string Id => "asg.course-deleted.v1";
    public int Order => 100;

    public async Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default)
    {
        if (notification.Kind != "course.deleted") return;
        if (notification.Version != 1 || notification.CourseId <= 0 || notification.Id <= 0)
            throw new InvalidOperationException("Unsupported CRS deletion event.");
        if (configuredAsg.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("ASG event cleanup requires its own context and transaction.");

        await using var db = new MoodleDbContext(new DbContextOptionsBuilder<MoodleDbContext>()
            .UseNpgsql(configuredAsg.Database.GetConnectionString()).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AsgCourseWriteFence.LockAsync(db, notification.CourseId, ct);
        if (await db.DeletedCourses.AnyAsync(x => x.CourseId == notification.CourseId, ct))
        {
            await transaction.CommitAsync(ct);
            return;
        }
        var assignments = await db.Assignments.Where(a => a.CourseId == notification.CourseId)
            .Select(a => a.Id).ToArrayAsync(ct);
        if (assignments.Length > 0)
        {
            var asg = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            var files = new NpgsqlConnectionStringBuilder(configuredFiles.Database.GetConnectionString());
            if (asg.Host != files.Host || asg.Port != files.Port || asg.Database != files.Database || asg.Username != files.Username)
                throw new InvalidOperationException("ASG cleanup and FILE references must share the same database.");

            await using var fileDb = new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options);
            await fileDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
            var entries = fileDb.Entries.Where(e => e.Component == "mod_assign" && assignments.Contains(e.ContextId));
            var hashes = await entries.Select(e => e.ContentHash).Distinct().OrderBy(x => x).ToArrayAsync(ct);
            foreach (var hash in hashes)
            {
                var key = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(hash));
                await fileDb.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
            }
            var now = clock.GetUtcNow();
            await entries.Where(e => e.DeletedAt == null).ExecuteUpdateAsync(s => s.SetProperty(e => e.DeletedAt, now), ct);
            await fileDb.Blobs.Where(b => hashes.Contains(b.ContentHash) && b.OrphanedAt == null
                    && !fileDb.Entries.Any(e => e.ContentHash == b.ContentHash && e.DeletedAt == null))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.OrphanedAt, now), ct);
            await db.SubmissionRules.Where(x => assignments.Contains(x.AssignmentId)).ExecuteDeleteAsync(ct);
            await db.Submissions.Where(x => assignments.Contains(x.AssignmentId)).ExecuteDeleteAsync(ct);
            await db.Assignments.Where(x => x.CourseId == notification.CourseId).ExecuteDeleteAsync(ct);
        }
        db.DeletedCourses.Add(new AsgDeletedCourse { CourseId = notification.CourseId, EventId = notification.Id, DeletedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}