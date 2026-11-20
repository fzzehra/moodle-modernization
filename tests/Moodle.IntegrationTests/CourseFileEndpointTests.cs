using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Files;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;
using static Moodle.IntegrationTests.FileRolesApiFixture;

namespace Moodle.IntegrationTests;

/// <summary>
/// Ders dosyaları (FILE-FR-13) yeni sistemde; T-FILE-03 (öğrenci ders notunu indirir) ve T-FILE-05 (öğretmen
/// ders dosyalarını yönetir, öğrenci yönetemez) senaryolarının karşılığı. Yükleme uçları henüz olmadığından
/// taslak servis üzerinden oluşturulur; kaydetme, listeleme, indirme ve silme HTTP uçlarıyla yapılır.
/// Reddedilen erişim FILE'ın kuralı gereği 404'tür (FILE-BR-11).
/// </summary>
public sealed class CourseFileEndpointTests(FileRolesApiFixture fixture) : IClassFixture<FileRolesApiFixture>
{
    private static readonly string AreaUrl = $"/api/v1/file-areas/{CourseId}/course/files/0";

    private sealed record FileRow(long FileId, string Name, long SizeBytes);
    private sealed record FileList(List<FileRow> Files);

    private WebApplicationFactory<Program> Factory(int userId) => fixture.CreateFactory(userId);

    /// <summary>Kullanıcının taslağına dosya koyar (servis üzerinden) ve taslak kimliğini döner.</summary>
    private static async Task<long> DraftWithFileAsync(WebApplicationFactory<Program> factory, int userId, string name, string text)
    {
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var draft = await files.CreateDraftAsync(userId);
        await files.AddToDraftAsync(userId, draft.DraftId, name, new MemoryStream(Encoding.UTF8.GetBytes(text)));
        return draft.DraftId;
    }

    private static Task<HttpResponseMessage> CommitAsync(HttpClient client, long draftId, string areaUrl) =>
        client.PostAsJsonAsync($"{areaUrl}/commit", new { draftId, replace = false });

    /// <summary>Düzenleyen öğretmen ders alanına bir dosya ekler ve kimliğini döner.</summary>
    private async Task<long> TeacherAddsAsync(string name, string text)
    {
        var factory = Factory(EditingTeacher);
        var draftId = await DraftWithFileAsync(factory, EditingTeacher, name, text);
        var response = await CommitAsync(factory.CreateClient(), draftId, AreaUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FileList>())!.Files.Single(f => f.Name == name).FileId;
    }

