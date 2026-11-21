using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Courses;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

// YZ destekli (Claude Code): CRS-FR-07 kayıt başlangıç/bitiş zamanı güncelleme PostgreSQL entegrasyon testleri.
public sealed class CrsFr07EnrolmentDatesPostgresTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2027, 1, 31, 17, 0, 0, TimeSpan.Zero);

    private async Task<(HttpClient Client, string Path, CourseEnrolment Enrolment)> CreateEnrolmentAsync(object body)
    {
        var client = fixture.CreateFactory().CreateClient();
        var category = await CreateCategoryAsync(client);
        var course = await CreateCourseAsync(client, category.Id);
        var path = $"/api/v1/courses/{course.Id}/enrolments";
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = (await response.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        return (client, $"{path}/{enrolment.Id}", enrolment);
    }

    private async Task<CourseEnrolment> StoredAsync(int id)
    {
        await using var db = fixture.Database.CreateContext();
        return await db.Enrolments.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task<CourseEnrolment> PatchOkAsync(HttpClient client, string path, object body)
    {
        var response = await client.PatchAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CourseEnrolment>())!;
    }

    [Fact]
    public async Task CRS_FR_07_PATCH_updates_start_and_end_and_keeps_unsent_fields()
    {
        var (client, path, enrolment) = await CreateEnrolmentAsync(new { userId = 101, role = "Teacher", suspended = true });

        var updated = await PatchOkAsync(client, path, new { startsAt = Start.ToOffset(TimeSpan.FromHours(3)), endsAt = End });
        Assert.Equal(Start, updated.StartsAt);
        Assert.Equal(End, updated.EndsAt);
        Assert.True(updated.Suspended);
        Assert.Equal("Teacher", updated.Role);

        // Only one date sent: the other date, status and role stay unchanged.
        var newEnd = End.AddDays(14);
        updated = await PatchOkAsync(client, path, new { endsAt = newEnd });
        Assert.Equal(Start, updated.StartsAt);
        Assert.Equal(newEnd, updated.EndsAt);

        var stored = await StoredAsync(enrolment.Id);
        Assert.Equal(Start, stored.StartsAt);
        Assert.Equal(newEnd, stored.EndsAt);
        Assert.True(stored.Suspended);
        Assert.Equal("Teacher", stored.Role);
        Assert.Equal(101, stored.UserId);
        Assert.Equal(enrolment.CourseId, stored.CourseId);

        // Explicit null clears a date, matching the course PATCH contract.
        updated = await PatchOkAsync(client, path, new { endsAt = (DateTimeOffset?)null });
        Assert.Equal(Start, updated.StartsAt);
        Assert.Null(updated.EndsAt);
        Assert.Null((await StoredAsync(enrolment.Id)).EndsAt);

        await using var db = fixture.Database.CreateContext();
        var changed = await db.Database.SqlQuery<string>($"""
            SELECT f AS "Value" FROM crs_events, unnest("ChangedFields") AS f
            WHERE "Kind" = 'enrolment.updated' AND "EnrolmentId" = {enrolment.Id}
            """).ToListAsync();
        Assert.Contains("StartsAt", changed); // Existing CRS-FR-10 events cover date changes.
        Assert.Contains("EndsAt", changed);
    }

    [Fact]
    public async Task CRS_FR_07_Existing_status_and_role_updates_keep_dates()
    {
        var (client, path, enrolment) = await CreateEnrolmentAsync(new { userId = 102, startsAt = Start, endsAt = End });

        var suspended = await PatchOkAsync(client, path, new { suspended = true });
        Assert.True(suspended.Suspended);
        var teacher = await PatchOkAsync(client, path, new { role = "Teacher" });
        Assert.True(teacher.Suspended);
        Assert.Equal("Teacher", teacher.Role);

        var stored = await StoredAsync(enrolment.Id);
        Assert.Equal(Start, stored.StartsAt);
        Assert.Equal(End, stored.EndsAt);
        Assert.True(stored.Suspended);
        Assert.Equal("Teacher", stored.Role);
    }

    [Fact]
    public async Task CRS_FR_07_Invalid_date_range_is_rejected_without_persisting_any_field()
    {
        var (client, path, enrolment) = await CreateEnrolmentAsync(new { userId = 103, startsAt = Start, endsAt = End });

        // End before start, both sent.
        await AssertErrorAsync(await client.PatchAsJsonAsync(path, new { startsAt = End, endsAt = Start, suspended = true }),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        // Equal start and end.
        await AssertErrorAsync(await client.PatchAsJsonAsync(path, new { endsAt = Start }),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        // Only start sent, but it is after the existing end.
        await AssertErrorAsync(await client.PatchAsJsonAsync(path, new { startsAt = End.AddDays(1), role = "Teacher" }),
            HttpStatusCode.UnprocessableEntity, "validation_failed");

        var stored = await StoredAsync(enrolment.Id);
        Assert.Equal(Start, stored.StartsAt);
        Assert.Equal(End, stored.EndsAt);
        Assert.False(stored.Suspended);
        Assert.Equal("Student", stored.Role);
    }

    [Fact]
    public async Task CRS_FR_07_Empty_patch_is_still_rejected()
    {
        var (client, path, _) = await CreateEnrolmentAsync(new { userId = 104 });
        await AssertErrorAsync(await client.PatchAsJsonAsync(path, new { }),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
    }
}
