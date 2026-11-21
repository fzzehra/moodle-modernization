using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Courses;
using Moodle.Domain.Courses;

namespace Moodle.IntegrationTests;

public sealed class CourseSectionIntegrationTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Fact]
    public async Task ASG_FR_01_T_ASG_02_real_section_creation_ownership_and_authorization()
    {
        using var teacher = await fixture.Login("v2-teacher");
        using var student = await fixture.Login();
        var url = $"/api/v1/courses/{fixture.CourseId}/sections";
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync(url, new { name = "Forbidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsJsonAsync(url, new { name = " " })).StatusCode);
        var token = teacher.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
        teacher.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync(url, new { name = "No token" })).StatusCode);
        teacher.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await teacher.PostAsJsonAsync(url, new { name = "Real section" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var section = (await response.Content.ReadFromJsonAsync<CourseSection>())!;
        using var scope = fixture.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICourseSectionQuery>();
        Assert.True(await query.BelongsToCourseAsync(section.Id, fixture.CourseId));
        Assert.False(await query.BelongsToCourseAsync(section.Id, fixture.CourseId + 100000));
        Assert.False(await query.BelongsToCourseAsync(int.MaxValue, fixture.CourseId));
        object Body(int id) => new { sectionId = id, title = "Real assignment", requiresConfirmation = true, enabledSubmissionTypes = new[] { "text", "file" } };
        var assignments = $"/api/v1/courses/{fixture.CourseId}/assignments";
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync(assignments, Body(section.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsJsonAsync(assignments, Body(int.MaxValue))).StatusCode);
        var created = await teacher.PostAsJsonAsync(assignments, Body(section.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var assignment = (await created.Content.ReadFromJsonAsync<Moodle.Domain.Assignments.Assignment>())!;
        Assert.Equal(section.Id, assignment.SectionId);
        Assert.Contains("Real assignment", await student.GetStringAsync(assignments));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync($"/api/v1/courses/{fixture.CourseId + 100000}/sections", new { name = "Other" })).StatusCode);
        var crs = scope.ServiceProvider.GetRequiredService<CrsDbContext>();
        var category = await crs.Courses.Where(x => x.Id == fixture.CourseId).Select(x => x.CategoryId).SingleAsync();
        var other = new Course { CategoryId = category, FullName = "Other", ShortName = Guid.NewGuid().ToString("N") };
        crs.Courses.Add(other); await crs.SaveChangesAsync();
        var foreign = await new CourseSectionService(crs).CreateAsync(other.Id, "Other section", default);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsJsonAsync(assignments, Body(foreign.Id))).StatusCode);
        Assert.False(await query.BelongsToCourseAsync(foreign.Id, fixture.CourseId));
        await crs.Courses.Where(x => x.Id == other.Id).ExecuteDeleteAsync();
        Assert.False(await query.BelongsToCourseAsync(foreign.Id, other.Id));
    }
}
