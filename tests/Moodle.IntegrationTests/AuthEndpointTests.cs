using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Api.Auth;
using Moodle.Application.Abstractions;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// Giriş, çıkış, oturum durumu, hesap durumu, başarısız giriş/kilitlenme, parola değişikliği ve
/// yetki sorgulama uçları (AUTH-FR-01, 02, 03, 04, 05, 07, 08, 09; WBS-2 §3.1.3).
/// Senaryolar WBS-1 §4.1.10'daki T-AUTH-01, 04, 05, 06, 09'u izler.
/// </summary>
public sealed class AuthEndpointTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // T-AUTH-01, AUTH-FR-01, FR-08: geçerli girişte oturum açılır, /session bunu yansıtır, çıkışta kapanır.
    [Fact]
    public async Task Login_succeeds_and_session_reflects_it_until_logout()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234");
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "parola1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.True(loginBody.Authenticated);
        Assert.False(loginBody.RequiresPasswordChange);

        var session = (await (await client.GetAsync("/api/v1/auth/session")).Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.True(session.Authenticated);
        Assert.Equal(username, session.Username);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        var afterLogout = (await (await client.GetAsync("/api/v1/auth/session")).Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.False(afterLogout.Authenticated);
    }

    // T-AUTH-05, AUTH-FR-05, NFR-01/02: yanlış parola ve var olmayan kullanıcı aynı genel mesajı alır.
    [Fact]
    public async Task Wrong_password_and_unknown_username_return_the_same_generic_error()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234");
        using var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "yanlis" });
        var unknownUser = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = Unique("yok"), password = "yanlis" });

        await AssertErrorAsync(wrongPassword, HttpStatusCode.Unauthorized, "invalid_credentials");
        await AssertErrorAsync(unknownUser, HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownUser.Content.ReadAsStringAsync());
    }

    // T-AUTH-04, AUTH-FR-04, BR-02: askıya alınmış hesap, var olmayan kullanıcıyla aynı genel mesajı alır
    // (suspanse olduğu sızdırılmaz).
    [Fact]
    public async Task Suspended_account_is_rejected_with_the_same_generic_error()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234", suspended: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "parola1234" });
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");

        var session = (await (await client.GetAsync("/api/v1/auth/session")).Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.False(session.Authenticated);
    }

    // T-AUTH-06, AUTH-BR-03, FR-05: beşinci başarısız denemede hesap kilitlenir (Identity'nin
    // MaxFailedAccessAttempts eşiğine ulaşan deneme doğrudan kilitli sonucu döner); doğru parola da
    // reddedilir.
    [Fact]
    public async Task Account_locks_out_after_repeated_failed_attempts()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "parola1234");
        using var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
            await AssertErrorAsync(
                await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "yanlis" }),
                HttpStatusCode.Unauthorized, "invalid_credentials");

        await AssertErrorAsync(
            await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "yanlis" }),
            HttpStatusCode.Unauthorized, "account_locked");

        var lockedOutWithCorrectPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "parola1234" });
        await AssertErrorAsync(lockedOutWithCorrectPassword, HttpStatusCode.Unauthorized, "account_locked");
    }

    // AUTH-FR-09: mevcut parola yanlışsa reddedilir; doğruysa değişir ve eski parolayla giriş artık başarısız olur.
    [Fact]
    public async Task Change_password_requires_the_correct_current_password()
    {
        var factory = fixture.CreateFactory();
        var username = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, username, "eskiParola1");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "eskiParola1" })).StatusCode);

        var wrongCurrent = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "yanlis", newPassword = "yeniParola1" });
        await AssertErrorAsync(wrongCurrent, HttpStatusCode.BadRequest, "current_password_invalid");

        var changed = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "eskiParola1", newPassword = "yeniParola1" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        await client.PostAsync("/api/v1/auth/logout", null);
        await AssertErrorAsync(
            await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "eskiParola1" }),
            HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "yeniParola1" })).StatusCode);
    }

    // AUTH-FR-02, 03, 07, NFR-01: oturumsuz istekler reddedilir; yığın izi veya hassas bilgi sızdırılmaz.
    [Theory]
    [InlineData("POST", "/api/v1/auth/change-password")]
    [InlineData("GET", "/api/v1/auth/permissions?courseId=1")]
    public async Task Protected_endpoints_reject_anonymous_requests(string method, string path)
    {
        using var client = fixture.CreateFactory().CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { });
        var response = await client.SendAsync(request);
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "not_authenticated");
    }

    // AUTH-FR-08: çıkış, oturumun zaten kapalı olduğu durumda da güvenle tekrarlanabilir (idempotent);
    // oturumu olmayanı 401 ile reddetmek yerine "oturum yok" durumunu zaten sağlar.
    [Fact]
    public async Task Logout_is_idempotent_for_an_anonymous_caller()
    {
        using var client = fixture.CreateFactory().CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
    }

    /// <summary>Boş sahte: dersle ilgili sorular her zaman "kayıtlı değil" döner.
    /// Bu test yalnızca sistem geneli (manager-only) yetenekleri sınar; CRS'nin kendi veritabanı
    /// bu izole AUTH fixture'ında yoktur, bu yüzden ders bağlamlı rol sorgusu CRS'ye değil bu sahteye gider
    /// (CRS/GRD testlerinin ICurrentUser/IAccessPolicy'yi aynı şekilde değiştirmesiyle tutarlı bir desen).</summary>
    private sealed class NoEnrolmentQuery : IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    // AUTH-FR-02, 07: site geneli yönetici yetenekleri yalnızca Manager rolündeki kullanıcıya açılır.
    [Fact]
    public async Task Permissions_reports_manager_only_capabilities_correctly()
    {
        var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEnrolmentQuery>();
            services.AddScoped<IEnrolmentQuery, NoEnrolmentQuery>();
        }));
        var manager = Unique("yonetici");
        var student = Unique("ogrenci");
        await AuthPostgresFixture.SeedUserAsync(factory, manager, "parola1234", systemRoles: ["Manager"]);
        await AuthPostgresFixture.SeedUserAsync(factory, student, "parola1234");

        using var managerClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await managerClient.PostAsJsonAsync("/api/v1/auth/login", new { username = manager, password = "parola1234" })).StatusCode);
        var managerResponse = await managerClient.GetAsync("/api/v1/auth/permissions?courseId=1&capabilities=course.create,category.manage");
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);
        var managerPermissions = (await managerResponse.Content.ReadFromJsonAsync<PermissionsResponse>())!;
        Assert.Equal(["category.manage", "course.create"], managerPermissions.Permissions.OrderBy(x => x));

        using var studentClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await studentClient.PostAsJsonAsync("/api/v1/auth/login", new { username = student, password = "parola1234" })).StatusCode);
        var studentResponse = await studentClient.GetAsync("/api/v1/auth/permissions?courseId=1&capabilities=course.create,category.manage");
        Assert.Equal(HttpStatusCode.OK, studentResponse.StatusCode);
        var studentPermissions = (await studentResponse.Content.ReadFromJsonAsync<PermissionsResponse>())!;
        Assert.Empty(studentPermissions.Permissions);
    }
}
