using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Api.Files;
using Moodle.Application.Files;

namespace Moodle.IntegrationTests;

/// <summary>
/// Dosya kaydetme, indirme, silme ve kota uçları (FILE-FR-02, 03, 04, 05, 09; BR-10, 11, 12, 13, 14;
/// BUG-01). Taslak oluşturma/yükleme HTTP yüzeyi yok (Aslı Ceren'in FR-01/07/08/10/11 kapsamı); bu
/// yüzden testler taslağı doğrudan <see cref="IFileService"/> ile hazırlar, yalnızca HTTP uçlarını sınar.
/// </summary>
public sealed class FileEndpointTests(FilesApiFixture fixture) : IClassFixture<FilesApiFixture>
{
    private static Stream Bytes(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static async Task<(long DraftId, long FileId)> SeedDraftFileAsync(
        WebApplicationFactory<Program> factory, int userId, string name, string content)
    {
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var draft = await files.CreateDraftAsync(userId);
        var meta = await files.AddToDraftAsync(userId, draft.DraftId, name, Bytes(content));
        return (draft.DraftId, meta.FileId);
    }

    // FILE-FR-02: taslaktaki dosyalar hedef alana taşınır ve orada listelenir.
    [Fact]
    public async Task Commit_moves_draft_files_to_target_area()
    {
        using var factory = fixture.CreateFactory(userId: 101);
        var (draftId, _) = await SeedDraftFileAsync(factory, 101, "rapor.pdf", Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var commit = await client.PostAsJsonAsync("/api/v1/file-areas/101/user/private/0/commit", new CommitRequest(draftId));
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        var listed = await client.GetFromJsonAsync<ListedFiles>("/api/v1/file-areas/101/user/private/0/files");
        Assert.Single(listed!.Files, f => f.Name == "rapor.pdf");
    }

    // FILE-FR-03, BR-12: taslak dosyaları her zaman indirmeye zorlanır (Content-Disposition: attachment).
    [Fact]
    public async Task Draft_downloads_are_always_forced_and_content_matches()
    {
        using var factory = fixture.CreateFactory(userId: 102);
        var content = "taslak içerik " + Guid.NewGuid();
        var (_, fileId) = await SeedDraftFileAsync(factory, 102, "taslak.txt", content);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/files/{fileId}/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(content, await response.Content.ReadAsStringAsync());
    }

    // FILE-FR-04, BR-11, 12: başkasının (taslak veya özel) dosyasına erişim "bulunamadı" (404) döner, 403 değil.
    [Fact]
    public async Task Unauthorized_access_returns_404_not_403()
    {
        using var ownerFactory = fixture.CreateFactory(userId: 103);
        var (draftId, _) = await SeedDraftFileAsync(ownerFactory, 103, "ozel.txt", Guid.NewGuid().ToString());
        using var ownerClient = ownerFactory.CreateClient();
        var committed = await (await ownerClient.PostAsJsonAsync("/api/v1/file-areas/103/user/private/0/commit", new CommitRequest(draftId)))
            .Content.ReadFromJsonAsync<ListedFiles>();
        var fileId = committed!.Files[0].FileId;

        using var strangerFactory = fixture.CreateFactory(userId: 104);
        using var strangerClient = strangerFactory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.GetAsync($"/api/v1/files/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.GetAsync($"/api/v1/files/{fileId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.DeleteAsync($"/api/v1/files/{fileId}")).StatusCode);
    }

    // FILE-FR-05, BR-13, 14: silinen dosya artık görünmez; FILE-FR-09, BUG-01: kota her kaydı tekil sayar.
    [Fact]
    public async Task Delete_hides_the_file_and_quota_counts_each_distinct_file_once()
    {
        using var factory = fixture.CreateFactory(userId: 105);
        var client = factory.CreateClient();

        var (draftA, _) = await SeedDraftFileAsync(factory, 105, "a.txt", "içerik-A-" + Guid.NewGuid());
        var filesA = await (await client.PostAsJsonAsync("/api/v1/file-areas/105/user/private/0/commit", new CommitRequest(draftA, Replace: false)))
            .Content.ReadFromJsonAsync<ListedFiles>();

        var (draftB, _) = await SeedDraftFileAsync(factory, 105, "b.txt", "içerik-B-" + Guid.NewGuid());
        var filesB = await (await client.PostAsJsonAsync("/api/v1/file-areas/105/user/private/0/commit", new CommitRequest(draftB, Replace: false)))
            .Content.ReadFromJsonAsync<ListedFiles>();

        var quotaBefore = await client.GetFromJsonAsync<QuotaResponse>("/api/v1/users/me/quota");
        Assert.Equal(filesA!.Files[0].SizeBytes + filesB!.Files[0].SizeBytes, quotaBefore!.UsedBytes);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/files/{filesA.Files[0].FileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/files/{filesA.Files[0].FileId}")).StatusCode);

        var quotaAfter = await client.GetFromJsonAsync<QuotaResponse>("/api/v1/users/me/quota");
        Assert.Equal(filesB.Files[0].SizeBytes, quotaAfter!.UsedBytes);
    }

    // Kayıtsız bir bileşene (ör. henüz politikası olmayan "course") commit reddedilir (güvenli varsayılan).
    [Fact]
    public async Task Commit_into_an_unregistered_component_is_rejected()
    {
        using var factory = fixture.CreateFactory(userId: 106);
        var (draftId, _) = await SeedDraftFileAsync(factory, 106, "x.txt", Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/file-areas/1/course/files/1/commit", new CommitRequest(draftId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // FILE-BR-13, 14: silinen dosya kendisi çöpe taşınır; içeriği başka etkin kayıt kullanmıyorsa
    // içerik de boşa düşer (OrphanedAt), ama paylaşılan içerik başka bir kayıt tarafından kullanılıyorsa
    // silinmez. Henüz testi olmayan bu davranış için açık bir test (PDF'te belirtildiği gibi).
    [Fact]
    public async Task Deleting_the_last_reference_orphans_the_shared_content_but_not_while_still_used()
    {
        using var factory = fixture.CreateFactory(userId: 107);
        var client = factory.CreateClient();
        var sharedContent = "paylaşılan içerik " + Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var draft1 = await files.CreateDraftAsync(107);
        var meta1 = await files.AddToDraftAsync(107, draft1.DraftId, "kopya1.txt", Bytes(sharedContent));
        await client.PostAsJsonAsync("/api/v1/file-areas/107/user/private/0/commit", new CommitRequest(draft1.DraftId, Replace: false));

        var draft2 = await files.CreateDraftAsync(107);
        var meta2 = await files.AddToDraftAsync(107, draft2.DraftId, "kopya2.txt", Bytes(sharedContent));
        await client.PostAsJsonAsync("/api/v1/file-areas/107/user/private/0/commit", new CommitRequest(draft2.DraftId, Replace: false));
        Assert.Equal(meta1.ContentHash, meta2.ContentHash); // aynı içerik -> aynı özet (FILE-BR-01)

        await using var db = fixture.Database.CreateContext();
        Assert.Null((await db.Blobs.SingleAsync(b => b.ContentHash == meta1.ContentHash)).OrphanedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/files/{meta1.FileId}")).StatusCode);
        await using var db2 = fixture.Database.CreateContext();
        Assert.Null((await db2.Blobs.SingleAsync(b => b.ContentHash == meta1.ContentHash)).OrphanedAt); // hâlâ kopya2 kullanıyor

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/files/{meta2.FileId}")).StatusCode);
        await using var db3 = fixture.Database.CreateContext();
        Assert.NotNull((await db3.Blobs.SingleAsync(b => b.ContentHash == meta1.ContentHash)).OrphanedAt); // artık hiçbiri kullanmıyor
    }

    private sealed record ListedFiles(IReadOnlyList<FileMeta> Files);
}
