using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Domain.Courses;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

public sealed class CrsEndpointTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    public static IEnumerable<object[]> ProtectedRoutes() => Routes();
    public static IEnumerable<object[]> DeniedRoutes() => Routes().Where(row =>
        (string)row[1] != "/categories" || (string)row[0] != "GET").Where(row =>
        (string)row[1] != "/courses" || (string)row[0] != "GET");

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Every_endpoint_requires_a_session(string method, string path, string? body)
    {
        using var factory = fixture.CreateFactory(authenticated: false);
        using var client = factory.CreateClient();
        using var response = await SendAsync(client, method, path, body);
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "not_authenticated");
    }

    [Theory]
    [MemberData(nameof(DeniedRoutes))]
    public async Task Resource_operations_require_capabilities(string method, string path, string? body)
    {
        using var factory = fixture.CreateFactory(permission: (_, _) => false);
        using var client = factory.CreateClient();
        using var response = await SendAsync(client, method, path, body);
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Categories_support_CRUD_parent_changes_and_integrity_checks()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var parent = await CreateCategoryAsync(client);
        var child = await CreateCategoryAsync(client, parent.Id);
        var detail = await client.GetFromJsonAsync<CourseCategory>($"/api/v1/categories/{child.Id}");
        Assert.Equal(parent.Id, detail!.ParentId);
        Assert.Contains((await client.GetFromJsonAsync<List<CourseCategory>>("/api/v1/categories"))!, x => x.Id == child.Id);

        var changed = await client.PatchAsJsonAsync($"/api/v1/categories/{child.Id}", new { name = "Updated child" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var category = (await changed.Content.ReadFromJsonAsync<CourseCategory>())!;
        Assert.Equal("Updated child", category.Name);
        Assert.Equal(parent.Id, category.ParentId);
        await AssertErrorAsync(await client.DeleteAsync($"/api/v1/categories/{parent.Id}"), HttpStatusCode.Conflict, "category_not_empty");
        await AssertErrorAsync(await client.PatchAsJsonAsync($"/api/v1/categories/{parent.Id}", new { parentId = child.Id }),
            HttpStatusCode.UnprocessableEntity, "validation_failed");

        var detached = await client.PatchAsJsonAsync($"/api/v1/categories/{child.Id}", new { parentId = (int?)null });
        Assert.Equal(HttpStatusCode.OK, detached.StatusCode);
        Assert.Null((await detached.Content.ReadFromJsonAsync<CourseCategory>())!.ParentId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/categories/{child.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/categories/{parent.Id}")).StatusCode);
        await AssertErrorAsync(await client.GetAsync($"/api/v1/categories/{child.Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Courses_support_CRUD_partial_updates_null_clearing_and_offset_dates()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var shortName = Guid.NewGuid().ToString();
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var response = await client.PostAsJsonAsync("/api/v1/courses", new
        {
            categoryId = category.Id, fullName = "Original course", shortName,
            idNumber = Guid.NewGuid().ToString(), startDate = start, endDate = start.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = (await response.Content.ReadFromJsonAsync<Course>())!;
        Assert.Equal(start.ToUniversalTime(), course.StartDate);
        Assert.Equal(TimeSpan.Zero, course.StartDate!.Value.Offset);
        Assert.Equal($"/api/v1/courses/{course.Id}", response.Headers.Location!.OriginalString);
        Assert.Contains((await client.GetFromJsonAsync<List<Course>>("/api/v1/courses"))!, x => x.Id == course.Id);
        Assert.Equal(shortName, (await client.GetFromJsonAsync<Course>($"/api/v1/courses/{course.Id}"))!.ShortName);

        var patch = await client.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new { fullName = "Updated course" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = (await patch.Content.ReadFromJsonAsync<Course>())!;
        Assert.Equal("Updated course", updated.FullName);
        Assert.Equal(course.IdNumber, updated.IdNumber);
        Assert.Equal(course.StartDate, updated.StartDate);
        Assert.Equal(shortName, updated.ShortName);
        var cleared = await client.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new
        {
            idNumber = (string?)null, startDate = (DateTimeOffset?)null, endDate = (DateTimeOffset?)null, visible = false
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        updated = (await cleared.Content.ReadFromJsonAsync<Course>())!;
        Assert.Null(updated.IdNumber);
        Assert.Null(updated.StartDate);
        Assert.Null(updated.EndDate);
        Assert.False(updated.Visible);
        await AssertErrorAsync(await client.DeleteAsync($"/api/v1/categories/{category.Id}"), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/v1/courses/{course.Id}/enrolments", new { userId = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        await using var db = fixture.Database.CreateContext();
        Assert.False(await db.Enrolments.AnyAsync(x => x.CourseId == course.Id));
        await AssertErrorAsync(await client.GetAsync($"/api/v1/courses/{course.Id}"), HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/categories/{category.Id}")).StatusCode);
    }

    [Fact]
    public async Task Enrolments_support_create_read_status_role_updates_and_unenrolment()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var course = await CreateCourseAsync(client, category.Id);
        var path = $"/api/v1/courses/{course.Id}/enrolments";
        var response = await client.PostAsJsonAsync(path, new { userId = 99 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = (await response.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        Assert.Equal($"{path}/{enrolment.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Student", (await client.GetFromJsonAsync<CourseEnrolment>(response.Headers.Location))!.Role);
        Assert.Single((await client.GetFromJsonAsync<List<CourseEnrolment>>(path))!);

        var suspended = await client.PatchAsJsonAsync($"{path}/{enrolment.Id}", new { suspended = true });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var updated = (await suspended.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        Assert.True(updated.Suspended);
        Assert.Equal("Student", updated.Role);
        var role = await client.PatchAsJsonAsync($"{path}/{enrolment.Id}", new { role = "Teacher" });
        Assert.Equal(HttpStatusCode.OK, role.StatusCode);
        updated = (await role.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        Assert.True(updated.Suspended);
        Assert.Equal("Teacher", updated.Role);
        await AssertErrorAsync(await client.PostAsJsonAsync(path, new { userId = 99 }), HttpStatusCode.Conflict, "enrolment_conflict");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{path}/{enrolment.Id}")).StatusCode);
        await AssertErrorAsync(await client.GetAsync($"{path}/{enrolment.Id}"), HttpStatusCode.NotFound);
        Assert.Empty((await client.GetFromJsonAsync<List<CourseEnrolment>>(path))!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_course_identifiers_return_conflict(bool duplicateIdNumber)
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var shortName = Guid.NewGuid().ToString();
        var idNumber = Guid.NewGuid().ToString();
        var first = await client.PostAsJsonAsync("/api/v1/courses", new { categoryId = category.Id, fullName = "Course", shortName, idNumber });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/courses", new
        {
            categoryId = category.Id, fullName = "Duplicate",
            shortName = duplicateIdNumber ? Guid.NewGuid().ToString() : shortName,
            idNumber = duplicateIdNumber ? idNumber : null
        });
        await AssertErrorAsync(response, HttpStatusCode.Conflict,
            duplicateIdNumber ? "course_id_number_conflict" : "course_short_name_conflict");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("control")]
    [InlineData("length")]
    [InlineData("dates")]
    [InlineData("category")]
    public async Task Invalid_course_input_is_rejected_without_persisting(string invalidField)
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var shortName = invalidField == "control" ? "Bad\tname" : invalidField == "length" ? new string('x', 101) : Guid.NewGuid().ToString();
        var response = await client.PostAsJsonAsync("/api/v1/courses", new
        {
            categoryId = invalidField == "category" ? int.MaxValue : category.Id,
            fullName = invalidField == "name" ? " " : "Course", shortName,
            startDate = DateTimeOffset.UtcNow, endDate = DateTimeOffset.UtcNow.AddDays(invalidField == "dates" ? -1 : 1)
        });
        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        await using var db = fixture.Database.CreateContext();
        Assert.False(await db.Courses.AnyAsync(x => x.CategoryId == category.Id));
    }

    [Theory]
    [InlineData("POST", "/categories", "{")]
    [InlineData("POST", "/courses", "null")]
    [InlineData("POST", "/courses", "{\"categoryId\":\"invalid\"}")]
    [InlineData("PATCH", "/courses/1", "{\"fullName\":null}")]
    [InlineData("PATCH", "/categories/1", "{\"name\":null}")]
    [InlineData("PATCH", "/courses/1/enrolments/1", "{\"suspended\":null}")]
    [InlineData("POST", "/categories", "{\"id\":123,\"name\":\"Injected ID\"}")]
    public async Task Malformed_or_unmapped_input_returns_standard_bad_request(string method, string path, string body)
    {
        foreach (var environment in new[] { "Development", "Production" })
        {
            using var factory = fixture.CreateFactory(environment: environment);
            using var client = factory.CreateClient();
            await AssertErrorAsync(await SendAsync(client, method, path, body), HttpStatusCode.BadRequest, "bad_request");
        }
    }

    [Theory]
    [InlineData("GET", "/categories/2147483647", null)]
    [InlineData("PATCH", "/categories/2147483647", "{\"name\":\"Updated\"}")]
    [InlineData("DELETE", "/categories/2147483647", null)]
    [InlineData("GET", "/courses/2147483647", null)]
    [InlineData("PATCH", "/courses/2147483647", "{\"fullName\":\"Updated\"}")]
    [InlineData("DELETE", "/courses/2147483647", null)]
    [InlineData("GET", "/courses/2147483647/enrolments", null)]
    [InlineData("POST", "/courses/2147483647/enrolments", "{\"userId\":99}")]
    [InlineData("GET", "/courses/2147483647/enrolments/2147483647", null)]
    [InlineData("PATCH", "/courses/2147483647/enrolments/2147483647", "{\"suspended\":true}")]
    [InlineData("DELETE", "/courses/2147483647/enrolments/2147483647", null)]
    public async Task Missing_resources_return_not_found(string method, string path, string? body)
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        await AssertErrorAsync(await SendAsync(client, method, path, body), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Enrolment_cannot_be_read_or_changed_through_another_course()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var category = await CreateCategoryAsync(admin);
        var first = await CreateCourseAsync(admin, category.Id);
        var second = await CreateCourseAsync(admin, category.Id);
        var response = await admin.PostAsJsonAsync($"/api/v1/courses/{first.Id}/enrolments", new { userId = 99 });
        var enrolment = (await response.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        using var limitedFactory = fixture.CreateFactory(permission: (_, context) => context == ContextRef.ForCourse(second.Id));
        using var limited = limitedFactory.CreateClient();
        var wrongPath = $"/api/v1/courses/{second.Id}/enrolments/{enrolment.Id}";
        await AssertErrorAsync(await limited.GetAsync(wrongPath), HttpStatusCode.NotFound);
        await AssertErrorAsync(await limited.PatchAsJsonAsync(wrongPath, new { suspended = true, role = "Teacher" }), HttpStatusCode.NotFound);
        await AssertErrorAsync(await limited.DeleteAsync(wrongPath), HttpStatusCode.NotFound);
        await using var db = fixture.Database.CreateContext();
        var unchanged = await db.Enrolments.SingleAsync(x => x.Id == enrolment.Id);
        Assert.False(unchanged.Suspended);
        Assert.Equal("Student", unchanged.Role);
    }

    [Theory]
    [InlineData("course.role.assign")]
    [InlineData("course.role.assign:Teacher")]
    public async Task Missing_role_permission_blocks_creation_and_entire_update(string deniedCapability)
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var category = await CreateCategoryAsync(admin);
        var course = await CreateCourseAsync(admin, category.Id);
        var path = $"/api/v1/courses/{course.Id}/enrolments";
        var response = await admin.PostAsJsonAsync(path, new { userId = 99 });
        var enrolment = (await response.Content.ReadFromJsonAsync<CourseEnrolment>())!;
        using var limitedFactory = fixture.CreateFactory(permission: (capability, _) => capability != deniedCapability);
        using var limited = limitedFactory.CreateClient();
        await AssertErrorAsync(await limited.PostAsJsonAsync(path, new { userId = 100, role = "Teacher" }), HttpStatusCode.Forbidden);
        await AssertErrorAsync(await limited.PatchAsJsonAsync($"{path}/{enrolment.Id}", new { suspended = true, role = "Teacher" }), HttpStatusCode.Forbidden);
        await using var db = fixture.Database.CreateContext();
        Assert.False(await db.Enrolments.AnyAsync(x => x.CourseId == course.Id && x.UserId == 100));
        var unchanged = await db.Enrolments.SingleAsync(x => x.Id == enrolment.Id);
        Assert.False(unchanged.Suspended);
        Assert.Equal("Student", unchanged.Role);
        Assert.Equal(HttpStatusCode.OK, (await limited.PatchAsJsonAsync($"{path}/{enrolment.Id}", new { suspended = true })).StatusCode);
    }

    [Fact]
    public async Task Lists_filter_unpermitted_resources_and_hidden_courses()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var allowedCategory = await CreateCategoryAsync(admin);
        var otherCategory = await CreateCategoryAsync(admin);
        var visible = await CreateCourseAsync(admin, allowedCategory.Id);
        var hidden = await CreateCourseAsync(admin, allowedCategory.Id, visible: false);
        var other = await CreateCourseAsync(admin, otherCategory.Id);
        using var limitedFactory = fixture.CreateFactory(permission: (capability, context) =>
            capability == "category.view" && context == new ContextRef(ContextLevel.CourseCategory, allowedCategory.Id)
            || capability == "course.view" && (context == ContextRef.ForCourse(visible.Id) || context == ContextRef.ForCourse(hidden.Id)));
        using var limited = limitedFactory.CreateClient();
        Assert.Equal(visible.Id, Assert.Single((await limited.GetFromJsonAsync<List<Course>>("/api/v1/courses"))!).Id);
        Assert.Equal(allowedCategory.Id, Assert.Single((await limited.GetFromJsonAsync<List<CourseCategory>>("/api/v1/categories"))!).Id);
        await AssertErrorAsync(await limited.GetAsync($"/api/v1/courses/{hidden.Id}"), HttpStatusCode.Forbidden);
        await AssertErrorAsync(await limited.GetAsync($"/api/v1/courses/{other.Id}"), HttpStatusCode.Forbidden);
        await AssertErrorAsync(await limited.GetAsync($"/api/v1/categories/{otherCategory.Id}"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Moving_resources_requires_permission_on_the_destination()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var source = await CreateCategoryAsync(admin);
        var target = await CreateCategoryAsync(admin);
        var course = await CreateCourseAsync(admin, source.Id);
        using var limitedFactory = fixture.CreateFactory(permission: (_, context) =>
            context != new ContextRef(ContextLevel.CourseCategory, target.Id));
        using var limited = limitedFactory.CreateClient();
        await AssertErrorAsync(await limited.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new
        { categoryId = target.Id, fullName = "Forbidden move" }), HttpStatusCode.Forbidden);
        await AssertErrorAsync(await limited.PatchAsJsonAsync($"/api/v1/categories/{source.Id}", new
        { parentId = target.Id, name = "Forbidden move" }), HttpStatusCode.Forbidden);
        await using var db = fixture.Database.CreateContext();
        var unchangedCourse = await db.Courses.SingleAsync(x => x.Id == course.Id);
        Assert.Equal(source.Id, unchangedCourse.CategoryId);
        Assert.Equal(course.FullName, unchangedCourse.FullName);
        var unchangedCategory = await db.Categories.SingleAsync(x => x.Id == source.Id);
        Assert.Null(unchangedCategory.ParentId);
        Assert.Equal(source.Name, unchangedCategory.Name);
    }

    [Theory]
    [InlineData("category-name")]
    [InlineData("category-parent")]
    [InlineData("category-length")]
    [InlineData("category-empty-patch")]
    [InlineData("course-empty-patch")]
    [InlineData("enrolment-user")]
    [InlineData("enrolment-role")]
    [InlineData("enrolment-role-length")]
    [InlineData("enrolment-dates")]
    [InlineData("enrolment-empty-patch")]
    public async Task Invalid_category_enrolment_or_empty_patch_returns_validation_error(string scenario)
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var course = await CreateCourseAsync(client, category.Id);
        var path = $"/api/v1/courses/{course.Id}/enrolments";
        HttpResponseMessage response;
        switch (scenario)
        {
            case "category-name":
                response = await client.PostAsJsonAsync("/api/v1/categories", new { name = "\t" });
                break;
            case "category-parent":
                response = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Invalid parent", parentId = int.MaxValue });
                break;
            case "category-length":
                response = await client.PostAsJsonAsync("/api/v1/categories", new { name = new string('x', 256) });
                break;
            case "category-empty-patch":
                response = await client.PatchAsJsonAsync($"/api/v1/categories/{category.Id}", new { });
                break;
            case "course-empty-patch":
                response = await client.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new { });
                break;
            case "enrolment-empty-patch":
                var created = await client.PostAsJsonAsync(path, new { userId = 99 });
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                var enrolment = (await created.Content.ReadFromJsonAsync<CourseEnrolment>())!;
                response = await client.PatchAsJsonAsync($"{path}/{enrolment.Id}", new { });
                break;
            default:
                response = await client.PostAsJsonAsync(path, new
                {
                    userId = scenario == "enrolment-user" ? 0 : 99,
                    role = scenario == "enrolment-role" ? "\t" : scenario == "enrolment-role-length" ? new string('x', 51) : "Student",
                    startsAt = DateTimeOffset.UtcNow,
                    endsAt = DateTimeOffset.UtcNow.AddDays(scenario == "enrolment-dates" ? -1 : 1)
                });
                break;
        }
        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
    }
}
