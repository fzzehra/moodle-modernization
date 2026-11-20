using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Api.Files;
using Moodle.Application.Files;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;
using Npgsql;
using static Moodle.IntegrationTests.FileRolesApiFixture;

namespace Moodle.IntegrationTests;

/// <summary>
/// Yönetici dosya ayarları ve bakım ucu (WBS-2 §3.6.4; FILE-FR-12; FILE-BR-04, 05, 08, 10). Yalnızca site yöneticisi.
/// </summary>
public sealed class FileAdminEndpointTests(FileRolesApiFixture fixture) : IClassFixture<FileRolesApiFixture>, IAsyncLifetime
{
    private const string Settings = "/api/v1/admin/file-settings";
    private const string Maintenance = "/api/v1/admin/file-maintenance/run";
    private static readonly FileSettings Defaults = new(50L * 1024 * 1024, 100L * 1024 * 1024, 96, 24);

    public async Task InitializeAsync()
    {
        await using var db = fixture.Database.CreateContext();
        await new FileSettingsService(db).UpdateAsync(Defaults);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Client(int? userId, bool fakeAuth = false) => fixture.CreateFactory(userId, fakeAuth).CreateClient();

    private async Task<FileSettings> StoredAsync()
    {
        await using var db = fixture.Database.CreateContext();
        return await new FileSettingsService(db).GetAsync();
    }

    [Fact]
    public async Task FILE_ayarlari_site_yoneticisi_okur()
    {
        var response = await Client(SiteAdmin).GetAsync(Settings);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Defaults, await response.Content.ReadFromJsonAsync<FileSettings>());
    }

