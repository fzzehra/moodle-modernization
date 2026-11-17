using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

public sealed class CrsUnavailableAuthFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
}

public sealed class CrsUnavailableAuthTests(CrsUnavailableAuthFactory factory) : IClassFixture<CrsUnavailableAuthFactory>
{
    public static IEnumerable<object[]> ProtectedRoutes() => Routes();

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Production_rejects_every_CRS_endpoint_with_repository_AUTH_placeholders(string method, string path, string? body)
    {
        using var client = factory.CreateClient();
        using var response = await SendAsync(client, method, path, body);
        await AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "auth_not_ready");
    }

    [Fact]
    public async Task Health_OpenAPI_and_existing_pages_remain_available()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var response = await client.GetAsync("/api/v1/openapi.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/categories").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/courses").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/courses/{courseId}/enrolments/{enrolmentId}").TryGetProperty("patch", out _));
    }
}
