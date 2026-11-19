using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.IntegrationTests;

public sealed class CrsEventsPostgresTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    private async Task<CrsDbContext> ResetAsync()
    {
        var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE crs_event_receipts, crs_events, crs_enrolments, crs_courses, crs_categories RESTART IDENTITY CASCADE
            """);
        return db;
    }

    private static async Task<Course> CreateCourseAsync(CrsService service) => await service.SaveCourseAsync(new()
    {
        CategoryId = (await service.SaveCategoryAsync(new() { Name = "Events" })).Id,
        FullName = "Private course title must not enter event", ShortName = "EVENTS"
    });

    private static Task<List<EventRow>> EventsAsync(CrsDbContext db) =>
        db.Database.SqlQueryRaw<EventRow>("SELECT * FROM crs_events ORDER BY \"Id\"").ToListAsync();

    [Fact]
    public async Task CRS_FR_10_NFR_07_Service_lifecycle_records_all_six_event_kinds()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        course.FullName = "Changed private title";
        await service.SaveCourseAsync(course);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        await service.UpdateEnrolmentAsync(enrolment.Id, true, "Teacher");
        await service.UnenrolAsync(enrolment.Id);
        await service.DeleteCourseAsync(course.Id);
        await using var reader = fixture.CreateContext();
        var events = await EventsAsync(reader);
        Assert.Equal(["course.created", "course.updated", "enrolment.created", "enrolment.updated",
            "enrolment.deleted", "course.deleted"], events.Select(x => x.Kind));
        Assert.All(events, x => { Assert.Equal(course.Id, x.CourseId); Assert.Equal(1, x.Version); Assert.NotEqual(default, x.OccurredAt); });
        Assert.Equal(["FullName"], events[1].ChangedFields);
        Assert.Equal(["Role", "Suspended"], events[3].ChangedFields);
        Assert.Equal(enrolment.Id, events[4].EnrolmentId);
        Assert.Equal(42, events[4].UserId);
        Assert.Null(events[0].UserId);
        Assert.Equal(events.Count, events.Select(x => x.Id).Distinct().Count());
        Assert.Empty(await reader.Courses.ToListAsync());
    }

    [Fact]
    public async Task CRS_NFR_05_07_Noops_and_normalized_values_do_not_produce_events()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        await service.SaveCourseAsync(new() { Id = course.Id, CategoryId = course.CategoryId,
            FullName = course.FullName, ShortName = course.ShortName, IdNumber = "   " });
        await service.UpdateEnrolmentAsync(enrolment.Id, false, new string("Student".ToCharArray()));
        // Even an explicit no-op SQL UPDATE cannot create a misleading change notification.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = \"FullName\" WHERE \"Id\" = {course.Id}");
        Assert.Equal(2, (await EventsAsync(db)).Count);
    }

    [Fact]
    public async Task CRS_FR_10_Rollback_removes_data_and_events_and_hides_uncommitted_notifications()
    {
        await using var db = await ResetAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await CreateCourseAsync(new(db));
            Assert.Single(await EventsAsync(db));
            await using var observer = fixture.CreateContext();
            Assert.Empty(await EventsAsync(observer));
            await transaction.RollbackAsync();
        }
        await using var verification = fixture.CreateContext();
        Assert.Empty(await EventsAsync(verification));
        Assert.Empty(await verification.Courses.ToListAsync());
    }

    [Fact]
    public async Task CRS_FR_10_Failed_validation_and_database_constraint_do_not_create_events()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateEnrolmentAsync(enrolment.Id, false, ""));
        db.Courses.Add(new() { CategoryId = course.CategoryId, FullName = "Duplicate", ShortName = course.ShortName });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await using var verification = fixture.CreateContext();
        Assert.Equal(2, (await EventsAsync(verification)).Count);
        Assert.Single(await verification.Courses.ToListAsync());
    }

    [Fact]
    public async Task CRS_FR_10_Outbox_failure_rolls_back_the_business_write()
    {
        await using var db = await ResetAsync();
        var category = await new CrsService(db).SaveCategoryAsync(new() { Name = "Failure" });
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE crs_events ADD CONSTRAINT crs_test_reject CHECK (\"Kind\" <> 'course.created')");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => new CrsService(db).SaveCourseAsync(new()
                { CategoryId = category.Id, FullName = "Rejected", ShortName = "REJECT" }));
            await using var verification = fixture.CreateContext();
            Assert.Empty(await verification.Courses.ToListAsync());
            Assert.Empty(await EventsAsync(verification));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE crs_events DROP CONSTRAINT crs_test_reject");
        }
    }

    [Fact]
    public async Task CRS_FR_10_Cascade_course_deletion_records_each_removed_enrolment()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 43 });
        await service.DeleteCourseAsync(course.Id);
        var events = await EventsAsync(db);
        Assert.Equal(2, events.Count(x => x.Kind == "enrolment.deleted"));
        Assert.Single(events, x => x.Kind == "course.deleted");
        Assert.Empty(await db.Enrolments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CRS_NFR_08_Dispatch_is_ordered_durable_and_deduplicated_across_contexts()
    {
        await using var db = await ResetAsync();
        await CreateCourseAsync(new(db));
        var calls = new List<string>();
        var pipeline = new CrsEventExtensionPipeline([
            new Extension("b", 1, e => { calls.Add("b"); return Task.CompletedTask; }),
            new Extension("a", 0, e => { calls.Add("a"); return Task.CompletedTask; })]);
        Assert.Equal(2, await new CrsEventDispatcher(db, pipeline).DispatchAsync());
        await using var retry = fixture.CreateContext();
        Assert.Equal(0, await new CrsEventDispatcher(retry, pipeline).DispatchAsync());
        Assert.Equal(["a", "b"], calls);
        Assert.Equal(2, await ReceiptCountAsync(retry));
    }

    [Fact]
    public async Task CRS_NFR_08_Failing_extension_stops_followers_and_retry_uses_same_idempotency_key()
    {
        await using var db = await ResetAsync();
        await CreateCourseAsync(new(db));
        var effects = new HashSet<(string, long)>();
        var firstAttempts = 0;
        var fail = true;
        var followerCalls = 0;
        var pipeline = new CrsEventExtensionPipeline([
            new Extension("first", 0, e => { firstAttempts++; effects.Add(("first", e.Id)); return Task.CompletedTask; }),
            new Extension("failing", 1, e => { if (fail) throw new InvalidOperationException("test"); return Task.CompletedTask; }),
            new Extension("follower", 2, e => { followerCalls++; return Task.CompletedTask; })]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CrsEventDispatcher(db, pipeline).DispatchAsync());
        Assert.Equal(0, followerCalls);
        Assert.Equal(0, await ReceiptCountAsync(db));
        fail = false;
        await using var retry = fixture.CreateContext();
        Assert.Equal(3, await new CrsEventDispatcher(retry, pipeline).DispatchAsync());
        Assert.Equal(2, firstAttempts);
        Assert.Single(effects); // Consumer's idempotency key prevents repeating its external effect.
        Assert.Equal(1, followerCalls);
    }

    [Fact]
    public async Task CRS_NFR_08_Concurrent_dispatchers_do_not_deliver_receipted_event_twice()
    {
        await using var db = await ResetAsync();
        await CreateCourseAsync(new(db));
        var calls = 0;
        var pipeline = new CrsEventExtensionPipeline([new Extension("only", 0, async e =>
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
        })]);
        await using var other = fixture.CreateContext();
        var counts = await Task.WhenAll(new CrsEventDispatcher(db, pipeline).DispatchAsync(),
            new CrsEventDispatcher(other, pipeline).DispatchAsync());
        Assert.Equal(1, counts.Sum());
        Assert.Equal(1, calls);
        Assert.Equal(1, await ReceiptCountAsync(db));
    }

    [Fact]
    public async Task CRS_NFR_08_Dispatch_rejects_uncommitted_callers_transaction()
    {
        await using var db = await ResetAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await CreateCourseAsync(new(db));
        var pipeline = new CrsEventExtensionPipeline([new Extension("only", 0, _ => Task.CompletedTask)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CrsEventDispatcher(db, pipeline).DispatchAsync());
    }

    [Fact]
    public async Task CRS_FR_10_Rolled_back_updates_and_deletions_leave_no_success_events()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await service.UpdateEnrolmentAsync(enrolment.Id, true, "Teacher");
            await service.DeleteCourseAsync(course.Id);
            await transaction.RollbackAsync();
        }
        await using var verification = fixture.CreateContext();
        Assert.Equal(2, (await EventsAsync(verification)).Count);
        Assert.Single(await verification.Courses.ToListAsync());
        var persisted = Assert.Single(await verification.Enrolments.ToListAsync());
        Assert.False(persisted.Suspended);
        Assert.Equal("Student", persisted.Role);
    }

    [Fact]
    public async Task CRS_NFR_07_Payload_contains_only_metadata_and_no_field_values()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42, Role = "secret-value-not-for-events" });
        var serialized = System.Text.Json.JsonSerializer.Serialize(await EventsAsync(db));
        Assert.DoesNotContain("Private course", serialized);
        Assert.DoesNotContain("secret-value-not-for-events", serialized);
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT column_name AS "Value" FROM information_schema.columns WHERE table_name = 'crs_events'
            """).ToListAsync();
        Assert.Equal(["ChangedFields", "CourseId", "EnrolmentId", "Id", "Kind", "OccurredAt", "UserId", "Version"],
            columns.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CRS_NFR_08_Bounded_dispatch_and_new_consumer_replay_preserve_receipts()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        var course = await CreateCourseAsync(service);
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        var calls = new List<long>();
        var first = new Extension("first", 0, e => { calls.Add(e.Id); return Task.CompletedTask; });
        var dispatcher = new CrsEventDispatcher(db, new([first]));
        Assert.Equal(1, await dispatcher.DispatchAsync(1));
        Assert.Equal(1, await dispatcher.DispatchAsync(1));
        Assert.Equal(0, await dispatcher.DispatchAsync(1));
        Assert.True(calls[0] < calls[1]);
        var laterCalls = 0;
        var late = new Extension("late", 1, e => { laterCalls++; return Task.CompletedTask; });
        Assert.Equal(2, await new CrsEventDispatcher(db, new([first, late])).DispatchAsync());
        Assert.Equal(2, laterCalls);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task CRS_NFR_08_Extensions_cannot_mutate_later_consumers_field_list()
    {
        await using var db = await ResetAsync();
        await CreateCourseAsync(new(db));
        var pipeline = new CrsEventExtensionPipeline([
            new Extension("first", 0, e => { e.ChangedFields[0] = "tampered"; return Task.CompletedTask; }),
            new Extension("second", 1, e => { Assert.DoesNotContain("tampered", e.ChangedFields); return Task.CompletedTask; })]);
        Assert.Equal(2, await new CrsEventDispatcher(db, pipeline).DispatchAsync());
        Assert.DoesNotContain("tampered", Assert.Single(await EventsAsync(db)).ChangedFields);
    }

    [Fact]
    public async Task CRS_NFR_08_Cancellation_rolls_back_receipt_and_allows_retry()
    {
        await using var db = await ResetAsync();
        await CreateCourseAsync(new(db));
        using var cancellation = new CancellationTokenSource();
        var pipeline = new CrsEventExtensionPipeline([new Extension("cancel", 0, e =>
        {
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        })]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CrsEventDispatcher(db, pipeline).DispatchAsync(ct: cancellation.Token));
        Assert.Equal(0, await ReceiptCountAsync(db));
        Assert.Equal(1, await new CrsEventDispatcher(db, new([
            new Extension("cancel", 0, _ => Task.CompletedTask)])).DispatchAsync());
    }

    [Fact]
    public async Task CRS_FR_10_Savepoint_rollback_keeps_only_committed_changes_events()
    {
        await using var db = await ResetAsync();
        var service = new CrsService(db);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var course = await CreateCourseAsync(service);
            await transaction.CreateSavepointAsync("before_enrolment");
            await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
            await transaction.RollbackToSavepointAsync("before_enrolment");
            await transaction.CommitAsync();
        }
        await using var verification = fixture.CreateContext();
        Assert.Equal("course.created", Assert.Single(await EventsAsync(verification)).Kind);
        Assert.Single(await verification.Courses.ToListAsync());
        Assert.Empty(await verification.Enrolments.ToListAsync());
    }

    [Fact]
    public async Task CRS_NFR_07_Migration_is_reversible_and_does_not_rewrite_existing_business_rows()
    {
        await using var db = await ResetAsync();
        var course = await CreateCourseAsync(new(db));
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261117094833_InitialCrs");
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::integer AS "Value" FROM information_schema.tables WHERE table_name = 'crs_events'
            """).SingleAsync());
        Assert.NotNull(await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == course.Id));
        await migrator.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await EventsAsync(db)); // No fabricated historical events / migration backfill.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"Visible\" = false WHERE \"Id\" = {course.Id}");
        Assert.Equal("course.updated", Assert.Single(await EventsAsync(db)).Kind);
    }

    private static Task<int> ReceiptCountAsync(CrsDbContext db) => db.Database
        .SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM crs_event_receipts").SingleAsync();

    private sealed class Extension(string id, int order, Func<CrsChangeEvent, Task> handle) : ICrsEventExtension
    {
        public string Id => id;
        public int Order => order;
        public Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default) => handle(notification);
    }

    private sealed class EventRow
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "";
        public int CourseId { get; set; }
        public int? EnrolmentId { get; set; }
        public int? UserId { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public int Version { get; set; }
        public string[] ChangedFields { get; set; } = [];
    }
}
