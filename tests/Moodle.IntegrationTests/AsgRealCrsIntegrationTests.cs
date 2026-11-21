using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;

namespace Moodle.IntegrationTests;

public sealed class AsgRealLearnerTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Theory]
    [InlineData("Student", false, -1, 1, true)]
    [InlineData("Student", false, 1, 2, false)]
    [InlineData("Student", false, -2, -1, false)]
    [InlineData("Student", true, -1, 1, false)]
    [InlineData("EditingTeacher", false, -1, 1, false)]
    [InlineData("Teacher", false, -1, 1, false)]
    public async Task Real_crs_required_learners_honor_role_suspension_and_dates(string role, bool suspended, int starts, int ends, bool expected)
    {
        using var scope = fixture.Services.CreateScope();
        var crs = scope.ServiceProvider.GetRequiredService<CrsDbContext>();
        var user = await AuthPostgresFixture.SeedUserAsync(fixture, "audience-" + Guid.NewGuid().ToString("N"), "test-pass");
        crs.Enrolments.Add(new CourseEnrolment { CourseId = fixture.CourseId, UserId = user.Id,
            Role = role, Suspended = suspended, StartsAt = DateTimeOffset.UtcNow.AddDays(starts), EndsAt = DateTimeOffset.UtcNow.AddDays(ends) });
        await crs.SaveChangesAsync();
        var query = scope.ServiceProvider.GetRequiredService<IAssignmentLearnerQuery>();
        var ids = await query.ListRequiredLearnerIdsAsync(fixture.CourseId, default);
        Assert.Equal(expected, ids.Contains(user.Id));
        Assert.DoesNotContain(fixture.TeacherId, ids);
    }
}

public sealed class AsgRealDeletionWorkerTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Fact]
    public async Task Real_authenticated_delete_dispatches_to_asg_and_grd_workers()
    {
        var assignment = await fixture.ExistingAssignment();
        await AuthPostgresFixture.SeedUserAsync(fixture, "cleanup-manager", "test-pass", systemRoles: ["Manager"]);
        using var client = await fixture.Login("cleanup-manager");
        var response = await client.DeleteAsync($"/api/v1/courses/{fixture.CourseId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
            if (await db.DeletedCourses.AnyAsync(x => x.CourseId == fixture.CourseId, timeout.Token))
            {
                Assert.False(await db.Assignments.AnyAsync(x => x.Id == assignment.Id));
                break;
            }
            await Task.Delay(50, timeout.Token);
        }
    }
}
