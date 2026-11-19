using Microsoft.AspNetCore.Identity;
using Moodle.Application.Auth;
using Moodle.Application.Seeding;

namespace Moodle.Infrastructure.Auth;

/// <summary>
/// Örnek veriyi (<c>ortam/ornek-veri.feature</c>) AUTH tablolarına yükler: kullanıcılar ve sistem rolleri.
/// Kimlikler <see cref="SeedDataset.UserId"/> ile aynı sırada üretilir (<c>Order</c> 10, diğer modüllerden
/// önce) ki CRS/ASG/GRD'nin kullanıcıya yalnızca kimlikle başvuran kayıtları geriye dönük eşleşsin —
/// bkz. <c>SeedDataset.UserId</c> açıklaması. Şifre, örnek veri kuralı gereği kullanıcı adıyla aynıdır.
/// Tekrar çalıştırılabilir: var olan kullanıcı atlanır.
/// </summary>
public sealed class AuthSeedContributor(
    UserManager<AppUser> userManager, RoleManager<IdentityRole<int>> roleManager) : ISeedContributor
{
    public int Order => 10;
    public string Name => "AUTH: kullanıcılar ve sistem rolleri";

    public async Task SeedAsync(SeedDataset data, CancellationToken ct = default)
    {
        foreach (var roleName in new[] { CapabilityRules.SiteAdmin, CapabilityRules.Manager })
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));

        foreach (var seedUser in data.Users)
        {
            var id = data.UserId(seedUser.Username);
            if (await userManager.FindByIdAsync(id.ToString()) is not null) continue;

            var user = new AppUser
            {
                Id = id,
                UserName = seedUser.Username,
                Email = seedUser.Email,
                EmailConfirmed = true,
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
            };

            var created = await userManager.CreateAsync(user, seedUser.Username);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    $"Örnek kullanıcı oluşturulamadı ({seedUser.Username}): " +
                    string.Join(", ", created.Errors.Select(e => e.Description)));

            if (seedUser.SystemRole == "manager")
                await userManager.AddToRoleAsync(user, CapabilityRules.Manager);
        }
    }
}
