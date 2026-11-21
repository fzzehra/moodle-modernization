using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moodle.Application.Seeding;

namespace Moodle.Infrastructure.Auth;

/// <summary>Explicit local-only administrator provisioning; never ships a default password.</summary>
public sealed class ConfiguredSiteAdminSeedContributor(
    AuthDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole<int>> roles,
    IConfiguration configuration, IHostEnvironment environment) : ISeedContributor
{
    public int Order => 11;
    public string Name => "AUTH: explicitly configured local SiteAdmin";
    public async Task SeedAsync(SeedDataset data, CancellationToken ct = default)
    {
        var username = configuration["Development:SiteAdmin:Username"];
        var password = configuration["Development:SiteAdmin:Password"];
        if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password)) return;
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            throw new InvalidOperationException("Sample SiteAdmin provisioning is restricted to Development/Testing.");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Explicit sample SiteAdmin username and password are both required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({0x41555448},{11})", ct);
        var existing = await users.FindByNameAsync(username);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, "SiteAdmin"))
                throw new InvalidOperationException("Existing user cannot be promoted by sample seed.");
            await tx.CommitAsync(ct);
            return;
        }
        if (!await roles.RoleExistsAsync("SiteAdmin"))
            Check(await roles.CreateAsync(new IdentityRole<int>("SiteAdmin")));
        var user = new AppUser { Id = (await db.Users.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1,
            UserName = username, Email = username + "@example.invalid", EmailConfirmed = true, RequirePasswordChange = true };
        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRoleAsync(user, "SiteAdmin"));
        await tx.CommitAsync(ct);
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Configured SiteAdmin seed failed: " + string.Join(",", result.Errors.Select(x => x.Code)));
    }
}
