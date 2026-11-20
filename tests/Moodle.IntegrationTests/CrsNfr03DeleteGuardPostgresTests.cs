using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using Npgsql;

namespace Moodle.IntegrationTests;

public sealed class CrsNfr03DeleteGuardPostgresTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    private static async Task<Course> CreateAsync(CrsDbContext db)
    {
        var service = new CrsService(db);
        var category = await service.SaveCategoryAsync(new() { Name = "Delete protection" });
        var course = await service.SaveCourseAsync(new()
            { CategoryId = category.Id, FullName = "Protected course", ShortName = Guid.NewGuid().ToString("N") });
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        return course;
    }

    [Theory]
    [InlineData(CrsCourseOperationKind.Backup, false)]
    [InlineData(CrsCourseOperationKind.Backup, true)]
    [InlineData(CrsCourseOperationKind.Restore, false)]
    [InlineData(CrsCourseOperationKind.Restore, true)]
    public async Task CRS_NFR_03_Pending_and_running_backup_restore_preserve_data_events_and_cache(
        CrsCourseOperationKind kind, bool running)
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(writer);
        var registry = new CrsCourseOperationRegistry(writer);
        var token = await registry.RegisterPendingAsync(course.Id, kind);
        if (running) await registry.MarkRunningAsync(course.Id, token);
        var counter = new ReadCounter();
        await using var db = CounterContext(counter);
        var service = new CrsService(db);
        await service.FindCourseAsync(course.Id);
        await service.ListEnrolmentsAsync(course.Id);
        var before = await EventsAsync(db, course.Id);
        var conflict = await Assert.ThrowsAsync<CrsConflictException>(() => service.DeleteCourseAsync(course.Id));
        Assert.Equal("course_operation_in_progress", conflict.Code);
        counter.Reads = 0;
        Assert.NotNull(await service.FindCourseAsync(course.Id));
        Assert.Single(await service.ListEnrolmentsAsync(course.Id));
        Assert.Equal(0, counter.Reads); // Both snapshots remain valid after rejected deletion.
        Assert.Equal(before, await EventsAsync(db, course.Id));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State == EntityState.Deleted);
        await using var verification = fixture.CreateContext();
        Assert.True(await verification.Courses.AnyAsync(x => x.Id == course.Id));
        Assert.Single(await verification.Enrolments.Where(x => x.CourseId == course.Id).ToListAsync());
        await registry.ReleaseAsync(course.Id, token);
    }

    [Fact]
    public async Task CRS_NFR_03_Releasing_all_operations_allows_deletion_cascade_events_and_cache_refresh()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(writer);
        var registry = new CrsCourseOperationRegistry(writer);
        var backup = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        var restore = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Restore);
        await registry.ReleaseAsync(course.Id, backup);
        await Assert.ThrowsAsync<CrsConflictException>(() => new CrsService(writer).DeleteCourseAsync(course.Id));
        await registry.MarkRunningAsync(course.Id, restore);
        await registry.ReleaseAsync(course.Id, restore);
        await using var reader = fixture.CreateContext();
        var cached = new CrsService(reader);
        await cached.FindCourseAsync(course.Id);
        await cached.ListCoursesAsync();
        await cached.ListEnrolmentsAsync(course.Id);
        await new CrsService(writer).DeleteCourseAsync(course.Id);
        Assert.Null(await cached.FindCourseAsync(course.Id));
        Assert.DoesNotContain(await cached.ListCoursesAsync(), x => x.Id == course.Id);
        Assert.Empty(await cached.ListEnrolmentsAsync(course.Id));
        var events = await EventsAsync(reader, course.Id);
        Assert.Single(events, x => x == "course.deleted");
        Assert.Single(events, x => x == "enrolment.deleted");
    }

    [Fact]
    public async Task CRS_NFR_03_Operation_on_other_course_does_not_block_unrelated_deletion()
    {
        await using var db = fixture.CreateContext();
        var a = await CreateAsync(db);
        var b = await CreateAsync(db);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(a.Id, CrsCourseOperationKind.Backup);
        await registry.ReleaseAsync(b.Id, token); // Cannot release a different course's protection.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => registry.MarkRunningAsync(b.Id, token));
        await Assert.ThrowsAsync<CrsConflictException>(() => new CrsService(db).DeleteCourseAsync(a.Id));
        await new CrsService(db).DeleteCourseAsync(b.Id);
        Assert.Null(await new CrsService(db).FindCourseAsync(b.Id));
        await registry.ReleaseAsync(a.Id, token);
    }

    [Fact]
    public async Task CRS_NFR_03_Foreign_key_protects_against_direct_SQL_deletion_without_success_events()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        var events = await EventsAsync(db, course.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM crs_courses WHERE \"Id\" = {course.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(events, await EventsAsync(db, course.Id));
        Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        Assert.Single(await db.Enrolments.Where(x => x.CourseId == course.Id).ToListAsync());
        await registry.ReleaseAsync(course.Id, token);
    }

    [Fact]
    public async Task CRS_NFR_03_Repeated_running_or_release_does_not_write_or_create_course_events()
    {
        await using var seed = fixture.CreateContext();
        var course = await CreateAsync(seed);
        var counter = new ReadCounter();
        await using var db = CounterContext(counter);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        await registry.MarkRunningAsync(course.Id, token);
        Assert.Equal(2, counter.Writes); // Measurement control: real INSERT and UPDATE were observed.
        counter.Writes = 0;
        await registry.MarkRunningAsync(course.Id, token);
        Assert.Equal(0, counter.Writes);
        await registry.ReleaseAsync(course.Id, token);
        Assert.Equal(1, counter.Writes);
        counter.Writes = 0;
        await registry.ReleaseAsync(course.Id, token);
        Assert.Equal(0, counter.Writes);
        Assert.Equal(["course.created", "enrolment.created"], await EventsAsync(db, course.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CRS_NFR_03_Registration_commit_or_rollback_controls_waiting_delete(bool commit)
    {
        await using var owner = fixture.CreateContext();
        var course = await CreateAsync(owner);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        var registry = new CrsCourseOperationRegistry(owner);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        await using var deleting = fixture.CreateContext();
        await deleting.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var deletion = new CrsService(deleting).DeleteCourseAsync(course.Id, timeout.Token);
        await WaitForLockAsync(pid);
        if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        if (commit)
        {
            await Assert.ThrowsAsync<CrsConflictException>(() => deletion);
            Assert.True(await deleting.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
            await registry.ReleaseAsync(course.Id, token);
        }
        else
        {
            await deletion;
            Assert.False(await deleting.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CRS_NFR_03_Deletion_commit_or_rollback_controls_waiting_registration(bool commit)
    {
        await using var deleting = fixture.CreateContext();
        var course = await CreateAsync(deleting);
        await using var transaction = await deleting.Database.BeginTransactionAsync();
        await new CrsService(deleting).DeleteCourseAsync(course.Id);
        await using var registering = fixture.CreateContext();
        await registering.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)registering.Database.GetDbConnection()).ProcessID;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var registry = new CrsCourseOperationRegistry(registering);
        var admission = registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Restore, timeout.Token);
        await WaitForLockAsync(pid);
        if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        if (commit)
            await Assert.ThrowsAsync<KeyNotFoundException>(() => admission);
        else
        {
            var token = await admission;
            await Assert.ThrowsAsync<CrsConflictException>(() => new CrsService(registering).DeleteCourseAsync(course.Id));
            await registry.ReleaseAsync(course.Id, token);
            Assert.DoesNotContain("course.deleted", await EventsAsync(registering, course.Id));
        }
    }

    [Fact]
    public async Task CRS_NFR_03_Cancelled_lock_wait_does_not_delete_or_emit_success_event()
    {
        await using var owner = fixture.CreateContext();
        var course = await CreateAsync(owner);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await new CrsCourseOperationRegistry(owner).RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        await using var deleting = fixture.CreateContext();
        await deleting.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var deletion = new CrsService(deleting).DeleteCourseAsync(course.Id, cancellation.Token);
        await WaitForLockAsync(pid);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deletion);
        await transaction.RollbackAsync();
        Assert.True(await deleting.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        Assert.DoesNotContain("course.deleted", await EventsAsync(deleting, course.Id));
        await new CrsService(deleting).DeleteCourseAsync(course.Id);
    }

    [Fact]
    public async Task CRS_NFR_03_Rolled_back_release_keeps_protection_and_rolled_back_delete_keeps_cache()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await registry.ReleaseAsync(course.Id, token);
            await tx.RollbackAsync();
        }
        await Assert.ThrowsAsync<CrsConflictException>(() => new CrsService(db).DeleteCourseAsync(course.Id));
        await registry.ReleaseAsync(course.Id, token);
        await using var observer = fixture.CreateContext();
        var cached = new CrsService(observer);
        await cached.FindCourseAsync(course.Id);
        await cached.ListEnrolmentsAsync(course.Id);
        var before = await EventsAsync(observer, course.Id);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await new CrsService(db).DeleteCourseAsync(course.Id);
            Assert.NotNull(await cached.FindCourseAsync(course.Id));
            await tx.RollbackAsync();
        }
        Assert.NotNull(await cached.FindCourseAsync(course.Id));
        Assert.Single(await cached.ListEnrolmentsAsync(course.Id));
        Assert.Equal(before, await EventsAsync(observer, course.Id));
    }

    [Fact]
    public async Task CRS_NFR_03_Missing_courses_cannot_admit_operations_and_keep_delete_not_found_contract()
    {
        await using var db = fixture.CreateContext();
        var registry = new CrsCourseOperationRegistry(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => registry.RegisterPendingAsync(int.MaxValue, CrsCourseOperationKind.Backup));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CrsService(db).DeleteCourseAsync(int.MaxValue));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CrsService(db).DeleteCourseAsync(0));
    }

    [Fact]
    public async Task CRS_NFR_03_Unavailable_guard_table_fails_closed_without_deleting_or_creating_events()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var before = await EventsAsync(db, course.Id);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE crs_course_operations RENAME TO crs_course_operations_unavailable");
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => new CrsService(db).DeleteCourseAsync(course.Id));
            Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
            Assert.Equal(before, await EventsAsync(db, course.Id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE crs_course_operations_unavailable RENAME TO crs_course_operations");
        }
    }

    [Fact]
    public async Task CRS_NFR_03_Ambient_transaction_controls_deletion_and_outbox_rollback()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var before = await EventsAsync(db, course.Id);
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await new CrsService(db).DeleteCourseAsync(course.Id);
            // No Complete(): registry must not commit the caller's transaction.
        }
        Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        Assert.Single(await db.Enrolments.Where(x => x.CourseId == course.Id).ToListAsync());
        Assert.Equal(before, await EventsAsync(db, course.Id));
    }

    [Fact]
    public async Task CRS_NFR_03_Manually_enlisted_registration_is_not_committed_by_registry()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        await db.Database.OpenConnectionAsync();
        using (var transaction = new CommittableTransaction())
        {
            db.Database.EnlistTransaction(transaction);
            await new CrsCourseOperationRegistry(db).RegisterPendingAsync(course.Id, CrsCourseOperationKind.Restore);
            transaction.Rollback();
        }
        db.Database.EnlistTransaction(null);
        await new CrsService(db).DeleteCourseAsync(course.Id);
        Assert.False(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
    }

    [Fact]
    public async Task CRS_NFR_03_Migration_refuses_to_drop_active_protection_and_can_be_reapplied_after_release()
    {
        await using var db = fixture.CreateContext();
        var course = await CreateAsync(db);
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        var migrator = db.GetService<IMigrator>();
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261119000100_CrsEvents"));
        await Assert.ThrowsAsync<CrsConflictException>(() => new CrsService(db).DeleteCourseAsync(course.Id));
        await registry.ReleaseAsync(course.Id, token);
        await migrator.MigrateAsync("20261119000100_CrsEvents");
        await migrator.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await new CrsService(db).DeleteCourseAsync(course.Id);
    }

    private async Task WaitForLockAsync(int pid)
    {
        await using var observer = fixture.CreateContext();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (await observer.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value" FROM pg_stat_activity WHERE pid = {pid} AND wait_event_type = 'Lock'
                """).AnyAsync()) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Expected PostgreSQL row-lock wait was not observed.");
    }

    private static Task<List<string>> EventsAsync(CrsDbContext db, int id) => db.Database.SqlQuery<string>($"""
        SELECT "Kind" AS "Value" FROM crs_events WHERE "CourseId" = {id} ORDER BY "Id"
        """).ToListAsync();

    private CrsDbContext CounterContext(ReadCounter counter)
    {
        using var db = fixture.CreateContext();
        return new(new DbContextOptionsBuilder<CrsDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(counter).Options);
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public int Writes { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM crs_courses", StringComparison.Ordinal) ||
                command.CommandText.Contains("FROM crs_enrolments", StringComparison.Ordinal)) Reads++;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (command.CommandText.TrimStart().StartsWith("INSERT", StringComparison.Ordinal) ||
                command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal) ||
                command.CommandText.TrimStart().StartsWith("DELETE", StringComparison.Ordinal)) Writes++;
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class CrsNfr03DeleteGuardApiTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    [Fact]
    public async Task CRS_NFR_03_Server_returns_409_for_registered_operation_then_204_after_release()
    {
        await using var db = fixture.Database.CreateContext();
        var service = new CrsService(db);
        var category = await service.SaveCategoryAsync(new() { Name = "Guard API" });
        var course = await service.SaveCourseAsync(new()
            { CategoryId = category.Id, FullName = "Guard API", ShortName = Guid.NewGuid().ToString("N") });
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Restore);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var rejected = await client.DeleteAsync($"/api/v1/courses/{course.Id}");
        await CrsApiTestSupport.AssertErrorAsync(rejected, System.Net.HttpStatusCode.Conflict, "course_operation_in_progress");
        await registry.ReleaseAsync(course.Id, token);
        using var allowed = await client.DeleteAsync($"/api/v1/courses/{course.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, allowed.StatusCode);
    }
}
