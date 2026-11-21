using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Infrastructure.Auth;

// Test-only bootstrap. Connection comes from environment; generated credentials go only to an ignored fixture file.
var connection = Environment.GetEnvironmentVariable("E2E_AUTH_CONNECTION")
    ?? throw new InvalidOperationException("E2E_AUTH_CONNECTION required");
var path = args.Single();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AuthDbContext>(o => o.UseNpgsql(connection));
services.AddIdentityCore<AppUser>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<AuthDbContext>();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
if (File.Exists(path))
{
    var fixture = JsonSerializer.Deserialize<Credentials>(await File.ReadAllTextAsync(path))!;
    if (!fixture.username.StartsWith("e2e-admin-", StringComparison.Ordinal)) throw new InvalidOperationException("Not an E2E fixture");
    foreach (var username in new[] { fixture.username, fixture.teacherUsername, fixture.learnerUsername })
    {
        var user = await users.FindByNameAsync(username);
        if (user is not null) Check(await users.DeleteAsync(user));
    }
    File.Delete(path);
    return;
}
if (!await roles.RoleExistsAsync("SiteAdmin")) throw new InvalidOperationException("Real SiteAdmin role missing");
var name = "e2e-admin-" + Guid.NewGuid().ToString("N");
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!";
var id = RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue);
while (await users.FindByIdAsync(id.ToString()) is not null) id = RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue);
var admin = new AppUser { Id = id, UserName = name, Email = name + "@example.invalid", EmailConfirmed = true, RequirePasswordChange = false };
Check(await users.CreateAsync(admin, password));
try
{
    Check(await users.AddToRoleAsync(admin, "SiteAdmin"));
    var teacher = await CreateParticipant(name + "-teacher", password);
    var learner = await CreateParticipant(name + "-learner", password);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Credentials(name, password, teacher.UserName!, teacher.Id, learner.UserName!, learner.Id)));
}
catch
{
    foreach (var username in new[] { name + "-teacher", name + "-learner", name })
    {
        var partial = await users.FindByNameAsync(username);
        if (partial is not null) Check(await users.DeleteAsync(partial));
    }
    throw;
}
async Task<AppUser> CreateParticipant(string username, string secret)
{
    var key = RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue);
    while (await users.FindByIdAsync(key.ToString()) is not null) key = RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue);
    var user = new AppUser { Id = key, UserName = username, Email = username + "@example.invalid", EmailConfirmed = true };
    Check(await users.CreateAsync(user, secret));
    return user;
}
static void Check(IdentityResult result)
{
    if (!result.Succeeded) throw new InvalidOperationException(string.Join(",", result.Errors.Select(x => x.Code)));
}
record Credentials(string username, string password, string teacherUsername, int teacherId, string learnerUsername, int learnerId);
