using System.Data.Common;
using System.Text.RegularExpressions;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.IntegrationTests;

public sealed class CrsNfr04CachePostgresTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    private CrsDbContext ReadContext(QueryCounter counter)
    {
        using var db = fixture.CreateContext();
        return new(new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(counter).Options);
    }

    private static async Task<Course> CreateAsync(CrsService service, string name = "Original") =>
        await service.SaveCourseAsync(new()
        {
            CategoryId = (await service.SaveCategoryAsync(new() { Name = "Cache" })).Id,
            FullName = name, ShortName = Guid.NewGuid().ToString("N")
        });

    [Fact]
    public async Task CRS_NFR_04_Repeated_reads_hit_cache_and_return_defensive_copies()
    {
        await using var seed = fixture.CreateContext();
        var course = await CreateAsync(new(seed));
        var enrolment = await new CrsService(seed).EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var service = new CrsService(db);
        var firstList = await service.ListCoursesAsync();
        var firstCourse = (await service.FindCourseAsync(course.Id))!;
        var firstParticipants = await service.ListEnrolmentsAsync(course.Id);
        var firstEnrolment = (await service.FindEnrolmentAsync(course.Id, enrolment.Id))!;
        firstList.Single(x => x.Id == course.Id).FullName = "Caller mutation";
        firstList.Clear();
        firstCourse.FullName = "Caller mutation";
        firstParticipants.Clear();
        firstEnrolment.Role = "Caller mutation";

        Assert.Equal("Original", (await service.ListCoursesAsync()).Single(x => x.Id == course.Id).FullName);
        Assert.Equal("Original", (await service.FindAsync(course.Id))!.FullName);
        Assert.Single(await service.ListEnrolmentsAsync(course.Id));
        Assert.Equal("Student", (await service.FindEnrolmentAsync(course.Id, enrolment.Id))!.Role);
        Assert.Equal(2, counter.Courses);
        Assert.Equal(2, counter.Enrolments);
        Assert.True(counter.Revisions >= 8); // A hit is not a promise of zero SQL.
    }

    [Fact]
    public async Task CRS_NFR_04_Course_change_invalidates_list_and_its_detail_only()
    {
        await using var writer = fixture.CreateContext();
        var a = await CreateAsync(new(writer));
        var b = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.ListCoursesAsync();
        await reader.FindCourseAsync(a.Id);
        await reader.FindCourseAsync(b.Id);
        await reader.ListEnrolmentsAsync(a.Id);
        await reader.ListEnrolmentsAsync(b.Id);
        counter.Reset();
        a.FullName = "Committed change";
        a.Visible = false;
        await new CrsService(writer).SaveCourseAsync(a);
        Assert.Equal("Committed change", (await reader.FindCourseAsync(a.Id))!.FullName);
        Assert.False((await reader.FindAsync(a.Id))!.Visible);
        Assert.Equal("Original", (await reader.FindCourseAsync(b.Id))!.FullName);
        Assert.Equal("Committed change", (await reader.ListCoursesAsync()).Single(x => x.Id == a.Id).FullName);
        await reader.ListEnrolmentsAsync(a.Id);
        await reader.ListEnrolmentsAsync(b.Id);
        Assert.Equal(2, counter.Courses);
        Assert.Equal(0, counter.Enrolments);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("status-role")]
    [InlineData("delete")]
    public async Task CRS_NFR_04_Enrolment_changes_refresh_related_keys_and_preserve_other_courses(string operation)
    {
        await using var writer = fixture.CreateContext();
        var service = new CrsService(writer);
        var a = await CreateAsync(service);
        var b = await CreateAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = a.Id, UserId = 42 });
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.ListCoursesAsync();
        await reader.FindCourseAsync(a.Id);
        await reader.ListEnrolmentsAsync(a.Id);
        await reader.ListEnrolmentsAsync(b.Id);
        await reader.FindEnrolmentAsync(a.Id, enrolment.Id);
        counter.Reset();
        if (operation == "create") await service.EnrolAsync(new() { CourseId = a.Id, UserId = 43 });
        if (operation == "status-role") await service.UpdateEnrolmentAsync(enrolment.Id, true, "Teacher");
        if (operation == "delete") await service.UnenrolAsync(enrolment.Id);
        var participants = await reader.ListEnrolmentsAsync(a.Id);
        var current = await reader.FindEnrolmentAsync(a.Id, enrolment.Id);
        if (operation == "create") Assert.Equal(2, participants.Count);
        if (operation == "status-role") { Assert.True(current!.Suspended); Assert.Equal("Teacher", current.Role); }
        if (operation == "delete") { Assert.Empty(participants); Assert.Null(current); }
        await reader.ListCoursesAsync();
        await reader.FindCourseAsync(a.Id);
        await reader.ListEnrolmentsAsync(b.Id);
        Assert.Equal(0, counter.Courses);
        Assert.Equal(2, counter.Enrolments);
    }

    [Fact]
    public async Task CRS_NFR_04_Course_creation_and_cascade_deletion_refresh_lists_and_missing_entries()
    {
        await using var writer = fixture.CreateContext();
        var service = new CrsService(writer);
        var course = await CreateAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.ListCoursesAsync();
        await reader.FindCourseAsync(course.Id);
        await reader.ListEnrolmentsAsync(course.Id);
        await reader.FindEnrolmentAsync(course.Id, enrolment.Id);
        var created = await CreateAsync(service, "New course");
        Assert.Contains(await reader.ListCoursesAsync(), x => x.Id == created.Id);
        await service.DeleteCourseAsync(course.Id);
        Assert.Null(await reader.FindCourseAsync(course.Id));
        Assert.DoesNotContain(await reader.ListCoursesAsync(), x => x.Id == course.Id);
        Assert.Empty(await reader.ListEnrolmentsAsync(course.Id));
        Assert.Null(await reader.FindEnrolmentAsync(course.Id, enrolment.Id));
        // A previously cached miss is replaced after an actual INSERT using that identifier.
        writer.ChangeTracker.Clear();
        writer.Courses.Add(new() { Id = course.Id, CategoryId = course.CategoryId, FullName = "Recreated", ShortName = Guid.NewGuid().ToString("N") });
        await writer.SaveChangesAsync();
        Assert.Equal("Recreated", (await reader.FindCourseAsync(course.Id))!.FullName);
    }

    [Fact]
    public async Task CRS_NFR_04_Noops_and_failed_writes_preserve_cached_snapshots_and_event_count()
    {
        await using var writer = fixture.CreateContext();
        var service = new CrsService(writer);
        var course = await CreateAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        await reader.ListEnrolmentsAsync(course.Id);
        var eventsBefore = await EventCountAsync(writer, course.Id);
        counter.Reset();
        await service.SaveCourseAsync(course);
        await service.UpdateEnrolmentAsync(enrolment.Id, false, "Student");
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateEnrolmentAsync(enrolment.Id, false, ""));
        writer.Courses.Add(new() { CategoryId = course.CategoryId, FullName = "Conflict", ShortName = course.ShortName });
        await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync());
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal("Student", Assert.Single(await reader.ListEnrolmentsAsync(course.Id)).Role);
        Assert.Equal(eventsBefore, await EventCountAsync(db, course.Id));
        Assert.Equal(0, counter.Courses);
        Assert.Equal(0, counter.Enrolments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CRS_NFR_04_Transaction_reads_bypass_cache_and_commit_or_rollback_controls_refresh(bool commit)
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        counter.Reset();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = 'Inside transaction' WHERE \"Id\" = {course.Id}");
            Assert.Equal("Inside transaction", (await reader.FindCourseAsync(course.Id))!.FullName);
            Assert.Equal("Inside transaction", (await reader.FindCourseAsync(course.Id))!.FullName);
            Assert.Equal(0, counter.Revisions);
            if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        }
        counter.Reset();
        Assert.Equal(commit ? "Inside transaction" : "Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal(commit ? 1 : 0, counter.Courses);
    }

    [Fact]
    public async Task CRS_NFR_04_Uncommitted_external_changes_are_not_visible_and_rollback_does_not_evict()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        counter.Reset();
        await using (var transaction = await writer.Database.BeginTransactionAsync())
        {
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = 'Never committed' WHERE \"Id\" = {course.Id}");
            Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
            await transaction.RollbackAsync();
        }
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal(0, counter.Courses);
    }

    [Fact]
    public async Task CRS_NFR_04_Lower_event_id_committing_later_still_invalidates_course_list()
    {
        await using var first = fixture.CreateContext();
        var course = await CreateAsync(new(first));
        await using var delayed = await first.Database.BeginTransactionAsync();
        await first.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = 'Late commit' WHERE \"Id\" = {course.Id}");
        await using var second = fixture.CreateContext();
        await CreateAsync(new(second)); // Higher sequence ID commits first.
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        Assert.Equal("Original", (await reader.ListCoursesAsync()).Single(x => x.Id == course.Id).FullName);
        counter.Reset();
        await delayed.CommitAsync();
        Assert.Equal("Late commit", (await reader.ListCoursesAsync()).Single(x => x.Id == course.Id).FullName);
        Assert.Equal(1, counter.Courses);
    }

    [Fact]
    public async Task CRS_NFR_04_Active_queries_change_at_start_and_end_without_any_write_or_new_event()
    {
        await using var writer = fixture.CreateContext();
        var service = new CrsService(writer);
        var course = await CreateAsync(service);
        // Give setup and initial assertions ample time; no production clock changes are required.
        var starts = DateTimeOffset.UtcNow.AddSeconds(3);
        var ends = starts.AddSeconds(3);
        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42, StartsAt = starts, EndsAt = ends });
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.ListEnrolmentsAsync(course.Id); // Warm the raw participant cache.
        var eventsBefore = await EventCountAsync(db, course.Id);
        Assert.False(await reader.IsActivelyEnrolledAsync(42, course.Id));
        Assert.DoesNotContain(42, await reader.ListEnrolledUserIdsAsync(course.Id));
        Assert.DoesNotContain(42, await reader.ListRequiredLearnerIdsAsync(course.Id));
        await WaitUntilAsync(starts);
        Assert.True(await reader.IsActivelyEnrolledAsync(42, course.Id));
        Assert.False(await reader.IsActivelyEnrolledAsync(43, course.Id));
        Assert.Contains(42, await reader.ListEnrolledUserIdsAsync(course.Id));
        Assert.Contains(42, await reader.ListRequiredLearnerIdsAsync(course.Id));
        await WaitUntilAsync(ends);
        Assert.False(await reader.IsActivelyEnrolledAsync(42, course.Id));
        Assert.DoesNotContain(42, await reader.ListEnrolledUserIdsAsync(course.Id));
        Assert.DoesNotContain(42, await reader.ListRequiredLearnerIdsAsync(course.Id));
        Assert.Equal(eventsBefore, await EventCountAsync(db, course.Id));
    }

    [Fact]
    public async Task CRS_NFR_04_Enrolment_detail_keys_cannot_leak_a_record_from_another_course()
    {
        await using var db = fixture.CreateContext();
        var service = new CrsService(db);
        var a = await CreateAsync(service);
        var b = await CreateAsync(service);
        var enrolment = await service.EnrolAsync(new() { CourseId = a.Id, UserId = 42 });
        Assert.NotNull(await service.FindEnrolmentAsync(a.Id, enrolment.Id));
        Assert.Null(await service.FindEnrolmentAsync(b.Id, enrolment.Id));
        Assert.Empty(await service.ListEnrolmentsAsync(b.Id));
    }

    [Fact]
    public async Task CRS_NFR_04_Outbox_delivery_and_replay_do_not_evict_unchanged_data()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        counter.Reset();
        var dispatcher = new CrsEventDispatcher(writer, new([new ReceiptOnlyExtension()]));
        Assert.True(await dispatcher.DispatchAsync() > 0);
        await dispatcher.DispatchAsync();
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal(0, counter.Courses);
    }

    [Fact]
    public async Task CRS_NFR_04_Database_revision_failure_is_not_hidden_by_a_cached_result()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        await writer.Database.ExecuteSqlRawAsync("ALTER TABLE crs_events RENAME TO crs_events_unavailable");
        try
        {
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => reader.FindCourseAsync(course.Id));
        }
        finally
        {
            await writer.Database.ExecuteSqlRawAsync("ALTER TABLE crs_events_unavailable RENAME TO crs_events");
        }
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
    }

    [Fact]
    public async Task CRS_NFR_04_Manually_enlisted_transaction_does_not_populate_cache()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        counter.Reset();
        await db.Database.OpenConnectionAsync();
        using (var transaction = new CommittableTransaction())
        {
            db.Database.EnlistTransaction(transaction);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = 'Enlisted change' WHERE \"Id\" = {course.Id}");
            Assert.Equal("Enlisted change", (await reader.FindCourseAsync(course.Id))!.FullName);
            Assert.Equal(0, counter.Revisions);
            transaction.Rollback();
        }
        db.Database.EnlistTransaction(null);
        counter.Reset();
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal(0, counter.Courses);
    }

    [Fact]
    public async Task CRS_NFR_04_Ambient_transaction_rollback_does_not_poison_cache()
    {
        await using var writer = fixture.CreateContext();
        var course = await CreateAsync(new(writer));
        var counter = new QueryCounter();
        await using var db = ReadContext(counter);
        var reader = new CrsService(db);
        await reader.FindCourseAsync(course.Id);
        counter.Reset();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crs_courses SET \"FullName\" = 'Ambient change' WHERE \"Id\" = {course.Id}");
            Assert.Equal("Ambient change", (await reader.FindCourseAsync(course.Id))!.FullName);
            Assert.Equal(0, counter.Revisions);
            // No Complete(): dispose rolls back both the row and its outbox event.
        }
        counter.Reset();
        Assert.Equal("Original", (await reader.FindCourseAsync(course.Id))!.FullName);
        Assert.Equal(0, counter.Courses);
    }

    private sealed class ReceiptOnlyExtension : ICrsEventExtension
    {
        public string Id => "crs-nfr04-test-receipt";
        public int Order => 0;
        public Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task WaitUntilAsync(DateTimeOffset instant)
    {
        var delay = instant - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(30);
        if (delay > TimeSpan.Zero) await Task.Delay(delay);
    }

    private static Task<long> EventCountAsync(CrsDbContext db, int courseId) => db.Database
        .SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM crs_events WHERE \"CourseId\" = {courseId}").SingleAsync();

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Courses { get; private set; }
        public int Enrolments { get; private set; }
        public int Revisions { get; private set; }
        public void Reset() => (Courses, Enrolments, Revisions) = (0, 0, 0);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Regex.IsMatch(command.CommandText, "FROM\\s+\"?crs_courses", RegexOptions.IgnoreCase)) Courses++;
            if (Regex.IsMatch(command.CommandText, "FROM\\s+\"?crs_enrolments", RegexOptions.IgnoreCase)) Enrolments++;
            if (Regex.IsMatch(command.CommandText, "FROM\\s+\"?crs_events", RegexOptions.IgnoreCase)) Revisions++;
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class CrsNfr04CacheApiTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    [Fact]
    public async Task CRS_NFR_04_Reading_data_never_caches_permission_decisions_across_requests()
    {
        await using var db = fixture.Database.CreateContext();
        var service = new CrsService(db);
        var course = await service.SaveCourseAsync(new()
        {
            CategoryId = (await service.SaveCategoryAsync(new() { Name = "Cache authorization" })).Id,
            FullName = "Authorization", ShortName = Guid.NewGuid().ToString("N")
        });
        var allow = true;
        using var factory = fixture.CreateFactory(permission: (_, _) => allow);
        using var client = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync($"/api/v1/courses/{course.Id}/enrolments")).StatusCode);
        allow = false;
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/courses/{course.Id}/enrolments")).StatusCode);
    }
}
