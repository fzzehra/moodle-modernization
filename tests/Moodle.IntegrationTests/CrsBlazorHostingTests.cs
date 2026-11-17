using System.Net;
using System.Text.RegularExpressions;
using Moodle.Web.Crs;

namespace Moodle.IntegrationTests;

public sealed class CrsBlazorHostingTests(CrsUnavailableAuthFactory factory) : IClassFixture<CrsUnavailableAuthFactory>
{
    [Theory]
    [InlineData("/courses", "Dersler")]
    [InlineData("/courses/1", "Ders detayı")]
    [InlineData("/courses/1/participants", "Katılımcılar")]
    public async Task Pages_use_existing_shell_and_boot_interactive_Blazor(string path, string heading)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<h1>{heading}</h1>", html);
        Assert.Contains("app-shell", html);
        Assert.Contains("Ana sayfa", html);
        Assert.Contains("Dersler</a>", html);
        Assert.Contains("blazor.web", html);
        Assert.Contains("Ders bilgileri y", html); // Loading is prerendered; browser HTTP starts after hydration.
        Assert.DoesNotContain("<form", html);
        var script = Regex.Match(html, "<script src=\"([^\"]*blazor[^\"]*)\"");
        Assert.True(script.Success);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(WebUtility.HtmlDecode(script.Groups[1].Value))).StatusCode);
        var css = Regex.Match(html, "<link rel=\"stylesheet\" href=\"([^\"]*)\"");
        Assert.True(css.Success);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(WebUtility.HtmlDecode(css.Groups[1].Value))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_content/Moodle.Web/crs-api.js")).StatusCode);
    }

    [Fact]
    public async Task Real_API_AUTH_unavailable_is_preserved_and_translated_for_the_UI()
    {
        using var client = new CrsApiClient(factory.CreateClient());
        var error = await Assert.ThrowsAsync<CrsApiException>(() => client.CoursesAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.Status);
        Assert.Equal("auth_not_ready", error.Code);
        Assert.Contains("AUTH", error.Message);
        Assert.Contains("henüz hazır değil", error.Message);
    }
}
