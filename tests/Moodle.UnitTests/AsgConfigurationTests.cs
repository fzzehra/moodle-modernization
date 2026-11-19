using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Api;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure;
using Moodle.Infrastructure.Fakes;
using Moodle.Web.Assignments;

namespace Moodle.UnitTests;

public class AsgConfigurationTests
{
    [Theory]
    [InlineData("Production", true, false, false)]
    [InlineData("Staging", true, false, false)]
    [InlineData("Production", false, true, false)]
    [InlineData("Development", false, true, false)]
    [InlineData("Development", true, false, true)]
    [InlineData("Testing", true, true, true)]
    [InlineData("Production", false, false, true)]
    public void Synthetic_configuration_is_explicit_and_environment_scoped(string environment, bool fakes, bool seed, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Development:UseFakeIdentity"] = fakes.ToString(), ["Database:SeedAsg"] = seed.ToString(), ["Database:Initialize"] = "true"
        }).Build();
        var host = new Host { EnvironmentName = environment };
        if (valid) Assert.Equal(fakes, DevelopmentConfiguration.Validate(host, config));
        else Assert.Throws<InvalidOperationException>(() => DevelopmentConfiguration.Validate(host, config));
    }
    [Fact]
    public void Default_infrastructure_does_not_impersonate_admin()
    {
        // AUTH artık gerçek: varsayılan ICurrentUser, HttpContext olmadan (bu çıplak ServiceCollection'da
        // olduğu gibi) her zaman oturumsuzdur ve FakeCurrentUser değildir. IAccessPolicy.HasCapabilityAsync
        // artık gerçek veritabanına bağlı olduğundan burada (veritabansız birim test) çağrılmaz; aynı koruma
        // uçtan uca AsgEnvironmentTests.Production_without_AUTH_returns_401_instead_of_fake_admin'de sınanır.
        using var provider = new ServiceCollection().AddInfrastructure().BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ICurrentUser>().IsAuthenticated);
        Assert.Null(scope.ServiceProvider.GetRequiredService<ICurrentUser>().UserId);
        Assert.IsNotType<FakeCurrentUser>(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
    }
    [Theory]
    [InlineData("http://localhost:5202", "http", "localhost:5202")]
    [InlineData("http://localhost:8080", "http", "localhost:8080")]
    [InlineData("https://localhost:7098", "https", "localhost:7098")]
    public async Task Matching_api_origin_transfers_cookie_only_to_same_instance(string target, string scheme, string host)
    {
        var handler = new CaptureHandler();
        var client = Create(target, scheme, host, handler);
        Assert.Equal("ok", await client.SendAsync<string>(HttpMethod.Get, "/api/v1/test"));
        Assert.Equal(1, handler.Calls); Assert.Equal("synthetic=session", handler.Cookie);
        Assert.Equal(new Uri(target + "/api/v1/test"), handler.Uri);
    }
    [Theory]
    [InlineData("http://localhost:8080", "http", "localhost:5202")]
    [InlineData("http://other.example:5202", "http", "localhost:5202")]
    [InlineData("http://localhost:7098", "https", "localhost:7098")]
    public async Task Wrong_api_origin_sends_neither_request_nor_cookie(string target, string scheme, string host)
    {
        var handler = new CaptureHandler();
        var ex = await Assert.ThrowsAsync<AsgClientException>(() => Create(target, scheme, host, handler).SendAsync<string>(HttpMethod.Get, "/api/v1/test"));
        Assert.Equal(503, ex.Status); Assert.Equal(0, handler.Calls);
    }
    private static AsgClient Create(string target, string scheme, string host, CaptureHandler handler)
    {
        var context = new DefaultHttpContext(); context.Request.Scheme = scheme; context.Request.Host = new HostString(host);
        context.Request.Headers.Cookie = "synthetic=session";
        return new(new HttpClient(handler) { BaseAddress = new Uri(target) }, new HttpContextAccessor { HttpContext = context });
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public int Calls; public string? Cookie; public Uri? Uri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Cookie = request.Headers.GetValues("Cookie").Single(); Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"ok\"", Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
