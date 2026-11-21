using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.IntegrationTests;

public sealed class CrsApiFixture : IAsyncLifetime
{
    public CrsPostgresFixture Database { get; } = new();
    private readonly WebApplicationFactory<Program> _root = new();

    public Task InitializeAsync() => Database.InitializeAsync();

    public WebApplicationFactory<Program> CreateFactory(
        bool authenticated = true, Func<string, ContextRef, bool>? permission = null,
        string environment = "Production", IReadOnlyDictionary<string, string?>? settings = null, int userId = 42)
    {
        using var db = Database.CreateContext();
        var connectionString = db.Database.GetConnectionString();
        return _root.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
                builder.UseSetting(key, value);
            builder.UseEnvironment(environment).ConfigureTestServices(services =>
            {
                services.RemoveAll<CrsDbContext>();
                services.RemoveAll<DbContextOptions<CrsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CrsDbContext>>();
                services.AddDbContext<CrsDbContext>(options => options.UseNpgsql(connectionString));
                services.RemoveAll<ICurrentUser>();
                services.RemoveAll<IAccessPolicy>();
                // These stand-ins are registered only in the test host, never in Program.cs.
                services.AddSingleton<ICurrentUser>(new TestUser(authenticated, userId));
                services.AddSingleton<IAccessPolicy>(new TestPolicy(userId, permission ?? ((_, _) => true)));
            });
        });
    }

    public async Task DisposeAsync()
    {
        await _root.DisposeAsync();
        await Database.DisposeAsync();
    }

    private sealed class TestUser(bool authenticated, int userId) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public int? UserId => authenticated ? userId : null;
        public string? Username => authenticated ? "crs-api-test" : null;
    }

    private sealed class TestPolicy(int expectedUserId, Func<string, ContextRef, bool> permission) : IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default) =>
            Task.FromResult(userId == expectedUserId && permission(capability, context));
    }
}

public static class CrsApiTestSupport
{
    public static IEnumerable<object[]> Routes()
    {
        yield return ["GET", "/categories", null!];
        yield return ["GET", "/categories/1", null!];
        yield return ["POST", "/categories", "{\"name\":\"Category\"}"];
        yield return ["PATCH", "/categories/1", "{\"name\":\"Updated\"}"];
        yield return ["DELETE", "/categories/1", null!];
        yield return ["GET", "/courses", null!];
        yield return ["GET", "/courses/1", null!];
        yield return ["POST", "/courses", "{\"categoryId\":1,\"fullName\":\"Course\",\"shortName\":\"C1\"}"];
        yield return ["PATCH", "/courses/1", "{\"fullName\":\"Updated\"}"];
        yield return ["DELETE", "/courses/1", null!];
        yield return ["GET", "/courses/1/enrolments", null!];
        yield return ["GET", "/courses/1/enrolments/1", null!];
        yield return ["POST", "/courses/1/enrolments", "{\"userId\":99}"];
        yield return ["PATCH", "/courses/1/enrolments/1", "{\"suspended\":true}"];
        yield return ["DELETE", "/courses/1/enrolments/1", null!];
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1" + path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var error = json.RootElement.GetProperty("error");
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        if (code is not null) Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("   at ", body);
    }

    public static async Task<CourseCategory> CreateCategoryAsync(HttpClient client, int? parentId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Category " + Guid.NewGuid(), parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = (await response.Content.ReadFromJsonAsync<CourseCategory>())!;
        Assert.Equal($"/api/v1/categories/{category.Id}", response.Headers.Location?.OriginalString);
        return category;
    }

    public static async Task<Course> CreateCourseAsync(HttpClient client, int categoryId, bool visible = true)
    {
        var response = await client.PostAsJsonAsync("/api/v1/courses", new
        {
            categoryId, fullName = "Course " + Guid.NewGuid(), shortName = Guid.NewGuid().ToString(), visible
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = (await response.Content.ReadFromJsonAsync<Course>())!;
        Assert.Equal($"/api/v1/courses/{course.Id}", response.Headers.Location?.OriginalString);
        return course;
    }
}
