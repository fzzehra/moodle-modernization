using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Fakes;
namespace Moodle.IntegrationTests;

public class AsgEnvironmentTests
{
    [Fact]
    public async Task Production_without_AUTH_returns_401_instead_of_fake_admin()
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b=>b.UseEnvironment("Production").ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Development:UseFakeIdentity"]="false", ["Database:SeedAsg"]="false", ["Database:Initialize"]="false"
        })));
        using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/assignments/1")).StatusCode);
        using var scope=app.Services.CreateScope();
        Assert.IsNotType<FakeCurrentUser>(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        Assert.IsNotType<FakeAccessPolicy>(scope.ServiceProvider.GetRequiredService<IAccessPolicy>());
    }
    [Theory]
    [InlineData("Production")] [InlineData("Staging")]
    public void Non_development_cannot_enable_fake_admin(string environment)
    {
        using var app=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>b.UseEnvironment(environment).ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Development:UseFakeIdentity"]="true", ["Database:SeedAsg"]="false"
        })));
        Assert.Throws<InvalidOperationException>(()=>app.CreateClient());
    }
}
