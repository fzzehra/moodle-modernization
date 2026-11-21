using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Api.Auth;
using Moodle.Application.Abstractions;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// AUTH-FR-09 / AUTH-BR-06 (T-AUTH-09), YZ destekli (Claude Code): zorunlu parola değişikliği gerçek
/// PostgreSQL, gerçek ASP.NET Core Identity ve gerçek HTTP çereziyle sınanır.
/// </summary>
public sealed class AuthFr09ForcedPasswordTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    // Matches no endpoint and touches no database: 403 while the change is pending, 404 afterwards.
    private const string Probe = "/api/v1/fr09-probe";
    private const string Capabilities = "/api/v1/auth/permissions?courseId=1&capabilities=course.create,category.manage";

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>The isolated AUTH fixture has no CRS database; course roles are never needed here.</summary>
    private sealed class NoEnrolmentQuery : IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private WebApplicationFactory<Program> Factory() =>
        fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEnrolmentQuery>();
            services.AddScoped<IEnrolmentQuery, NoEnrolmentQuery>();
        }));

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task<IReadOnlyList<string>> GrantedAsync(HttpClient client)
    {
        var response = await client.GetAsync(Capabilities);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PermissionsResponse>())!.Permissions.OrderBy(x => x).ToList();
    }

    [Fact]
    public async Task AUTH_FR_09_Required_change_blocks_protected_API_until_password_is_changed()
    {
        var factory = Factory();
        var username = Unique("ogrenci10");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "eskiParola1",
            systemRoles: ["Manager"], requirePasswordChange: true);
        using var client = factory.CreateClient();

        // T-AUTH-09: login succeeds but tells the client to start the password change flow.
        Assert.True((await LoginAsync(client, username, "eskiParola1")).RequiresPasswordChange);

        // AUTH endpoints needed to finish the flow stay available.
        var session = (await (await client.GetAsync("/api/v1/auth/session")).Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.True(session.Authenticated);

        // Other API operations are rejected before reaching modules, including owner-only FILE endpoints.
        foreach (var (method, path) in new[]
                 {
                     ("GET", "/api/v1/courses"), ("POST", "/api/v1/categories"),
                     ("GET", "/api/v1/users/me/quota"), ("GET", Probe),
                 })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method == "POST") request.Content = JsonContent.Create(new { name = "Blocked" });
            await AssertErrorAsync(await client.SendAsync(request), HttpStatusCode.Forbidden, "password_change_required");
        }
        // The real access policy grants nothing either, even to a Manager.
        Assert.Empty(await GrantedAsync(client));

        // A failed change keeps the requirement.
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "yanlis", newPassword = "yeniParola1" }), HttpStatusCode.BadRequest, "current_password_invalid");
        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.Forbidden, "password_change_required");

        // Successful change through the existing endpoint clears the flag and opens normal access in the same session.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "eskiParola1", newPassword = "yeniParola1" })).StatusCode);
        await using (var db = fixture.CreateContext())
            Assert.False((await db.Users.AsNoTracking().SingleAsync(x => x.UserName == username)).RequirePasswordChange);
        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.NotFound);
        Assert.Equal(["category.manage", "course.create"], await GrantedAsync(client));

        // Later logins no longer require a change.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.False((await LoginAsync(client, username, "yeniParola1")).RequiresPasswordChange);
        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AUTH_FR_09_User_without_requirement_is_not_blocked()
    {
        var factory = Factory();
        var username = Unique("yonetici");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234", systemRoles: ["Manager"]);
        using var client = factory.CreateClient();

        Assert.False((await LoginAsync(client, username, "parola1234")).RequiresPasswordChange);
        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.NotFound);
        Assert.Equal(["category.manage", "course.create"], await GrantedAsync(client));
    }

    [Fact]
    public async Task AUTH_FR_09_Requirement_set_during_an_open_session_takes_effect_without_new_login()
    {
        var factory = Factory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234");
        using var client = factory.CreateClient();
        await LoginAsync(client, username, "parola1234");
        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.NotFound);

        await using (var db = fixture.CreateContext())
        {
            var user = await db.Users.SingleAsync(x => x.UserName == username);
            user.RequirePasswordChange = true;
            await db.SaveChangesAsync();
        }

        await AssertErrorAsync(await client.GetAsync(Probe), HttpStatusCode.Forbidden, "password_change_required");
    }

    [Fact]
    public async Task AUTH_FR_09_Anonymous_requests_still_receive_the_endpoints_own_401()
    {
        using var client = Factory().CreateClient();
        await AssertErrorAsync(await client.GetAsync("/api/v1/users/me/quota"), HttpStatusCode.Unauthorized, "not_authenticated");
    }
}
