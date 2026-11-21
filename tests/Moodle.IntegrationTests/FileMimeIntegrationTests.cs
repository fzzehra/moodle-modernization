using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Files;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

public sealed class FileMimeIntegrationTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    private static byte[] Bytes(string extension) => extension switch
    {
        "pdf" => Pdf(),
        "png" => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8V8AAAAASUVORK5CYII="),
        "jpg" or "jpeg" => Convert.FromBase64String("/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD9U6KKKAP/2Q=="),
        _ => Encoding.UTF8.GetBytes("Türkçe dosya içeriği")
    };

    private static byte[] Pdf()
    {
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\nendstream"
        ];
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(text.ToString()));
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(text.ToString());
        text.Append("xref\n0 5\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    [Fact]
    public void All_five_models_match_their_migration_snapshots()
    {
        using var scope = fixture.Services.CreateScope();
        DbContext[] contexts = [
            scope.ServiceProvider.GetRequiredService<FilesDbContext>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Auth.AuthDbContext>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Courses.CrsDbContext>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Persistence.MoodleDbContext>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Grading.GrdDbContext>()
        ];
        Assert.All(contexts, db => Assert.False(db.Database.HasPendingModelChanges(), db.GetType().Name));
    }

    [Theory]
    [InlineData("pdf", "application/pdf", false)]
    [InlineData("png", "image/png", false)]
    [InlineData("jpg", "image/jpeg", false)]
    [InlineData("jpeg", "image/jpeg", false)]
    [InlineData("txt", "text/plain", false)]
    [InlineData("pdf", "application/pdf", true)]
    [InlineData("png", "image/png", true)]
    [InlineData("jpg", "image/jpeg", true)]
    [InlineData("jpeg", "image/jpeg", true)]
    [InlineData("txt", "text/plain", true)]
    public async Task Server_mime_survives_draft_commit_and_authorized_download(string extension, string mime, bool assignment)
    {
        using var uploader = await fixture.Login(assignment ? "v2-student" : "v2-teacher");
        using var reader = await fixture.Login(assignment ? "v2-teacher" : "v2-student");
        var draftResponse = await uploader.PostAsync("/api/v1/file-drafts/", null);
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<DraftInfo>())!;
        var bytes = Bytes(extension);
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/x-untrusted-client");
        form.Add(part, "file", Guid.NewGuid() + "." + extension);
        var uploaded = await uploader.PostAsync($"/api/v1/file-drafts/{draft.DraftId}/files", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var meta = (await uploaded.Content.ReadFromJsonAsync<FileMeta>())!;
        Assert.Equal(mime, meta.MimeType);
        var draftContent = await uploader.GetAsync($"/api/v1/files/{meta.FileId}/content");
        Assert.Equal(mime, draftContent.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await draftContent.Content.ReadAsByteArrayAsync());
        if (assignment)
        {
            // Existing ASG fixture only: this is not claimed as real CRS assignment-creation acceptance.
            var a = await fixture.ExistingAssignment(["file"]);
            var saved = await uploader.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",
                new { Text = "", Version = 0, FileDraftId = draft.DraftId });
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            Assert.Equal(1, (await saved.Content.ReadFromJsonAsync<Submission>())!.FileCount);
        }
        else
        {
            var saved = await uploader.PostAsJsonAsync($"/api/v1/file-areas/{fixture.CourseId}/course/files/0/commit",
                new { draftId = draft.DraftId, replace = false });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var listed = await reader.GetStringAsync($"/api/v1/file-areas/{fixture.CourseId}/course/files/0/files");
            Assert.Contains(meta.Name, listed);
        }
        using var scope = fixture.Services.CreateScope();
        var entry = await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Entries.SingleAsync(e => e.Id == meta.FileId);
        Assert.Equal(mime, entry.MimeType); Assert.NotEqual("draft", entry.Area);
        var download = await reader.GetAsync($"/api/v1/files/{meta.FileId}/content?download=true");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(mime, download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("wrong.pdf", "application/pdf", "plain text")]
    [InlineData("wrong.png", "image/png", "%PDF-1.4")]
    [InlineData("wrong.exe", "text/plain", "plain text")]
    public async Task Client_mime_cannot_override_invalid_extension_or_signature(string name, string clientMime, string text)
    {
        using var client = await fixture.Login();
        var draft = (await (await client.PostAsync("/api/v1/file-drafts/", null)).Content.ReadFromJsonAsync<DraftInfo>())!;
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        part.Headers.ContentType = new MediaTypeHeaderValue(clientMime);
        form.Add(part, "file", name);
        var response = await client.PostAsync($"/api/v1/file-drafts/{draft.DraftId}/files", form);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Entries.AnyAsync(e => e.ItemId == draft.DraftId && e.Area == "draft"));
    }
}

/// <summary>Test-only real AUTH users; does not add or modify any application seed account.</summary>
public sealed class FileAdminRealAuthIntegrationTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Fact]
    public async Task Real_site_admin_can_read_save_and_run_maintenance_but_teacher_cannot()
    {
        var name = "file-admin-" + Guid.NewGuid().ToString("N");
        await AuthPostgresFixture.SeedUserAsync(fixture, name, "test-pass", systemRoles: ["SiteAdmin"]);
        using var admin = await fixture.Login(name);
        using var teacher = await fixture.Login("v2-teacher");
        const string path = "/api/v1/admin/file-settings";
        var initial = await admin.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var settings = await initial.Content.ReadAsStringAsync();
        var saved = await admin.PutAsync(path, new StringContent(settings, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/api/v1/admin/file-maintenance/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PutAsync(path, new StringContent(settings, Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync("/api/v1/admin/file-maintenance/run", null)).StatusCode);
    }
}
