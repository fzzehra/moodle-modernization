using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Moodle.Web.Files;
using Moodle.Web.Pages;
using static Moodle.UnitTests.FileWebTestSupport;

namespace Moodle.UnitTests;

/// <summary>FILE Blazor arayüzü: FileManager bileşeni, ders dosyaları ve yönetici dosya ayarları sayfaları.</summary>
public sealed class FileBlazorTests
{
    private static IRenderedComponent<FileManager> RenderManager(BunitContext context, bool canWrite = true, Action? onDenied = null) =>
        context.Render<FileManager>(p => p
            .Add(x => x.Area, Area)
            .Add(x => x.CanWrite, canWrite)
            .Add(x => x.OnWriteDenied, () => onDenied?.Invoke()));

    /// <summary>Taslak, yükleme ve kaydetme uçlarını yanıtlayan sunucu; kaydetmeden sonra liste yeni dosyayı içerir.</summary>
    private static Func<FileWebRequest, HttpResponseMessage> UploadServer(Func<FileWebRequest, HttpResponseMessage?>? overrides = null)
    {
        var committed = false;
        return request =>
        {
            if (overrides?.Invoke(request) is { } custom) return custom;
            switch (request.Method, request.PathAndQuery)
            {
                case ("POST", DraftsPath): return Json(new FileDraftInfo(55, DateTimeOffset.UnixEpoch), HttpStatusCode.Created);
                case ("POST", DraftFilesPath): return Json(new FileUploadResult(3, request.UploadName!, 5, "text/plain", new string('c', 64)), HttpStatusCode.Created);
                case ("POST", CommitPath): committed = true; return List(Uploaded);
                case ("GET", ListPath): return committed ? List(Notes, Slides, Uploaded) : List(Notes, Slides);
                default: return Error(HttpStatusCode.NotFound, "not_found");
            }
        };
    }