    [Theory]
    [InlineData(1L, 0L, 1, 0)]                    // alt sınırlar; kota 0 = sınırsız
    [InlineData(5_000_000L, 1L, 8760, 8760)]       // üst sınırlar
    public async Task FILE_BR_08_BR_10_ayarlar_sinir_degerlerde_yazilir(long max, long quota, int ttl, int trash)
    {
        var expected = new FileSettings(max, quota, ttl, trash);
        var response = await Client(SiteAdmin).PutAsJsonAsync(Settings, expected);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<FileSettings>());
        Assert.Equal(expected, await StoredAsync());
    }

    [Theory]
    [InlineData("""{"maxFileBytes":0,"userQuotaBytes":0,"draftTtlHours":96,"trashRetentionHours":24}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":-1,"draftTtlHours":96,"trashRetentionHours":24}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":0,"trashRetentionHours":24}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":8761,"trashRetentionHours":24}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":96,"trashRetentionHours":-1}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":96,"trashRetentionHours":8761}""")]
    [InlineData("""{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":96}""")]
    public async Task FILE_gecersiz_ayar_422_verir_ve_ayar_degismez(string json)
    {
        var response = await Client(SiteAdmin).PutAsync(Settings, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(Defaults, await StoredAsync());
    }

    [Fact]
    public async Task FILE_bilinmeyen_alan_400_verir_ve_ayar_degismez()
    {
        const string json = """{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":96,"trashRetentionHours":24,"virusScan":true}""";
        var response = await Client(SiteAdmin).PutAsync(Settings, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Equal(Defaults, await StoredAsync());
    }

    [Theory]
    [InlineData(Manager)]        // Manager site yöneticisi değildir
    [InlineData(EditingTeacher)]
    [InlineData(Student)]
    public async Task FILE_site_yoneticisi_olmayan_403_alir(int userId)
    {
        var client = Client(userId);
        await AssertErrorAsync(await client.GetAsync(Settings), HttpStatusCode.Forbidden, "forbidden");
        await AssertErrorAsync(await client.PutAsJsonAsync(Settings, Defaults with { DraftTtlHours = 1 }), HttpStatusCode.Forbidden, "forbidden");
        await AssertErrorAsync(await client.PostAsync(Maintenance, null), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(Defaults, await StoredAsync());
    }

    [Fact]
    public async Task FILE_oturumsuz_istek_401_alir()
    {
        var client = Client(null);
        await AssertErrorAsync(await client.GetAsync(Settings), HttpStatusCode.Unauthorized, "not_authenticated");
        await AssertErrorAsync(await client.PutAsJsonAsync(Settings, Defaults), HttpStatusCode.Unauthorized, "not_authenticated");
        await AssertErrorAsync(await client.PostAsync(Maintenance, null), HttpStatusCode.Unauthorized, "not_authenticated");
    }

    [Fact]
    public async Task FILE_AUTH_sahteyken_503_auth_not_ready()
    {
        var client = Client(null, fakeAuth: true);
        await AssertErrorAsync(await client.GetAsync(Settings), HttpStatusCode.ServiceUnavailable, "auth_not_ready");
        await AssertErrorAsync(await client.PostAsync(Maintenance, null), HttpStatusCode.ServiceUnavailable, "auth_not_ready");
    }

    [Fact]
    public async Task FILE_FR_12_bakim_ucu_temizligi_calistirir_ve_sonucu_doner()
    {
        long draftId;
        await using (var db = fixture.Database.CreateContext())
        {
            var old = DateTimeOffset.UtcNow.AddDays(-30);
            var draft = new FileDraft { OwnerUserId = Student, CreatedAt = old, LastActivityAt = old };
            db.Drafts.Add(draft);
            await db.SaveChangesAsync();
            draftId = draft.DraftId;
        }

        var response = await Client(SiteAdmin).PostAsync(Maintenance, null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<FileMaintenanceResponse>())!;
        Assert.True(body.ExpiredDrafts >= 1);
        Assert.Empty(body.Errors);
        await using var check = fixture.Database.CreateContext();
        Assert.False(await check.Drafts.AnyAsync(d => d.DraftId == draftId));
    }

    [Fact]
    public async Task FILE_FR_12_temizlik_baska_bir_ornekte_yururken_409_maintenance_running()
    {
        string connectionString;
        await using (var db = fixture.Database.CreateContext())
            connectionString = db.Database.GetConnectionString()!;

        // Başka bir API örneği temizliği yürütüyor: aynı danışma kilidini ayrı bir bağlantı tutar.
        await using var other = new NpgsqlConnection(connectionString);
        await other.OpenAsync();
        await using (var hold = new NpgsqlCommand($"SELECT pg_advisory_lock({FileMaintenanceService.AdvisoryLockKey})", other))
            await hold.ExecuteNonQueryAsync();

        var response = await Client(SiteAdmin).PostAsync(Maintenance, null);
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "maintenance_running");

        await using (var release = new NpgsqlCommand($"SELECT pg_advisory_unlock({FileMaintenanceService.AdvisoryLockKey})", other))
            await release.ExecuteNonQueryAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await Client(SiteAdmin).PostAsync(Maintenance, null)).StatusCode);
    }
}

/// <summary><see cref="IdentitySiteAdminQuery"/>: gerçek AUTH veritabanında SiteAdmin ile Manager ayrımı.</summary>
public sealed class SiteAdminQueryTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task FILE_yalnizca_etkin_SiteAdmin_site_yoneticisidir_Manager_degildir()
    {
        var factory = fixture.CreateFactory();
        var admin = await AuthPostgresFixture.SeedUserAsync(factory, Unique("admin"), "parola1234", systemRoles: ["SiteAdmin"]);
        var manager = await AuthPostgresFixture.SeedUserAsync(factory, Unique("manager"), "parola1234", systemRoles: ["Manager"]);
        var suspended = await AuthPostgresFixture.SeedUserAsync(factory, Unique("askida"), "parola1234", suspended: true, systemRoles: ["SiteAdmin"]);
        var plain = await AuthPostgresFixture.SeedUserAsync(factory, Unique("ogrenci"), "parola1234");

        using var scope = factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ISiteAdminQuery>();
        Assert.IsType<IdentitySiteAdminQuery>(query);

        Assert.True(await query.IsSiteAdminAsync(admin.Id));
        Assert.False(await query.IsSiteAdminAsync(manager.Id));
        Assert.False(await query.IsSiteAdminAsync(suspended.Id));
        Assert.False(await query.IsSiteAdminAsync(plain.Id));
        Assert.False(await query.IsSiteAdminAsync(-1));
    }
}
