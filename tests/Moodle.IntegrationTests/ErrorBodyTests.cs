using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Errors;

namespace Moodle.IntegrationTests;

/// <summary>K3: beklenmeyen hatada yığın izi ve iç ayrıntı istemciye dönmez.</summary>
public class ErrorBodyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ErrorBodyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.Configure(app =>
        {
            app.UseMiddleware<Moodle.Api.Errors.ApiErrorMiddleware>();
            app.Run(ctx => ctx.Request.Path.Value switch
            {
                "/api/test/patla" => throw new InvalidOperationException("gizli iç ayrıntı: Server=db;Password=x"),
                "/api/test/yok" => throw AppException.NotFound("Ödev bulunamadı."),
                _ => Task.CompletedTask,
            });
        })).CreateClient();
    }

    [Fact]
    public async Task Beklenmeyen_hata_500_doner_ve_ayrinti_sizdirmaz()
    {
        var res = await _client.GetAsync("/api/test/patla");
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.DoesNotContain("gizli", body);
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("   at ", body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("internal_error", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AppException_kendi_durum_kodu_ve_mesajiyla_doner()
    {
        var res = await _client.GetAsync("/api/test/yok");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("Ödev bulunamadı.", doc.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
}
