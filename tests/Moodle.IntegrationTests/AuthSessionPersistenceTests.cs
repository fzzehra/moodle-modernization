using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Api.Auth;

namespace Moodle.IntegrationTests;

/// <summary>
/// YZ destekli (Claude Code), AUTH-NFR-03 (aday): oturum kalıcılığı ve güvenlik damgası. Gerçek PostgreSQL,
/// gerçek Identity ve gerçek oturum çereziyle. Çerezler bir host'tan diğerine elle taşınır (HandleCookies=false).
/// </summary>
public sealed class AuthSessionPersistenceTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private const string CookieName = ".AspNetCore.Identity.Application";
    private const string Password = "parola1234";
    private static readonly WebApplicationFactoryClientOptions NoCookies = new() { HandleCookies = false };

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static IEnumerable<string> SessionSetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(x => x.StartsWith(CookieName, StringComparison.Ordinal))
            : [];

    /// <summary>Cookie request header value for the session (also handles a chunked cookie).</summary>
    private static string? SessionCookie(HttpResponseMessage response)
    {
        var pairs = SessionSetCookies(response).Select(x => x.Split(';')[0]).Where(x => !x.EndsWith('=')).ToList();
        return pairs.Count == 0 ? null : string.Join("; ", pairs);
    }

    private static async Task<(string Cookie, HttpResponseMessage Response)> LoginAsync(WebApplicationFactory<Program> factory, string username)
    {
        using var client = factory.CreateClient(NoCookies);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (SessionCookie(response) ?? throw new InvalidOperationException("No session cookie issued."), response);
    }

    private static async Task<HttpResponseMessage> SendAsync(WebApplicationFactory<Program> factory, HttpMethod method,
        string path, string cookie, object? body = null)
    {
        using var client = factory.CreateClient(NoCookies);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<SessionResponse> SessionAsync(WebApplicationFactory<Program> factory, string cookie)
    {
        var response = await SendAsync(factory, HttpMethod.Get, "/api/v1/auth/session", cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private WebApplicationFactory<Program> FactoryWithKeys(string keysPath) =>
        fixture.CreateFactory().WithWebHostBuilder(builder => builder.UseSetting("DataProtection:KeysPath", keysPath));

    private WebApplicationFactory<Program> FactoryValidatingEveryRequest() =>
        fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero)));

    [Fact]
    public async Task AUTH_NFR_03_Login_issues_a_persistent_14_day_http_only_cookie()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, Password);

        var (_, response) = await LoginAsync(factory, username);

        var header = SessionSetCookies(response).First();
        var attributes = header.Split(';').Select(x => x.Trim()).ToList();
        Assert.Contains(attributes, x => x.Equals("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, x => x.Equals("samesite=lax", StringComparison.OrdinalIgnoreCase));
        var expires = attributes.Single(x => x.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        var expiresAt = DateTimeOffset.Parse(expires["expires=".Length..], CultureInfo.InvariantCulture);
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        Assert.InRange(remaining, TimeSpan.FromDays(14) - TimeSpan.FromMinutes(5), TimeSpan.FromDays(14) + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AUTH_NFR_03_Session_survives_restart_when_keys_are_persisted_and_is_rejected_with_other_keys()
    {
        var keys = Path.Combine(Path.GetTempPath(), "moodle-dp-" + Guid.NewGuid().ToString("N"));
        var otherKeys = Path.Combine(Path.GetTempPath(), "moodle-dp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var username = Unique("ogrenci");
            string cookie;
            await using (var first = FactoryWithKeys(keys))
            {
                await AuthPostgresFixture.SeedUserAsync(first, username, Password);
                (cookie, _) = await LoginAsync(first, username);
                Assert.True((await SessionAsync(first, cookie)).Authenticated);
            } // First host stopped: in-memory key ring is gone.
            Assert.NotEmpty(Directory.GetFiles(keys, "*.xml"));

            await using (var restarted = FactoryWithKeys(keys))
            {
                var session = await SessionAsync(restarted, cookie);
                Assert.True(session.Authenticated);
                Assert.Equal(username, session.Username);
            }

            // Control: the same cookie is not readable by a host whose key directory is empty (lost keys).
            await using var withLostKeys = FactoryWithKeys(otherKeys);
            Assert.False((await SessionAsync(withLostKeys, cookie)).Authenticated);
        }
        finally
        {
            foreach (var path in new[] { keys, otherKeys })
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task AUTH_NFR_03_Password_change_refreshes_own_session_and_old_sessions_are_rejected_on_validation()
    {
        var factory = FactoryValidatingEveryRequest();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, Password);
        var (current, _) = await LoginAsync(factory, username);
        var (otherDevice, _) = await LoginAsync(factory, username);

        var change = await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/change-password", current,
            new { currentPassword = Password, newPassword = "yeniParola1" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // The changing session receives a re-issued, still persistent cookie carrying the new security stamp.
        var refreshed = SessionCookie(change);
        Assert.NotNull(refreshed);
        Assert.Contains(SessionSetCookies(change).First().Split(';'), x => x.Trim().StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.True((await SessionAsync(factory, refreshed!)).Authenticated);

        // Cookies carrying the old stamp (another device, and the pre-change cookie itself) are rejected.
        Assert.False((await SessionAsync(factory, otherDevice)).Authenticated);
        Assert.False((await SessionAsync(factory, current)).Authenticated);
    }

    [Fact]
    public async Task AUTH_NFR_03_With_default_interval_other_sessions_are_not_rejected_immediately()
    {
        // Documents the real behaviour: revalidation happens after the 30-minute interval, not instantly.
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, Password);
        var (current, _) = await LoginAsync(factory, username);
        var (otherDevice, _) = await LoginAsync(factory, username);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/change-password",
            current, new { currentPassword = Password, newPassword = "yeniParola1" })).StatusCode);

        Assert.True((await SessionAsync(factory, otherDevice)).Authenticated);
    }

    [Fact]
    public async Task AUTH_FR_09_Required_change_still_unblocks_the_same_session_with_stamp_validation()
    {
        var factory = FactoryValidatingEveryRequest();
        var username = Unique("ogrenci10");
        await AuthPostgresFixture.SeedUserAsync(factory, username, Password, requirePasswordChange: true);
        var (cookie, login) = await LoginAsync(factory, username);
        Assert.True((await login.Content.ReadFromJsonAsync<LoginResponse>())!.RequiresPasswordChange);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(factory, HttpMethod.Get, "/api/v1/fr09-probe", cookie)).StatusCode);

        var change = await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/change-password", cookie,
            new { currentPassword = Password, newPassword = "yeniParola1" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        var refreshed = SessionCookie(change)!;

        Assert.True((await SessionAsync(factory, refreshed)).Authenticated);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(factory, HttpMethod.Get, "/api/v1/fr09-probe", refreshed)).StatusCode);
    }
}
