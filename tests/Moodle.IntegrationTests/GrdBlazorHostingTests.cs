using System.Net;
using Moodle.Web.Grd;

namespace Moodle.IntegrationTests;

/// <summary>GRD sayfaları gerçek hostta mevcut kabukla açılır; varlıklar sunulur; AUTH engeli korunur.</summary>
public sealed class GrdBlazorHostingTests(GrdUnavailableAuthFactory factory) : IClassFixture<GrdUnavailableAuthFactory>
{
    [Theory]
    [InlineData("/assignments/1/learners/3/grade", "Ödev puanı")]
    [InlineData("/assignments/1/learners/3/grade?attempt=2", "Ödev puanı")]
    [InlineData("/courses/1/my-grades", "Notlarım")]
    [InlineData("/courses/1/gradebook", "Not defteri")]
    public async Task GRD_pages_use_the_existing_shell_and_prerender_loading(string path, string heading)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<h1>{heading}</h1>", html);
        Assert.Contains("app-shell", html);
        Assert.Contains("Notlar</a>", html);
        Assert.Contains("Dersler</a>", html);
        Assert.Contains("blazor.web", html);
        Assert.Contains("Not bilgileri y", html); // API yüklemesi etkileşimli render'dan sonra başlar.
        Assert.Contains("_content/Moodle.Web/grd.css", html);
        Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public async Task GRD_landing_page_and_static_assets_are_served()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/grades");
        Assert.Contains("<h1>Notlar</h1>", html);
        Assert.Contains("grades-course", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_content/Moodle.Web/grd.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_content/Moodle.Web/grd-api.js")).StatusCode);
    }

    [Fact]
    public async Task GRD_real_API_AUTH_unavailable_is_preserved_and_translated_for_the_UI()
    {
        using var client = new GrdApiClient(factory.CreateClient());
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.MyGradesAsync(1));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.Status);
        Assert.Equal("auth_not_ready", error.Code);
        Assert.Contains("AUTH", error.Message);
    }
}
