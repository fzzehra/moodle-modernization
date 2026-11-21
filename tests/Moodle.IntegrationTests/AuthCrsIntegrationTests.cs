using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// YZ destekli (Claude Code): gerçek AUTH oturumu (Identity çerezi, AuthCurrentUser, AuthAccessPolicy,
/// AUTH-FR-09 koruması) ile CRS uçları. ICurrentUser/IAccessPolicy test yerine geçenlerle değiştirilmez;
/// ders bağlamındaki rol gerçek CRS kaydından (IEnrolmentQuery = CrsService) okunur.
/// AUTH-FR-02, 03, 07; CRS-FR-01, 04, 06, 09.
/// </summary>
public sealed class AuthCrsIntegrationTests(AuthPostgresFixture auth, CrsPostgresFixture crs)
    : IClassFixture<AuthPostgresFixture>, IClassFixture<CrsPostgresFixture>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private WebApplicationFactory<Program> Factory()
    {
        using var db = crs.CreateContext();
        var crsConnection = db.Database.GetConnectionString();
        return auth.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CrsDbContext>();
            services.RemoveAll<DbContextOptions<CrsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CrsDbContext>>();
            services.AddDbContext<CrsDbContext>(options => options.UseNpgsql(crsConnection));
        }));
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "parola1234" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Real_AUTH_manager_can_create_course_and_student_cannot()
    {
        var factory = Factory();
        var manager = Unique("yonetici");
        var student = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, manager, "parola1234", systemRoles: ["Manager"]);
        await AuthPostgresFixture.SeedUserAsync(factory, student, "parola1234");
        using var managerClient = await LoginAsync(factory, manager);
        using var studentClient = await LoginAsync(factory, student);

        // AUTH-FR-09 guard does not block a user without a pending password change.
        var category = await CreateCategoryAsync(managerClient);
        var course = await CreateCourseAsync(managerClient, category.Id); // Asserts 201 Created.

        var denied = await studentClient.PostAsJsonAsync("/api/v1/courses",
            new { categoryId = category.Id, fullName = "Denied", shortName = Unique("denied") });
        await AssertErrorAsync(denied, HttpStatusCode.Forbidden, "forbidden");
        await AssertErrorAsync(await studentClient.DeleteAsync($"/api/v1/courses/{course.Id}"), HttpStatusCode.Forbidden, "forbidden");

        // Visible courses remain readable for a signed-in student (course.view).
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync($"/api/v1/courses/{course.Id}")).StatusCode);
        await using var db = crs.CreateContext();
        Assert.False(await db.Courses.AnyAsync(x => x.FullName == "Denied"));
        Assert.True(await db.Courses.AnyAsync(x => x.Id == course.Id));
    }

    [Fact]
    public async Task Real_AUTH_course_role_comes_from_CRS_enrolment()
    {
        var factory = Factory();
        var manager = Unique("yonetici");
        var teacherName = Unique("ogretmen");
        var studentName = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, manager, "parola1234", systemRoles: ["Manager"]);
        var teacher = await AuthPostgresFixture.SeedUserAsync(factory, teacherName, "parola1234");
        var student = await AuthPostgresFixture.SeedUserAsync(factory, studentName, "parola1234");
        using var managerClient = await LoginAsync(factory, manager);
        var category = await CreateCategoryAsync(managerClient);
        var course = await CreateCourseAsync(managerClient, category.Id);
        var path = $"/api/v1/courses/{course.Id}/enrolments";
        Assert.Equal(HttpStatusCode.Created, (await managerClient.PostAsJsonAsync(path, new { userId = teacher.Id, role = "EditingTeacher" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await managerClient.PostAsJsonAsync(path, new { userId = student.Id, role = "Student" })).StatusCode);

        using var teacherClient = await LoginAsync(factory, teacherName);
        using var studentClient = await LoginAsync(factory, studentName);

        // AUTH-FR-03: editing teacher of this course may update it and see participants.
        var updated = await teacherClient.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new { fullName = "Updated by teacher" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Updated by teacher", (await updated.Content.ReadFromJsonAsync<Course>())!.FullName);
        Assert.Equal(HttpStatusCode.OK, (await teacherClient.GetAsync(path)).StatusCode);

        // A student enrolment grants neither course update nor participant management.
        await AssertErrorAsync(await studentClient.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new { fullName = "Student edit" }),
            HttpStatusCode.Forbidden, "forbidden");
        await AssertErrorAsync(await studentClient.PostAsJsonAsync(path, new { userId = 999_999 }), HttpStatusCode.Forbidden, "forbidden");

        // Roles are per course: the teacher has no rights in another course (AUTH-FR-07).
        var other = await CreateCourseAsync(managerClient, category.Id);
        await AssertErrorAsync(await teacherClient.PatchAsJsonAsync($"/api/v1/courses/{other.Id}", new { fullName = "Other" }),
            HttpStatusCode.Forbidden, "forbidden");

        // Suspending the CRS enrolment removes the course role immediately.
        var enrolments = await (await managerClient.GetAsync(path)).Content.ReadFromJsonAsync<List<CourseEnrolment>>();
        var teacherEnrolment = enrolments!.Single(x => x.UserId == teacher.Id);
        Assert.Equal(HttpStatusCode.OK, (await managerClient.PatchAsJsonAsync($"{path}/{teacherEnrolment.Id}", new { suspended = true })).StatusCode);
        await AssertErrorAsync(await teacherClient.PatchAsJsonAsync($"/api/v1/courses/{course.Id}", new { fullName = "After suspension" }),
            HttpStatusCode.Forbidden, "forbidden");
    }
}
