using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.IntegrationTests;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 HTTP ve arka plan görevi testleri.
public sealed class CrsBackgroundDeletionApiTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    private static readonly Dictionary<string, string?> Fast = new()
    {
        ["Crs:BackgroundDeletion:Enabled"] = "true",
        ["Crs:BackgroundDeletion:PollIntervalMilliseconds"] = "50",
    };

    // Accepts requests but never reaches its first poll during a test.
    private static readonly Dictionary<string, string?> Idle = new()
    {
        ["Crs:BackgroundDeletion:Enabled"] = "true",
        ["Crs:BackgroundDeletion:PollIntervalMilliseconds"] = "3600000",
    };

    private sealed record JobBody(Guid Id, int CourseId, int? CategoryId, string State, int Attempts,
        int? RequestedByUserId, string? ErrorCode, string? ErrorMessage);

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> read, Func<T, bool> done, string what)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (true)
        {
            var value = await read();
            if (done(value)) return value;
            try { await poll.WaitForNextTickAsync(timeout.Token); }
            catch (OperationCanceledException) { Assert.Fail($"Timed out waiting for {what}; last value: {value}"); }
        }
    }

    private static async Task<JobBody> GetJobAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/courses/deletion-jobs/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JobBody>())!;
    }

    private static async Task<JobBody> AcceptedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<JobBody>())!;
        Assert.Equal($"/api/v1/courses/deletion-jobs/{job.Id}", response.Headers.Location?.OriginalString);
        return job;
    }

    private async Task<Course> CreateCourseAsync()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();
        var category = await CrsApiTestSupport.CreateCategoryAsync(client);
        var course = await CrsApiTestSupport.CreateCourseAsync(client, category.Id);
        await using var db = fixture.Database.CreateContext();
        await new CrsService(db).EnrolAsync(new() { CourseId = course.Id, UserId = 7 });
        return course;
    }

    private async Task<int> JobCountAsync(int courseId)
    {
        await using var db = fixture.Database.CreateContext();
        return await db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM crs_course_deletion_jobs WHERE "CourseId" = {courseId}
            """).SingleAsync();
    }

    private async Task<string[]> EventsAsync(int courseId)
    {
        await using var db = fixture.Database.CreateContext();
        return await CrsBackgroundDeletionPostgresTests.EventsAsync(db, courseId);
    }

    [Fact]
    public async Task CRS_FR_11_Disabled_background_deletion_keeps_synchronous_204_behavior()
    {
        var course = await CreateCourseAsync();
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/courses/{course.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        Assert.Equal(0, await JobCountAsync(course.Id));
        Assert.Single(await EventsAsync(course.Id), x => x == "course.deleted");
    }

    [Fact]
    public async Task CRS_NFR_06_Enabled_deletion_is_accepted_queued_and_completed_by_worker()
    {
        var course = await CreateCourseAsync();
        await using var factory = fixture.CreateFactory(settings: Fast);
        var client = factory.CreateClient();

        var job = await AcceptedAsync(await client.DeleteAsync($"/api/v1/courses/{course.Id}"));
        Assert.Equal(course.Id, job.CourseId);
        Assert.Equal(42, job.RequestedByUserId);
        Assert.Contains(job.State, new[] { "Pending", "Running", "Succeeded" });

        var done = await WaitForAsync(() => GetJobAsync(client, job.Id), x => x.State is "Succeeded" or "Failed", "job completion");
        Assert.Equal("Succeeded", done.State);
        Assert.Equal(1, done.Attempts);
        Assert.Null(done.ErrorCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        var events = await EventsAsync(course.Id);
        Assert.Single(events, x => x == "course.deleted");
        Assert.Single(events, x => x == "enrolment.deleted");
    }

    [Fact]
    public async Task CRS_FR_11_Repeated_request_returns_existing_pending_job()
    {
        var course = await CreateCourseAsync();
        await using var factory = fixture.CreateFactory(settings: Idle);
        var client = factory.CreateClient();

        var first = await AcceptedAsync(await client.DeleteAsync($"/api/v1/courses/{course.Id}"));
        var second = await AcceptedAsync(await client.DeleteAsync($"/api/v1/courses/{course.Id}"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Pending", second.State);
        Assert.Equal(1, await JobCountAsync(course.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        await DrainAsync(first.Id); // Leave no pending work for other tests.
    }

    [Fact]
    public async Task CRS_NFR_03_Enabled_deletion_of_course_with_backup_is_rejected_with_409_and_no_job()
    {
        var course = await CreateCourseAsync();
        await using var db = fixture.Database.CreateContext();
        var registry = new CrsCourseOperationRegistry(db);
        var token = await registry.RegisterPendingAsync(course.Id, CrsCourseOperationKind.Backup);
        await using var factory = fixture.CreateFactory(settings: Fast);

        var response = await factory.CreateClient().DeleteAsync($"/api/v1/courses/{course.Id}");

        await CrsApiTestSupport.AssertErrorAsync(response, HttpStatusCode.Conflict, "course_operation_in_progress");
        Assert.Equal(0, await JobCountAsync(course.Id));
        Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        await registry.ReleaseAsync(course.Id, token);
    }

    [Fact]
    public async Task CRS_FR_11_Unauthorized_deletion_request_is_rejected_before_queueing()
    {
        var course = await CreateCourseAsync();
        await using var forbidden = fixture.CreateFactory(settings: Fast,
            permission: (capability, _) => capability != "course.delete");
        await using var anonymous = fixture.CreateFactory(authenticated: false, settings: Fast);

        await CrsApiTestSupport.AssertErrorAsync(
            await forbidden.CreateClient().DeleteAsync($"/api/v1/courses/{course.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await CrsApiTestSupport.AssertErrorAsync(
            await anonymous.CreateClient().DeleteAsync($"/api/v1/courses/{course.Id}"), HttpStatusCode.Unauthorized);
        await CrsApiTestSupport.AssertErrorAsync(
            await anonymous.CreateClient().GetAsync($"/api/v1/courses/deletion-jobs/{Guid.NewGuid()}"), HttpStatusCode.Unauthorized);

        Assert.Equal(0, await JobCountAsync(course.Id));
        await using var db = fixture.Database.CreateContext();
        Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
    }

    [Fact]
    public async Task CRS_FR_11_Status_of_pending_job_requires_course_delete_on_existing_course()
    {
        var course = await CreateCourseAsync();
        await using var owner = fixture.CreateFactory(settings: Idle);
        var job = await AcceptedAsync(await owner.CreateClient().DeleteAsync($"/api/v1/courses/{course.Id}"));

        // Course still exists: only course.delete in the course context counts, not ownership or other contexts.
        await using var otherUser = fixture.CreateFactory(settings: Idle, userId: 43,
            permission: (capability, context) => capability == "course.delete" && context.Level != ContextLevel.Course);
        await using var ownerWithoutGrant = fixture.CreateFactory(settings: Idle,
            permission: (capability, _) => capability != "course.delete");
        await using var courseManager = fixture.CreateFactory(settings: Idle, userId: 43,
            permission: (capability, context) => capability == "course.delete" && context == ContextRef.ForCourse(course.Id));

        await CrsApiTestSupport.AssertErrorAsync(await otherUser.CreateClient()
            .GetAsync($"/api/v1/courses/deletion-jobs/{job.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await CrsApiTestSupport.AssertErrorAsync(await ownerWithoutGrant.CreateClient()
            .GetAsync($"/api/v1/courses/deletion-jobs/{job.Id}"), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal("Pending", (await GetJobAsync(courseManager.CreateClient(), job.Id)).State);
        await CrsApiTestSupport.AssertErrorAsync(await courseManager.CreateClient()
            .GetAsync($"/api/v1/courses/deletion-jobs/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        await DrainAsync(job.Id);
    }

    [Fact]
    public async Task CRS_FR_11_Status_of_succeeded_job_is_visible_to_owner_and_site_or_category_managers_only()
    {
        var course = await CreateCourseAsync();
        await using var factory = fixture.CreateFactory(settings: Fast);
        var client = factory.CreateClient();
        var job = await AcceptedAsync(await client.DeleteAsync($"/api/v1/courses/{course.Id}"));
        await WaitForAsync(() => GetJobAsync(client, job.Id), x => x.State == "Succeeded", "job success");

        var category = new ContextRef(ContextLevel.CourseCategory, course.CategoryId);
        await using var owner = fixture.CreateFactory(permission: (_, _) => false); // Requester (42), no grants.
        await using var categoryManager = fixture.CreateFactory(userId: 43,
            permission: (capability, context) => capability == "course.delete" && context == category);
        await using var siteManager = fixture.CreateFactory(userId: 43,
            permission: (capability, context) => capability == "course.delete" && context == ContextRef.Site);
        await using var courseOnly = fixture.CreateFactory(userId: 43,
            permission: (capability, context) => capability == "course.delete" && context.Level == ContextLevel.Course);
        await using var otherCategory = fixture.CreateFactory(userId: 43, permission: (capability, context) =>
            capability == "course.delete" && context.Level == ContextLevel.CourseCategory && context != category);

        Assert.Equal("Succeeded", (await GetJobAsync(owner.CreateClient(), job.Id)).State);
        Assert.Equal("Succeeded", (await GetJobAsync(categoryManager.CreateClient(), job.Id)).State);
        Assert.Equal("Succeeded", (await GetJobAsync(siteManager.CreateClient(), job.Id)).State);
        await CrsApiTestSupport.AssertErrorAsync(await courseOnly.CreateClient()
            .GetAsync($"/api/v1/courses/deletion-jobs/{job.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await CrsApiTestSupport.AssertErrorAsync(await otherCategory.CreateClient()
            .GetAsync($"/api/v1/courses/deletion-jobs/{job.Id}"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task CRS_NFR_06_Pending_and_abandoned_running_jobs_survive_restart_and_are_processed_by_new_host()
    {
        var course = await CreateCourseAsync();
        Guid jobId;
        await using (var first = fixture.CreateFactory(settings: Idle))
        {
            jobId = (await AcceptedAsync(await first.CreateClient().DeleteAsync($"/api/v1/courses/{course.Id}"))).Id;
        } // Host stopped before its worker polled.

        await using (var db = fixture.Database.CreateContext())
        {
            // Simulate a crash in the middle of processing.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE crs_course_deletion_jobs SET "State" = 2, "Attempts" = 1, "StartedAt" = clock_timestamp()
                WHERE "Id" = {jobId} AND "State" = 1
                """);
            Assert.True(await db.Courses.AsNoTracking().AnyAsync(x => x.Id == course.Id));
        }

        await using var second = fixture.CreateFactory(settings: Fast);
        var client = second.CreateClient(); // Starts the second host and its worker on the same database.
        var done = await WaitForAsync(() => GetJobAsync(client, jobId), x => x.State is "Succeeded" or "Failed", "recovered job");

        Assert.Equal("Succeeded", done.State);
        Assert.Equal(2, done.Attempts); // The interrupted attempt is counted.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        Assert.Single(await EventsAsync(course.Id), x => x == "course.deleted");
    }

    private async Task DrainAsync(Guid jobId)
    {
        await using var factory = fixture.CreateFactory(settings: Fast);
        var client = factory.CreateClient();
        await WaitForAsync(() => GetJobAsync(client, jobId), x => x.State is "Succeeded" or "Failed", "cleanup");
    }
}