    private static void SelectFile(IRenderedComponent<FileManager> manager, string name = "yeni.txt", string text = "selam") =>
        manager.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(text, name, contentType: "text/plain"));

    // ---------- FileManager: liste ----------

    [Fact]
    public async Task FILE_bilesen_API_yanitlayana_kadar_yukleniyor_gosterir()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FileWebHandler((_, _) => pending.Task);
        await using var context = Context(handler);
        var manager = RenderManager(context);

        manager.WaitForAssertion(() => Assert.Contains("Dosyalar yükleniyor", manager.Markup));
        Assert.Empty(manager.FindAll("table"));
        pending.SetResult(List(Notes));
        manager.WaitForAssertion(() => Assert.DoesNotContain("Dosyalar yükleniyor", manager.Markup));
    }

    [Fact]
    public async Task FILE_FR_03_liste_ad_boyut_tarih_ve_indirme_baglantisi_gosterir()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var manager = RenderManager(context);

        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));
        var first = manager.Find("tr[data-file-id='1']");
        Assert.Contains("1,5 KB", first.TextContent);
        Assert.Contains("18.11.2026 09:30 UTC", first.TextContent);
        Assert.Contains("2 MB", manager.Find("tr[data-file-id='2']").TextContent);
        var link = first.QuerySelector("[data-testid=download]")!;
        Assert.Equal("api/v1/files/1/content?download=true", link.GetAttribute("href"));
        Assert.Equal("SENG101 Ders Notları.pdf", link.GetAttribute("download"));
        Assert.Equal(new FileWebRequest("GET", ListPath, null), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task FILE_bos_alan_bos_mesaji_gosterir()
    {
        using var handler = Handler(_ => List());
        await using var context = Context(handler);
        var manager = RenderManager(context);

        manager.WaitForAssertion(() => Assert.Contains("henüz dosya yok", manager.Find("[data-testid=file-empty]").TextContent));
        Assert.Empty(manager.FindAll("table"));
    }

    [Fact]
    public async Task FILE_CanWrite_false_iken_silme_ve_yukleme_yoktur()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var manager = RenderManager(context, canWrite: false);

        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));
        Assert.Empty(manager.FindAll("[data-testid=delete]"));
        Assert.Empty(manager.FindAll("[data-testid=upload-panel]"));
        Assert.Empty(manager.FindComponents<InputFile>());
        Assert.DoesNotContain("İşlem", manager.Find("thead").TextContent);
        Assert.Equal(2, manager.FindAll("[data-testid=download]").Count); // okuma sürer
    }

    // ---------- Silme ----------

    [Fact]
    public async Task FILE_FR_05_silme_onay_ister_vazgecilirse_istek_gitmez_onaylanirsa_siler()
    {
        var deleted = false;
        using var handler = Handler(request => (request.Method, request.PathAndQuery) switch
        {
            ("DELETE", "/api/v1/files/1") => SetDeleted(),
            ("GET", ListPath) => deleted ? List(Slides) : List(Notes, Slides),
            _ => Error(HttpStatusCode.NotFound, "not_found"),
        });
        HttpResponseMessage SetDeleted() { deleted = true; return new HttpResponseMessage(HttpStatusCode.NoContent); }
        await using var context = Context(handler);
        var manager = RenderManager(context);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("[data-testid=delete]").Count));

        await manager.Find("tr[data-file-id='1'] [data-testid=delete]").ClickAsync(new MouseEventArgs());
        Assert.Contains("SENG101 Ders Notları.pdf", manager.Find("[role=alertdialog]").TextContent);
        await manager.Find("[role=alertdialog] button:last-child").ClickAsync(new MouseEventArgs()); // Vazgeç
        Assert.Empty(manager.FindAll("[role=alertdialog]"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == "DELETE");

        await manager.Find("tr[data-file-id='1'] [data-testid=delete]").ClickAsync(new MouseEventArgs());
        await manager.Find("[role=alertdialog] .crs-danger").ClickAsync(new MouseEventArgs()); // Evet, sil
        manager.WaitForAssertion(() => Assert.Contains("silindi", manager.Markup));
        Assert.Single(manager.FindAll("tbody tr"));
        Assert.Single(handler.Requests, r => r is { Method: "DELETE", PathAndQuery: "/api/v1/files/1" });
    }

    [Fact]
    public async Task FILE_silme_reddedilir_ve_dosya_listede_kalirsa_yazma_yetkisi_yok_sayilir()
    {
        var denied = false;
        using var handler = Handler(request => request.Method == "DELETE"
            ? Error(HttpStatusCode.NotFound, "not_found")
            : List(Notes, Slides));
        await using var context = Context(handler);
        var manager = RenderManager(context, onDenied: () => denied = true);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("[data-testid=delete]").Count));

        await manager.Find("tr[data-file-id='1'] [data-testid=delete]").ClickAsync(new MouseEventArgs());
        await manager.Find("[role=alertdialog] .crs-danger").ClickAsync(new MouseEventArgs());

        manager.WaitForAssertion(() => Assert.True(denied));
        Assert.Contains("yetkiniz yok", manager.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task FILE_silinecek_dosya_zaten_silinmisse_yetki_reddi_sayilmaz()
    {
        var denied = false;
        var deleteTried = false;
        using var handler = Handler(request =>
        {
            if (request.Method == "DELETE") { deleteTried = true; return Error(HttpStatusCode.NotFound, "not_found"); }
            return deleteTried ? List(Slides) : List(Notes, Slides);
        });
        await using var context = Context(handler);
        var manager = RenderManager(context, onDenied: () => denied = true);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("[data-testid=delete]").Count));

        await manager.Find("tr[data-file-id='1'] [data-testid=delete]").ClickAsync(new MouseEventArgs());
        await manager.Find("[role=alertdialog] .crs-danger").ClickAsync(new MouseEventArgs());

        manager.WaitForAssertion(() => Assert.Contains("zaten silinmiş", manager.Markup));
        Assert.False(denied);
        Assert.Empty(manager.FindAll("[role=alert]"));
    }

    // ---------- Yükleme ----------

    [Fact]
    public async Task FILE_FR_01_02_yukleme_akisi_taslaga_yukler_sonra_kaydeder()
    {
        using var handler = Handler(UploadServer());
        await using var context = Context(handler);
        var manager = RenderManager(context);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));

        SelectFile(manager);
        manager.WaitForAssertion(() => Assert.Contains("yeni.txt", manager.Find("[data-testid=selected]").TextContent));
        Assert.True(manager.Find("[data-testid=commit]").HasAttribute("disabled"));

        await manager.Find("[data-testid=upload]").ClickAsync(new MouseEventArgs());
        manager.WaitForAssertion(() => Assert.Contains("yeni.txt", manager.Find("[data-testid=draft]").TextContent));
        Assert.Contains("taslağa yüklendi", manager.Markup);

        await manager.Find("[data-testid=commit]").ClickAsync(new MouseEventArgs());
        manager.WaitForAssertion(() => Assert.Equal(3, manager.FindAll("tbody tr").Count));
        Assert.Contains("1 dosya kaydedildi", manager.Markup);
        Assert.Empty(manager.FindAll("[data-testid=draft]"));

        var upload = Assert.Single(handler.Requests, r => r.PathAndQuery == DraftFilesPath);
        Assert.Equal(("yeni.txt", "text/plain", "selam"), (upload.UploadName, upload.UploadType, upload.UploadText));
        using var commit = JsonDocument.Parse(Assert.Single(handler.Requests, r => r.PathAndQuery == CommitPath).Body!);
        Assert.Equal(55, commit.RootElement.GetProperty("draftId").GetInt64());
    }

    [Fact]
    public async Task FILE_yukleme_uclari_404_iken_uyari_gosterir_liste_ve_silme_calisir()
    {
        using var handler = Handler(request => (request.Method, request.PathAndQuery) switch
        {
            ("POST", DraftsPath) => Error(HttpStatusCode.NotFound, "not_found"),
            ("GET", ListPath) => List(Notes, Slides),
            _ => Error(HttpStatusCode.NotFound, "not_found"),
        });
        await using var context = Context(handler);
        var manager = RenderManager(context);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));

        SelectFile(manager);
        await manager.Find("[data-testid=upload]").ClickAsync(new MouseEventArgs());

        manager.WaitForAssertion(() => Assert.Contains("Yükleme şu an kullanılamıyor", manager.Find("[data-testid=upload-unavailable]").TextContent));
        Assert.Empty(manager.FindAll("[role=alert]"));
        Assert.Empty(manager.FindComponents<InputFile>());
        Assert.Equal(2, manager.FindAll("[data-testid=delete]").Count);
        Assert.False(manager.Find("[data-testid=delete]").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(413, "file_too_large", "boyut sınırını aşıyor")]
    [InlineData(415, "type_not_allowed", "dosya türü bu alanda kabul edilmiyor")]
    [InlineData(422, "quota_exceeded", "Kotanız doldu.")]
    [InlineData(409, "name_conflict", "Bu adla bir dosya zaten var")]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor")]
    [InlineData(503, "auth_not_ready", "AUTH")]
    public async Task FILE_yukleme_hatalari_Turkce_aciklanir(int status, string code, string text)
    {
        using var handler = Handler(UploadServer(request => request.PathAndQuery == DraftFilesPath
            ? Error((HttpStatusCode)status, code, code == "quota_exceeded" ? "Kotanız doldu." : "Password=private detail")
            : null));
        await using var context = Context(handler);
        var manager = RenderManager(context);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));

        SelectFile(manager);
        await manager.Find("[data-testid=upload]").ClickAsync(new MouseEventArgs());

        manager.WaitForAssertion(() => Assert.Contains(text, manager.Find("[role=alert]").TextContent));
        Assert.Equal(code, manager.Find("[role=alert]").GetAttribute("data-error-code"));
        Assert.DoesNotContain("private", manager.Markup);
    }

    [Theory]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    public async Task FILE_kaydetme_reddedilirse_yazma_yetkisi_yok_bildirilir(int status, string code)
    {
        var denied = false;
        using var handler = Handler(UploadServer(request => request.PathAndQuery == CommitPath ? Error((HttpStatusCode)status, code) : null));
        await using var context = Context(handler);
        var manager = RenderManager(context, onDenied: () => denied = true);
        manager.WaitForAssertion(() => Assert.Equal(2, manager.FindAll("tbody tr").Count));

        SelectFile(manager);
        await manager.Find("[data-testid=upload]").ClickAsync(new MouseEventArgs());
        manager.WaitForAssertion(() => Assert.False(manager.Find("[data-testid=commit]").HasAttribute("disabled")));
        await manager.Find("[data-testid=commit]").ClickAsync(new MouseEventArgs());

        manager.WaitForAssertion(() => Assert.True(denied));
        Assert.Equal(code, manager.Find("[role=alert]").GetAttribute("data-error-code"));
    }

    [Theory]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor", "Oturum açmanız gerekiyor")]
    [InlineData(403, "forbidden", "yetkiniz yok", "İşlem tamamlanamadı")]
    [InlineData(404, "not_found", "bulunamadı", "İşlem tamamlanamadı")]
    [InlineData(503, "auth_not_ready", "AUTH", "Dosya işlemleri henüz kullanılamıyor")]
    [InlineData(500, "internal_error", "daha sonra", "İşlem tamamlanamadı")]
    public async Task FILE_liste_hatalari_ic_ayrinti_gostermeden_aciklanir_ve_yeniden_denenir(int status, string code, string text, string heading)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, code));
        await using var context = Context(handler);
        var manager = RenderManager(context);

        manager.WaitForAssertion(() => Assert.Contains(text, manager.Find("[role=alert]").TextContent));
        Assert.Equal(heading, manager.Find("[role=alert] h2").TextContent);
        Assert.DoesNotContain("private", manager.Markup);
        Assert.Empty(manager.FindAll("table"));
        Assert.Empty(manager.FindAll("[data-testid=upload-panel]"));

        await manager.Find("[role=alert] button").ClickAsync(new MouseEventArgs());
        manager.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));
        Assert.All(handler.Requests, r => Assert.Equal("GET", r.Method));
    }

    // ---------- CourseFiles sayfası ----------

    [Fact]
    public async Task FILE_FR_13_ders_dosyalari_sayfasi_ders_alanini_listeler_ve_reddedilen_yazmada_denetimleri_gizler()
    {
        using var handler = Handler(UploadServer(request => request.PathAndQuery == CommitPath ? Error(HttpStatusCode.NotFound, "not_found") : null));
        await using var context = Context(handler);
        var page = context.Render<CourseFiles>(p => p.Add(x => x.CourseId, 7));

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Equal("Ders dosyaları", page.Find("h1").TextContent);
        Assert.Equal(ListPath, handler.Requests.First().PathAndQuery);
        Assert.NotEmpty(page.FindAll("[data-testid=upload-panel]")); // yetki bilinmiyor: önce denetimler görünür

        var manager = page.FindComponent<FileManager>();
        SelectFile(manager);
        await page.Find("[data-testid=upload]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.False(page.Find("[data-testid=commit]").HasAttribute("disabled")));
        await page.Find("[data-testid=commit]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-testid=upload-panel]")));
        Assert.Empty(page.FindAll("[data-testid=delete]"));
        Assert.Equal(2, page.FindAll("[data-testid=download]").Count);
    }

    // ---------- AdminFileSettings sayfası ----------

    [Fact]
    public async Task FILE_yonetici_ayarlari_yuklenir_ve_kaydedilir()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Json(Settings with { DraftTtlHours = 48 })
            : Default(request));
        await using var context = Context(handler);
        var page = context.Render<AdminFileSettings>();

        page.WaitForAssertion(() => Assert.Equal("96", page.Find("#draft-ttl-hours").GetAttribute("value")));
        Assert.Equal("52428800", page.Find("#max-file-bytes").GetAttribute("value"));
        Assert.Equal("104857600", page.Find("#user-quota-bytes").GetAttribute("value"));
        Assert.Equal("24", page.Find("#trash-retention-hours").GetAttribute("value"));

        await page.Find("#draft-ttl-hours").ChangeAsync(new ChangeEventArgs { Value = "48" });
        await page.Find("form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("Dosya ayarları kaydedildi", page.Markup));
        using var body = JsonDocument.Parse(Assert.Single(handler.Requests, r => r.Method == "PUT").Body!);
        Assert.Equal(48, body.RootElement.GetProperty("draftTtlHours").GetInt32());
        Assert.Equal(52428800, body.RootElement.GetProperty("maxFileBytes").GetInt64());
    }

    [Fact]
    public async Task FILE_yonetici_ayarlarinda_sunucunun_dogrulama_hatasi_gosterilir_ve_form_korunur()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Error(HttpStatusCode.UnprocessableEntity, "validation_failed", "draftTtlHours 1 ile 8760 arasında olmalıdır.")
            : Default(request));
        await using var context = Context(handler);
        var page = context.Render<AdminFileSettings>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        await page.Find("#draft-ttl-hours").ChangeAsync(new ChangeEventArgs { Value = "0" });
        await page.Find("form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("draftTtlHours 1 ile 8760 arasında olmalıdır.", page.Find("[role=alert]").TextContent));
        Assert.Equal("0", page.Find("#draft-ttl-hours").GetAttribute("value"));
    }

    [Fact]
    public async Task FILE_FR_12_temizlik_calistirilir_ve_sonuc_gosterilir()
    {
        using var handler = Handler(request => request.PathAndQuery == MaintenancePath
            ? Json(new FileMaintenanceResult(2, 5, 1, ["abc: diskten silinemedi"]), HttpStatusCode.Accepted)
            : Default(request));
        await using var context = Context(handler);
        var page = context.Render<AdminFileSettings>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid=run-maintenance]")));

        await page.Find("[data-testid=run-maintenance]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("2", page.Find("[data-testid=expired-drafts]").TextContent));
        Assert.Equal("5", page.Find("[data-testid=purged-entries]").TextContent);
        Assert.Equal("1", page.Find("[data-testid=purged-blobs]").TextContent);
        Assert.Contains("abc: diskten silinemedi", page.Find("[data-testid=maintenance-errors]").TextContent);
        Assert.Single(handler.Requests, r => r is { Method: "POST", PathAndQuery: MaintenancePath });
    }

    [Fact]
    public async Task FILE_FR_12_temizlik_zaten_calisiyorsa_409_mesaji()
    {
        using var handler = Handler(request => request.PathAndQuery == MaintenancePath
            ? Error(HttpStatusCode.Conflict, "maintenance_running")
            : Default(request));
        await using var context = Context(handler);
        var page = context.Render<AdminFileSettings>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid=run-maintenance]")));

        await page.Find("[data-testid=run-maintenance]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("Temizlik zaten çalışıyor", page.Find("[role=alert] h2").TextContent));
        Assert.Contains("Temizlik zaten çalışıyor", page.Find("[role=alert] p").TextContent);
        Assert.Empty(page.FindAll("[data-testid=maintenance-result]"));
        Assert.NotEmpty(page.FindAll("form")); // ayar formu kalır
    }

    [Theory]
    [InlineData(403, "forbidden", "yetkiniz yok")]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor")]
    [InlineData(503, "auth_not_ready", "AUTH")]
    public async Task FILE_yonetici_olmayan_ayar_formunu_goremez(int status, string code, string text)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, code));
        await using var context = Context(handler);
        var page = context.Render<AdminFileSettings>();

        page.WaitForAssertion(() => Assert.Contains(text, page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll("form"));
        Assert.Empty(page.FindAll("[data-testid=run-maintenance]"));
    }
}
