using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using Moodle.Infrastructure.Auth;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

/// <summary>AUTH'un kendi Postgres fixture'ı (CRS/GRD'deki desenle aynı, bkz. CrsPostgresFixture).</summary>
public sealed class AuthPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private readonly WebApplicationFactory<Program> _root = new();

    public AuthDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Yalnızca AUTH'un veritabanını gerçek (Testcontainers) Postgres'e yönlendirir. Diğer modüllerin
    /// bağlam dizeleri değiştirilmez; AUTH testleri CRS/GRD/ASG tablolarına dokunmaz.
    /// </summary>
    public WebApplicationFactory<Program> CreateFactory(string environment = "Production") =>
        _root.WithWebHostBuilder(builder => builder.UseEnvironment(environment).ConfigureTestServices(services =>
        {
            services.RemoveAll<AuthDbContext>();
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(_container.GetConnectionString()));
        }));

    // AppUser.Id, SeedDataset.UserId ile hizalanabilsin diye ValueGeneratedNever'dır (AuthDbContext);
    // testlerin kendi kullanıcıları için burada yüksek, çakışmasız bir aralıktan elle kimlik verilir.
    private static int _nextTestUserId = 100_000;

    /// <summary>Testin kendi kullanıcısını doğrudan UserManager ile oluşturur (gerçek parola özetiyle).</summary>
    public static async Task<AppUser> SeedUserAsync(
        WebApplicationFactory<Program> factory, string username, string password,
        bool suspended = false, bool deleted = false, string[]? systemRoles = null)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        var user = new AppUser
        {
            Id = Interlocked.Increment(ref _nextTestUserId),
            UserName = username, Email = $"{username}@example.com", EmailConfirmed = true,
            Suspended = suspended, Deleted = deleted,
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));

        foreach (var role in systemRoles ?? [])
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    public Task DisposeAsync() => Task.WhenAll(_root.DisposeAsync().AsTask(), _container.DisposeAsync().AsTask());
}
