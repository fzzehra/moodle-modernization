using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Moodle.Web.Files;

namespace Moodle.IntegrationTests;

public sealed class FileHostingFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
}

/// <summary>FILE sayfaları gerçek hostta mevcut kabukla açılır; varlıklar sunulur; gerçek API hatası arayüze çevrilir (GRD deseni).</summary>
public sealed class FileBlazorHostingTests(FileHostingFactory factory) : IClassFixture<FileHostingFactory>
{
    [Theory]
    [InlineData("/courses/1/files", "Ders dosyaları", "Dosyalar y")]
    [InlineData("/admin/files", "Dosya ayarları", "Dosya ayarları y")]
    public async Task FILE_pages_use_the_existing_shell_and_prerender_loading(string path, string heading, string loading)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<h1>{heading}</h1>", html);
        Assert.Contains("app-shell", html);
        Assert.Contains("Dosya ayarları</a>", html); // NavMenu bağlantısı
        Assert.Contains("Dersler</a>", html);
        Assert.Contains("blazor.web", html);
        // API yüklemesi etkileşimli render'dan sonra başlar. Parametreyle gelen metin prerender'da HTML kodlanır
        // (ör. "ı" → "&#x131;"), bu yüzden çözülmüş metinde aranır.
        Assert.Contains(loading, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain("<table", html);
        Assert.DoesNotContain("type=\"file\"", html);
    }

    [Fact]
    public async Task FILE_static_asset_is_served()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/_content/Moodle.Web/file-api.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unsupported FILE request", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FILE_real_API_anonymous_requests_are_translated_for_the_UI()
    {
        using var client = new FileApiClient(factory.CreateClient());

        var list = await Assert.ThrowsAsync<FileApiException>(() => client.ListAsync(FileAreaRef.CourseFiles(1)));
        Assert.Equal(HttpStatusCode.Unauthorized, list.Status);
        Assert.Equal("not_authenticated", list.Code);
        Assert.Contains("oturum açmanız gerekiyor", list.Message);

        var settings = await Assert.ThrowsAsync<FileApiException>(() => client.SettingsAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, settings.Status);

        // Yükleme uçları henüz yok (Aslı Ceren): arayüz "yükleme kullanılamıyor" olarak gösterir.
        var draft = await Assert.ThrowsAsync<FileApiException>(() => client.CreateDraftAsync());
        Assert.Equal(FileApiClient.UploadUnavailable, draft.Code);
    }
}
