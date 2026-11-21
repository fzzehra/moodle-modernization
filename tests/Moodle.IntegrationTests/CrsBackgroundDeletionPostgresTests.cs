using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using Npgsql;

namespace Moodle.IntegrationTests;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 kuyruk ve işleyici PostgreSQL testleri.
public sealed class CrsBackgroundDeletionPostgresTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    private static readonly CrsBackgroundDeletionOptions Enabled = new(true, TimeSpan.FromMilliseconds(50), 3);

    private static async Task<Course> CreateAsync(CrsDbContext db)
    {
        var service = new CrsService(db);
        var category = await service.SaveCategoryAsync(new() { Name = "Background deletion" });
        var course = await service.SaveCourseAsync(new()
            { CategoryId = category.Id, FullName = "Queued course", ShortName = Guid.NewGuid().ToString("N") });
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        return course;
    }

    private static CrsCourseDeletionProcessor Processor(CrsDbContext db, CrsBackgroundDeletionOptions? options = null) =>
        new(db, new CrsService(db), options ?? Enabled, NullLogger<CrsCourseDeletionProcessor>.Instance);

    // Processes queued jobs until the given job leaves Pending (other tests' leftovers are processed too).
    private async Task<CrsCourseDeletionJob> ProcessUntilSettledAsync(Guid jobId, CrsBackgroundDeletionOptions? options = null)
    {
        while (true)
        {
            await using var db = fixture.CreateContext();
            var job = (await new CrsCourseDeletionJobs(db).FindAsync(jobId))!;
            if (job.State is not ("Pending" or "Running")) return job;
            Assert.True(await Processor(db, options).ProcessNextAsync(), "Pending job was not claimed.");
        }
    }

    internal static async Task<string[]> EventsAsync(CrsDbContext db, int courseId) =>
        (await db.Database.SqlQuery<string>($"""
            SELECT "Kind" AS "Value" FROM crs_events WHERE "CourseId" = {courseId} ORDER BY "Id"
            """).ToListAsync()).ToArray();

    [Fact]
    public async Task CRS_FR_11_Enqueue_persists_pending_job_and_returns_existing_active_job_for_same_course()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var jobs = new CrsCourseDeletionJobs(db);
        var first = await jobs.EnqueueAsync(course.Id, 42);
        var second = await jobs.EnqueueAsync(course.Id, 99);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Pending", first.State);
        Assert.Equal(42, second.RequestedByUserId);
        Assert.Equal(course.CategoryId, first.CategoryId);
        Assert.Equal(0, first.Attempts);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id)); // Not deleted synchronously.

        // Partial unique index is the database backstop for one active job per course.
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crs_course_deletion_jobs ("Id", "CourseId", "State") VALUES ({Guid.NewGuid()}, {course.Id}, 2)
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

        var done = await ProcessUntilSettledAsync(first.Id);
        Assert.Equal("Succeeded", done.State);
        // Course is gone: a new request is rejected as not found, never deduplicated onto the finished job.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => jobs.EnqueueAsync(course.Id, 42));
    }

    [Fact]
    public async Task CRS_FR_11_Enqueue_of_missing_course_is_rejected_without_job()
    {
        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CrsCourseDeletionJobs(db).EnqueueAsync(int.MaxValue, 42));
        Assert.Empty(await db.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM crs_course_deletion_jobs WHERE "CourseId" = {int.MaxValue}
            """).ToListAsync());
    }

    [Fact]
    public async Task CRS_NFR_06_Worker_processing_deletes_course_with_cascade_events_and_cache_refresh()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        await using var reader = fixture.CreateContext();
        var cached = new CrsService(reader);
        Assert.NotNull(await cached.FindCourseAsync(course.Id));
        Assert.Single(await cached.ListEnrolmentsAsync(course.Id));

        var job = await new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42);
        var done = await ProcessUntilSettledAsync(job.Id);

        Assert.Equal("Succeeded", done.State);
        Assert.Equal(1, done.Attempts);
        Assert.NotNull(done.StartedAt);
        Assert.NotNull(done.CompletedAt);
        Assert.Null(done.ErrorCode);
        Assert.Equal(course.CategoryId, done.CategoryId);
        Assert.Null(await cached.FindCourseAsync(course.Id));
        Assert.Empty(await cached.ListEnrolmentsAsync(course.Id));
        var events = await EventsAsync(reader, course.Id);
        Assert.Single(events, x => x == "course.deleted");
        Assert.Single(events, x => x == "enrolment.deleted");
    }

    [Fact]
    public async Task CRS_NFR_03_Enqueue_precheck_rejects_course_with_registered_operation()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        var conflict = await Assert.ThrowsAsync<CrsConflictException>(() =>
            new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42));
        Assert.Equal("course_operation_in_progress", conflict.Code);
        Assert.Empty(await db.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM crs_course_deletion_jobs WHERE "CourseId" = {course.Id}
            """).ToListAsync());
        await registry.ReleaseAsync(course.Id, token);
    }

    [Fact]
    public async Task CRS_NFR_03_Operation_registered_after_enqueue_fails_job_permanently_and_rolls_back_deletion()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var job = await new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Restore);
        var before = await EventsAsync(db, course.Id);

        await using var worker = fixture.CreateContext();
        var failed = await ProcessUntilSettledAsync(job.Id);

        Assert.Equal("Failed", failed.State);
        Assert.Equal("course_operation_in_progress", failed.ErrorCode);
        Assert.Equal(1, failed.Attempts); // Permanent: no retry.
        Assert.NotNull(failed.CompletedAt);
        // Deletion transaction rolled back: course, enrolment and events unchanged.
        await using var verification = fixture.CreateContext();
        Assert.True(await verification.Courses.AnyAsync(x => x.Id == course.Id));
        Assert.Single(await verification.Enrolments.Where(x => x.CourseId == course.Id).ToListAsync());
        Assert.Equal(before, await EventsAsync(verification, course.Id));
        await Processor(worker).ProcessNextAsync(); // A failed job is never claimed again.
        Assert.Equal("Failed", (await new CrsCourseDeletionJobs(verification).FindAsync(job.Id))!.State);
        await registry.ReleaseAsync(course.Id, token);
    }

    [Fact]
    public async Task CRS_FR_11_Failure_is_recorded_in_separate_committed_transaction_after_rollback()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var job = await new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42);
        await new CrsService(db).DeleteCourseAsync(course.Id); // Deleted synchronously after enqueue.

        await using var worker = fixture.CreateContext();
        var processor = Processor(worker);
        CrsCourseDeletionJob current;
        do
        {
            Assert.True(await processor.ProcessNextAsync());
            current = (await new CrsCourseDeletionJobs(worker).FindAsync(job.Id))!;
        } while (current.State == "Pending");

        Assert.Null(worker.Database.CurrentTransaction); // Nothing left open on the worker context.
        Assert.Empty(worker.ChangeTracker.Entries());
        await using var observer = fixture.CreateContext(); // Another connection sees the committed failure.
        var failed = (await new CrsCourseDeletionJobs(observer).FindAsync(job.Id))!;
        Assert.Equal("Failed", failed.State);
        Assert.Equal("course_not_found", failed.ErrorCode);
        Assert.Equal("Silinecek ders bulunamadı.", failed.ErrorMessage);
        Assert.Equal(1, failed.Attempts);
        Assert.Single(await EventsAsync(observer, course.Id), x => x == "course.deleted"); // Only the sync delete.
    }

    [Fact]
    public async Task CRS_NFR_06_Unexpected_error_retries_then_fails_without_storing_exception_text()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var suffix = Guid.NewGuid().ToString("N");
        // Test-only trigger simulating an unexpected database failure for this course.
        // DDL cannot be parameterized; only an integer ID and a hex GUID are embedded.
        var create = $"""
            CREATE FUNCTION crs_test_fail_{suffix}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF OLD."Id" = {course.Id} THEN RAISE EXCEPTION 'secret-trigger-text Password=hunter2'; END IF;
                RETURN OLD;
            END; $body$;
            CREATE TRIGGER crs_test_fail_{suffix} BEFORE DELETE ON crs_courses
                FOR EACH ROW EXECUTE FUNCTION crs_test_fail_{suffix}();
            """;
        var drop = $"""
            DROP TRIGGER crs_test_fail_{suffix} ON crs_courses;
            DROP FUNCTION crs_test_fail_{suffix}();
            """;
        await db.Database.ExecuteSqlRawAsync(create);
        try
        {
            var job = await new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42);
            var options = Enabled with { MaxAttempts = 2 };
            CrsCourseDeletionJob current;
            do
            {
                await using var worker = fixture.CreateContext();
                Assert.True(await Processor(worker, options).ProcessNextAsync());
                current = (await new CrsCourseDeletionJobs(worker).FindAsync(job.Id))!;
            } while (current.State == "Pending" && current.Attempts == 0);

            Assert.Equal("Pending", current.State); // First failure: retried.
            Assert.Equal(1, current.Attempts);
            Assert.Equal("deletion_failed", current.ErrorCode);
            Assert.Null(current.CompletedAt);

            var failed = await ProcessUntilSettledAsync(job.Id, options);
            Assert.Equal("Failed", failed.State);
            Assert.Equal(2, failed.Attempts);
            Assert.Equal("deletion_failed", failed.ErrorCode);
            Assert.DoesNotContain("secret", failed.ErrorMessage);
            Assert.DoesNotContain("hunter2", failed.ErrorMessage);
            Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
            Assert.DoesNotContain("course.deleted", await EventsAsync(db, course.Id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(drop);
        }
    }

    [Fact]
    public async Task CRS_NFR_06_Recovery_returns_abandoned_running_jobs_to_pending_but_skips_locked_ones()
    {
        await using var db = fixture.CreateContext();
        var abandoned = await new CrsCourseDeletionJobs(db).EnqueueAsync((await CreateAsync(db)).Id, 42);
        var active = await new CrsCourseDeletionJobs(db).EnqueueAsync((await CreateAsync(db)).Id, 42);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE crs_course_deletion_jobs SET "State" = 2, "Attempts" = 1 WHERE "Id" IN ({abandoned.Id}, {active.Id})
            """);

        // Another instance is currently processing "active" and holds its row lock.
        await using var other = fixture.CreateContext();
        await using (var transaction = await other.Database.BeginTransactionAsync())
        {
            await other.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM crs_course_deletion_jobs WHERE "Id" = {active.Id} FOR UPDATE
                """).ToListAsync();
            await using var worker = fixture.CreateContext();
            await Processor(worker).RecoverAsync();
            var jobs = new CrsCourseDeletionJobs(worker);
            Assert.Equal("Pending", (await jobs.FindAsync(abandoned.Id))!.State);
            Assert.Equal("Running", (await jobs.FindAsync(active.Id))!.State);
            await transaction.RollbackAsync();
        }

        await using var cleanup = fixture.CreateContext();
        await Processor(cleanup).RecoverAsync();
        Assert.Equal("Succeeded", (await ProcessUntilSettledAsync(abandoned.Id)).State);
        Assert.Equal("Succeeded", (await ProcessUntilSettledAsync(active.Id)).State);
    }

    [Fact]
    public async Task CRS_NFR_06_Claim_skips_job_locked_by_another_worker()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var job = await new CrsCourseDeletionJobs(db).EnqueueAsync(course.Id, 42);
        await using var other = fixture.CreateContext();
        await using (var transaction = await other.Database.BeginTransactionAsync())
        {
            await other.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM crs_course_deletion_jobs WHERE "State" = 1 FOR UPDATE
                """); // Locks every pending job, including leftovers from other tests.
            await using var worker = fixture.CreateContext();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            Assert.False(await Processor(worker).ProcessNextAsync(timeout.Token)); // SKIP LOCKED: no wait, no double claim.
            await transaction.RollbackAsync();
        }
        Assert.Equal("Succeeded", (await ProcessUntilSettledAsync(job.Id)).State);
    }
}
