using System.Net;
using Microsoft.EntityFrameworkCore;
using Moodle.Web.Crs;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

public sealed class CrsWebApiContractTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    [Fact]
    public async Task UI_HTTP_contracts_support_course_and_participant_workflow_on_PostgreSQL()
    {
        using var factory = fixture.CreateFactory();
        using var http = factory.CreateClient();
        var category = await CreateCategoryAsync(http);
        using var client = new CrsApiClient(factory.CreateClient());
        Assert.Contains(await client.CategoriesAsync(), x => x.Id == category.Id);
        var request = new CourseWriteRequest(category.Id, "Arayüz dersi", Guid.NewGuid().ToString(), null, true,
            new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(3)), null);
        var course = await client.CreateCourseAsync(request);
        Assert.Equal(request.FullName, (await client.CourseAsync(course.Id)).FullName);
        Assert.Contains(await client.CoursesAsync(), x => x.Id == course.Id);
        course = await client.UpdateCourseAsync(course.Id, request with { FullName = "Güncel ders", StartDate = null });
        Assert.Equal("Güncel ders", course.FullName);
        Assert.Null(course.StartDate);
        var enrolment = await client.EnrolAsync(course.Id, new(42, "Student", false, null, null));
        Assert.Contains(await client.EnrolmentsAsync(course.Id), x => x.UserId == 42);
        var suspended = await client.UpdateEnrolmentAsync(course.Id, enrolment.Id, new(true, null));
        Assert.True(suspended.Suspended);
        Assert.Equal("Student", suspended.Role);
        var teacher = await client.UpdateEnrolmentAsync(course.Id, enrolment.Id, new(null, "Teacher"));
        Assert.Equal("Teacher", teacher.Role);
        Assert.True(teacher.Suspended);
        await client.UnenrolAsync(course.Id, enrolment.Id);
        Assert.Empty(await client.EnrolmentsAsync(course.Id));
        await client.DeleteCourseAsync(course.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await Assert.ThrowsAsync<CrsApiException>(() => client.CourseAsync(course.Id))).Status);
    }

    [Fact]
    public async Task Status_only_UI_update_does_not_require_role_assignment_and_forbidden_role_is_not_saved()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var category = await CreateCategoryAsync(admin);
        using var adminClient = new CrsApiClient(factory.CreateClient());
        var course = await adminClient.CreateCourseAsync(new(category.Id, "Kayıt dersi", Guid.NewGuid().ToString(), null, true, null, null));
        var enrolment = await adminClient.EnrolAsync(course.Id, new(42, "Student", false, null, null));
        using var limitedFactory = fixture.CreateFactory(permission: (capability, _) => !capability.StartsWith("course.role.assign", StringComparison.Ordinal));
        using var limited = new CrsApiClient(limitedFactory.CreateClient());
        var updated = await limited.UpdateEnrolmentAsync(course.Id, enrolment.Id, new(true, null));
        Assert.True(updated.Suspended);
        var error = await Assert.ThrowsAsync<CrsApiException>(() => limited.UpdateEnrolmentAsync(course.Id, enrolment.Id, new(null, "Teacher")));
        Assert.Equal(HttpStatusCode.Forbidden, error.Status);
        await using var db = fixture.Database.CreateContext();
        Assert.Equal("Student", (await db.Enrolments.SingleAsync(x => x.Id == enrolment.Id)).Role);
    }
}