    private static async Task<List<FileRow>> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync($"{AreaUrl}/files");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FileList>())!.Files;
    }

    [Fact]
    public async Task FILE_FR_13_T_FILE_05_duzenleyen_ogretmen_ders_dosyasi_ekler_ve_siler()
    {
        var fileId = await TeacherAddsAsync("hafta1.pdf", "Hafta 1 notları");
        var teacher = Factory(EditingTeacher).CreateClient();
        Assert.Contains(await ListAsync(teacher), f => f.FileId == fileId);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/v1/files/{fileId}")).StatusCode);
        Assert.DoesNotContain(await ListAsync(teacher), f => f.FileId == fileId);
    }

    [Fact]
    public async Task FILE_FR_13_T_FILE_03_ogrenci_ders_dosyasini_listeler_ve_indirir()
    {
        var fileId = await TeacherAddsAsync("SENG101 Ders Notları.pdf", "ders notu içeriği");
        var student = Factory(Student).CreateClient();

        Assert.Contains(await ListAsync(student), f => f.FileId == fileId);
        var content = await student.GetAsync($"/api/v1/files/{fileId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("ders notu içeriği", await content.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FILE_FR_13_T_FILE_05_ogrenci_ders_dosyasi_ekleyemez_ve_silemez()
    {
        var fileId = await TeacherAddsAsync("odev-aciklama.pdf", "açıklama");
        var factory = Factory(Student);
        var student = factory.CreateClient();

        await AssertErrorAsync(await student.DeleteAsync($"/api/v1/files/{fileId}"), HttpStatusCode.NotFound, "not_found");
        var draftId = await DraftWithFileAsync(factory, Student, "ogrenci.pdf", "öğrenci dosyası");
        await AssertErrorAsync(await CommitAsync(student, draftId, AreaUrl), HttpStatusCode.NotFound, "not_found");

        var names = (await ListAsync(student)).Select(f => f.Name).ToList();
        Assert.Contains("odev-aciklama.pdf", names);
        Assert.DoesNotContain("ogrenci.pdf", names);
    }

    [Fact]
    public async Task FILE_FR_13_duzenleme_yetkisiz_ogretmen_okur_ama_yazamaz()
    {
        var fileId = await TeacherAddsAsync("asistan-okur.pdf", "okunur");
        var factory = Factory(Teacher);
        var assistant = factory.CreateClient();

        Assert.Contains(await ListAsync(assistant), f => f.FileId == fileId);
        Assert.Equal(HttpStatusCode.OK, (await assistant.GetAsync($"/api/v1/files/{fileId}/content")).StatusCode);
        await AssertErrorAsync(await assistant.DeleteAsync($"/api/v1/files/{fileId}"), HttpStatusCode.NotFound, "not_found");
        var draftId = await DraftWithFileAsync(factory, Teacher, "asistan.pdf", "asistan dosyası");
        await AssertErrorAsync(await CommitAsync(assistant, draftId, AreaUrl), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task FILE_FR_13_FILE_BR_11_kayitsiz_kullanici_ders_dosyalarini_goremez()
    {
        var fileId = await TeacherAddsAsync("gizli.pdf", "kayıtlılara özel");
        var outsider = Factory(NotEnrolled).CreateClient();

        await AssertErrorAsync(await outsider.GetAsync($"{AreaUrl}/files"), HttpStatusCode.NotFound, "not_found");
        await AssertErrorAsync(await outsider.GetAsync($"/api/v1/files/{fileId}"), HttpStatusCode.NotFound, "not_found");
        await AssertErrorAsync(await outsider.GetAsync($"/api/v1/files/{fileId}/content"), HttpStatusCode.NotFound, "not_found");
        await AssertErrorAsync(await outsider.DeleteAsync($"/api/v1/files/{fileId}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task FILE_FR_13_yonetici_kayitli_olmadan_ders_dosyasi_ekler()
    {
        var factory = Factory(Manager);
        var draftId = await DraftWithFileAsync(factory, Manager, "yonetici.pdf", "yönetici dosyası");
        Assert.Equal(HttpStatusCode.OK, (await CommitAsync(factory.CreateClient(), draftId, AreaUrl)).StatusCode);
    }

    [Fact]
    public async Task FILE_FR_13_olmayan_dersin_dosya_alani_404()
    {
        var factory = Factory(SiteAdmin);
        var missingArea = $"/api/v1/file-areas/{MissingCourseId}/course/files/0";
        await AssertErrorAsync(await factory.CreateClient().GetAsync($"{missingArea}/files"), HttpStatusCode.NotFound, "not_found");
        var draftId = await DraftWithFileAsync(factory, SiteAdmin, "yok.pdf", "yok");
        await AssertErrorAsync(await CommitAsync(factory.CreateClient(), draftId, missingArea), HttpStatusCode.NotFound, "not_found");
    }
}

/// <summary>Örnek veri: SENG101 ders dosyası (FILE-FR-13; T-FILE-03, T-FILE-05 için başlangıç verisi).</summary>
public sealed class CourseFilesSeedTests(FilesPostgresFixture files, CrsPostgresFixture crs)
    : IClassFixture<FilesPostgresFixture>, IClassFixture<CrsPostgresFixture>
{
    private sealed class AnyCourse : Moodle.Application.Abstractions.ICourseDirectory
    {
        public Task<Moodle.Application.Abstractions.CourseInfo?> FindAsync(int courseId, CancellationToken ct = default) =>
            Task.FromResult<Moodle.Application.Abstractions.CourseInfo?>(new(courseId, 1, "Programlamaya Giriş", "SENG101", true, null, null));
    }

    private sealed class TeacherOf(int teacherId) : Moodle.Application.Abstractions.IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) => Task.FromResult(userId == teacherId);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([teacherId]);
        public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(userId == teacherId ? "EditingTeacher" : null);
    }

    private sealed class NoCapabilities : Moodle.Application.Abstractions.IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId, string capability, Moodle.Application.Abstractions.ContextRef context, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task FILE_FR_13_SENG101_ders_notlari_eklenir_ve_tekrar_calistirma_kopya_uretmez()
    {
        var data = SampleDataset.Create();
        await using (var crsDb = crs.CreateContext())
            await new CrsSeedContributor(crsDb).SeedAsync(data);

        int courseId;
        await using (var crsDb = crs.CreateContext())
            courseId = await crsDb.Courses.Where(c => c.ShortName == "SENG101").Select(c => c.Id).SingleAsync();

        var policy = new CourseFilePolicy(new AnyCourse(), new TeacherOf(data.UserId("ogretmen1")), new NoCapabilities());
        for (var run = 0; run < 2; run++)
        {
            await using var filesDb = files.CreateContext();
            await using var crsDb = crs.CreateContext();
            var service = new FileService(filesDb, new FakeFileStore(), new FilePolicyRegistry([new UserFilePolicy(), policy]), TimeProvider.System);
            await new CourseFilesSeedContributor(service, crsDb).SeedAsync(data);
        }

        await using var check = files.CreateContext();
        var entry = Assert.Single(await check.Entries
            .Where(e => e.Component == "course" && e.Area == "files" && e.ContextId == courseId && e.DeletedAt == null)
            .ToListAsync());
        Assert.Equal("SENG101 Ders Notları.pdf", entry.Name);
        Assert.Equal(data.UserId("ogretmen1"), entry.OwnerUserId);
        Assert.True(entry.SizeBytes is > 100 and < 10_000);
    }
}
