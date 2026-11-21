using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Courses;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;
using Moodle.Domain.Courses;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Assignments;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Files;
using Moodle.Infrastructure.Persistence;
using Npgsql;
using Moodle.Infrastructure.Grading;
using Moodle.Domain.Grading;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public sealed class AsgCleanupPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "asg-cleanup-" + Guid.NewGuid().ToString("N"));
    public MoodleDbContext Asg() => new(new DbContextOptionsBuilder<MoodleDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    public FilesDbContext Files() => new(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    public CrsDbContext Crs() => new(new DbContextOptionsBuilder<CrsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    public GrdDbContext Grd() => new(new DbContextOptionsBuilder<GrdDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    public LocalDiskFileStore Store => new(new(Root));
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var asg = Asg(); await using var files = Files(); await using var crs = Crs();
        await using var grd = Grd(); await grd.Database.MigrateAsync();
        await asg.Database.MigrateAsync(); await files.Database.MigrateAsync(); await crs.Database.MigrateAsync();
    }
    public async Task DisposeAsync() { await postgres.DisposeAsync(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}

public sealed class AsgCourseDeletionTests(AsgCleanupPostgresFixture fixture) : IClassFixture<AsgCleanupPostgresFixture>
{
    private sealed record Data(int Course, int Assignment, int Submission, long File, string Hash, byte[] Bytes, CrsChangeEvent Event);
    private async Task<Data> Seed(bool shared = false)
    {
        await using var crs = fixture.Crs();
        var category = new CourseCategory { Name = "Cleanup " + Guid.NewGuid() };
        crs.Categories.Add(category); await crs.SaveChangesAsync();
        var course = new Course { CategoryId = category.Id, FullName = "Cleanup", ShortName = Guid.NewGuid().ToString("N") };
        crs.Courses.Add(course); await crs.SaveChangesAsync();
        await using var asg = fixture.Asg();
        var a = new Assignment { CourseId = course.Id, SectionId = 1, Title = "Existing integration fixture" };
        await new AssignmentStore(asg).AddAsync(a, default);
        var submission = new Submission { AssignmentId = a.Id, LearnerId = 1, Text = "fixture", FileCount = 1, Version = 1, UpdatedAt = DateTimeOffset.UtcNow };
        asg.Submissions.Add(submission); asg.SubmissionRules.Add(new() { AssignmentId = a.Id, LearnerId = 1, Locked = true });
        await asg.SaveChangesAsync();
        var bytes = Encoding.UTF8.GetBytes("cleanup file " + Guid.NewGuid());
        var blob = await fixture.Store.WriteAsync(new MemoryStream(bytes));
        await using var files = fixture.Files();
        files.Blobs.Add(new FileBlob { ContentHash = blob.ContentHash, SizeBytes = blob.SizeBytes, StorageKey = blob.StorageKey, CreatedAt = DateTimeOffset.UtcNow });
        var entry = new FileEntry { ContentHash = blob.ContentHash, ContextId = a.Id, Component = "mod_assign", Area = "submission",
            ItemId = submission.Id, Name = "cleanup.txt", MimeType = "text/plain", OwnerUserId = 1, SizeBytes = bytes.Length,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        files.Entries.Add(entry);
        if (shared) files.Entries.Add(new FileEntry { ContentHash = blob.ContentHash, ContextId = 1, Component = "user", Area = "private",
            ItemId = course.Id, Name = "shared.txt", MimeType = "text/plain", OwnerUserId = 1, SizeBytes = bytes.Length,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await files.SaveChangesAsync();
        await using var grd = fixture.Grd();
        var categoryGrade = new GradeCategory { CourseId = course.Id, Name = "Parent" };
        grd.Categories.Add(categoryGrade); await grd.SaveChangesAsync();
        var child = new GradeCategory { CourseId = course.Id, ParentId = categoryGrade.Id, Name = "Child" };
        grd.Categories.Add(child); await grd.SaveChangesAsync();
        var gradeItem = new GradeItem { CourseId = course.Id, AssignmentId = a.Id, CategoryId = child.Id, Name = "Assignment" };
        grd.Items.Add(gradeItem); await grd.SaveChangesAsync();
        grd.AssignmentGrades.Add(new AssignmentGrade { AssignmentId = a.Id, LearnerId = 1, Score = 80 });
        grd.LearnerGrades.Add(new LearnerGrade { ItemId = gradeItem.Id, LearnerId = 1, FinalScore = 80 });
        await grd.SaveChangesAsync();
        await new CrsService(crs).DeleteCourseAsync(course.Id);
        var eventId = await crs.Database.SqlQuery<long>($"""
            SELECT "Id" AS "Value" FROM crs_events WHERE "CourseId" = {course.Id} AND "Kind" = 'course.deleted'
            """).SingleAsync();
        return new(course.Id, a.Id, submission.Id, entry.Id, blob.ContentHash, bytes,
            new(eventId, "course.deleted", course.Id, null, null, DateTimeOffset.UtcNow, 1, []));
    }
    private async Task Handle(CrsChangeEvent e, CancellationToken ct = default)
    {
        await using var asg = fixture.Asg(); await using var files = fixture.Files();
        await new AsgCourseDeletedExtension(asg, files, TimeProvider.System).HandleAsync(e, ct);
    }
    private async Task<int> Dispatch()
    {
        await using var crs = fixture.Crs(); await using var asg = fixture.Asg(); await using var files = fixture.Files();
        await using var grd = fixture.Grd();
        return await new CrsEventDispatcher(crs, new([new GrdCourseDeletedExtension(grd, asg), new AsgCourseDeletedExtension(asg, files, TimeProvider.System)])).DispatchAsync(1000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_event_cleans_asg_atomically_and_obeys_shared_file_retention(bool shared)
    {
        var data = await Seed(shared);
        await Dispatch();
        await using var asg = fixture.Asg(); await using var files = fixture.Files(); await using var crs = fixture.Crs();
        Assert.False(await asg.Assignments.AnyAsync(x => x.Id == data.Assignment));
        Assert.False(await asg.Submissions.AnyAsync(x => x.Id == data.Submission));
        Assert.False(await asg.SubmissionRules.AnyAsync(x => x.AssignmentId == data.Assignment));
        Assert.True(await asg.DeletedCourses.AnyAsync(x => x.CourseId == data.Course && x.EventId == data.Event.Id));
        await using var grd = fixture.Grd();
        Assert.False(await grd.AssignmentGrades.AnyAsync(x => x.AssignmentId == data.Assignment));
        Assert.False(await grd.Items.AnyAsync(x => x.CourseId == data.Course));
        Assert.False(await grd.Categories.AnyAsync(x => x.CourseId == data.Course));
        var deletedAt = (await files.Entries.SingleAsync(x => x.Id == data.File)).DeletedAt;
        Assert.NotNull(deletedAt); Assert.True(await fixture.Store.ExistsAsync(data.Hash));
        var blob = await files.Blobs.SingleAsync(x => x.ContentHash == data.Hash);
        Assert.Equal(shared, blob.OrphanedAt is null);
        await Handle(data.Event); await Handle(data.Event with { Id = data.Event.Id + 1000000 });
        Assert.Equal(0, await Dispatch());
        files.ChangeTracker.Clear();
        Assert.Equal(deletedAt, (await files.Entries.SingleAsync(x => x.Id == data.File)).DeletedAt);
        var receipts = await crs.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM crs_event_receipts
            WHERE "EventId" = {data.Event.Id} AND "ExtensionId" = 'asg.course-deleted.v1'
            """).SingleAsync();
        Assert.Equal(1, receipts);
        await files.Settings.ExecuteUpdateAsync(s => s.SetProperty(x => x.TrashRetentionHours, 0));
        await new FileMaintenanceService(files, fixture.Store, TimeProvider.System, NullLogger<FileMaintenanceService>.Instance).RunAsync();
        Assert.Equal(shared, await fixture.Store.ExistsAsync(data.Hash));
        if (shared)
        {
            await using var stream = await fixture.Store.OpenReadAsync(data.Hash);
            using var bytes = new MemoryStream(); await stream!.CopyToAsync(bytes);
            Assert.Equal(data.Bytes, bytes.ToArray());
        }
    }

    [Fact]
    public async Task Database_failure_rolls_back_files_rules_submissions_and_receipt_then_retries()
    {
        var data = await Seed();
        await using var setup = fixture.Asg();
        await setup.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION asg_test_fail_delete() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected cleanup failure'; END; $$;
            CREATE TRIGGER asg_test_fail_delete BEFORE DELETE ON "Assignments"
            FOR EACH ROW EXECUTE FUNCTION asg_test_fail_delete();
            """);
        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => Dispatch());
            Assert.Equal("P0001", ex.SqlState);
            await using var files = fixture.Files(); await using var crs = fixture.Crs();
            Assert.True(await setup.Assignments.AnyAsync(x => x.Id == data.Assignment));
            Assert.True(await setup.Submissions.AnyAsync(x => x.Id == data.Submission));
            Assert.True(await setup.SubmissionRules.AnyAsync(x => x.AssignmentId == data.Assignment));
            Assert.False(await setup.DeletedCourses.AnyAsync(x => x.CourseId == data.Course));
            Assert.Null((await files.Entries.SingleAsync(x => x.Id == data.File)).DeletedAt);
            Assert.Null((await files.Blobs.SingleAsync(x => x.ContentHash == data.Hash)).OrphanedAt);
            Assert.Equal(0, await crs.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM crs_event_receipts
                WHERE "EventId" = {data.Event.Id} AND "ExtensionId" = 'asg.course-deleted.v1'
                """).SingleAsync());
        }
        finally { await setup.Database.ExecuteSqlRawAsync("""DROP TRIGGER asg_test_fail_delete ON "Assignments"; DROP FUNCTION asg_test_fail_delete();"""); }
        await Dispatch();
        Assert.False(await setup.Assignments.AnyAsync(x => x.Id == data.Assignment));
    }

    [Fact]
    public async Task Concurrent_delivery_is_idempotent_and_delayed_creates_are_rejected()
    {
        var data = await Seed();
        await Task.WhenAll(Handle(data.Event), Handle(data.Event));
        await using var asg = fixture.Asg();
        Assert.Single(await asg.DeletedCourses.Where(x => x.CourseId == data.Course).ToListAsync());
        var ex = await Assert.ThrowsAsync<AppException>(() => new AssignmentStore(asg).AddAsync(
            new Assignment { CourseId = data.Course, SectionId = 1, Title = "late write" }, default));
        Assert.Equal(404, ex.StatusCode);
        Assert.False(await asg.Assignments.AnyAsync(x => x.CourseId == data.Course));
    }

    [Fact]
    public async Task Cleanup_waits_for_inflight_submission_then_removes_it()
    {
        var data = await Seed();
        await using var writerDb = fixture.Asg();
        var store = new AssignmentStore(writerDb);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = store.WriteAsync(data.Assignment, async () =>
        {
            entered.SetResult(); await release.Task;
            store.AddSubmission(new Submission { AssignmentId = data.Assignment, LearnerId = 2, Text = "in flight", Version = 1, UpdatedAt = DateTimeOffset.UtcNow });
            return true;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var deleting = Handle(data.Event);
        try
        {
            await using var observer = fixture.Asg();
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var count = await observer.Database.SqlQuery<int>($"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE 'SELECT pg_advisory_xact_lock%'
                    """).SingleAsync();
                if (count > 0) break;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Cleanup did not wait for ASG course fence.");
                await Task.Delay(25);
            }
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(writing, deleting).WaitAsync(TimeSpan.FromSeconds(30));
        await using var check = fixture.Asg();
        Assert.False(await check.Submissions.AnyAsync(x => x.AssignmentId == data.Assignment));
    }

    [Fact]
    public async Task Cancellation_leaves_state_intact_and_can_retry()
    {
        var data = await Seed();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handle(data.Event, cancellation.Token));
        await using var asg = fixture.Asg();
        Assert.True(await asg.Assignments.AnyAsync(x => x.Id == data.Assignment));
        Assert.False(await asg.DeletedCourses.AnyAsync(x => x.CourseId == data.Course));
        await Handle(data.Event);
        Assert.False(await asg.Assignments.AnyAsync(x => x.Id == data.Assignment));
    }

    [Fact]
    public async Task Unsupported_version_rejected_and_other_event_ignored()
    {
        var data = await Seed();
        await Handle(data.Event with { Kind = "course.updated" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handle(data.Event with { Version = 2 }));
        await using var asg = fixture.Asg();
        Assert.True(await asg.Assignments.AnyAsync(x => x.Id == data.Assignment));
        await Handle(data.Event);
    }
    [Fact]
    public async Task Grd_failure_rolls_back_then_retry_removes_grades_and_preserves_other_courses()
    {
        var data = await Seed();
        var other = await Seed();
        await using var grd = fixture.Grd();
        await using var asg = fixture.Asg();
        await grd.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION grd_test_fail_delete() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected GRD failure'; END; $$;
            CREATE TRIGGER grd_test_fail_delete BEFORE DELETE ON grd_items FOR EACH ROW EXECUTE FUNCTION grd_test_fail_delete();
            """);
        var extension = new GrdCourseDeletedExtension(grd, asg);
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => extension.HandleAsync(data.Event));
            Assert.True(await grd.AssignmentGrades.AnyAsync(x => x.AssignmentId == data.Assignment));
            Assert.True(await grd.Items.AnyAsync(x => x.CourseId == data.Course));
        }
        finally
        {
            await grd.Database.ExecuteSqlRawAsync("DROP TRIGGER grd_test_fail_delete ON grd_items; DROP FUNCTION grd_test_fail_delete();");
        }
        await extension.HandleAsync(data.Event);
        await extension.HandleAsync(data.Event);
        Assert.False(await grd.AssignmentGrades.AnyAsync(x => x.AssignmentId == data.Assignment));
        Assert.False(await grd.Items.AnyAsync(x => x.CourseId == data.Course));
        Assert.True(await grd.AssignmentGrades.AnyAsync(x => x.AssignmentId == other.Assignment));
        Assert.True(await grd.Items.AnyAsync(x => x.CourseId == other.Course));
    }}