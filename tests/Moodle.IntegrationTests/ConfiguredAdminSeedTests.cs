using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Auth;

namespace Moodle.IntegrationTests;

public sealed class ConfiguredAdminSeedTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Fact]
    public async Task Explicit_local_admin_is_real_idempotent_requires_password_change_and_never_promotes_existing_user()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var name = "configured-admin-" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        IConfiguration Config(string? username = null) => new ConfigurationBuilder().AddInMemoryCollection(
            username is null ? [] : new Dictionary<string,string?> { ["Development:SiteAdmin:Username"] = username, ["Development:SiteAdmin:Password"] = password }).Build();
        var count = await db.Users.CountAsync();
        await new ConfiguredSiteAdminSeedContributor(db,users,roles,Config(),env).SeedAsync(SampleDataset.Create());
        Assert.Equal(count, await db.Users.CountAsync());
        var seed = new ConfiguredSiteAdminSeedContributor(db,users,roles,Config(name),env);
        await seed.SeedAsync(SampleDataset.Create()); await seed.SeedAsync(SampleDataset.Create());
        var admin = (await users.FindByNameAsync(name))!;
        Assert.True(admin.Id > 0);
        Assert.True(admin.RequirePasswordChange);
        Assert.True(await users.IsInRoleAsync(admin, "SiteAdmin"));
        Assert.True(await users.CheckPasswordAsync(admin, password));
        Assert.Single(await db.Users.Where(x => x.UserName == name).ToArrayAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConfiguredSiteAdminSeedContributor(db,users,roles,Config("v2-student"),env).SeedAsync(SampleDataset.Create()));
        Assert.False(await users.IsInRoleAsync((await users.FindByNameAsync("v2-student"))!, "SiteAdmin"));
        var production = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Production" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConfiguredSiteAdminSeedContributor(db,users,roles,Config(name),production).SeedAsync(SampleDataset.Create()));
    }
}
