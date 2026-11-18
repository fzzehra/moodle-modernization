using System.Net;
using System.Text;
using Moodle.Web.Crs;
using static Moodle.UnitTests.CrsWebTestSupport;

namespace Moodle.UnitTests;

public sealed class CrsWebClientTests
{
    [Theory]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor")]
    [InlineData(403, "forbidden", "yetkiniz yok")]
    [InlineData(404, "not_found", "bulunamadı")]
    [InlineData(503, "auth_not_ready", "AUTH")]
    [InlineData(500, "internal_error", "daha sonra")]
    public async Task HTTP_errors_are_translated_without_showing_internal_messages(int status, string code, string expected)
    {
        using var handler = Handler(_ => Json(new { error = new { code, message = "Password=private" } }, (HttpStatusCode)status));
        using var client = new CrsApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
        var error = await Assert.ThrowsAsync<CrsApiException>(() => client.CoursesAsync());
        Assert.Equal((HttpStatusCode)status, error.Status);
        Assert.Equal(code, error.Code);
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData(409)]
    [InlineData(422)]
    public async Task Expected_business_errors_are_available_to_the_form(int status)
    {
        using var handler = Handler(_ => Json(new { error = new { code = "business_rule", message = "Alanları kontrol edin." } }, (HttpStatusCode)status));
        using var client = new CrsApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
        Assert.Equal("Alanları kontrol edin.", (await Assert.ThrowsAsync<CrsApiException>(() => client.CoursesAsync())).Message);
    }

    [Theory]
    [InlineData("<html>private failure</html>")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Nonstandard_error_bodies_do_not_crash_the_UI(string body)
    {
        using var handler = Handler(_ => new(HttpStatusCode.BadGateway) { Content = new StringContent(body, Encoding.UTF8) });
        using var client = new CrsApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
        var error = await Assert.ThrowsAsync<CrsApiException>(() => client.CoursesAsync());
        Assert.DoesNotContain("private", error.Message);
        Assert.Equal(HttpStatusCode.BadGateway, error.Status);
    }

    [Fact]
    public async Task Network_failures_produce_a_retryable_message()
    {
        using var handler = new CrsWebHandler((_, _) => throw new HttpRequestException("private socket detail"));
        using var client = new CrsApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
        var error = await Assert.ThrowsAsync<CrsApiException>(() => client.CoursesAsync());
        Assert.Equal("connection_failed", error.Code);
        Assert.DoesNotContain("private", error.Message);
    }
}
