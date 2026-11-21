using System.Net;
using System.Text;
using System.Text.Json;
using Moodle.Web.Files;
using static Moodle.UnitTests.FileWebTestSupport;

namespace Moodle.UnitTests;

/// <summary>FileApiClient: doğru uçlar, gövde sözleşmesi ve kullanıcıya gösterilen hata mesajları (WBS-2 §3.6.4).</summary>
public sealed class FileWebClientTests
{
    [Fact]
    public async Task FILE_FR_13_ders_dosyasi_alani_listelenir()
    {
        using var handler = Handler();
        using var client = Client(handler);
        var files = await client.ListAsync(Area);
        Assert.Equal(["SENG101 Ders Notları.pdf", "hafta2.pptx"], files.Select(f => f.Name));
        Assert.Equal(new FileWebRequest("GET", ListPath, null), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task FILE_alt_klasor_yolu_sorguyla_gonderilir()
    {
        using var handler = Handler(_ => List());
        using var client = Client(handler);
        await client.ListAsync(Area, "/hafta 1/");
        Assert.Equal(ListPath + "?path=%2Fhafta%201%2F", Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task FILE_FR_03_meta_ve_indirme_baglantisi()
    {
        using var handler = Handler(_ => Json(Notes));
        using var client = Client(handler);
        Assert.Equal(Notes, await client.MetaAsync(1));
        Assert.Equal("/api/v1/files/1", Assert.Single(handler.Requests).PathAndQuery);
        Assert.Equal("api/v1/files/1/content?download=true", FileApiClient.DownloadUrl(1));
    }

    [Fact]
    public async Task FILE_FR_05_silme_DELETE_gonderir_ve_204_kabul_eder()
    {
        using var handler = Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = Client(handler);
        await client.DeleteAsync(9);
        Assert.Equal(new FileWebRequest("DELETE", "/api/v1/files/9", null), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task FILE_FR_09_kota_okunur()
    {
        using var handler = Handler(_ => Json(new FileQuota(100, 40, false)));
        using var client = Client(handler);
        Assert.Equal(new FileQuota(100, 40, false), await client.QuotaAsync());
        Assert.Equal("/api/v1/users/me/quota", Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task FILE_FR_01_02_yukleme_taslak_olusturur_dosyayi_cok_parcali_gonderir_ve_kaydeder()
    {
        using var handler = Handler(request => (request.Method, request.PathAndQuery) switch
        {
            ("POST", DraftsPath) => Json(new FileDraftInfo(55, DateTimeOffset.UnixEpoch), HttpStatusCode.Created),
            ("POST", DraftFilesPath) => Json(new { fileId = 3, name = "yeni.txt", sizeBytes = 5, mimeType = "text/plain", contentHash = new string('c', 64) }, HttpStatusCode.Created),
            ("POST", CommitPath) => List(Uploaded),
            _ => Error(HttpStatusCode.NotFound, "not_found"),
        });
        using var client = Client(handler);

        var draft = await client.CreateDraftAsync();
        var uploaded = await client.UploadToDraftAsync(draft.DraftId, "Ödev açıklaması.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("merhaba")));
        var saved = await client.CommitAsync(Area, draft.DraftId);

        Assert.Equal(3, uploaded.FileId);
        Assert.Equal(5, uploaded.Size);
        Assert.Equal([Uploaded], saved);
        var requests = handler.Requests.ToArray();
        Assert.Equal(new FileWebRequest("POST", DraftsPath, null), requests[0]);
        Assert.Equal(("POST", DraftFilesPath, "Ödev açıklaması.txt", "text/plain", "merhaba"),
            (requests[1].Method, requests[1].PathAndQuery, requests[1].UploadName, requests[1].UploadType, requests[1].UploadText));
        Assert.Equal(CommitPath, requests[2].PathAndQuery);
        using var body = JsonDocument.Parse(requests[2].Body!);
        Assert.Equal(55, body.RootElement.GetProperty("draftId").GetInt64());
        Assert.False(body.RootElement.GetProperty("replace").GetBoolean()); // alandaki dosyalar korunur
    }

    [Theory]
    [InlineData(404)]
    [InlineData(405)]
    public async Task FILE_yukleme_uclari_yoksa_upload_unavailable(int status)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, "not_found"));
        using var client = Client(handler);
        var draft = await Assert.ThrowsAsync<FileApiException>(() => client.CreateDraftAsync());
        var upload = await Assert.ThrowsAsync<FileApiException>(() => client.UploadToDraftAsync(1, "a.txt", null, new MemoryStream([1])));
        Assert.All(new[] { draft, upload }, e => Assert.Equal(FileApiClient.UploadUnavailable, e.Code));
        Assert.Contains("Yükleme şu an kullanılamıyor", draft.Message);
    }

    [Fact]
    public async Task FILE_yonetici_ayarlari_okunur_ve_PUT_ile_yazilir()
    {
        using var handler = Handler(request => Json(Settings));
        using var client = Client(handler);
        Assert.Equal(Settings, await client.SettingsAsync());
        await client.SaveSettingsAsync(Settings with { DraftTtlHours = 48 });

        var put = handler.Requests.Last();
        Assert.Equal(("PUT", SettingsPath), (put.Method, put.PathAndQuery));
        using var body = JsonDocument.Parse(put.Body!);
        Assert.Equal(["maxFileBytes", "userQuotaBytes", "draftTtlHours", "trashRetentionHours"],
            body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(48, body.RootElement.GetProperty("draftTtlHours").GetInt32());
    }

    [Fact]
    public async Task FILE_FR_12_bakim_ucu_POST_ile_cagrilir_ve_sonuc_okunur()
    {
        using var handler = Handler(_ => Json(new FileMaintenanceResult(2, 3, 1, ["x"]), HttpStatusCode.Accepted));
        using var client = Client(handler);
        var result = await client.RunMaintenanceAsync();
        Assert.Equal((2, 3, 1), (result.ExpiredDrafts, result.PurgedEntries, result.PurgedBlobs));
        Assert.Equal(["x"], result.Errors);
        Assert.Equal(new FileWebRequest("POST", MaintenancePath, null), Assert.Single(handler.Requests));
    }

    [Theory]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor")]
    [InlineData(403, "forbidden", "yetkiniz yok")]
    [InlineData(404, "not_found", "bulunamadı")]
    [InlineData(409, "maintenance_running", "Temizlik zaten çalışıyor")]
    [InlineData(409, "name_conflict", "Bu adla bir dosya zaten var")]
    [InlineData(409, "conflict", "çakışıyor")]
    [InlineData(413, "file_too_large", "boyut sınırını aşıyor")]
    [InlineData(415, "type_not_allowed", "dosya türü bu alanda kabul edilmiyor")]
    [InlineData(400, "bad_request", "İstek geçersiz")]
    [InlineData(503, "auth_not_ready", "AUTH")]
    [InlineData(500, "internal_error", "daha sonra")]
    public async Task FILE_hatalar_ic_ayrinti_gostermeden_Turkce_mesaja_cevrilir(int status, string code, string expected)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, code));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<FileApiException>(() => client.ListAsync(Area));
        Assert.Equal((HttpStatusCode)status, error.Status);
        Assert.Equal(code, error.Code);
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task FILE_422_sunucunun_dogrulama_mesajini_gosterir()
    {
        using var handler = Handler(_ => Error(HttpStatusCode.UnprocessableEntity, "validation_failed", "draftTtlHours 1 ile 8760 arasında olmalıdır."));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<FileApiException>(() => client.SaveSettingsAsync(Settings));
        Assert.Equal("draftTtlHours 1 ile 8760 arasında olmalıdır.", error.Message);
    }

    [Fact]
    public async Task FILE_baglanti_hatasi_ve_bozuk_yanit_anlasilir_mesaj_verir()
    {
        using var down = new FileWebHandler((_, _) => throw new HttpRequestException("down"));
        using var downClient = Client(down);
        Assert.Equal("connection_failed", (await Assert.ThrowsAsync<FileApiException>(() => downClient.ListAsync(Area))).Code);

        using var broken = Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });
        using var brokenClient = Client(broken);
        Assert.Equal("invalid_response", (await Assert.ThrowsAsync<FileApiException>(() => brokenClient.ListAsync(Area))).Code);
    }
}
