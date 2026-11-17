using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Moodle.IntegrationTests;

public class ApiSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Saglik_ucu_200_doner()
    {
        var res = await _client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task OpenApi_belgesi_uretilir()
    {
        var res = await _client.GetAsync("/api/v1/openapi.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("openapi", out _));
    }

    [Fact]
    public async Task Olmayan_api_yolu_tek_tip_hata_govdesi_doner()
    {
        var res = await _client.GetAsync("/api/v1/yok");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("not_found", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Ana_sayfa_Blazor_duzeniyle_acilir()
    {
        var res = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Ana sayfa", html);
    }
}
